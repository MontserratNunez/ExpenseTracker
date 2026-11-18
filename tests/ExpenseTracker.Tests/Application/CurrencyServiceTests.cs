using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Tests.Fakes;

namespace ExpenseTracker.Tests.Application;

public class CurrencyServiceTests
{
    private readonly FakeRepository<UserSettings> _settings = new();
    private readonly FakeRepository<ExchangeRate> _rates = new();
    private readonly FixedDateProvider _dates = new(new DateOnly(2026, 11, 18));
    private readonly CurrencyService _service;

    public CurrencyServiceTests()
    {
        _settings.Items.Add(new UserSettings { Id = 1, BaseCurrency = Currency.DOP });
        _service = new CurrencyService(_settings, _rates, _dates);
    }

    [Fact]
    public async Task Convert_TenUsdAt6050_Returns605()
    {
        await _service.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60.50m });

        var result = await _service.ConvertToBaseAsync(10m, Currency.USD);

        Assert.Equal(605.00m, result);
    }

    [Fact]
    public async Task Convert_BaseCurrency_ReturnsSameAmountWithoutRate()
    {
        Assert.Equal(123.45m, await _service.ConvertToBaseAsync(123.45m, Currency.DOP));
    }

    [Fact]
    public async Task Convert_WithoutRate_ThrowsConfigureMessage()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ConvertToBaseAsync(10m, Currency.USD));

        Assert.Equal("Configure la tasa de USD en Configuración", ex.Message);
    }

    [Fact]
    public async Task Convert_RoundsToTwoDecimals()
    {
        await _service.SetRateAsync(new ExchangeRateInput { Currency = Currency.EUR, RateToBase = 65.123456m });

        Assert.Equal(21.49m, await _service.ConvertToBaseAsync(0.33m, Currency.EUR));
    }

    [Fact]
    public async Task SetRate_Twice_UpdatesInsteadOfDuplicating_AndStampsDate()
    {
        await _service.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60m });
        _dates.Today = new DateOnly(2026, 12, 1);
        await _service.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 61m });

        var rate = Assert.Single(await _service.ListRatesAsync());
        Assert.Equal(61m, rate.RateToBase);
        Assert.Equal(new DateOnly(2026, 12, 1), rate.UpdatedOn);
    }

    [Fact]
    public async Task SetRate_ForBaseCurrency_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.SetRateAsync(new ExchangeRateInput { Currency = Currency.DOP, RateToBase = 1m }));
    }

    [Fact]
    public async Task SetRate_NonPositive_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 0m }));
    }

    [Fact]
    public async Task DeleteRate_RemovesIt_AndIgnoresMissing()
    {
        await _service.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60m });

        await _service.DeleteRateAsync(Currency.USD);
        await _service.DeleteRateAsync(Currency.EUR);

        Assert.Empty(await _service.ListRatesAsync());
    }

    [Fact]
    public async Task SetBaseCurrency_UpdatesSettings_AndChangesConversion()
    {
        await _service.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60m });

        await _service.SetBaseCurrencyAsync(Currency.USD);

        Assert.Equal(Currency.USD, await _service.GetBaseCurrencyAsync());
        Assert.Equal(10m, await _service.ConvertToBaseAsync(10m, Currency.USD));
    }

    [Fact]
    public async Task GetBaseCurrency_WithoutSettings_DefaultsToDop_AndSetCreatesThem()
    {
        _settings.Items.Clear();

        Assert.Equal(Currency.DOP, await _service.GetBaseCurrencyAsync());

        await _service.SetBaseCurrencyAsync(Currency.EUR);

        Assert.Equal(Currency.EUR, Assert.Single(_settings.Items).BaseCurrency);
    }
}
