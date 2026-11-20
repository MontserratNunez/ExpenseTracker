using Bunit;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Tests.Components;

public class MoneyInputTests : BunitContext
{
    private IRenderedComponent<MoneyFormHost> RenderForm(MoneyModel? model = null, bool showCurrencySelector = true)
        => Render<MoneyFormHost>(p => p
            .Add(h => h.Model, model ?? new MoneyModel())
            .Add(h => h.ShowCurrencySelector, showCurrencySelector));

    private static string AmountText(IRenderedComponent<MoneyFormHost> cut)
        => cut.Find("input[inputmode=decimal]").GetAttribute("value") ?? string.Empty;

    private static void Type(IRenderedComponent<MoneyFormHost> cut, string text)
        => cut.Find("input[inputmode=decimal]").Change(text);

    [Fact]
    public void ShowsTheCurrentAmountFormatted_AndEmptyForZero()
    {
        Assert.Equal("4,885.40", AmountText(RenderForm(new MoneyModel { Amount = 4885.40m })));
        Assert.Equal(string.Empty, AmountText(RenderForm(new MoneyModel { Amount = 0m })));
    }

    [Theory]
    [InlineData("5000", 5000, "5,000.00")]
    [InlineData("5,000", 5000, "5,000.00")]
    [InlineData("5000.5", 5000.5, "5,000.50")]
    [InlineData("5,000.50", 5000.50, "5,000.50")]
    [InlineData("  700 ", 700, "700.00")]
    public void AcceptsTheFourFormats_UpdatesTheModel_AndFormatsOnBlur(string typed, double expected, string shown)
    {
        var model = new MoneyModel();
        var cut = RenderForm(model);

        Type(cut, typed);

        Assert.Equal((decimal)expected, model.Amount);
        Assert.Equal(shown, AmountText(cut));
        Assert.Empty(cut.FindAll(".validation-message"));
    }

    [Fact]
    public void ADecimalComma_IsRejected_NotReadAsThousands()
    {
        var model = new MoneyModel { Amount = 100m };
        var cut = RenderForm(model);

        Type(cut, "5000,5");

        cut.WaitForAssertion(() => Assert.Contains("monto válido", cut.Find(".validation-message").TextContent));
        Assert.Equal(100m, model.Amount);                       // the last valid amount is kept, never 50005
        Assert.Equal("5000,5", AmountText(cut));                // what the user typed stays so they can fix it
        Assert.Equal("true", cut.Find("input[inputmode=decimal]").GetAttribute("aria-invalid"));
        Assert.Contains("invalid", cut.Find("input[inputmode=decimal]").ClassList);
    }

    [Fact]
    public void ABadAmount_BlocksTheSubmit_UntilItIsFixed()
    {
        var model = new MoneyModel { Amount = 100m };
        var cut = RenderForm(model);

        Type(cut, "abc");
        cut.Find("form").Submit();

        Assert.Equal(0, cut.Instance.Submitted);
        Assert.Equal(1, cut.Instance.InvalidSubmits);
        Assert.Contains("monto válido", cut.Find(".validation-message").TextContent);

        Type(cut, "250.75");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".validation-message")));
        cut.Find("form").Submit();

        Assert.Equal(1, cut.Instance.Submitted);
        Assert.Equal(250.75m, model.Amount);
    }

    [Fact]
    public void MoreThanTwoDecimals_ShowsItsOwnMessage()
    {
        var cut = RenderForm();

        Type(cut, "5.123");

        cut.WaitForAssertion(() => Assert.Equal("Use hasta 2 decimales.", cut.Find(".validation-message").TextContent));
    }

    [Fact]
    public void ZeroOrEmpty_ShowsTheModelRangeMessageOnSubmit()
    {
        var cut = RenderForm();

        cut.Find("form").Submit();

        Assert.Equal(0, cut.Instance.Submitted);
        cut.WaitForAssertion(() => Assert.Equal("El monto debe ser mayor que cero.", cut.Find(".validation-message").TextContent));
    }

    [Fact]
    public void ValidAmount_Submits()
    {
        var model = new MoneyModel();
        var cut = RenderForm(model);

        Type(cut, "1,300");
        cut.Find("form").Submit();

        Assert.Equal(1, cut.Instance.Submitted);
        Assert.Equal(1300m, model.Amount);
    }

    [Fact]
    public void CurrencySelector_ChangesTheModelCurrency()
    {
        var model = new MoneyModel();
        var cut = RenderForm(model);

        cut.Find("select").Change("USD");

        Assert.Equal(Currency.USD, model.Currency);
        Assert.Equal(new[] { "DOP", "USD", "EUR" }, cut.FindAll("select option").Select(o => o.TextContent));
    }

    [Fact]
    public void WithoutTheSelector_ShowsTheCurrencySymbol()
    {
        var cut = RenderForm(new MoneyModel { Currency = Currency.USD }, showCurrencySelector: false);

        Assert.Empty(cut.FindAll("select"));
        Assert.Equal("US$", cut.Find(".field-group__affix").TextContent);
    }

    [Fact]
    public void FollowsTheModel_WhenItChangesFromOutside()
    {
        var cut = RenderForm(new MoneyModel { Amount = 10m });
        Assert.Equal("10.00", AmountText(cut));

        cut.Render(p => p.Add(h => h.Model, new MoneyModel { Amount = 1234.5m }));

        Assert.Equal("1,234.50", AmountText(cut));
    }

    [Fact]
    public void LabelHintAndControl_AreLinked_ForAccessibility()
    {
        var cut = RenderForm();

        var input = cut.Find("input[inputmode=decimal]");
        Assert.Equal(input.Id, cut.Find("label").GetAttribute("for"));
        Assert.Equal($"{input.Id}-hint", input.GetAttribute("aria-describedby"));
        Assert.Equal($"{input.Id}-hint", cut.Find(".form-field__hint").Id);
    }
}
