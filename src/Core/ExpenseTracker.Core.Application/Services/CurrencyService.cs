using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.Services;

public class CurrencyService : IExchangeRateProvider
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

    /// <summary>One entry per configured currency: the rate in force today and its history.</summary>
    public async Task<List<ExchangeRateDto>> ListRatesAsync()
    {
        var today = _dateProvider.Today;
        var rates = await _rates.ListAsync();

        return rates
            .GroupBy(r => r.Currency)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var history = g.OrderByDescending(r => r.EffectiveFrom).ToList();
                var current = history.FirstOrDefault(r => r.EffectiveFrom <= today) ?? history[^1];
                var dto = ToDto(current);
                dto.History = history.Select(ToDto).ToList();
                return dto;
            })
            .ToList();
    }

    /// <summary>
    /// Adds a rate that applies from a date (today by default). The previous ones are kept;
    /// only a rate with that same date is replaced.
    /// </summary>
    public async Task<ExchangeRateDto> SetRateAsync(ExchangeRateInput input)
    {
        if (!Enum.IsDefined(input.Currency))
            throw new ArgumentException("La moneda no es válida.");
        if (input.RateToBase <= 0)
            throw new ArgumentException("La tasa debe ser mayor que cero.");

        var baseCurrency = await GetBaseCurrencyAsync();
        if (input.Currency == baseCurrency)
            throw new InvalidOperationException("No se necesita una tasa para la moneda base.");

        var from = input.EffectiveFrom ?? _dateProvider.Today;
        var existing = (await _rates.ListAsync(r => r.Currency == input.Currency))
            .FirstOrDefault(r => r.EffectiveFrom == from);

        if (existing is null)
        {
            existing = new ExchangeRate { Currency = input.Currency, RateToBase = input.RateToBase, EffectiveFrom = from };
            await _rates.AddAsync(existing);
        }
        else
        {
            existing.RateToBase = input.RateToBase;
            await _rates.UpdateAsync(existing);
        }

        return ToDto(existing);
    }

    /// <summary>Removes a currency's rates, unless some of them already converted past months.</summary>
    public async Task DeleteRateAsync(Currency currency)
    {
        var today = _dateProvider.Today;
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var existing = await _rates.ListAsync(r => r.Currency == currency);

        if (existing.Any(r => r.EffectiveFrom < thisMonth))
            throw new InvalidOperationException(
                $"La tasa de {currency} ya se usó en meses anteriores y no se puede quitar. Guarda una tasa nueva.");

        foreach (var rate in existing)
            await _rates.DeleteAsync(rate);
    }

    public async Task<decimal> GetRateAsync(Currency currency, DateOnly date)
        => (await GetConverterAsync(new[] { currency })).RateOn(currency, date);

    /// <summary>Converts an amount to the base currency with the rate in force on a date.</summary>
    public async Task<decimal> ConvertToBaseAsync(decimal amount, Currency currency, DateOnly date)
        => (await GetConverterAsync(new[] { currency })).ToBase(amount, currency, date);

    /// <summary>Loads the base currency and all rates once (2 queries) for converting many amounts.</summary>
    public async Task<CurrencyConverter> GetConverterAsync()
    {
        var baseCurrency = await GetBaseCurrencyAsync();
        return new CurrencyConverter(baseCurrency, await _rates.ListAsync());
    }

    /// <summary>
    /// Like <see cref="GetConverterAsync()"/>, but the rates are only queried when some of the
    /// currencies that will be converted differ from the base currency (1 query instead of 2).
    /// </summary>
    public async Task<CurrencyConverter> GetConverterAsync(IEnumerable<Currency> currenciesToConvert)
    {
        var baseCurrency = await GetBaseCurrencyAsync();
        if (currenciesToConvert.All(c => c == baseCurrency))
            return new CurrencyConverter(baseCurrency, Array.Empty<ExchangeRate>());

        return new CurrencyConverter(baseCurrency, await _rates.ListAsync());
    }

    private async Task<UserSettings?> GetSettingsAsync()
        => (await _settings.ListAsync()).FirstOrDefault();

    private static ExchangeRateDto ToDto(ExchangeRate r) => new()
    {
        Currency = r.Currency,
        RateToBase = r.RateToBase,
        EffectiveFrom = r.EffectiveFrom
    };
}
