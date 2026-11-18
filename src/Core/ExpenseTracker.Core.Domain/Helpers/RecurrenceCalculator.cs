using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Domain.Helpers;

public static class RecurrenceCalculator
{
    /// <summary>
    /// Returns the dates on which a recurring item occurs within [from, to] (both inclusive).
    /// </summary>
    public static List<DateOnly> GetOccurrences(
        DateOnly start, Frequency frequency, DateOnly? end, DateOnly from, DateOnly to)
    {
        var result = new List<DateOnly>();
        var last = end.HasValue && end.Value < to ? end.Value : to;

        for (var n = 0; ; n++)
        {
            var date = GetNthOccurrence(start, frequency, n);
            if (date > last)
                break;
            if (date >= from)
                result.Add(date);
        }

        return result;
    }

    /// <summary>Monthly equivalent of an amount charged with the given frequency.</summary>
    public static decimal ToMonthlyAmount(decimal amount, Frequency frequency)
    {
        var monthly = frequency switch
        {
            Frequency.Weekly => amount * 52m / 12m,
            Frequency.Biweekly => amount * 26m / 12m,
            Frequency.Monthly => amount,
            Frequency.Quarterly => amount / 3m,
            Frequency.SemiAnnual => amount / 6m,
            Frequency.Annual => amount / 12m,
            _ => throw new ArgumentOutOfRangeException(nameof(frequency))
        };

        return Math.Round(monthly, 2, MidpointRounding.AwayFromZero);
    }

    // Computed from the start date each time (not chained), so a day-31 start is clamped
    // in short months and returns to the 31st in months that have it.
    private static DateOnly GetNthOccurrence(DateOnly start, Frequency frequency, int n)
        => frequency switch
        {
            Frequency.Weekly => start.AddDays(7 * n),
            Frequency.Biweekly => start.AddDays(14 * n),
            Frequency.Monthly => start.AddMonths(n),
            Frequency.Quarterly => start.AddMonths(3 * n),
            Frequency.SemiAnnual => start.AddMonths(6 * n),
            Frequency.Annual => start.AddYears(n),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency))
        };
}
