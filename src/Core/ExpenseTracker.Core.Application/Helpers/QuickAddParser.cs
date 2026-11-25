using System.Globalization;
using System.Text.RegularExpressions;

namespace ExpenseTracker.Core.Application.Helpers;

/// <summary>
/// Splits what the user types in the quick-add row of Expenses ("Supermercado 700") into a name and an amount.
/// The rule is simple: the last token, when it is a number, is the amount and the rest is the name.
/// </summary>
public static class QuickAddParser
{
    private static readonly Regex Pattern = new(
        @"^(?<name>.+?)\s+(?<amount>\d[\d,]*(\.\d{1,2})?)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns the name and, when the text ends with an amount, the amount (greater than zero).
    /// Without a recognizable amount the whole text is the name. Empty text returns an empty name.
    /// </summary>
    public static (string Name, decimal? Amount) Parse(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        var match = Pattern.Match(trimmed);
        if (!match.Success)
            return (trimmed, null);

        var digits = match.Groups["amount"].Value.Replace(",", string.Empty);
        if (!decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            return (trimmed, null);

        return (match.Groups["name"].Value.Trim(), amount);
    }
}
