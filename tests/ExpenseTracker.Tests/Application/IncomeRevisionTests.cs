using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Tests.Fakes;

namespace ExpenseTracker.Tests.Application;

/// <summary>Phase 15B: a past month is calculated with the values it had.</summary>
public class IncomeRevisionTests
{
    private const int Savings = 1;

    private readonly FakeIncomeSourceRepository _sources = new();
    private readonly FakeRepository<ExchangeRate> _rates = new();
    private readonly FakeBudgetPlanRepository _plans = new();
    private readonly FixedDateProvider _dates = new(new DateOnly(2026, 11, 10));
    private readonly CurrencyService _currency;
    private readonly IncomeService _income;
    private readonly BudgetService _budget;
    private readonly ForecastService _forecast;

    public IncomeRevisionTests()
    {
        var settings = new FakeRepository<UserSettings>();
        settings.Items.Add(new UserSettings { Id = 1, BaseCurrency = Currency.DOP });
        var categories = new FakeRepository<Category>();
        categories.Items.Add(new Category { Id = Savings, Name = "Ahorro", Color = "#2E9E5B" });
        var expenses = new FakeExpenseRepository(categories);
        var debts = new FakeDebtRepository();

        _currency = new CurrencyService(settings, _rates, _dates);
        _income = new IncomeService(_sources, new FakeRepository<OneTimeIncome>(), new FakeRepository<BudgetAllocation>(), _currency, _dates);
        var ledger = new MonthlyLedgerService(expenses, debts, _currency, _dates);
        _budget = new BudgetService(_plans, categories, expenses, ledger, _income, _dates);
        _forecast = new ForecastService(expenses, debts, categories, _currency, _budget, _income, _dates);
    }

    private static DateOnly Month(int year, int month) => new(year, month, 1);

    private async Task<int> SalaryAsync(decimal amount = 50_000m, Currency currency = Currency.DOP, DateOnly? start = null)
        => (await _income.CreateSourceAsync(new IncomeSourceInput
        {
            Name = "Sueldo trabajo", Amount = amount, Currency = currency, StartDate = start ?? Month(2026, 8)
        })).Id;

    private static IncomeSourceInput Edit(decimal amount, DateOnly? changeFrom = null, Frequency frequency = Frequency.Monthly,
        DateOnly? start = null, DateOnly? end = null, bool active = true)
        => new()
        {
            Name = "Sueldo trabajo", Amount = amount, Frequency = frequency, StartDate = start ?? Month(2026, 8),
            EndDate = end, IsActive = active, ChangeFrom = changeFrom
        };

    private async Task<decimal> AssignedAsync(int month, int year = 2026)
        => (await _budget.GetPlanStatusAsync(year, month)).Categories.Single().Assigned;

    private async Task<decimal> IncomeAsync(int month, int year = 2026)
        => (await _income.GetMonthlyIncomeAsync(year, month)).Total;

    private void TenPercentForSavings(int? source = null)
        => _plans.Allocate(Savings, 10m, AllocationType.PercentageOfIncome, source);

    [Fact]
    public async Task RaiseFromDecember_LeavesNovemberAt5000_AndDecemberAt6000()
    {
        var id = await SalaryAsync();
        TenPercentForSavings(id);

        await _income.UpdateSourceAsync(id, Edit(60_000m, Month(2026, 12)));

        Assert.Equal(5_000m, await AssignedAsync(11));
        Assert.Equal(6_000m, await AssignedAsync(12));
        var dto = Assert.Single(await _income.ListSourcesAsync());
        Assert.Equal(50_000m, dto.Amount);
        Assert.Equal(Month(2026, 12), dto.NextRevision!.EffectiveFrom);
    }

    [Fact]
    public async Task RaiseFromThisMonth_DoesNotChangeEarlierMonths_AndIsReplacedNotDuplicated()
    {
        var id = await SalaryAsync();

        await _income.UpdateSourceAsync(id, Edit(60_000m));
        await _income.UpdateSourceAsync(id, Edit(61_000m));

        Assert.Equal(50_000m, await IncomeAsync(10));
        Assert.Equal(61_000m, await IncomeAsync(11));
        Assert.Equal(2, _sources.Items.Single().Revisions.Count);
    }

    [Fact]
    public async Task Change_CannotStartInThePast_AndRenamingAddsNoRevision()
    {
        var id = await SalaryAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _income.UpdateSourceAsync(id, Edit(60_000m, Month(2026, 10))));
        var rename = Edit(50_000m);
        rename.Name = "Otro nombre";
        await _income.UpdateSourceAsync(id, rename);

        Assert.Single(_sources.Items.Single().Revisions);
    }

    [Fact]
    public async Task CorrectHistory_ShowsTheMonthsBeforeSaving_AndOnlyThenChangesThem()
    {
        var id = await SalaryAsync();
        var correction = new IncomeRevisionInput { EffectiveFrom = Month(2026, 8), Until = Month(2026, 10), Amount = 45_000m };

        var preview = await _income.PreviewCorrectionAsync(id, correction);

        Assert.Equal(new[] { Month(2026, 8), Month(2026, 9), Month(2026, 10) }, preview.Months);
        Assert.Equal("Esto cambiará la evaluación de 3 meses pasados (agosto 2026 a octubre 2026).", preview.Warning);
        Assert.Equal(50_000m, await IncomeAsync(10));

        await _income.CorrectHistoryAsync(id, correction);

        Assert.Equal(45_000m, await IncomeAsync(8));
        Assert.Equal(45_000m, await IncomeAsync(10));
        Assert.Equal(50_000m, await IncomeAsync(11));
        Assert.Equal(50_000m, await IncomeAsync(12));
    }

    [Fact]
    public async Task CorrectHistory_BeforeTheStart_MovesTheStartBack()
    {
        var id = await SalaryAsync();

        await _income.CorrectHistoryAsync(id, new IncomeRevisionInput { EffectiveFrom = Month(2026, 6), Until = Month(2026, 7), Amount = 40_000m });

        Assert.Equal(40_000m, await IncomeAsync(6));
        Assert.Equal(50_000m, await IncomeAsync(8));
        Assert.Equal(Month(2026, 6), _sources.Items.Single().StartDate);
    }

    [Fact]
    public async Task FrequencyChangeFromJanuary_KeepsTheOccurrencesOfEarlierMonths()
    {
        var id = await SalaryAsync();

        await _income.UpdateSourceAsync(id, Edit(25_000m, Month(2027, 1), Frequency.Biweekly));

        Assert.Single(await _income.GetOccurrencesAsync(2026, 10));
        Assert.Single(await _income.GetOccurrencesAsync(2026, 12));
        Assert.True((await _income.GetOccurrencesAsync(2027, 1)).Count >= 2);
    }

    [Fact]
    public async Task ScheduledRaise_AppearsInNextYearEstimate()
    {
        var id = await SalaryAsync();
        await _income.UpdateSourceAsync(id, Edit(60_000m, Month(2027, 1)));

        var nextYear = await _forecast.GetNextYearAsync();

        Assert.Equal(720_000m, nextYear.ProjectedIncome);
    }

    [Fact]
    public async Task NewUsdRate_DoesNotChangeEarlierMonths()
    {
        _dates.Today = Month(2026, 8);
        await _currency.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60.50m });
        await SalaryAsync(1_000m, Currency.USD);
        TenPercentForSavings();
        _dates.Today = new DateOnly(2026, 11, 10);

        await _currency.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 61m });

        Assert.Equal(60_500m, await IncomeAsync(10));
        Assert.Equal(6_050m, await AssignedAsync(10));
        Assert.Equal(6_050m, await AssignedAsync(11)); // day 1 of November is before the new rate
        Assert.Equal(6_100m, await AssignedAsync(12));
        Assert.Equal(61_000m, await IncomeAsync(12));
        Assert.Equal(2, Assert.Single(await _currency.ListRatesAsync()).History.Count);
    }

    [Fact]
    public async Task Rate_UsedInPastMonths_CannotBeRemoved()
    {
        await _currency.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60m, EffectiveFrom = Month(2026, 9) });

        await Assert.ThrowsAsync<InvalidOperationException>(() => _currency.DeleteRateAsync(Currency.USD));
        Assert.Single(_rates.Items);
    }

    [Fact]
    public async Task Percentage_OfABiweeklySource_UsesTheMonthlyEquivalent()
    {
        var id = (await _income.CreateSourceAsync(new IncomeSourceInput
        {
            Name = "Quincena", Amount = 1_200m, Frequency = Frequency.Biweekly, StartDate = new DateOnly(2026, 1, 2)
        })).Id;
        TenPercentForSavings(id);

        // January has three paydays and February two: the plan does not move.
        Assert.Equal(260m, await AssignedAsync(1));
        Assert.Equal(260m, await AssignedAsync(2));
    }

    [Fact]
    public async Task SourceWithoutIncomeThatMonth_CountsZero_AndWarns()
    {
        var id = await SalaryAsync();
        TenPercentForSavings(id);

        var march = await _budget.GetPlanStatusAsync(2026, 3);
        var november = await _budget.GetPlanStatusAsync(2026, 11);

        Assert.Equal(0m, march.Categories.Single().Assigned);
        Assert.Equal("Sueldo trabajo no tenía ingreso en marzo 2026.", Assert.Single(march.Warnings));
        Assert.Empty(november.Warnings);
        Assert.Equal(50_000m, november.Categories.Single().BaseAmount);
        Assert.Equal("Sueldo trabajo", november.Categories.Single().BaseName);
    }

    [Fact]
    public async Task SourceUsedInPastMonths_CannotBeDeleted_OnlyFinished()
    {
        var id = await SalaryAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _income.DeleteSourceAsync(id));
        await _income.UpdateSourceAsync(id, Edit(50_000m, end: new DateOnly(2026, 11, 30)));

        Assert.Equal(50_000m, await IncomeAsync(10));
        Assert.Equal(0m, await IncomeAsync(12));
    }

    [Fact]
    public async Task SourceThatStartsThisMonth_CanBeDeleted()
    {
        var id = await SalaryAsync(start: Month(2026, 11));

        await _income.DeleteSourceAsync(id);

        Assert.Empty(_sources.Items);
    }

    [Fact]
    public async Task DeactivatingOrMovingDates_ThatReachPastMonths_NeedsConfirmation()
    {
        var id = await SalaryAsync();
        var off = Edit(50_000m, active: false);

        Assert.Equal(3, (await _income.PreviewUpdateAsync(id, off)).Months.Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _income.UpdateSourceAsync(id, off));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _income.UpdateSourceAsync(id, Edit(50_000m, start: Month(2026, 10))));
        Assert.Equal(50_000m, await IncomeAsync(8));

        off.ConfirmPastChanges = true;
        await _income.UpdateSourceAsync(id, off);
        Assert.Equal(0m, await IncomeAsync(8));
    }
}
