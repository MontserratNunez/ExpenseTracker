using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Tests.Fakes;

namespace ExpenseTracker.Tests.Application;

public class BudgetPlanServiceTests
{
    private const int Food = 1;
    private const int Outings = 2;
    private const int Pets = 3;

    private readonly FakeRepository<Category> _categories = new();
    private readonly FakeIncomeSourceRepository _sources = new();
    private readonly FakeBudgetPlanRepository _plans = new();
    private readonly FixedDateProvider _dates = new(new DateOnly(2026, 11, 10));
    private readonly BudgetPlanService _service;
    private readonly BudgetService _budget;

    public BudgetPlanServiceTests()
    {
        _categories.Items.AddRange(new[]
        {
            new Category { Id = Food, Name = "Comida", Color = "#E67E22" },
            new Category { Id = Outings, Name = "Salidas", Color = "#9B59B6" },
            new Category { Id = Pets, Name = "Mascotas", Color = "#795548", IsActive = false },
        });

        var settings = new FakeRepository<UserSettings>();
        settings.Items.Add(new UserSettings { Id = 1, BaseCurrency = Currency.DOP });
        var currency = new CurrencyService(settings, new FakeRepository<ExchangeRate>(), _dates);
        var income = new IncomeService(_sources, new FakeRepository<OneTimeIncome>(), new FakeRepository<BudgetAllocation>(), currency, _dates);
        var expenses = new FakeExpenseRepository(_categories);
        var ledger = new MonthlyLedgerService(expenses, new FakeDebtRepository(), currency, _dates);
        _service = new BudgetPlanService(_plans, _categories, _sources, income, _dates);
        _budget = new BudgetService(_plans, _categories, expenses, ledger, income, _dates);
    }

    private static DateOnly Month(int year, int month) => new(year, month, 1);

    private static AllocationInput Fixed(int category, decimal value) => new() { CategoryId = category, Value = value };

    private static BudgetPlanInput Input(string name, DateOnly from, DateOnly? to = null, params AllocationInput[] allocations)
        => new()
        {
            Name = name, EffectiveFrom = from, EffectiveTo = to,
            Allocations = allocations.Length == 0 ? null : allocations.ToList()
        };

    private BudgetPlan Plan(int id) => _plans.Items.Single(p => p.Id == id);

    /// <summary>A plan created in September 2026, seen from November.</summary>
    private async Task<int> RunningPlanAsync()
    {
        _dates.Today = new DateOnly(2026, 9, 5);
        var id = await _service.CreateAsync(Input("Plan actual", Month(2026, 9), null, Fixed(Food, 10_000m)));
        _dates.Today = new DateOnly(2026, 11, 10);
        return id;
    }

    private void AssertNoSharedMonth()
    {
        for (var month = Month(2025, 1); month < Month(2030, 1); month = month.AddMonths(1))
            Assert.True(_plans.Items.Count(p => p.AppliesTo(month.Year, month.Month)) <= 1, $"Two plans in {month:yyyy-MM}");
    }

    [Fact]
    public async Task PlanCreatedInNovember_RulesNovember_AndMayHasNoPlan()
    {
        await _service.CreateAsync(Input("Plan 2026", new DateOnly(2026, 11, 18), null, Fixed(Food, 10_000m)));

        var november = await _budget.GetPlanStatusAsync(2026, 11);
        var may = await _budget.GetPlanStatusAsync(2026, 5);

        Assert.True(november.HasPlan);
        Assert.Equal(Month(2026, 11), november.PlanFrom); // stored as day 1
        Assert.Single(november.Categories);
        Assert.False(may.HasPlan);
        Assert.Empty(may.Categories);
        Assert.Equal(0m, may.TotalAssigned);
        Assert.False(may.IsOverAssigned);
    }

    [Fact]
    public async Task Create_InThePast_IsRejected()
        => await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(Input("Viejo", Month(2026, 10))));

    [Fact]
    public async Task Create_WithEndBeforeStart_IsRejected()
        => await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(Input("Mal", Month(2027, 3), Month(2027, 2))));

    [Fact]
    public async Task Create_NextYearPlan_ClosesTheOpenOneInDecember_AndWarnsFirst()
    {
        var current = await RunningPlanAsync();
        var input = Input("Plan 2027", Month(2027, 1));

        var preview = await _service.PreviewAsync(input);
        await _service.CreateAsync(input);

        Assert.Equal("El plan «Plan actual» terminará en diciembre 2026.", preview.Notice);
        Assert.Null(preview.Conflict);
        Assert.Equal(new DateOnly(2026, 12, 31), Plan(current).EffectiveTo);
        AssertNoSharedMonth();
    }

    [Fact]
    public async Task Create_OverAFuturePlan_ShowsTheConflict_AndCanBeTrimmed()
    {
        await _service.CreateAsync(Input("Plan 2027", Month(2027, 1)));
        var input = Input("Fin de año", Month(2026, 11));

        var preview = await _service.PreviewAsync(input);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(input));
        Assert.Single(_plans.Items);

        Assert.Equal(new DateOnly(2026, 12, 31), preview.SuggestedEnd);
        input.EffectiveTo = preview.SuggestedEnd;
        await _service.CreateAsync(input);

        Assert.Equal(2, _plans.Items.Count);
        Assert.Null(_plans.Items.Single(p => p.Name == "Plan 2027").EffectiveTo); // untouched
        AssertNoSharedMonth();
    }

    [Fact]
    public async Task Create_OverAPlanThatStartsTheSameMonth_IsRejected()
    {
        await _service.CreateAsync(Input("Primero", Month(2026, 11)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(Input("Segundo", Month(2026, 11))));

        Assert.Null((await _service.PreviewAsync(Input("Segundo", Month(2026, 11)))).SuggestedEnd);
        Assert.Single(_plans.Items);
    }

    [Fact]
    public async Task Create_CopiesTheAllocationsOfTheChosenPlan()
    {
        var current = await RunningPlanAsync();
        var input = Input("Plan 2027", Month(2027, 1));
        input.CopyFromPlanId = current;

        var created = await _service.CreateAsync(input);

        Assert.Equal(10_000m, Assert.Single(Plan(created).Allocations).Value);
    }

    [Fact]
    public async Task Update_OfAPlanStartedBefore_CreatesAVersion_AndKeepsThePastMonths()
    {
        var original = await RunningPlanAsync();

        var version = await _service.UpdateAsync(original, Input("Plan actual", Month(2026, 9), null, Fixed(Food, 12_000m)));

        Assert.NotEqual(original, version);
        Assert.Equal(new DateOnly(2026, 10, 31), Plan(original).EffectiveTo);
        Assert.Equal(Month(2026, 11), Plan(version).EffectiveFrom);
        Assert.Equal(10_000m, (await _budget.GetPlanStatusAsync(2026, 10)).Categories.Single().Assigned);
        Assert.Equal(12_000m, (await _budget.GetPlanStatusAsync(2026, 11)).Categories.Single().Assigned);
        AssertNoSharedMonth();
    }

    [Fact]
    public async Task Update_InDecember_OfANovemberPlan_StartsTheVersionInDecember()
    {
        var original = await _service.CreateAsync(Input("Plan", Month(2026, 11), null, Fixed(Food, 10_000m)));
        _dates.Today = new DateOnly(2026, 12, 3);

        var version = await _service.SetValueAsync(original, Food, 8_000m);

        Assert.Equal(Month(2026, 12), Plan(version).EffectiveFrom);
        Assert.Equal(new DateOnly(2026, 11, 30), Plan(original).EffectiveTo);
        Assert.Equal(10_000m, Plan(original).Allocations.Single().Value);
    }

    [Fact]
    public async Task Update_WithoutChanges_DoesNotSplitTheHistory()
    {
        var original = await RunningPlanAsync();

        var saved = await _service.UpdateAsync(original, Input("Plan actual", Month(2026, 9), null, Fixed(Food, 10_000m)));

        Assert.Equal(original, saved);
        Assert.Single(_plans.Items);
    }

    [Fact]
    public async Task Update_OfACurrentOrFuturePlan_ModifiesIt()
    {
        var current = await _service.CreateAsync(Input("Ahora", Month(2026, 11), new DateOnly(2026, 12, 31), Fixed(Food, 1_000m)));
        var future = await _service.CreateAsync(Input("Luego", Month(2027, 1)));

        Assert.Equal(current, await _service.SetValueAsync(current, Food, 2_000m));
        Assert.Equal(future, await _service.UpdateAsync(future, Input("Después", Month(2027, 3))));

        Assert.Equal(2, _plans.Items.Count);
        Assert.Equal(Month(2027, 3), Plan(future).EffectiveFrom);
        AssertNoSharedMonth();
    }

    [Fact]
    public async Task Update_ThatWouldOverlap_OrMoveToThePast_IsRejected()
    {
        var current = await _service.CreateAsync(Input("Ahora", Month(2026, 11), new DateOnly(2026, 12, 31)));
        var future = await _service.CreateAsync(Input("Luego", Month(2027, 1)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateAsync(future, Input("Luego", Month(2026, 12))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateAsync(current, Input("Ahora", Month(2026, 11))));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(future, Input("Luego", Month(2026, 10))));
        AssertNoSharedMonth();
    }

    [Fact]
    public async Task PastPlans_CannotChange()
    {
        var original = await RunningPlanAsync();
        await _service.CreateAsync(Input("Nuevo", Month(2026, 11)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateAsync(original, Input("Otro", Month(2026, 11))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.EndAsync(original, Month(2026, 12)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(original));
    }

    [Fact]
    public async Task End_FinishesTheRunningPlan_ButNeverBeforeTheCurrentMonth()
    {
        var id = await RunningPlanAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.EndAsync(id, Month(2026, 10)));
        await _service.EndAsync(id, Month(2026, 11));

        Assert.Equal(new DateOnly(2026, 11, 30), Plan(id).EffectiveTo);
        Assert.False((await _budget.GetPlanStatusAsync(2026, 12)).HasPlan);
    }

    [Fact]
    public async Task Delete_OnlyFuturePlans()
    {
        var current = await _service.CreateAsync(Input("Ahora", Month(2026, 11)));
        var future = await _service.CreateAsync(Input("Luego", Month(2027, 1)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(current));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.EndAsync(future, Month(2027, 2)));
        await _service.DeleteAsync(future);

        Assert.Equal(current, Assert.Single(_plans.Items).Id);
    }

    [Fact]
    public async Task Allocations_InvalidRowsSaveNothing_AndZeroLeavesTheCategoryOut()
    {
        var id = await _service.CreateAsync(Input("Plan", Month(2026, 11), null, Fixed(Food, 10_000m), Fixed(Outings, 0m)));
        Assert.Equal(Food, Assert.Single(Plan(id).Allocations).CategoryId);

        var percent = new AllocationInput { CategoryId = Outings, AllocationType = AllocationType.PercentageOfIncome, Value = 101m };
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(id, Input("Plan", Month(2026, 11), null, Fixed(Food, 99m), percent)));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateAsync(id, Input("Plan", Month(2026, 11), null, Fixed(99, 100m))));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SetValueAsync(id, Food, 0m));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SetValueAsync(id, Outings, 500m));

        Assert.Equal(10_000m, Assert.Single(Plan(id).Allocations).Value);
    }

    [Fact]
    public async Task Editor_ListsEveryActiveCategoryWithTheAllocationsOfThatPlan()
    {
        var id = await _service.CreateAsync(Input("Plan", Month(2026, 11), null, Fixed(Food, 10_000m)));

        var editor = await _service.GetEditorAsync(id);

        Assert.Equal(2, editor.Rows.Count); // inactive Mascotas without allocation is hidden
        Assert.Equal(10_000m, editor.Rows.Single(r => r.CategoryId == Food).Value);
        Assert.Equal(0m, editor.Rows.Single(r => r.CategoryId == Outings).Value);
    }

    [Fact]
    public async Task Timeline_IsOrdered_AndTellsPastCurrentAndFuture()
    {
        await RunningPlanAsync();
        await _service.CreateAsync(Input("Ahora", Month(2026, 11), new DateOnly(2026, 12, 31)));
        await _service.CreateAsync(Input("Luego", Month(2027, 1)));

        var timeline = await _service.GetTimelineAsync();

        Assert.Equal(new[] { "Plan actual", "Ahora", "Luego" }, timeline.Select(p => p.Name));
        Assert.True(timeline[0].IsPast);
        Assert.True(timeline[1].IsCurrent);
        Assert.False(timeline[1].EditCreatesVersion);
        Assert.True(timeline[2].IsFuture);
    }
}
