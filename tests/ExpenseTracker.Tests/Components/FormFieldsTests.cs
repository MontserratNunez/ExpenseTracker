using Bunit;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Tests.Components;

public class FormFieldsTests : BunitContext
{
    private IRenderedComponent<FieldsFormHost> RenderForm(FieldsModel? model = null)
        => Render<FieldsFormHost>(p => p.Add(h => h.Model, model ?? new FieldsModel()));

    [Fact]
    public void EveryLabel_PointsToItsControl()
    {
        var cut = RenderForm();

        foreach (var label in cut.FindAll("label.form-field__label"))
        {
            var id = label.GetAttribute("for");
            Assert.False(string.IsNullOrEmpty(id));
            Assert.Single(cut.FindAll($"#{id}"));
        }

        var ids = cut.FindAll("[id]").Select(e => e.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count()); // generated ids never collide
    }

    [Fact]
    public void Hint_IsRenderedAndReferencedByTheControl()
    {
        var cut = RenderForm();

        var input = cut.Find("input[type=text]");
        Assert.Equal("Como aparece en la lista", cut.Find($"#{input.GetAttribute("aria-describedby")}").TextContent);
    }

    [Fact]
    public void RequiredField_ShowsTheSpanishMessageUnderTheField_AndBlocksSubmit()
    {
        var cut = RenderForm();

        cut.Find("form").Submit();

        Assert.Equal(0, cut.Instance.Submitted);
        cut.WaitForAssertion(() =>
        {
            var message = cut.Find(".validation-message");
            Assert.Equal("El nombre es obligatorio.", message.TextContent);
            Assert.Contains("invalid", cut.Find("input[type=text]").ClassList);
        });
    }

    [Fact]
    public void FillingTheRequiredField_ClearsTheMessage_AndSubmits()
    {
        var model = new FieldsModel();
        var cut = RenderForm(model);
        cut.Find("form").Submit();

        cut.Find("input[type=text]").Change("Supermercado");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".validation-message")));
        cut.Find("form").Submit();

        Assert.Equal("Supermercado", model.Name);
        Assert.Equal(1, cut.Instance.Submitted);
    }

    [Fact]
    public void NumberField_ShowsItsSuffix_AndValidatesTheRange()
    {
        var model = new FieldsModel { Name = "x" };
        var cut = RenderForm(model);

        Assert.Equal("%", cut.Find(".field-group__affix--end").TextContent);

        cut.Find("input[type=number]").Change("150");
        cut.Find("form").Submit();

        Assert.Equal(150m, model.Rate);
        cut.WaitForAssertion(() => Assert.Equal("La tasa anual debe estar entre 0 y 100.", cut.Find(".validation-message").TextContent));
        Assert.Equal(0, cut.Instance.Submitted);
    }

    [Fact]
    public void NumberField_WithText_ShowsTheSpanishParsingMessage()
    {
        var cut = RenderForm(new FieldsModel { Name = "x" });

        cut.Find("input[type=number]").Change("abc");

        cut.WaitForAssertion(() => Assert.Equal("El campo Tasa debe ser un número.", cut.Find(".validation-message").TextContent));
    }

    [Fact]
    public void DateFields_BindDateOnlyAndNullableDateOnly()
    {
        var model = new FieldsModel { Name = "x" };
        var cut = RenderForm(model);
        cut.FindAll("input[type=date]")[0].Change("2026-11-05");
        cut.FindAll("input[type=date]")[1].Change("2027-01-31");

        Assert.Equal(new DateOnly(2026, 11, 5), model.Date);
        Assert.Equal(new DateOnly(2027, 1, 31), model.EndDate);

        cut.FindAll("input[type=date]")[1].Change("");
        Assert.Null(model.EndDate);
    }

    [Fact]
    public void Select_BindsANullableEnum_AndEmptyMeansNull()
    {
        var model = new FieldsModel();
        var cut = RenderForm(model);

        cut.Find("select").Change("Quarterly");
        Assert.Equal(Frequency.Quarterly, model.Frequency);

        cut.Find("select").Change("");
        Assert.Null(model.Frequency);
    }

    [Fact]
    public void Textarea_BindsTheText()
    {
        var model = new FieldsModel();
        var cut = RenderForm(model);

        cut.Find("textarea").Change("Pagado en efectivo");

        Assert.Equal("Pagado en efectivo", model.Notes);
    }

    [Fact]
    public void Toggle_IsASwitch_WithItsLabelAndHint_AndBindsABool()
    {
        var model = new FieldsModel();
        var cut = RenderForm(model);

        var toggle = cut.Find("input.toggle__input");
        Assert.Equal("switch", toggle.GetAttribute("role"));
        Assert.Equal("Activa", cut.Find(".toggle__label").TextContent);
        Assert.Equal("Cuenta en los totales", cut.Find($"#{toggle.GetAttribute("aria-describedby")}").TextContent);

        toggle.Change(true);
        Assert.True(model.Active);

        cut.Find("input.toggle__input").Change(false);
        Assert.False(model.Active);
    }
}
