using System.Globalization;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.Helpers;

public static class MoneyFormatter
{
    private static readonly CultureInfo DefaultCulture = CreateDefaultCulture();

    public static string GetSymbol(Currency currency) => currency switch
    {
        Currency.DOP => "RD$",
        Currency.USD => "US$",
        Currency.EUR => "€",
        _ => currency.ToString()
    };

    /// <summary>Formats an amount like "RD$ 4,885.40" or "-US$ 10.00".</summary>
    public static string Format(decimal amount, Currency currency, CultureInfo? culture = null)
    {
        var number = Math.Abs(amount).ToString("N2", culture ?? DefaultCulture);
        var sign = amount < 0 ? "-" : string.Empty;
        return $"{sign}{GetSymbol(currency)} {number}";
    }

    // es-DO uses "." for decimals and "," for thousands. If the runtime has no culture
    // data (invariant globalization), the invariant culture produces the same separators.
    private static CultureInfo CreateDefaultCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo("es-DO");
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
