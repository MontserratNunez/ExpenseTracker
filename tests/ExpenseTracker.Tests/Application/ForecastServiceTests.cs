using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Application.DTOs.Debts;
using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.DTOs.Forecast;
using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Tests.Fakes;

namespace ExpenseTracker.Tests.Application;

public class ForecastServiceTests
{
    private const int Food = 1;
    private const int Insurance = 2;
    private const int Subscriptions = 3;
    private const int Savings = 4;

    private readonly FakeRepository<Category> _categories = new();
    private readonly FakeRepository<UserSettings> _settings = new();
    private readonly FakeRepository<ExchangeRate> _rates = new();
    private readonly FakeRepository<IncomeSource> _sources = new();
    private readonly FakeRepository<OneTimeIncome> _oneTimeIncome = new();
    private readonly FakeRepository<BudgetAllocation> _allocations = new();
    private readonly FakeBudgetPlanRepository _plans = new();
    private readonly FakeDebtRepository _debts = new();
    private readonly FakeExpenseRepository _expenses;
    private readonly FixedDateProvider _dates = new(new DateOnly(2026, 11, 18));
    private readonly CurrencyService _currency;
    private readonly IncomeService _income;
    private readonly ExpenseService _expenseService;
    private readonly DebtService _debtService;
    private readonly BudgetService _budget;
    private readonly ForecastService _service;

    public ForecastServiceTests()
    {
        _expenses = new FakeExpenseRepository(_categories);
        _settings.Items.Add(new UserSettings { Id = 1, BaseCurrency = Currency.DOP });
        _categories.Items.AddRange(new[]
        {
            new Category { Id = Food, Name = "Comida", Color = "#E67E22" },
            new Category { Id = Insurance, Name = "Seguros", Color = "#16A085" },
            new Category { Id = Subscriptions, Name = "Suscripciones", Color = "#34495E" },
            new Category { Id = Savings, Name = "Ahorro", Color = "#2E9E5B", IsSavings = true },
        });

        _currency = new CurrencyService(_settings, _rates, _dates);
        _income = new IncomeService(_sources, _oneTimeIncome, _allocations, _currency, _dates);
        var ledger = new MonthlyLedgerService(_expenses, _debts, _currency, _dates);
        _expenseService = new ExpenseService(_expenses, _categories, _currency, ledger, _dates);
        _debtService = new DebtService(_debts, _currency, _dates);
        _budget = new BudgetService(_plans, _categories, _expenses, ledger, _income, _dates);
        _service = new ForecastService(_expenses, _debts, _categories, _currency, _budget, _income, _dates);
    }

    private Task AddRecurringAsync(string name, decimal amount, DateOnly start, Frequency frequency, int category,
        Currency currency = Currency.DOP, DateOnly? end = null)
        => _expenseService.CreateAsync(new ExpenseInput
        {
            Name = name, Amount = amount, Date = start, CategoryId = category, Currency = currency,
            IsRecurring = true, Frequency = frequency, EndDate = end
        });

    private Task AddOneTimeAsync(decimal amount, DateOnly date, int category = Food)
        => _expenseService.CreateAsync(new ExpenseInput { Name = "Gasto", Amount = amount, Date = date, CategoryId = category });

    private Task AddSalaryAsync(decimal amount, DateOnly? end = null)
        => _income.CreateSourceAsync(new IncomeSourceInput { Name = "Sueldo", Amount = amount, StartDate = new DateOnly(2026, 1, 1), EndDate = end });

    private static DebtInput SmallDebt(decimal original, decimal installment, int day = 10) => new()
    {
        Name = "Tarjeta", DebtType = DebtType.CreditCard, OriginalAmount = original, MonthlyPayment = installment,
        PaymentDay = day, StartDate = new DateOnly(2026, 1, 1)
    };

    // ---------- Horizons ----------

    [Fact]
    public async Task Horizons_CoverTheRangesOfThePlan()
    {
        var next = await _service.GetNextMonthAsync();
        var rest = await _service.GetRestOfYearAsync();
        var year = await _service.GetNextYearAsync();

        Assert.Equal((new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), (next.From, next.To));
        Assert.Equal((new DateOnly(2026, 11, 19), new DateOnly(2026, 12, 31)), (rest.From, rest.To));
        Assert.Equal((new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31)), (year.From, year.To));
        Assert.Equal(12, year.Months.Count);
    }

    [Fact]
    public async Task RestOfYear_OnDecember31_IsEmpty()
    {
        _dates.Today = new DateOnly(2026, 12, 31);

        var rest = await _service.GetRestOfYearAsync();

        Assert.Empty(rest.Months);
        Assert.Equal(0m, rest.TotalExpenses);
    }

    // ---------- Recurring ----------

    [Fact]
    public async Task NextYear_AnnualInsuranceAppearsOnlyInItsMonth()
    {
        await AddRecurringAsync("Seguro", 20_000m, new DateOnly(2025, 11, 25), Frequency.Annual, Insurance);

        var year = await _service.GetNextYearAsync();

        Assert.Equal(20_000m, year.RecurringTotal);
        Assert.Equal(20_000m, year.Months.Single(m => m.Month == 11).Recurring);
        Assert.All(year.Months.Where(m => m.Month != 11), m => Assert.Equal(0m, m.Recurring));
        Assert.Equal(1, year.RecurringItems.Single().Occurrences);
    }

    [Fact]
    public async Task NextMonth_CountsEveryOccurrenceOfMonthlyAndBiweeklyExpenses()
    {
        await AddRecurringAsync("Luz", 1_300m, new DateOnly(2026, 1, 20), Frequency.Monthly, Food);
        await AddRecurringAsync("Quincena", 100m, new DateOnly(2026, 11, 20), Frequency.Biweekly, Food); // Dec 4, Dec 18 -> 2

        var next = await _service.GetNextMonthAsync();

        Assert.Equal(1_300m + 2 * 100m, next.RecurringTotal);
    }

    [Fact]
    public async Task Recurring_StopsAtItsEndDate()
    {
        await AddRecurringAsync("Gym", 500m, new DateOnly(2026, 1, 5), Frequency.Monthly, Food, end: new DateOnly(2027, 3, 31));

        var year = await _service.GetNextYearAsync();

        Assert.Equal(3 * 500m, year.RecurringTotal); // Jan, Feb, Mar
    }

    [Fact]
    public async Task Recurring_ForeignCurrency_IsConvertedWithTheCurrentRate()
    {
        await _currency.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60.50m });
        await AddRecurringAsync("Netflix", 10m, new DateOnly(2026, 1, 15), Frequency.Monthly, Subscriptions, Currency.USD);

        Assert.Equal(605m, (await _service.GetNextMonthAsync()).RecurringTotal);
    }

    [Fact]
    public async Task RestOfYear_DoesNotCountWhatWasAlreadySpentThisMonth()
    {
        await AddRecurringAsync("Netflix", 600m, new DateOnly(2026, 1, 15), Frequency.Monthly, Subscriptions); // Nov 15 already happened
        await AddRecurringAsync("Luz", 1_300m, new DateOnly(2026, 1, 20), Frequency.Monthly, Food);            // Nov 20 still pending

        var rest = await _service.GetRestOfYearAsync();

        var november = rest.Months.Single(m => m.Month == 11);
        Assert.True(november.IsPartial);
        Assert.Equal(1_300m, november.Recurring);                     // only the pending one
        Assert.Equal(1_900m, rest.Months.Single(m => m.Month == 12).Recurring);
        Assert.Equal(3_200m, rest.RecurringTotal);
    }

    // ---------- Debts ----------

    [Fact]
    public async Task Debts_StopAddingInstallmentsOnceSettled()
    {
        // Balance 3,000 at 1,000 per month: Nov 10 (unpaid, overdue), Dec 10, Jan 10.
        await _debtService.CreateAsync(SmallDebt(3_000m, 1_000m));

        var nextMonth = await _service.GetNextMonthAsync();   // December
        var nextYear = await _service.GetNextYearAsync();     // only January is left

        Assert.Equal(1_000m, nextMonth.DebtsTotal);
        Assert.Equal(1_000m, nextYear.DebtsTotal);
        Assert.Equal(1_000m, nextYear.Months.Single(m => m.Month == 1).Debts);
        Assert.All(nextYear.Months.Where(m => m.Month != 1), m => Assert.Equal(0m, m.Debts));
    }

    [Fact]
    public async Task RestOfYear_IncludesTheUnpaidInstallmentOfThisMonth_EvenIfItsDateHasPassed()
    {
        await _debtService.CreateAsync(SmallDebt(3_000m, 1_000m)); // Nov 10 is before today (Nov 18) and unpaid

        var rest = await _service.GetRestOfYearAsync();

        Assert.Equal(2_000m, rest.DebtsTotal); // Nov (pending) + Dec
        Assert.Equal(1_000m, rest.Months.Single(m => m.Month == 11).Debts);
    }

    [Fact]
    public async Task RestOfYear_AfterPayingThisMonthsInstallment_OnlyCountsTheRest()
    {
        var debt = await _debtService.CreateAsync(SmallDebt(3_000m, 1_000m));
        await _debtService.RegisterPaymentAsync(debt.Id, new DebtPaymentInput { Amount = 1_000m, Date = new DateOnly(2026, 11, 12) });

        var rest = await _service.GetRestOfYearAsync();

        Assert.Equal(1_000m, rest.DebtsTotal); // only December; January falls next year
    }

    [Fact]
    public async Task Debts_SettledDebtAddsNothing()
    {
        var debt = await _debtService.CreateAsync(SmallDebt(1_000m, 1_000m));
        await _debtService.RegisterPaymentAsync(debt.Id, new DebtPaymentInput { Amount = 1_000m, Date = new DateOnly(2026, 11, 1) });

        Assert.Equal(0m, (await _service.GetNextYearAsync()).DebtsTotal);
    }

    [Fact]
    public async Task Debts_InterestMakesTheLastInstallmentDifferent()
    {
        await _debtService.CreateAsync(new DebtInput
        {
            Name = "Prestamo", OriginalAmount = 100_000m, AnnualInterestRate = 12m, MonthlyPayment = 8_884.88m,
            PaymentDay = 15, StartDate = new DateOnly(2026, 12, 1)
        });

        var year = await _service.GetNextYearAsync();

        // The loan lasts 12 installments starting in December 2026: one falls in 2026 and the other 11 in 2027.
        Assert.Equal(11, year.DebtItems.Single().Occurrences);
        Assert.Equal(11, year.Months.Count(m => m.Debts > 0));
    }

    // ---------- Variables ----------

    [Fact]
    public async Task Variables_UseTheAverageOfTheLastThreeFullMonths()
    {
        await AddOneTimeAsync(3_000m, new DateOnly(2026, 8, 10));
        await AddOneTimeAsync(2_000m, new DateOnly(2026, 9, 10));
        await AddOneTimeAsync(4_000m, new DateOnly(2026, 10, 10));
        await AddOneTimeAsync(9_999m, new DateOnly(2026, 11, 5));   // current month: not part of the history
        await AddOneTimeAsync(9_999m, new DateOnly(2026, 7, 31));   // too old

        var next = await _service.GetNextMonthAsync();
        var year = await _service.GetNextYearAsync();

        Assert.Equal(3_000m, next.VariablesTotal);                  // 9,000 / 3
        Assert.Equal(36_000m, year.VariablesTotal);
        Assert.Equal(VariableBasis.History, next.VariableItems.Single().Basis);
        Assert.True(next.UsesHistory);
        Assert.False(next.UsesPlan);
    }

    [Fact]
    public async Task Variables_RestOfYear_ScalesTheCurrentMonthByRemainingDays()
    {
        await AddOneTimeAsync(9_000m, new DateOnly(2026, 10, 10));  // 3,000 per month

        var rest = await _service.GetRestOfYearAsync();

        // November has 30 days and 12 remain after the 18th: 3,000 x 12/30 = 1,200, plus all of December
        Assert.Equal(1_200m, rest.Months.Single(m => m.Month == 11).Variables);
        Assert.Equal(3_000m, rest.Months.Single(m => m.Month == 12).Variables);
        Assert.Equal(4_200m, rest.VariablesTotal);
    }

    [Fact]
    public async Task Variables_WithoutHistory_FallBackToThePlan()
    {
        _plans.Allocate(Food, 10_000m);

        var next = await _service.GetNextMonthAsync();

        Assert.Equal(10_000m, next.VariablesTotal);
        var item = Assert.Single(next.VariableItems);
        Assert.Equal(VariableBasis.Plan, item.Basis);
        Assert.Equal("Comida", item.CategoryName);
        Assert.True(next.UsesPlan);
        Assert.False(next.UsesHistory);
    }

    [Fact]
    public async Task Variables_HistoryWinsOverThePlan_ForTheSameCategory()
    {
        _plans.Allocate(Food, 10_000m);
        await AddOneTimeAsync(3_000m, new DateOnly(2026, 10, 10));

        var next = await _service.GetNextMonthAsync();

        Assert.Equal(1_000m, next.VariablesTotal);
        Assert.Equal(VariableBasis.History, next.VariableItems.Single().Basis);
    }

    [Fact]
    public async Task Variables_PlanFallback_DoesNotCountWhatRecurringExpensesAlreadyCover()
    {
        // Plan assigns 5,000 to savings and 3,000 of it is an automatic monthly saving.
        _plans.Allocate(Savings, 5_000m);
        await AddRecurringAsync("Ahorro automatico", 3_000m, new DateOnly(2026, 1, 1), Frequency.Monthly, Savings);

        var next = await _service.GetNextMonthAsync();

        Assert.Equal(3_000m, next.RecurringTotal);
        Assert.Equal(2_000m, next.VariablesTotal);
    }

    [Fact]
    public async Task Variables_PlanFullyCoveredByRecurring_AddsNothing()
    {
        _plans.Allocate(Savings, 3_000m);
        await AddRecurringAsync("Ahorro automatico", 3_000m, new DateOnly(2026, 1, 1), Frequency.Monthly, Savings);

        Assert.Equal(0m, (await _service.GetNextMonthAsync()).VariablesTotal);
    }

    // ---------- Income and balance ----------

    [Fact]
    public async Task Balance_IsProjectedIncomeMinusEstimatedExpenses()
    {
        await AddSalaryAsync(50_000m);
        await AddRecurringAsync("Luz", 1_300m, new DateOnly(2026, 1, 20), Frequency.Monthly, Food);
        await AddOneTimeAsync(9_000m, new DateOnly(2026, 10, 10));

        var next = await _service.GetNextMonthAsync();

        Assert.Equal(50_000m, next.ProjectedIncome);
        Assert.Equal(1_300m + 3_000m, next.TotalExpenses);
        Assert.Equal(50_000m - 4_300m, next.Balance);
    }

    [Fact]
    public async Task Income_StopsWhenTheSourceEnds_AndIncludesRegisteredOneTimeIncome()
    {
        await AddSalaryAsync(10_000m, end: new DateOnly(2027, 6, 30));
        await _income.CreateOneTimeAsync(new OneTimeIncomeInput { Description = "Bono", Amount = 5_000m, Date = new DateOnly(2027, 12, 15) });

        var year = await _service.GetNextYearAsync();

        Assert.Equal(6 * 10_000m + 5_000m, year.ProjectedIncome);
        Assert.Equal(5_000m, year.Months.Single(m => m.Month == 12).Income);
    }

    [Fact]
    public async Task NegativeBalance_WhenExpensesExceedIncome()
    {
        await AddSalaryAsync(1_000m);
        await AddRecurringAsync("Alquiler", 5_000m, new DateOnly(2026, 1, 1), Frequency.Monthly, Food);

        Assert.Equal(-4_000m, (await _service.GetNextMonthAsync()).Balance);
    }

    // ---------- Month table ----------

    [Fact]
    public async Task MonthTable_AddsUpToTheTotals()
    {
        await AddSalaryAsync(50_000m);
        await AddRecurringAsync("Seguro", 20_000m, new DateOnly(2025, 11, 25), Frequency.Annual, Insurance);
        await AddRecurringAsync("Luz", 1_300m, new DateOnly(2026, 1, 20), Frequency.Monthly, Food);
        await AddOneTimeAsync(9_000m, new DateOnly(2026, 10, 10));
        await _debtService.CreateAsync(SmallDebt(30_000m, 1_000m));

        var year = await _service.GetNextYearAsync();

        Assert.Equal(year.RecurringTotal, year.Months.Sum(m => m.Recurring));
        Assert.Equal(year.DebtsTotal, year.Months.Sum(m => m.Debts));
        Assert.Equal(year.VariablesTotal, year.Months.Sum(m => m.Variables));
        Assert.Equal(year.TotalExpenses, year.RecurringTotal + year.DebtsTotal + year.VariablesTotal);
        Assert.Equal(year.Balance, year.ProjectedIncome - year.TotalExpenses);
        Assert.All(year.Months, m => Assert.Equal(m.Income - m.TotalExpenses, m.Balance));
        Assert.All(year.Months, m => Assert.False(m.IsPartial));
    }

    [Fact]
    public async Task Estimate_InvalidRange_ReturnsEmpty()
    {
        var result = await _service.EstimateAsync(new DateOnly(2027, 1, 1), new DateOnly(2026, 1, 1));

        Assert.Empty(result.Months);
    }
}
