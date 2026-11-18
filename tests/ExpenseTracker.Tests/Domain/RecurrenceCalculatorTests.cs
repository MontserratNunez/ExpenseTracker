using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Core.Domain.Helpers;

namespace ExpenseTracker.Tests.Domain;

public class RecurrenceCalculatorTests
{
    [Fact]
    public void Monthly_StartingOn31_ClampsToEndOfShortMonths()
    {
        var start = new DateOnly(2026, 1, 31);

        var result = RecurrenceCalculator.GetOccurrences(
            start, Frequency.Monthly, null, new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(new[] { new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31) }, result);
    }

    [Fact]
    public void Monthly_StartingOn31_InLeapYearUses29Feb()
    {
        var result = RecurrenceCalculator.GetOccurrences(
            new DateOnly(2028, 1, 31), Frequency.Monthly, null, new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29));

        Assert.Equal(new[] { new DateOnly(2028, 2, 29) }, result);
    }

    [Fact]
    public void Annual_AppearsOnlyInAnniversaryMonth()
    {
        var start = new DateOnly(2025, 3, 15);

        var march = RecurrenceCalculator.GetOccurrences(
            start, Frequency.Annual, null, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        var april = RecurrenceCalculator.GetOccurrences(
            start, Frequency.Annual, null, new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30));

        Assert.Equal(new[] { new DateOnly(2026, 3, 15) }, march);
        Assert.Empty(april);
    }

    [Fact]
    public void Biweekly_CanOccurThreeTimesInAMonth()
    {
        var result = RecurrenceCalculator.GetOccurrences(
            new DateOnly(2026, 1, 2), Frequency.Biweekly, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(
            new[] { new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 16), new DateOnly(2026, 1, 30) },
            result);
    }

    [Fact]
    public void EndDate_CutsOccurrences()
    {
        var result = RecurrenceCalculator.GetOccurrences(
            new DateOnly(2026, 1, 10), Frequency.Monthly, new DateOnly(2026, 3, 10),
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(3, result.Count);
        Assert.Equal(new DateOnly(2026, 3, 10), result[^1]);
    }

    [Fact]
    public void StartAfterRange_ReturnsEmpty()
    {
        var result = RecurrenceCalculator.GetOccurrences(
            new DateOnly(2027, 1, 1), Frequency.Weekly, null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(Frequency.Monthly, 1200, 1200)]
    [InlineData(Frequency.Quarterly, 1200, 400)]
    [InlineData(Frequency.SemiAnnual, 1200, 200)]
    [InlineData(Frequency.Annual, 20000, 1666.67)]
    [InlineData(Frequency.Biweekly, 1200, 2600)]
    public void ToMonthlyAmount_ReturnsMonthlyEquivalent(Frequency frequency, double amount, double expected)
    {
        var result = RecurrenceCalculator.ToMonthlyAmount((decimal)amount, frequency);

        Assert.Equal((decimal)expected, result);
    }
}
