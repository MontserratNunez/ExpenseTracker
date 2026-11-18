using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Tests.Fakes;

namespace ExpenseTracker.Tests.Application;

public class IncomeServiceTests
{
    private readonly FakeRepository<IncomeSource> _sources = new();
    private readonly FakeRepository<OneTimeIncome> _oneTime = new();
    private readonly FakeRepository<UserSettings> _settings = new();
    private readonly FakeRepository<ExchangeRate> _rates = new();
    private readonly FixedDateProvider _dates = new(new DateOnly(2026, 11, 18));
    private readonly CurrencyService _currency;
    private readonly IncomeService _service;

    public IncomeServiceTests()
    {
        _settings.Items.Add(new UserSettings { Id = 1, BaseCurrency = Currency.DOP });
        _currency = new CurrencyService(_settings, _rates, _dates);
        _service = new IncomeService(_sources, _oneTime, _currency, _dates);
    }

    private static IncomeSourceInput Source(string name, decimal amount, DateOnly start, DateOnly? end = null,
        Frequency frequency = Frequency.Monthly, Currency currency = Currency.DOP, bool active = true)
        => new() { Name = name, Amount = amount, StartDate = start, EndDate = end, Frequency = frequency, Currency = currency, IsActive = active };

    private Task AddBonusAsync(decimal amount, DateOnly date, Currency currency = Currency.DOP)
        => _service.CreateOneTimeAsync(new OneTimeIncomeInput { Description = "Bono", Amount = amount, Date = date, Currency = currency });

    [Fact]
    public async Task MonthlyIncome_MatchesEnunciadoExample_65000()
    {
        await _service.CreateSourceAsync(Source("Sueldo trabajo", 50_000m, new DateOnly(2026, 1, 1)));
        await _service.CreateSourceAsync(Source("Sueldo universidad", 10_000m, new DateOnly(2026, 1, 1)));
        await AddBonusAsync(5_000m, new DateOnly(2026, 11, 10));

        var result = await _service.GetMonthlyIncomeAsync(2026, 11);

        Assert.Equal(65_000m, result.Total);
        Assert.Equal(60_000m, result.RecurringTotal);
        Assert.Equal(5_000m, result.OneTimeTotal);
        Assert.Equal(3, result.Lines.Count);
    }

    [Fact]
    public async Task MonthlyIncome_SourceWithPastEndDate_DoesNotCountInLaterMonths()
    {
        await _service.CreateSourceAsync(Source("Contrato", 20_000m, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)));

        Assert.Equal(20_000m, (await _service.GetMonthlyIncomeAsync(2026, 6)).Total);
        Assert.Equal(0m, (await _service.GetMonthlyIncomeAsync(2026, 7)).Total);
        Assert.Equal(0m, (await _service.GetMonthlyIncomeAsync(2026, 11)).Total);
    }

    [Fact]
    public async Task MonthlyIncome_SourceNotStartedYet_DoesNotCount()
    {
        await _service.CreateSourceAsync(Source("Futuro", 1_000m, new DateOnly(2027, 1, 1)));

        Assert.Equal(0m, (await _service.GetMonthlyIncomeAsync(2026, 11)).Total);
    }

    [Fact]
    public async Task MonthlyIncome_InactiveSource_IsIgnored()
    {
        await _service.CreateSourceAsync(Source("Pausado", 5_000m, new DateOnly(2026, 1, 1), active: false));

        Assert.Equal(0m, (await _service.GetMonthlyIncomeAsync(2026, 11)).Total);
    }

    [Fact]
    public async Task MonthlyIncome_BiweeklyCountsTwiceOrThreeTimesDependingOnCalendar()
    {
        await _service.CreateSourceAsync(Source("Quincena", 1_000m, new DateOnly(2026, 1, 2), frequency: Frequency.Biweekly));

        // Biweekly from Jan 2 lands on Jan 2/16/30, Feb 13/27, Mar 13/27...
        Assert.Equal(3_000m, (await _service.GetMonthlyIncomeAsync(2026, 1)).Total);
        Assert.Equal(2_000m, (await _service.GetMonthlyIncomeAsync(2026, 2)).Total);
        Assert.Equal(3, (await _service.GetMonthlyIncomeAsync(2026, 1)).Lines.Single().Occurrences);
    }

    [Fact]
    public async Task MonthlyIncome_AnnualSourceOnlyCountsInItsMonth()
    {
        await _service.CreateSourceAsync(Source("Bonificación", 30_000m, new DateOnly(2025, 12, 15), frequency: Frequency.Annual));

        Assert.Equal(30_000m, (await _service.GetMonthlyIncomeAsync(2026, 12)).Total);
        Assert.Equal(0m, (await _service.GetMonthlyIncomeAsync(2026, 11)).Total);
    }

    [Fact]
    public async Task MonthlyIncome_ForeignCurrencySource_IsConvertedToBase()
    {
        await _currency.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60m });
        await _service.CreateSourceAsync(Source("Freelance", 500m, new DateOnly(2026, 1, 1), currency: Currency.USD));

        var result = await _service.GetMonthlyIncomeAsync(2026, 11);

        Assert.Equal(30_000m, result.Total);
        Assert.Equal(500m, result.Lines.Single().Amount);
    }

    [Fact]
    public async Task OneTime_StoresAmountInBase_AndListsOnlyThatMonth()
    {
        await _currency.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60.50m });
        await AddBonusAsync(10m, new DateOnly(2026, 11, 5), Currency.USD);
        await AddBonusAsync(100m, new DateOnly(2026, 10, 31));

        var november = await _service.ListOneTimeAsync(2026, 11);

        var item = Assert.Single(november);
        Assert.Equal(605.00m, item.AmountInBase);
        Assert.Equal(Currency.USD, item.Currency);
    }

    [Fact]
    public async Task OneTime_WithoutRate_ThrowsAndSavesNothing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => AddBonusAsync(10m, new DateOnly(2026, 11, 5), Currency.USD));

        Assert.Empty(_oneTime.Items);
    }

    [Fact]
    public async Task OneTime_Update_RecalculatesAmountInBase()
    {
        await _currency.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60m });
        var created = await _service.CreateOneTimeAsync(
            new OneTimeIncomeInput { Description = "Venta", Amount = 10m, Currency = Currency.USD, Date = new DateOnly(2026, 11, 1) });

        var updated = await _service.UpdateOneTimeAsync(created.Id,
            new OneTimeIncomeInput { Description = "Venta", Amount = 20m, Currency = Currency.USD, Date = new DateOnly(2026, 11, 1) });

        Assert.Equal(1_200m, updated.AmountInBase);
    }

    [Fact]
    public async Task Source_EndBeforeStart_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateSourceAsync(Source("X", 100m, new DateOnly(2026, 5, 1), new DateOnly(2026, 4, 1))));
    }

    [Fact]
    public async Task Source_NonPositiveAmountOrEmptyName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateSourceAsync(Source("X", 0m, new DateOnly(2026, 1, 1))));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateSourceAsync(Source(" ", 10m, new DateOnly(2026, 1, 1))));
    }

    [Fact]
    public async Task Source_UpdateAndDelete_Work_AndMissingIdThrows()
    {
        var created = await _service.CreateSourceAsync(Source("Sueldo", 100m, new DateOnly(2026, 1, 1)));

        var updated = await _service.UpdateSourceAsync(created.Id, Source("Sueldo 2", 200m, new DateOnly(2026, 1, 1)));
        Assert.Equal("Sueldo 2", updated.Name);
        Assert.Equal(200m, _sources.Items.Single().Amount);

        await _service.DeleteSourceAsync(created.Id);
        Assert.Empty(_sources.Items);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteSourceAsync(created.Id));
    }

    [Fact]
    public async Task RecurringMonthlyTotal_UsesMonthlyEquivalent_OfCurrentSourcesOnly()
    {
        await _service.CreateSourceAsync(Source("Sueldo", 50_000m, new DateOnly(2026, 1, 1)));
        await _service.CreateSourceAsync(Source("Quincena", 1_200m, new DateOnly(2026, 1, 2), frequency: Frequency.Biweekly)); // 2,600
        await _service.CreateSourceAsync(Source("Terminado", 9_999m, new DateOnly(2025, 1, 1), new DateOnly(2026, 3, 1)));
        await _service.CreateSourceAsync(Source("Pausado", 9_999m, new DateOnly(2026, 1, 1), active: false));

        Assert.Equal(52_600m, await _service.GetRecurringMonthlyTotalAsync());
    }

    [Fact]
    public async Task MonthlyIncome_InvalidMonth_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetMonthlyIncomeAsync(2026, 13));
    }
}
