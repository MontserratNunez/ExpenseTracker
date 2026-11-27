using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Application.Helpers;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Tests.Fakes;

namespace ExpenseTracker.Tests.Application;

/// <summary>Phase 16: savings are a reserve of the plan, not an expense.</summary>
public class SavingsReserveTests
{
    private const int Food = 1;

    private readonly FakeIncomeSourceRepository _sources = new();
    private readonly FakeBudgetPlanRepository _plans = new();
    private readonly FixedDateProvider _dates = new(new DateOnly(2026, 11, 10));
    private readonly IncomeService _income;
    private readonly ExpenseService _expenseService;
    private readonly BudgetPlanService _planService;
    private readonly BudgetService _budget;
    private readonly SavingsImpactService _impact;

    public SavingsReserveTests()
    {
        var settings = new FakeRepository<UserSettings>();
        settings.Items.Add(new UserSettings { Id = 1, BaseCurrency = Currency.DOP });
        var categories = new FakeRepository<Category>();
        categories.Items.Add(new Category { Id = Food, Name = "Comida", Color = "#E67E22" });
        var expenses = new FakeExpenseRepository(categories);
        var currency = new CurrencyService(settings, new FakeRepository<ExchangeRate>(), _dates);

        _income = new IncomeService(_sources, new FakeRepository<OneTimeIncome>(), _plans, currency, _dates);
        var ledger = new MonthlyLedgerService(expenses, new FakeDebtRepository(), currency, _dates);
        _expenseService = new ExpenseService(expenses, categories, currency, ledger, _dates);
        _planService = new BudgetPlanService(_plans, categories, _sources, _income, _dates);
        _budget = new BudgetService(_plans, categories, expenses, ledger, _income, _dates);
        _impact = new SavingsImpactService(_budget, expenses, currency, _dates);
    }

    private Task SalaryAsync(decimal amount = 30_000m)
        => _income.CreateSourceAsync(new IncomeSourceInput { Name = "Sueldo", Amount = amount, StartDate = new DateOnly(2026, 1, 1) });

    private Task<int> PlanAsync(decimal reserve, AllocationType type = AllocationType.FixedAmount, int? source = null)
        => _planService.CreateAsync(new BudgetPlanInput
        {
            Name = "Plan", EffectiveFrom = new DateOnly(2026, 11, 1),
            Savings = new SavingsInput { Type = type, Value = reserve, IncomeSourceId = source }
        });

    private static ExpenseInput Expense(decimal amount, DateOnly? date = null)
        => new() { Name = "Gasto", Amount = amount, CategoryId = Food, Date = date ?? new DateOnly(2026, 11, 5) };

    private async Task<SavingsStatusDto> SavingsAsync(int month = 11)
        => (await _budget.GetPlanStatusAsync(2026, month)).Savings!;

    // ---------- The seven cases of 16.4 ----------

    [Theory]
    [InlineData(10_000, 0, 15_000, 20_000, SavingsState.OnPlan)]
    [InlineData(22_000, 0, 3_000, 8_000, SavingsState.NearSavings)]
    [InlineData(24_000, 0, 1_000, 6_000, SavingsState.NearSavings)]
    [InlineData(25_000, 0, 0, 5_000, SavingsState.UsingSavings)]
    [InlineData(28_000, 0, -3_000, 2_000, SavingsState.UsingSavings)]
    [InlineData(31_000, 0, -6_000, -1_000, SavingsState.Deficit)]
    [InlineData(24_000, 605, 395, 5_395, SavingsState.NearSavings)]
    public void Cases_Of_16_4(int used, int pending, int margin, int realSavings, SavingsState state)
    {
        var status = SavingsReserveCalculator.Calculate(30_000m, 5_000m, used, pending, 15m);

        Assert.Equal(25_000m, status.Spendable);
        Assert.Equal(margin, status.Margin);
        Assert.Equal(realSavings, status.RealSavings);
        Assert.Equal(state, status.State);
        Assert.Equal(realSavings >= 5_000, status.IsMet);
    }

    [Fact]
    public void Messages_SayWhatTheExamplesSay()
    {
        string Message(decimal used, decimal pending = 0)
            => SavingsReserveCalculator.Message(SavingsReserveCalculator.Calculate(30_000m, 5_000m, used, pending, 15m), Currency.DOP);

        Assert.Equal("Puedes gastar RD$ 15,000.00 sin tocar tu ahorro.", Message(10_000m));
        Assert.Equal("Te quedan RD$ 1,000.00 antes de usar tu ahorro.", Message(24_000m));
        Assert.Equal("Has usado RD$ 3,000.00 de tus RD$ 5,000.00 de ahorro.", Message(28_000m));
        Assert.Equal("Gastaste RD$ 1,000.00 más de lo que ganaste este mes.", Message(31_000m));
        Assert.Equal("Te quedan RD$ 395.00 antes de usar tu ahorro.", Message(24_000m, 605m));
    }

    [Fact]
    public void Extra_UsedSavings_AndProjection()
    {
        var roomy = SavingsReserveCalculator.Calculate(30_000m, 5_000m, 10_000m, 0m, 15m, expectedSpending: 27_400m);
        var over = SavingsReserveCalculator.Calculate(30_000m, 5_000m, 33_000m, 0m, 15m);

        Assert.Equal(15_000m, roomy.Extra);
        Assert.Equal(2_600m, roomy.Projection);
        Assert.Equal(2_400m, roomy.ProjectedSavingsUse);
        Assert.Equal(5_000m, over.UsedSavings); // never more than the reserve
        Assert.Null(over.ProjectedSavingsUse);
    }

    // ---------- Through the plan ----------

    [Fact]
    public async Task PercentageReserve_IsTakenFromTheIncomeOfTheMonth_AndTheMarginIsGlobal()
    {
        await SalaryAsync();
        await PlanAsync(10m, AllocationType.PercentageOfIncome);
        await _expenseService.CreateAsync(Expense(24_000m)); // no allocation: still counts

        var status = await _budget.GetPlanStatusAsync(2026, 11);

        Assert.Equal(3_000m, status.Savings!.Reserve);
        Assert.Equal(3_000m, status.Savings.Margin);
        Assert.Equal(27_000m, status.Unassigned);
    }

    [Fact]
    public async Task AllocationsPlusReserveOverTheIncome_AreFlagged()
    {
        await SalaryAsync();
        var id = await PlanAsync(5_000m);
        await _planService.UpdateAsync(id, new BudgetPlanInput
        {
            Name = "Plan", EffectiveFrom = new DateOnly(2026, 11, 1),
            Allocations = new() { new AllocationInput { CategoryId = Food, Value = 26_000m } }
        });

        var status = await _budget.GetPlanStatusAsync(2026, 11);

        Assert.True(status.IsOverAssigned);
        Assert.Equal(5_000m, status.Savings!.Reserve); // kept when only the categories change
    }

    [Fact]
    public async Task PastMonth_IsMetOrNot()
    {
        await SalaryAsync();
        await PlanAsync(5_000m);
        await _expenseService.CreateAsync(Expense(28_000m));
        _dates.Today = new DateOnly(2026, 12, 3);

        var november = await SavingsAsync();

        Assert.True(november.IsClosed);
        Assert.False(november.IsMet);
        Assert.Equal(3_000m, november.Shortfall);
    }

    [Fact]
    public async Task ChangingTheReserveOfAPlanFromBefore_CreatesAVersion()
    {
        await SalaryAsync();
        var original = await PlanAsync(5_000m);
        _dates.Today = new DateOnly(2026, 12, 3);

        var version = await _planService.UpdateAsync(original, new BudgetPlanInput
        {
            Name = "Plan", EffectiveFrom = new DateOnly(2026, 11, 1), Savings = new SavingsInput { Value = 8_000m }
        });

        Assert.NotEqual(original, version);
        Assert.Equal(5_000m, (await SavingsAsync(11)).Reserve);
        Assert.Equal(8_000m, (await SavingsAsync(12)).Reserve);
    }

    [Theory]
    [InlineData(-1, 15)]
    [InlineData(101, 15)]
    [InlineData(10, 101)]
    public async Task InvalidSavings_AreRejected(int value, int threshold)
        => await Assert.ThrowsAsync<ArgumentException>(() => _planService.CreateAsync(new BudgetPlanInput
        {
            Name = "Plan", EffectiveFrom = new DateOnly(2026, 11, 1),
            Savings = new SavingsInput { Type = AllocationType.PercentageOfIncome, Value = value, AlertThresholdPercent = threshold }
        }));

    [Fact]
    public async Task SourceUsedByAReserve_CannotBeDeleted()
    {
        await _income.CreateSourceAsync(new IncomeSourceInput { Name = "Nuevo", Amount = 1_000m, StartDate = new DateOnly(2026, 11, 1) });
        await PlanAsync(10m, AllocationType.PercentageOfIncome, source: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _income.DeleteSourceAsync(1));
    }

    // ---------- Effect of saving an expense ----------

    [Fact]
    public async Task Impact_WarnsOnlyWhenTheExpenseStartsUsingTheSavings()
    {
        await SalaryAsync();
        await PlanAsync(5_000m);
        await _expenseService.CreateAsync(Expense(24_000m));

        var within = await _impact.PreviewAsync(Expense(500m));
        var crossing = await _impact.PreviewAsync(Expense(1_300m));

        Assert.Null(within.Warning);
        Assert.Equal("Te quedan RD$ 500.00 dentro del plan.", within.Effect);
        Assert.Equal("Este gasto usa RD$ 300.00 de tu ahorro. ¿Guardar igual?", crossing.Warning);
        Assert.Equal("Has usado RD$ 300.00 de tus RD$ 5,000.00 de ahorro.", crossing.Effect);

        await _expenseService.CreateAsync(Expense(1_300m)); // saved anyway
        Assert.Null((await _impact.PreviewAsync(Expense(100m))).Warning); // already using them
    }

    [Fact]
    public async Task Impact_OfAnEdit_ReplacesTheOldAmount_AndPastMonthsSayNothing()
    {
        await SalaryAsync();
        await PlanAsync(5_000m);
        var saved = await _expenseService.CreateAsync(Expense(24_000m));

        var edit = await _impact.PreviewAsync(Expense(20_000m), saved.Id);
        var past = await _impact.PreviewAsync(Expense(90_000m, new DateOnly(2026, 10, 1)));

        Assert.Equal("Te quedan RD$ 5,000.00 dentro del plan.", edit.Effect);
        Assert.Null(past.Warning);
        Assert.Null(past.Effect);
    }
}
