using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.Services;

/// <summary>
/// Converts amounts to the base currency with rates loaded once, so a calculation over many
/// items does not query the database for each one.
/// </summary>
public class CurrencyConverter
{
    private readonly Dictionary<Currency, decimal> _rates;

    public CurrencyConverter(Currency baseCurrency, Dictionary<Currency, decimal> rates)
    {
        BaseCurrency = baseCurrency;
        _rates = rates;
    }

    public Currency BaseCurrency { get; }

    public decimal ToBase(decimal amount, Currency currency)
    {
        if (currency == BaseCurrency)
            return amount;

        if (!_rates.TryGetValue(currency, out var rate))
            throw new InvalidOperationException($"Configure la tasa de {currency} en Configuración");

        return Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
    }
}
