using System.Globalization;
using System.Text.RegularExpressions;

namespace ExpenseTracker.Core.Application.Helpers;

/// <summary>
/// Reads and formats the amounts typed in money fields, using the es-DO convention
/// (comma separates thousands, dot separates decimals).
/// </summary>
public static class MoneyParser
{
    public const int MaxDecimals = 2;

    // "5000", "5000.5", "5,000", "5,000.50" and "1,234,567.89": groups of three after the first comma, optional decimals.
    private static readonly Regex Valid = new(@"^(\d{1,3}(,\d{3})+|\d+)(\.\d+)?$", RegexOptions.Compiled);
    private static readonly Regex CurrencyPrefix = new(@"^(RD\$|US\$|\$|€)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly CultureInfo FormatCulture = CreateCulture();

    /// <summary>
    /// Parses a typed amount. Empty text is a valid zero (the model's own validation decides whether zero is allowed).
    /// A comma is only accepted as a thousands separator: "5000,5" is rejected instead of being read as 50005.
    /// </summary>
    public static bool TryParse(string? text, out decimal amount, out string? error)
    {
        amount = 0m;
        error = null;

        var value = CurrencyPrefix.Replace((text ?? string.Empty).Trim(), string.Empty).Trim();
        if (value.Length == 0)
            return true;

        if (!Valid.IsMatch(value))
        {
            error = "Escriba un monto válido, por ejemplo 5,000.50.";
            return false;
        }

        var plain = value.Replace(",", string.Empty);
        var dot = plain.IndexOf('.');
        if (dot >= 0 && plain.Length - dot - 1 > MaxDecimals)
        {
            error = $"Use hasta {MaxDecimals} decimales.";
            return false;
        }

        if (!decimal.TryParse(plain, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount) || amount > 999_999_999_999.99m)
        {
            amount = 0m;
            error = "El monto es demasiado grande.";
            return false;
        }

        return true;
    }

    /// <summary>Formats an amount as typed text, e.g. 5000.5 becomes "5,000.50".</summary>
    public static string Format(decimal amount) => amount.ToString("N2", FormatCulture);

    private static CultureInfo CreateCulture()
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
