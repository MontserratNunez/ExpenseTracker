using ExpenseTracker.Core.Application.Helpers;

namespace ExpenseTracker.Tests.Application;

public class QuickAddParserTests
{
    [Theory]
    [InlineData("Supermercado 700", "Supermercado", 700)]
    [InlineData("Supermercado 4,885.40", "Supermercado", 4885.40)]
    [InlineData("  Ticket de cine   381.2  ", "Ticket de cine", 381.2)]
    [InlineData("Seguro 20000", "Seguro", 20000)]
    public void Parse_TextEndingInAnAmount_SplitsNameAndAmount(string text, string name, double amount)
    {
        var result = QuickAddParser.Parse(text);

        Assert.Equal(name, result.Name);
        Assert.Equal((decimal)amount, result.Amount);
    }

    [Theory]
    [InlineData("Supermercado")]
    [InlineData("Combo 2x1 palomitas")]
    [InlineData("700")]
    [InlineData("Café 5.5.5")]
    public void Parse_WithoutAnAmountAtTheEnd_UsesTheWholeTextAsName(string text)
    {
        var result = QuickAddParser.Parse(text);

        Assert.Equal(text.Trim(), result.Name);
        Assert.Null(result.Amount);
    }

    [Fact]
    public void Parse_ZeroAmount_IsNotAnAmount()
    {
        var result = QuickAddParser.Parse("Regalo 0");

        Assert.Equal("Regalo 0", result.Name);
        Assert.Null(result.Amount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyText_GivesAnEmptyName(string? text)
    {
        var result = QuickAddParser.Parse(text);

        Assert.Equal(string.Empty, result.Name);
        Assert.Null(result.Amount);
    }
}
