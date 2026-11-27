using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.DTOs.Goals;
using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Tests.Fakes;

namespace ExpenseTracker.Tests.Application;

public class GoalServiceTests
{
    private const int Savings = 1;
    private const int Food = 2;

    private readonly FakeRepository<Category> _categories = new();
    private readonly FakeRepository<UserSettings> _settings = new();
    private readonly FakeRepository<ExchangeRate> _rates = new();
    private readonly FakeIncomeSourceRepository _sources = new();
    private readonly FakeRepository<OneTimeIncome> _oneTime = new();
    private readonly FakeRepository<BudgetAllocation> _allocations = new();
    private readonly FakeBudgetPlanRepository _plans = new();
    private readonly FakeRepository<Goal> _goals = new();
    private readonly FakeDebtRepository _debts = new();
    private readonly FakeExpenseRepository _expenses;
    private readonly FixedDateProvider _dates = new(new DateOnly(2026, 2, 28));
    private readonly CurrencyService _currency;
    private readonly ExpenseService _expenseService;
    private readonly BudgetService _budget;
    private readonly GoalService _service;

    public GoalServiceTests()
    {
        _expenses = new FakeExpenseRepository(_categories);
        _settings.Items.Add(new UserSettings { Id = 1, BaseCurrency = Currency.DOP });
        _categories.Items.Add(new Category { Id = Savings, Name = "Ahorro", Color = "#2E9E5B", IsSavings = true });
        _categories.Items.Add(new Category { Id = Food, Name = "Comida", Color = "#E67E22" });

        _currency = new CurrencyService(_settings, _rates, _dates);
        var income = new IncomeService(_sources, _oneTime, _allocations, _currency, _dates);
        var ledger = new MonthlyLedgerService(_expenses, _debts, _currency, _dates);
        _expenseService = new ExpenseService(_expenses, _categories, _currency, ledger, _dates);
        _budget = new BudgetService(_plans, _categories, _expenses, ledger, income, _dates);
        _service = new GoalService(_goals, _categories, _expenses, _currency, _budget, _dates);
    }

    private static GoalInput Annual200k(int year = 2026) => new()
    {
        Name = "Ahorro de 200,000 anualmente", TargetAmount = 200_000m, Period = GoalPeriod.Annual, Year = year, CategoryId = Savings
    };

    private Task SaveAsync(decimal amount, DateOnly date, int category = Savings)
        => _expenseService.CreateAsync(new ExpenseInput { Name = "Aporte", Amount = amount, Date = date, CategoryId = category });

    private async Task<GoalStatusDto> StatusAsync() => Assert.Single(await _service.GetGoalsStatusAsync());

    /// <summary>The enunciado numbers: 45,000 saved toward 200,000 with 10 months left.</summary>
    private async Task Seed45kAsync()
    {
        await SaveAsync(15_000m, new DateOnly(2026, 1, 10));
        await SaveAsync(15_000m, new DateOnly(2026, 2, 10));
        await SaveAsync(15_000m, new DateOnly(2026, 2, 20));
    }

    // ---------- Enunciado example ----------

    [Fact]
    public async Task Status_MatchesEnunciadoExample_45kOf200k()
    {
        await _service.CreateAsync(Annual200k());
        await Seed45kAsync();

        var status = await StatusAsync();

        Assert.Equal(45_000m, status.Progress);
        Assert.Equal(22.5m, status.ProgressPercent);
        Assert.Equal(155_000m, status.Remaining);
        Assert.Equal(10, status.RemainingMonths);
        Assert.Equal(15_500m, status.RequiredMonthly);
        Assert.Equal("Ahorro", status.Goal.CategoryName);
    }

    [Fact]
    public async Task Annual_UsesJanFirstToDecThirtyFirstOfTheGivenYear()
    {
        var goal = await _service.CreateAsync(Annual200k(2026));

        Assert.Equal(new DateOnly(2026, 1, 1), goal.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 31), goal.EndDate);
    }

    [Fact]
    public async Task Progress_OnlyCountsTheLinkedCategoryWithinThePeriod()
    {
        await _service.CreateAsync(Annual200k());
        await SaveAsync(10_000m, new DateOnly(2026, 2, 1));
        await SaveAsync(9_999m, new DateOnly(2025, 12, 31));            // before the year
        await SaveAsync(8_888m, new DateOnly(2026, 2, 1), Food);        // other category

        Assert.Equal(10_000m, (await StatusAsync()).Progress);
    }

    [Fact]
    public async Task Progress_CountsRecurringContributionsAlreadyMade_NotFutureOnes()
    {
        await _service.CreateAsync(Annual200k());
        await _expenseService.CreateAsync(new ExpenseInput
        {
            Name = "Ahorro automatico", Amount = 5_000m, CategoryId = Savings, IsRecurring = true,
            Frequency = Frequency.Monthly, Date = new DateOnly(2026, 1, 15)
        });

        // Today is Feb 28: Jan 15 and Feb 15 happened; Mar 15 onwards have not.
        Assert.Equal(10_000m, (await StatusAsync()).Progress);
    }

    [Fact]
    public async Task Progress_ForeignCurrencyRecurring_IsConvertedToBase()
    {
        await _currency.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60m });
        await _service.CreateAsync(Annual200k());
        await _expenseService.CreateAsync(new ExpenseInput
        {
            Name = "Ahorro USD", Amount = 100m, Currency = Currency.USD, CategoryId = Savings, IsRecurring = true,
            Frequency = Frequency.Monthly, Date = new DateOnly(2026, 2, 1)
        });

        Assert.Equal(6_000m, (await StatusAsync()).Progress);
    }

    // ---------- Projection and state ----------

    [Fact]
    public async Task Projection_OnTrack_WhenAverageContributionReachesTheTarget()
    {
        await _service.CreateAsync(Annual200k());
        await Seed45kAsync();

        var status = await StatusAsync();

        // Jan and Feb elapsed: 22,500 per month; 45,000 + 22,500 x 10
        Assert.Equal(22_500m, status.AverageMonthly);
        Assert.Equal(270_000m, status.Projection);
        Assert.Equal(GoalState.OnTrack, status.State);
        Assert.Equal(0m, status.Shortfall);
    }

    [Fact]
    public async Task Projection_Behind_ReportsTheShortfall()
    {
        await _service.CreateAsync(Annual200k());
        await SaveAsync(10_000m, new DateOnly(2026, 2, 1));

        var status = await StatusAsync();

        Assert.Equal(5_000m, status.AverageMonthly);
        Assert.Equal(60_000m, status.Projection);
        Assert.Equal(GoalState.Behind, status.State);
        Assert.Equal(140_000m, status.Shortfall);
    }

    [Fact]
    public async Task RequiredMonthly_IsRecalculatedAsMonthsPass()
    {
        await _service.CreateAsync(Annual200k());
        await Seed45kAsync();
        Assert.Equal(15_500m, (await StatusAsync()).RequiredMonthly);

        _dates.Today = new DateOnly(2026, 3, 31);

        var march = await StatusAsync();
        Assert.Equal(9, march.RemainingMonths);
        Assert.Equal(17_222.22m, march.RequiredMonthly);
    }

    [Fact]
    public async Task RemainingMonths_IsAtLeastOne_InTheLastMonth()
    {
        _dates.Today = new DateOnly(2026, 12, 20);
        await _service.CreateAsync(Annual200k());

        var status = await StatusAsync();

        Assert.Equal(1, status.RemainingMonths);
        Assert.Equal(200_000m, status.RequiredMonthly);
    }

    [Fact]
    public async Task Completed_WhenProgressReachesTarget()
    {
        await _service.CreateAsync(Annual200k());
        await SaveAsync(200_000m, new DateOnly(2026, 2, 1));

        var status = await StatusAsync();

        Assert.Equal(GoalState.Completed, status.State);
        Assert.Equal(100m, status.ProgressPercent);
        Assert.Equal(0m, status.Remaining);
        Assert.Equal(0m, status.RequiredMonthly);
    }

    [Fact]
    public async Task NotStarted_WhenThePeriodBeginsInTheFuture()
    {
        await _service.CreateAsync(Annual200k(2027));

        var status = await StatusAsync();

        Assert.Equal(GoalState.NotStarted, status.State);
        Assert.Equal(0m, status.Progress);
        Assert.Equal(0m, status.Shortfall);
    }

    [Fact]
    public async Task Missed_WhenThePeriodEndedWithoutReachingTheTarget()
    {
        await _service.CreateAsync(Annual200k(2025));
        await SaveAsync(50_000m, new DateOnly(2025, 6, 1));

        var status = await StatusAsync();

        Assert.Equal(GoalState.Missed, status.State);
        Assert.Equal(150_000m, status.Shortfall);
    }

    // ---------- Monthly and custom periods ----------

    [Fact]
    public async Task Monthly_RestartsEveryMonth()
    {
        await _service.CreateAsync(new GoalInput
        {
            Name = "Ahorro mensual", TargetAmount = 10_000m, Period = GoalPeriod.Monthly, CategoryId = Savings
        });
        await SaveAsync(4_000m, new DateOnly(2026, 1, 15));   // last month: ignored
        await SaveAsync(2_500m, new DateOnly(2026, 2, 5));

        var february = await StatusAsync();
        Assert.Equal(2_500m, february.Progress);
        Assert.Equal(7_500m, february.RequiredMonthly);
        Assert.Equal(new DateOnly(2026, 2, 1), february.Goal.StartDate);
        Assert.Equal(new DateOnly(2026, 2, 28), february.Goal.EndDate);

        _dates.Today = new DateOnly(2026, 3, 1);
        var march = await StatusAsync();
        Assert.Equal(0m, march.Progress);
        Assert.Equal(10_000m, march.RequiredMonthly);
    }

    [Fact]
    public async Task Custom_UsesTheGivenDates()
    {
        await _service.CreateAsync(new GoalInput
        {
            Name = "Viaje", TargetAmount = 60_000m, Period = GoalPeriod.Custom, CategoryId = Savings,
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 8, 31)
        });
        await SaveAsync(12_000m, new DateOnly(2026, 2, 1));

        var status = await StatusAsync();

        Assert.Equal(6, status.RemainingMonths);              // Feb 28 -> Aug 31
        Assert.Equal(8_000m, status.RequiredMonthly);         // 48,000 / 6
    }

    // ---------- Plan hint ----------

    [Fact]
    public async Task PlanHint_WarnsWhenPlanAssignsLessThanTheGoalNeeds()
    {
        await _service.CreateAsync(Annual200k());
        await Seed45kAsync();
        _plans.Allocate(Savings, 5_000m);

        var status = await StatusAsync();

        Assert.Equal(5_000m, status.PlannedMonthly);
        Assert.True(status.PlanBelowRequired); // needs 15,500 per month
    }

    [Fact]
    public async Task PlanHint_NoWarning_WhenPlanCoversTheRequirementOrThereIsNoAllocation()
    {
        await _service.CreateAsync(Annual200k());
        await Seed45kAsync();

        var withoutPlan = await StatusAsync();
        Assert.Null(withoutPlan.PlannedMonthly);
        Assert.False(withoutPlan.PlanBelowRequired);

        _plans.Allocate(Savings, 20_000m);
        Assert.False((await StatusAsync()).PlanBelowRequired);
    }

    // ---------- CRUD and validation ----------

    [Fact]
    public async Task Create_InvalidInput_Throws()
    {
        var noName = Annual200k(); noName.Name = " ";
        var noTarget = Annual200k(); noTarget.TargetAmount = 0m;
        var badYear = Annual200k(1900);
        var badRange = new GoalInput
        {
            Name = "X", TargetAmount = 1m, Period = GoalPeriod.Custom, CategoryId = Savings,
            StartDate = new DateOnly(2026, 5, 1), EndDate = new DateOnly(2026, 4, 1)
        };
        var noCategory = Annual200k(); noCategory.CategoryId = 99;

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(noName));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(noTarget));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(badYear));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(badRange));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(noCategory));
    }

    [Fact]
    public async Task Update_ChangesPeriodAndTarget_AndDeleteRemovesIt()
    {
        var goal = await _service.CreateAsync(Annual200k());

        var changed = Annual200k(2027);
        changed.TargetAmount = 300_000m;
        var updated = await _service.UpdateAsync(goal.Id, changed);

        Assert.Equal(300_000m, updated.TargetAmount);
        Assert.Equal(new DateOnly(2027, 12, 31), updated.EndDate);
        Assert.Equal(2027, (await _service.GetByIdAsync(goal.Id))!.StartDate.Year);

        await _service.DeleteAsync(goal.Id);
        Assert.Null(await _service.GetByIdAsync(goal.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(goal.Id));
    }

    [Fact]
    public async Task Status_WithNoGoals_IsEmpty_AndOrdersActiveBeforeFinished()
    {
        Assert.Empty(await _service.GetGoalsStatusAsync());

        await _service.CreateAsync(Annual200k());
        var done = Annual200k(); done.Name = "Completada"; done.TargetAmount = 1_000m;
        await _service.CreateAsync(done);
        await SaveAsync(5_000m, new DateOnly(2026, 2, 1));

        var list = await _service.GetGoalsStatusAsync();

        Assert.Equal(2, list.Count);
        Assert.Equal(GoalState.Completed, list[^1].State);
    }
}
