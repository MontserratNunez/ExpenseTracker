using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Application.DTOs.Debts;
using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Tests.Fakes;

namespace ExpenseTracker.Tests.Application;

public class BudgetServiceTests
{
    private const int Food = 1;
    private const int Outings = 2;
    private const int Savings = 3;
    private const int Pets = 4;

    private readonly FakeRepository<Category> _categories = new();
    private readonly FakeRepository<UserSettings> _settings = new();
    private readonly FakeRepository<ExchangeRate> _rates = new();
    private readonly FakeRepository<IncomeSource> _sources = new();
    private readonly FakeRepository<OneTimeIncome> _oneTime = new();
    private readonly FakeRepository<BudgetAllocation> _allocations = new();
    private readonly FakeDebtRepository _debts = new();
    private readonly FakeExpenseRepository _expenses;
    private readonly FixedDateProvider _dates = new(new DateOnly(2026, 11, 10));
    private readonly IncomeService _income;
    private readonly ExpenseService _expenseService;
    private readonly DebtService _debtService;
    private readonly BudgetService _service;

    public BudgetServiceTests()
    {
        _expenses = new FakeExpenseRepository(_categories);
        _settings.Items.Add(new UserSettings { Id = 1, BaseCurrency = Currency.DOP });
        _categories.Items.AddRange(new[]
        {
            new Category { Id = Food, Name = "Comida", Color = "#E67E22" },
            new Category { Id = Outings, Name = "Compras/Salidas", Color = "#9B59B6" },
            new Category { Id = Savings, Name = "Ahorro", Color = "#2E9E5B", IsSavings = true },
            new Category { Id = Pets, Name = "Mascotas", Color = "#795548" },
        });

        var currency = new CurrencyService(_settings, _rates, _dates);
        _income = new IncomeService(_sources, _oneTime, _allocations, currency, _dates);
        var ledger = new MonthlyLedgerService(_expenses, _debts, currency, _dates);
        _expenseService = new ExpenseService(_expenses, _categories, currency, ledger, _dates);
        _debtService = new DebtService(_debts, currency, _dates);
        _service = new BudgetService(_allocations, _categories, _sources, _expenses, ledger, _income, _dates);
    }

    private Task AddSalaryAsync(string name, decimal amount)
        => _income.CreateSourceAsync(new IncomeSourceInput { Name = name, Amount = amount, StartDate = new DateOnly(2026, 1, 1) });

    private Task SpendAsync(string name, decimal amount, DateOnly date, int category = Food)
        => _expenseService.CreateAsync(new ExpenseInput { Name = name, Amount = amount, Date = date, CategoryId = category });

    private static AllocationInput Fixed(int category, decimal value) => new() { CategoryId = category, AllocationType = AllocationType.FixedAmount, Value = value };

    private static AllocationInput Percent(int category, decimal value, int? source = null)
        => new() { CategoryId = category, AllocationType = AllocationType.PercentageOfIncome, Value = value, IncomeSourceId = source };

    private async Task<CategoryPlanDto> FoodStatusAsync(int year = 2026, int month = 11)
        => (await _service.GetPlanStatusAsync(year, month)).Categories.Single(c => c.CategoryId == Food);

    // ---------- Assigned amounts ----------

    [Fact]
    public async Task Assigned_MatchesEnunciadoExample_FixedAndPercentOfSalary()
    {
        await AddSalaryAsync("Sueldo trabajo", 50_000m);
        await _service.CreateAsync(Fixed(Food, 10_000m));
        await _service.CreateAsync(Fixed(Outings, 5_000m));
        await _service.CreateAsync(Percent(Savings, 10m, source: 1));

        var plan = await _service.GetPlanStatusAsync(2026, 11);

        Assert.Equal(10_000m, plan.Categories.Single(c => c.CategoryId == Food).Assigned);
        Assert.Equal(5_000m, plan.Categories.Single(c => c.CategoryId == Outings).Assigned);
        Assert.Equal(5_000m, plan.Categories.Single(c => c.CategoryId == Savings).Assigned);
        Assert.Equal(20_000m, plan.TotalAssigned);
    }

    [Fact]
    public async Task Percent_WithoutSource_UsesTotalRecurringIncome_AndWithSourceUsesOnlyThatOne()
    {
        await AddSalaryAsync("Sueldo trabajo", 50_000m);
        await AddSalaryAsync("Sueldo universidad", 10_000m);
        await _service.CreateAsync(Percent(Savings, 10m));            // 10% of 60,000
        await _service.CreateAsync(Percent(Pets, 10m, source: 2));    // 10% of 10,000

        var plan = await _service.GetPlanStatusAsync(2026, 11);

        Assert.Equal(6_000m, plan.Categories.Single(c => c.CategoryId == Savings).Assigned);
        Assert.Equal(1_000m, plan.Categories.Single(c => c.CategoryId == Pets).Assigned);
    }

    [Fact]
    public async Task Summary_ShowsAssignedPercentOfIncome_AndUnassigned()
    {
        await AddSalaryAsync("Sueldo", 60_000m);
        await _service.CreateAsync(Fixed(Food, 30_000m));

        var plan = await _service.GetPlanStatusAsync(2026, 11);

        Assert.Equal(60_000m, plan.Income);
        Assert.Equal(50m, plan.AssignedPercentOfIncome);
        Assert.Equal(30_000m, plan.Unassigned);
        Assert.False(plan.IsOverAssigned);
    }

    [Fact]
    public async Task Summary_AssigningMoreThanIncome_RaisesWarning()
    {
        await AddSalaryAsync("Sueldo", 65_000m);
        await _service.CreateAsync(Fixed(Food, 40_000m));
        await _service.CreateAsync(Fixed(Outings, 30_000m));

        var plan = await _service.GetPlanStatusAsync(2026, 11);

        Assert.True(plan.IsOverAssigned);
        Assert.Equal(-5_000m, plan.Unassigned);
    }

    // ---------- Spent, pending, available ----------

    [Fact]
    public async Task Available_IsAssignedMinusSpentMinusPending()
    {
        await _service.CreateAsync(Fixed(Food, 10_000m));
        await SpendAsync("Super", 3_000m, new DateOnly(2026, 11, 5));
        await _expenseService.CreateAsync(new ExpenseInput
        {
            Name = "Plan de comidas", Amount = 1_500m, CategoryId = Food, IsRecurring = true,
            Frequency = Frequency.Monthly, Date = new DateOnly(2026, 1, 25)
        });

        var food = await FoodStatusAsync();

        Assert.Equal(3_000m, food.Spent);
        Assert.Equal(1_500m, food.Pending); // the 25th is after "today" (Nov 10)
        Assert.Equal(5_500m, food.Available);
    }

    // ---------- Month-end estimate ----------

    [Fact]
    public async Task Estimate_FoodWith5585SpentOnDay10_IsAbout16756_AndRed()
    {
        await _service.CreateAsync(Fixed(Food, 10_000m));
        await SpendAsync("Supermercado", 4_885.40m, new DateOnly(2026, 11, 3));
        await SpendAsync("Supermercado", 700m, new DateOnly(2026, 11, 10));

        var food = await FoodStatusAsync();

        // 5,585.40 / 10 days = 558.54 per day x 20 remaining days (November has 30)
        Assert.Equal(16_756.20m, food.Estimated);
        Assert.Equal(PlanHealth.Red, food.Health);
        Assert.Equal(6_756.20m, food.EstimatedOverrun);
    }

    [Fact]
    public async Task Estimate_RecurringPendingIsAddedWithoutBeingExtrapolated()
    {
        await _service.CreateAsync(Fixed(Food, 20_000m));
        await SpendAsync("Super", 1_000m, new DateOnly(2026, 11, 5));
        await _expenseService.CreateAsync(new ExpenseInput
        {
            Name = "Almuerzos", Amount = 2_000m, CategoryId = Food, IsRecurring = true,
            Frequency = Frequency.Monthly, Date = new DateOnly(2026, 1, 28)
        });

        var food = await FoodStatusAsync();

        // pace from the one-time spending only: 1,000 / 10 x 20 = 2,000; plus 1,000 spent and 2,000 pending
        Assert.Equal(5_000m, food.Estimated);
    }

    [Fact]
    public async Task Estimate_EarlyInTheMonth_UsesThreeMonthDailyAverage()
    {
        _dates.Today = new DateOnly(2026, 11, 2);
        await _service.CreateAsync(Fixed(Food, 10_000m));
        // Aug (31) + Sep (30) + Oct (31) = 92 days; 9,200 spent -> 100 per day
        await SpendAsync("Agosto", 3_000m, new DateOnly(2026, 8, 10));
        await SpendAsync("Septiembre", 3_200m, new DateOnly(2026, 9, 10));
        await SpendAsync("Octubre", 3_000m, new DateOnly(2026, 10, 10));
        await SpendAsync("Hoy", 50m, new DateOnly(2026, 11, 1));

        var food = await FoodStatusAsync();

        // 50 spent + 100 per day x 28 remaining days; today's 50 is not extrapolated yet
        Assert.Equal(2_850m, food.Estimated);
    }

    [Fact]
    public async Task Estimate_PastMonth_IsJustSpentPlusPending()
    {
        await _service.CreateAsync(Fixed(Food, 10_000m));
        await SpendAsync("Super", 4_000m, new DateOnly(2026, 10, 15));

        var food = await FoodStatusAsync(2026, 10);

        Assert.Equal(4_000m, food.Estimated);
        Assert.Equal(PlanHealth.Green, food.Health);
    }

    [Fact]
    public async Task Estimate_FutureMonth_UsesHistoricDailyAverageForTheWholeMonth()
    {
        await _service.CreateAsync(Fixed(Food, 10_000m));
        // Aug-Oct 2026 = 92 days, 9,200 spent -> 100 per day; December has 31 days
        await SpendAsync("Agosto", 9_200m, new DateOnly(2026, 9, 1));

        // Window for December is Sep, Oct, Nov = 91 days
        var food = await FoodStatusAsync(2026, 12);

        Assert.Equal(Math.Round(9_200m / 91m * 31m, 2), food.Estimated);
    }

    // ---------- Health thresholds ----------

    [Theory]
    [InlineData(18_000, PlanHealth.Green)]   // exactly 90%
    [InlineData(18_001, PlanHealth.Yellow)]
    [InlineData(20_000, PlanHealth.Yellow)]  // exactly 100%
    [InlineData(20_001, PlanHealth.Red)]
    [InlineData(1_000, PlanHealth.Green)]
    public async Task Health_FollowsThresholdsOnEstimateVersusAssigned(int spentInClosedMonth, PlanHealth expected)
    {
        await _service.CreateAsync(Fixed(Food, 20_000m));
        await SpendAsync("Gasto", spentInClosedMonth, new DateOnly(2026, 10, 15));

        Assert.Equal(expected, (await FoodStatusAsync(2026, 10)).Health);
    }

    // ---------- Out of plan ----------

    [Fact]
    public async Task OutOfPlan_GroupsSpendingInCategoriesWithoutAllocation_IncludingDebts()
    {
        await _service.CreateAsync(Fixed(Food, 10_000m));
        await SpendAsync("Super", 1_000m, new DateOnly(2026, 11, 5), Food);
        await SpendAsync("Correa", 400m, new DateOnly(2026, 11, 6), Pets);
        await _debtService.CreateAsync(new DebtInput
        {
            Name = "Préstamo", OriginalAmount = 10_000m, MonthlyPayment = 2_000m, PaymentDay = 20, StartDate = new DateOnly(2026, 1, 1)
        });

        var plan = await _service.GetPlanStatusAsync(2026, 11);

        Assert.Equal(2, plan.OutOfPlan.Count);
        Assert.Equal(400m, plan.OutOfPlan.Single(c => c.CategoryId == Pets).Spent);
        Assert.Equal(2_000m, plan.OutOfPlan.Single(c => c.CategoryName == "Deudas").Pending);
        Assert.Equal(400m, plan.OutOfPlanSpent);
        Assert.Equal(2_000m, plan.OutOfPlanPending);
        Assert.DoesNotContain(plan.OutOfPlan, c => c.CategoryId == Food);
    }

    [Fact]
    public async Task TotalEstimated_AddsPlanEstimatesAndOutOfPlanSpending()
    {
        await _service.CreateAsync(Fixed(Food, 10_000m));
        await SpendAsync("Super", 1_000m, new DateOnly(2026, 11, 5), Food);    // 1,000 / 10 x 30 = 3,000
        await SpendAsync("Correa", 400m, new DateOnly(2026, 11, 6), Pets);

        var plan = await _service.GetPlanStatusAsync(2026, 11);

        Assert.Equal(3_400m, plan.TotalEstimated);
        Assert.Equal(1_400m, plan.TotalSpent);
    }

    // ---------- CRUD and editor ----------

    [Fact]
    public async Task Create_SecondAllocationForTheSameCategory_Throws()
    {
        await _service.CreateAsync(Fixed(Food, 10_000m));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(Fixed(Food, 5_000m)));
    }

    [Fact]
    public async Task Create_InvalidValues_Throw()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(Fixed(Food, 0m)));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(Percent(Food, 101m)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(Fixed(99, 100m)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(Percent(Food, 10m, source: 77)));
    }

    [Fact]
    public async Task Create_FixedAmount_IgnoresIncomeSource()
    {
        await AddSalaryAsync("Sueldo", 50_000m);
        var input = Fixed(Food, 1_000m);
        input.IncomeSourceId = 1;

        var created = await _service.CreateAsync(input);

        Assert.Null(created.IncomeSourceId);
    }

    [Fact]
    public async Task Update_And_Delete_Work()
    {
        var created = await _service.CreateAsync(Fixed(Food, 10_000m));

        await _service.UpdateAsync(created.Id, Fixed(Food, 12_000m));
        Assert.Equal(12_000m, _allocations.Items.Single().Value);

        await _service.DeleteAsync(created.Id);
        Assert.Empty(_allocations.Items);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(created.Id));
    }

    [Fact]
    public async Task Editor_ListsCategoriesWithTheirAllocations_AndIncomeBases()
    {
        await AddSalaryAsync("Sueldo trabajo", 50_000m);
        await _service.CreateAsync(Fixed(Food, 10_000m));
        _categories.Items.Single(c => c.Id == Pets).IsActive = false;

        var editor = await _service.GetEditorAsync();

        Assert.Equal(3, editor.Rows.Count); // inactive Mascotas without allocation is hidden
        Assert.Equal(10_000m, editor.Rows.Single(r => r.CategoryId == Food).Value);
        Assert.Equal(0m, editor.Rows.Single(r => r.CategoryId == Savings).Value);
        Assert.Equal(50_000m, editor.IncomeBases.RecurringTotal);
        Assert.Equal("Sueldo trabajo", Assert.Single(editor.IncomeBases.Sources).Name);
    }

    [Fact]
    public async Task SaveAll_UpsertsPositiveValues_AndRemovesZeroOnes()
    {
        await _service.CreateAsync(Fixed(Food, 10_000m));
        await _service.CreateAsync(Fixed(Outings, 5_000m));

        await _service.SaveAllAsync(new List<AllocationInput>
        {
            Fixed(Food, 12_000m),     // update
            Fixed(Outings, 0m),       // remove
            Fixed(Savings, 3_000m),   // create
            Fixed(Pets, 0m)           // nothing to remove
        });

        Assert.Equal(2, _allocations.Items.Count);
        Assert.Equal(12_000m, _allocations.Items.Single(a => a.CategoryId == Food).Value);
        Assert.Equal(3_000m, _allocations.Items.Single(a => a.CategoryId == Savings).Value);
    }

    [Fact]
    public async Task SaveAll_WithAnInvalidRow_SavesNothing()
    {
        await _service.CreateAsync(Fixed(Food, 10_000m));

        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAllAsync(new List<AllocationInput>
        {
            Fixed(Food, 99_999m),
            Percent(Savings, 150m)
        }));

        Assert.Equal(10_000m, _allocations.Items.Single().Value);
    }

    [Fact]
    public async Task SetValue_ChangesOnlyTheValue_KeepingTypeAndSource()
    {
        await AddSalaryAsync("Sueldo", 50_000m);
        await _service.CreateAsync(Percent(Savings, 10m, source: 1));

        await _service.SetValueAsync(Savings, 12m);

        var allocation = _allocations.Items.Single();
        Assert.Equal(12m, allocation.Value);
        Assert.Equal(AllocationType.PercentageOfIncome, allocation.AllocationType);
        Assert.Equal(1, allocation.IncomeSourceId);
        Assert.Equal(6_000m, (await _service.GetAssignedByCategoryAsync())[Savings]);
    }

    [Fact]
    public async Task SetValue_Rejects_ZeroAnOverHundredPercentAndACategoryOutsideThePlan()
    {
        await AddSalaryAsync("Sueldo", 50_000m);
        await _service.CreateAsync(Percent(Savings, 10m));
        await _service.CreateAsync(Fixed(Food, 10_000m));

        await Assert.ThrowsAsync<ArgumentException>(() => _service.SetValueAsync(Food, 0m));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SetValueAsync(Savings, 101m));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SetValueAsync(Pets, 500m));
        Assert.Equal(10m, _allocations.Items.Single(a => a.CategoryId == Savings).Value);
    }

    [Fact]
    public async Task IncomeSource_UsedByAnAllocation_CannotBeDeleted()
    {
        await AddSalaryAsync("Sueldo", 50_000m);
        var allocation = await _service.CreateAsync(Percent(Savings, 10m, source: 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _income.DeleteSourceAsync(1));

        await _service.DeleteAsync(allocation.Id);
        await _income.DeleteSourceAsync(1);
        Assert.Empty(_sources.Items);
    }
}
