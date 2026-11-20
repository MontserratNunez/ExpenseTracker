using ExpenseTracker.Core.Application.Helpers;

namespace ExpenseTracker.Tests.Application;

public class MoneyParserTests
{
    [Theory]
    [InlineData("5000", 5000)]
    [InlineData("5,000", 5000)]
    [InlineData("5000.5", 5000.5)]
    [InlineData("5,000.50", 5000.50)]
    [InlineData("  1,234,567.89  ", 1234567.89)]
    [InlineData("0.5", 0.5)]
    [InlineData("700", 700)]
    [InlineData("RD$ 4,885.40", 4885.40)]
    [InlineData("US$10", 10)]
    [InlineData("$ 25.5", 25.5)]
    public void TryParse_AcceptsTheSupportedFormats(string text, double expected)
    {
        Assert.True(MoneyParser.TryParse(text, out var amount, out var error));
        Assert.Equal((decimal)expected, amount);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParse_EmptyText_IsAValidZero(string? text)
    {
        Assert.True(MoneyParser.TryParse(text, out var amount, out var error));
        Assert.Equal(0m, amount);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("5000,5")]      // a European decimal comma must not become 50005
    [InlineData("1,23")]
    [InlineData("12,34,567")]
    [InlineData("1,2345")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1e5")]
    [InlineData("5.")]
    [InlineData(".5")]
    [InlineData("5..5")]
    [InlineData("1 000")]
    public void TryParse_RejectsAmbiguousOrInvalidText(string text)
    {
        Assert.False(MoneyParser.TryParse(text, out var amount, out var error));
        Assert.Equal(0m, amount);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryParse_MoreThanTwoDecimals_IsRejected()
    {
        Assert.False(MoneyParser.TryParse("5.123", out _, out var error));
        Assert.Equal("Use hasta 2 decimales.", error);
    }

    [Fact]
    public void TryParse_TooLarge_IsRejected()
    {
        Assert.False(MoneyParser.TryParse("1,000,000,000,000", out _, out var error));
        Assert.Equal("El monto es demasiado grande.", error);
        Assert.True(MoneyParser.TryParse("999,999,999,999.99", out var max, out _));
        Assert.Equal(999_999_999_999.99m, max);
    }

    [Theory]
    [InlineData(5000.5, "5,000.50")]
    [InlineData(10, "10.00")]
    [InlineData(1234567.891, "1,234,567.89")]
    public void Format_UsesThousandsAndTwoDecimals(double amount, string expected)
    {
        Assert.Equal(expected, MoneyParser.Format((decimal)amount));
    }

    [Theory]
    [InlineData("5000")]
    [InlineData("5,000.50")]
    [InlineData("4,885.40")]
    public void Format_ThenParse_RoundTrips(string text)
    {
        MoneyParser.TryParse(text, out var amount, out _);

        Assert.True(MoneyParser.TryParse(MoneyParser.Format(amount), out var again, out _));
        Assert.Equal(amount, again);
    }
}
