using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.Services;

/// <summary>
/// Converts amounts to the base currency with the rate history loaded once, so a calculation over many
/// items does not query the database for each one. Every conversion says which date it is for.
/// </summary>
public class CurrencyConverter
{
    private readonly Dictionary<Currency, List<ExchangeRate>> _rates;

    public CurrencyConverter(Currency baseCurrency, IEnumerable<ExchangeRate> rates)
    {
        BaseCurrency = baseCurrency;
        _rates = rates.GroupBy(r => r.Currency).ToDictionary(g => g.Key, g => g.OrderBy(r => r.EffectiveFrom).ToList());
    }

    public Currency BaseCurrency { get; }

    /// <summary>
    /// The latest rate that applies from the date or before it. Dates older than the first known rate
    /// use that first one.
    /// </summary>
    public decimal RateOn(Currency currency, DateOnly date)
    {
        if (currency == BaseCurrency)
            return 1m;

        if (!_rates.TryGetValue(currency, out var history))
            throw new InvalidOperationException($"Configure la tasa de {currency} en Configuración");

        return (history.LastOrDefault(r => r.EffectiveFrom <= date) ?? history[0]).RateToBase;
    }

    public decimal ToBase(decimal amount, Currency currency, DateOnly date)
        => currency == BaseCurrency
            ? amount
            : Math.Round(amount * RateOn(currency, date), 2, MidpointRounding.AwayFromZero);
}
