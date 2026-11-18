using ExpenseTracker.Core.Application.Helpers;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Tests.Application;

public class MoneyFormatterTests
{
    [Theory]
    [InlineData(4885.40, Currency.DOP, "RD$ 4,885.40")]
    [InlineData(10, Currency.USD, "US$ 10.00")]
    [InlineData(1234567.891, Currency.DOP, "RD$ 1,234,567.89")]
    [InlineData(0, Currency.DOP, "RD$ 0.00")]
    [InlineData(25.5, Currency.EUR, "€ 25.50")]
    [InlineData(-300, Currency.DOP, "-RD$ 300.00")]
    public void Format_UsesSymbolAndTwoDecimals(double amount, Currency currency, string expected)
    {
        Assert.Equal(expected, MoneyFormatter.Format((decimal)amount, currency));
    }
}
