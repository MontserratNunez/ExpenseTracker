using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.Services;

public class CurrencyService
{
    private readonly IRepository<UserSettings> _settings;
    private readonly IRepository<ExchangeRate> _rates;
    private readonly IDateProvider _dateProvider;

    public CurrencyService(
        IRepository<UserSettings> settings,
        IRepository<ExchangeRate> rates,
        IDateProvider dateProvider)
    {
        _settings = settings;
        _rates = rates;
        _dateProvider = dateProvider;
    }

    public async Task<Currency> GetBaseCurrencyAsync()
    {
        var settings = await GetSettingsAsync();
        return settings?.BaseCurrency ?? Currency.DOP;
    }

    public async Task SetBaseCurrencyAsync(Currency currency)
    {
        if (!Enum.IsDefined(currency))
            throw new ArgumentException("La moneda no es válida.");

        var settings = await GetSettingsAsync();
        if (settings is null)
        {
            await _settings.AddAsync(new UserSettings { BaseCurrency = currency });
            return;
        }

        if (settings.BaseCurrency == currency)
            return;

        settings.BaseCurrency = currency;
        await _settings.UpdateAsync(settings);
    }

    public async Task<List<ExchangeRateDto>> ListRatesAsync()
    {
        var rates = await _rates.ListAsync();
        return rates.OrderBy(r => r.Currency).Select(ToDto).ToList();
    }

    /// <summary>Creates or updates the rate of a currency against the base currency.</summary>
    public async Task<ExchangeRateDto> SetRateAsync(ExchangeRateInput input)
    {
        if (!Enum.IsDefined(input.Currency))
            throw new ArgumentException("La moneda no es válida.");
        if (input.RateToBase <= 0)
            throw new ArgumentException("La tasa debe ser mayor que cero.");

        var baseCurrency = await GetBaseCurrencyAsync();
        if (input.Currency == baseCurrency)
            throw new InvalidOperationException("No se necesita una tasa para la moneda base.");

        var today = _dateProvider.Today;
        var existing = (await _rates.ListAsync(r => r.Currency == input.Currency)).FirstOrDefault();

        if (existing is null)
        {
            existing = new ExchangeRate { Currency = input.Currency, RateToBase = input.RateToBase, UpdatedOn = today };
            await _rates.AddAsync(existing);
        }
        else
        {
            existing.RateToBase = input.RateToBase;
            existing.UpdatedOn = today;
            await _rates.UpdateAsync(existing);
        }

        return ToDto(existing);
    }

    public async Task DeleteRateAsync(Currency currency)
    {
        var existing = (await _rates.ListAsync(r => r.Currency == currency)).FirstOrDefault();
        if (existing is not null)
            await _rates.DeleteAsync(existing);
    }

    /// <summary>Converts an amount to the base currency using the current rate.</summary>
    public async Task<decimal> ConvertToBaseAsync(decimal amount, Currency currency)
    {
        var baseCurrency = await GetBaseCurrencyAsync();
        if (currency == baseCurrency)
            return amount;

        var rate = (await _rates.ListAsync(r => r.Currency == currency)).FirstOrDefault()
            ?? throw new InvalidOperationException($"Configure la tasa de {currency} en Configuración");

        return Math.Round(amount * rate.RateToBase, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<UserSettings?> GetSettingsAsync()
        => (await _settings.ListAsync()).FirstOrDefault();

    private static ExchangeRateDto ToDto(ExchangeRate r) => new()
    {
        Currency = r.Currency,
        RateToBase = r.RateToBase,
        UpdatedOn = r.UpdatedOn
    };
}
