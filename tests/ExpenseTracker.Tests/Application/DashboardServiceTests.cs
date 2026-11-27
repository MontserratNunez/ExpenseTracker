using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Application.DTOs.Debts;
using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.DTOs.Goals;
using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Tests.Fakes;

namespace ExpenseTracker.Tests.Application;

public class DashboardServiceTests
{
    private const int Food = 1;
    private const int Outings = 2;
    private const int Services = 3;
    private const int Insurance = 4;
    private const int Savings = 5;

    private readonly FakeRepository<Category> _categories = new();
    private readonly FakeRepository<UserSettings> _settings = new();
    private readonly FakeRepository<ExchangeRate> _rates = new();
    private readonly FakeRepository<IncomeSource> _sources = new();
    private readonly FakeRepository<OneTimeIncome> _oneTimeIncome = new();
    private readonly FakeRepository<BudgetAllocation> _allocations = new();
    private readonly FakeBudgetPlanRepository _plans = new();
    private readonly FakeRepository<Goal> _goals = new();
    private readonly FakeDebtRepository _debts = new();
    private readonly FakeExpenseRepository _expenses;
    private readonly FixedDateProvider _dates = new(new DateOnly(2026, 11, 18));
    private readonly CurrencyService _currency;
    private readonly IncomeService _income;
    private readonly ExpenseService _expenseService;
    private readonly DebtService _debtService;
    private readonly BudgetService _budget;
    private readonly GoalService _goalService;
    private readonly DashboardService _service;

    public DashboardServiceTests()
    {
        _expenses = new FakeExpenseRepository(_categories);
        _settings.Items.Add(new UserSettings { Id = 1, BaseCurrency = Currency.DOP });
        _categories.Items.AddRange(new[]
        {
            new Category { Id = Food, Name = "Comida", Color = "#E67E22" },
            new Category { Id = Outings, Name = "Entretenimiento", Color = "#F1C40F" },
            new Category { Id = Services, Name = "Servicios", Color = "#3498DB" },
            new Category { Id = Insurance, Name = "Seguros", Color = "#16A085" },
            new Category { Id = Savings, Name = "Ahorro", Color = "#2E9E5B", IsSavings = true },
        });

        _currency = new CurrencyService(_settings, _rates, _dates);
        _income = new IncomeService(_sources, _oneTimeIncome, _allocations, _currency, _dates);
        var ledger = new MonthlyLedgerService(_expenses, _debts, _currency, _dates);
        _expenseService = new ExpenseService(_expenses, _categories, _currency, ledger, _dates);
        _debtService = new DebtService(_debts, _currency, _dates);
        _budget = new BudgetService(_plans, _categories, _expenses, ledger, _income, _dates);
        _goalService = new GoalService(_goals, _categories, _expenses, _currency, _budget, _dates);
        var forecast = new ForecastService(_expenses, _debts, _categories, _currency, _budget, _income, _dates);
        _service = new DashboardService(_income, ledger, _budget, _goalService, forecast, _dates);
    }

    private Task AddIncomeAsync(decimal amount)
        => _income.CreateSourceAsync(new IncomeSourceInput { Name = "Sueldo", Amount = amount, StartDate = new DateOnly(2026, 1, 1) });

    private Task SpendAsync(string name, decimal amount, DateOnly date, int category = Food)
        => _expenseService.CreateAsync(new ExpenseInput { Name = name, Amount = amount, Date = date, CategoryId = category });

    private Task AddRecurringAsync(string name, decimal amount, DateOnly start, Frequency frequency, int category)
        => _expenseService.CreateAsync(new ExpenseInput
        {
            Name = name, Amount = amount, Date = start, CategoryId = category, IsRecurring = true, Frequency = frequency
        });

    // ---------- Pie chart numbers (rule 5.3) ----------

    [Fact]
    public async Task Month_WithEnunciadoData_GreenPlusRedPlusGrayEqualsIncome()
    {
        await AddIncomeAsync(60_000m);
        await _income.CreateOneTimeAsync(new OneTimeIncomeInput { Description = "Bono", Amount = 5_000m, Date = new DateOnly(2026, 11, 5) });
        await SpendAsync("Supermercado", 4_885.40m, new DateOnly(2026, 11, 3));
        await SpendAsync("Supermercado", 700m, new DateOnly(2026, 11, 10));
        await SpendAsync("Cine", 381.20m, new DateOnly(2026, 11, 12), Outings);
        await AddRecurringAsync("Recibo luz", 1_300m, new DateOnly(2026, 1, 20), Frequency.Monthly, Services);   // pending
        await AddRecurringAsync("Seguro", 20_000m, new DateOnly(2025, 11, 25), Frequency.Annual, Insurance);     // pending

        var dashboard = await _service.GetAsync();

        Assert.Equal(65_000m, dashboard.Income);
        Assert.Equal(5_966.60m, dashboard.Spent);
        Assert.Equal(21_300m, dashboard.Committed);
        Assert.Equal(37_733.40m, dashboard.Available);
        Assert.Equal(dashboard.Income, dashboard.Spent + dashboard.Committed + dashboard.Available);
        Assert.False(dashboard.IsDeficit);
        Assert.Equal(0m, dashboard.DeficitAmount);
    }

    [Fact]
    public async Task Deficit_WhenSpentPlusCommittedExceedIncome_AvailableIsZero()
    {
        await AddIncomeAsync(1_000m);
        await SpendAsync("Compra", 800m, new DateOnly(2026, 11, 3));
        await AddRecurringAsync("Alquiler", 500m, new DateOnly(2026, 1, 25), Frequency.Monthly, Services); // pending

        var dashboard = await _service.GetAsync();

        Assert.True(dashboard.IsDeficit);
        Assert.Equal(0m, dashboard.Available);
        Assert.Equal(300m, dashboard.DeficitAmount);
        Assert.Equal(800m, dashboard.Spent);
        Assert.Equal(500m, dashboard.Committed);
    }

    [Fact]
    public async Task NoIncome_MeansEmptyState()
    {
        var dashboard = await _service.GetAsync();

        Assert.False(dashboard.HasIncome);
        Assert.Equal(0m, dashboard.Available);
        Assert.False(dashboard.IsDeficit);
        Assert.Empty(dashboard.UpcomingPayments);
        Assert.Empty(dashboard.Goals);
        Assert.False(dashboard.HasPlan);
    }

    [Fact]
    public async Task DebtPayments_AndPendingInstallments_FeedSpentAndCommitted()
    {
        await AddIncomeAsync(50_000m);
        var paid = await _debtService.CreateAsync(DebtInput("Prestamo", 10_000m, 2_000m, day: 5));
        await _debtService.RegisterPaymentAsync(paid.Id, new DebtPaymentInput { Amount = 2_000m, Date = new DateOnly(2026, 11, 5) });
        await _debtService.CreateAsync(DebtInput("Tarjeta", 9_000m, 3_000m, day: 25));

        var dashboard = await _service.GetAsync();

        Assert.Equal(2_000m, dashboard.Spent);
        Assert.Equal(3_000m, dashboard.Committed);
    }

    // ---------- Upcoming payments ----------

    [Fact]
    public async Task Upcoming_ListsRecurringAndInstallmentsWithinSevenDays_InDateOrder()
    {
        await AddIncomeAsync(50_000m);
        await AddRecurringAsync("Internet", 1_500m, new DateOnly(2026, 1, 22), Frequency.Monthly, Services);   // in 4 days
        await AddRecurringAsync("Seguro", 20_000m, new DateOnly(2025, 11, 25), Frequency.Annual, Insurance);   // in exactly 7 days
        await AddRecurringAsync("Gimnasio", 800m, new DateOnly(2026, 1, 26), Frequency.Monthly, Services);     // in 8 days: out
        await AddRecurringAsync("Netflix", 600m, new DateOnly(2026, 1, 15), Frequency.Monthly, Services);      // already happened
        await _debtService.CreateAsync(DebtInput("Tarjeta", 9_000m, 3_000m, day: 20));                        // in 2 days

        var upcoming = (await _service.GetAsync()).UpcomingPayments;

        Assert.Equal(new[] { "Tarjeta", "Internet", "Seguro" }, upcoming.Select(u => u.Name.Replace("Cuota: ", "")));
        Assert.True(upcoming[0].IsDebt);
        Assert.Equal(3_000m, upcoming[0].Amount);
        Assert.All(upcoming, u => Assert.False(u.IsOverdue));
    }

    [Fact]
    public async Task Upcoming_ReachesIntoNextMonth_NearTheEndOfTheMonth()
    {
        _dates.Today = new DateOnly(2026, 11, 28);
        await AddRecurringAsync("Alquiler", 15_000m, new DateOnly(2026, 1, 3), Frequency.Monthly, Services); // Dec 3

        var upcoming = (await _service.GetAsync()).UpcomingPayments;

        var item = Assert.Single(upcoming);
        Assert.Equal(new DateOnly(2026, 12, 3), item.Date);
    }

    [Fact]
    public async Task Upcoming_FlagsAnOverdueInstallment()
    {
        await _debtService.CreateAsync(DebtInput("Prestamo", 10_000m, 2_000m, day: 5)); // due Nov 5, unpaid

        var item = Assert.Single((await _service.GetAsync()).UpcomingPayments);

        Assert.True(item.IsOverdue);
        Assert.True(item.IsDebt);
        Assert.NotNull(item.DebtId);
    }

    [Fact]
    public async Task Upcoming_IgnoresOneTimeExpensesAndAlreadyPaidInstallments()
    {
        var debt = await _debtService.CreateAsync(DebtInput("Prestamo", 10_000m, 2_000m, day: 20));
        await _debtService.RegisterPaymentAsync(debt.Id, new DebtPaymentInput { Amount = 2_000m, Date = new DateOnly(2026, 11, 10) });
        await SpendAsync("Cena", 900m, new DateOnly(2026, 11, 19));

        Assert.Empty((await _service.GetAsync()).UpcomingPayments);
    }

    // ---------- Plan highlights ----------

    [Fact]
    public async Task PlanHighlights_AreTheThreeCategoriesClosestToTheirLimit()
    {
        await AddIncomeAsync(100_000m);
        foreach (var (category, assigned) in new[] { (Food, 10_000m), (Outings, 10_000m), (Services, 10_000m), (Insurance, 10_000m) })
            _plans.Allocate(category, assigned);

        await SpendAsync("Super", 9_000m, new DateOnly(2026, 11, 2), Food);        // ~ 50,000 estimated
        await SpendAsync("Cine", 3_000m, new DateOnly(2026, 11, 2), Outings);      // ~ 16,700
        await SpendAsync("Taller", 600m, new DateOnly(2026, 11, 2), Services);     // ~ 1,000
        await SpendAsync("Poliza", 100m, new DateOnly(2026, 11, 2), Insurance);    // lowest

        var dashboard = await _service.GetAsync();

        Assert.True(dashboard.HasPlan);
        Assert.Equal(new[] { Food, Outings, Services }, dashboard.PlanHighlights.Select(c => c.CategoryId));
    }

    [Fact]
    public async Task PlanHighlights_EmptyWithoutAPlan()
    {
        await SpendAsync("Super", 100m, new DateOnly(2026, 11, 2));

        var dashboard = await _service.GetAsync();

        Assert.False(dashboard.HasPlan);
        Assert.Empty(dashboard.PlanHighlights);
    }

    // ---------- Goals and forecast ----------

    [Fact]
    public async Task Goals_ListNameAndPercent()
    {
        await _goalService.CreateAsync(new GoalInput
        {
            Name = "Ahorro anual", TargetAmount = 200_000m, Period = GoalPeriod.Annual, Year = 2026, CategoryId = Savings
        });
        await SpendAsync("Aporte", 45_000m, new DateOnly(2026, 3, 1), Savings);

        var goal = Assert.Single((await _service.GetAsync()).Goals);

        Assert.Equal("Ahorro anual", goal.Name);
        Assert.Equal(22.5m, goal.ProgressPercent);
    }

    [Fact]
    public async Task NextMonthEstimate_ComesFromTheForecast()
    {
        await AddRecurringAsync("Luz", 1_300m, new DateOnly(2026, 1, 20), Frequency.Monthly, Services);

        Assert.Equal(1_300m, (await _service.GetAsync()).NextMonthEstimate);
    }

    private static DebtInput DebtInput(string name, decimal original, decimal installment, int day) => new()
    {
        Name = name, DebtType = DebtType.Loan, OriginalAmount = original, MonthlyPayment = installment,
        PaymentDay = day, StartDate = new DateOnly(2026, 1, 1)
    };
}
