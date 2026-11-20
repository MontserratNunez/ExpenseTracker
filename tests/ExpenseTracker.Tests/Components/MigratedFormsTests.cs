using AngleSharp.Dom;
using Bunit;
using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Application.DTOs.Debts;
using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.DTOs.Goals;
using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Web.Components.Pages.Budget;
using ExpenseTracker.Web.Components.Pages.Debts;
using ExpenseTracker.Web.Components.Pages.Expenses;
using ExpenseTracker.Web.Components.Pages.Goals;
using ExpenseTracker.Web.Components.Pages.Incomes;
using Microsoft.AspNetCore.Components;

namespace ExpenseTracker.Tests.Components;

/// <summary>
/// The seven forms that save data, driven like a user would: fill the fields, submit, cancel.
/// They guard the migration to the design-system components.
/// </summary>
public class MigratedFormsTests : BunitContext
{
    private int saved;
    private int cancelled;
    private int deleted;

    private EventCallback OnSave => EventCallback.Factory.Create(this, () => saved++);
    private EventCallback OnCancel => EventCallback.Factory.Create(this, () => cancelled++);
    private EventCallback OnDelete => EventCallback.Factory.Create(this, () => deleted++);

    private static readonly List<CategoryDto> Categories = new()
    {
        new() { Id = 1, Name = "Comida", Color = "#E67E22", IsActive = true },
        new() { Id = 2, Name = "Ahorro", Color = "#2E9E5B", IsActive = true, IsSavings = true },
        new() { Id = 3, Name = "Vieja", Color = "#999999", IsActive = false }
    };

    // ---- helpers: find a control by its visible label, as a person would ----

    private static IElement Control(IRenderedComponent<IComponent> cut, string label)
    {
        var labelElement = cut.FindAll("label").First(l => l.TextContent.Trim() == label);
        return cut.Find($"#{labelElement.GetAttribute("for")}");
    }

    private static void Fill(IRenderedComponent<IComponent> cut, string label, string value) => Control(cut, label).Change(value);

    private static List<string> Errors(IRenderedComponent<IComponent> cut)
        => cut.FindAll(".validation-message").Select(e => e.TextContent.Trim()).ToList();

    private static void Submit(IRenderedComponent<IComponent> cut) => cut.Find("form").Submit();

    // ================= ExpenseForm =================

    private IRenderedComponent<ExpenseForm> RenderExpense(ExpenseInput? model = null, bool editing = false)
        => Render<ExpenseForm>(p => p
            .Add(f => f.Visible, true)
            .Add(f => f.Title, "Nuevo gasto")
            .Add(f => f.IsEditing, editing)
            .Add(f => f.Model, model ?? new ExpenseInput { Date = new DateOnly(2026, 11, 18) })
            .Add(f => f.Categories, Categories)
            .Add(f => f.OnSave, OnSave)
            .Add(f => f.OnCancel, OnCancel)
            .Add(f => f.OnDelete, OnDelete));

    [Fact]
    public void ExpenseForm_FilledIn_SavesTheValuesTheUserTyped()
    {
        var model = new ExpenseInput { Date = new DateOnly(2026, 11, 18) };
        var cut = RenderExpense(model);

        Fill(cut, "Nombre", "Supermercado");
        Fill(cut, "Monto", "4,885.40");
        Fill(cut, "Categoría", "1");
        Fill(cut, "Fecha", "2026-11-03");
        Fill(cut, "Notas", "Compra semanal");
        Submit(cut);

        Assert.Equal(1, saved);
        Assert.Equal("Supermercado", model.Name);
        Assert.Equal(4885.40m, model.Amount);
        Assert.Equal(Currency.DOP, model.Currency);
        Assert.Equal(1, model.CategoryId);
        Assert.Equal(new DateOnly(2026, 11, 3), model.Date);
        Assert.Equal("Compra semanal", model.Notes);
        Assert.False(model.IsRecurring);
    }

    [Fact]
    public void ExpenseForm_Empty_ShowsEveryMissingFieldInSpanish_AndDoesNotSave()
    {
        var cut = RenderExpense();

        Submit(cut);

        Assert.Equal(0, saved);
        cut.WaitForAssertion(() =>
        {
            var errors = Errors(cut);
            Assert.Contains("El nombre es obligatorio.", errors);
            Assert.Contains("El monto debe ser mayor que cero.", errors);
            Assert.Contains("Seleccione una categoría.", errors);
        });
    }

    [Fact]
    public void ExpenseForm_ForeignCurrency_KeepsTheChosenCurrency()
    {
        var model = new ExpenseInput { Date = new DateOnly(2026, 11, 18), Name = "Netflix", CategoryId = 1 };
        var cut = RenderExpense(model);

        cut.Find("select[aria-label=Moneda]").Change("USD");
        Fill(cut, "Monto", "10");
        Submit(cut);

        Assert.Equal(1, saved);
        Assert.Equal(Currency.USD, model.Currency);
        Assert.Equal(10m, model.Amount);
    }

    [Fact]
    public void ExpenseForm_Recurring_RevealsFrequencyAndEndDate_AndRelabelsTheDate()
    {
        var model = new ExpenseInput { Date = new DateOnly(2026, 11, 18), Name = "Netflix", Amount = 10, CategoryId = 1 };
        var cut = RenderExpense(model);
        Assert.DoesNotContain(cut.FindAll("label"), l => l.TextContent.Trim() == "Frecuencia");

        Control(cut, "Es recurrente").Change(true);

        Assert.True(model.IsRecurring);
        Assert.Contains(cut.FindAll("label"), l => l.TextContent.Trim() == "Primera ocurrencia");
        var options = Control(cut, "Frecuencia").QuerySelectorAll("option").Select(o => o.TextContent).ToList();
        Assert.Equal(new[] { "Semanal", "Quincenal", "Mensual", "Trimestral", "Semestral", "Anual" }, options);

        Fill(cut, "Frecuencia", "Monthly");
        Fill(cut, "Fin (opcional)", "2027-06-30");
        Submit(cut);

        Assert.Equal(1, saved);
        Assert.Equal(Frequency.Monthly, model.Frequency);
        Assert.Equal(new DateOnly(2027, 6, 30), model.EndDate);
    }

    [Fact]
    public void ExpenseForm_OnlyOffersActiveCategories_ButKeepsTheCurrentInactiveOne()
    {
        var fresh = RenderExpense();
        Assert.DoesNotContain(Control(fresh, "Categoría").QuerySelectorAll("option"), o => o.TextContent.Contains("Vieja"));

        var editing = RenderExpense(new ExpenseInput { CategoryId = 3, Date = new DateOnly(2026, 1, 1) }, editing: true);
        Assert.Contains(Control(editing, "Categoría").QuerySelectorAll("option"), o => o.TextContent == "Vieja (inactiva)");
    }

    [Fact]
    public void ExpenseForm_DeleteButton_OnlyWhenEditing_AndRaisesOnDelete()
    {
        Assert.DoesNotContain(RenderExpense().FindAll("button"), b => b.TextContent.Contains("Eliminar"));

        var cut = RenderExpense(editing: true);
        cut.FindAll("button").First(b => b.TextContent.Contains("Eliminar")).Click();

        Assert.Equal(1, deleted);
        Assert.Equal(0, saved);
    }

    [Fact]
    public void ExpenseForm_CancelAndCloseButtons_RaiseOnCancel()
    {
        var cut = RenderExpense();

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Cancelar").Click();
        cut.Find("button[aria-label=Cerrar]").Click();

        Assert.Equal(2, cancelled);
        Assert.Equal(0, saved);
    }

    [Fact]
    public void ExpenseForm_NotVisible_RendersNothing()
    {
        var cut = Render<ExpenseForm>(p => p.Add(f => f.Visible, false).Add(f => f.Model, new ExpenseInput()));

        Assert.Empty(cut.Markup.Trim());
    }

    // ================= IncomeSourceForm / OneTimeIncomeForm =================

    [Fact]
    public void IncomeSourceForm_FilledIn_Saves()
    {
        var model = new IncomeSourceInput { StartDate = new DateOnly(2026, 1, 1) };
        var cut = Render<IncomeSourceForm>(p => p
            .Add(f => f.Visible, true).Add(f => f.Model, model).Add(f => f.OnSave, OnSave).Add(f => f.OnCancel, OnCancel));

        Fill(cut, "Nombre", "Sueldo trabajo");
        Fill(cut, "Monto", "50000");
        Fill(cut, "Frecuencia", "Biweekly");
        Fill(cut, "Fin (opcional)", "2027-12-31");
        Control(cut, "Activa").Change(false);
        Submit(cut);

        Assert.Equal(1, saved);
        Assert.Equal("Sueldo trabajo", model.Name);
        Assert.Equal(50000m, model.Amount);
        Assert.Equal(Frequency.Biweekly, model.Frequency);
        Assert.Equal(new DateOnly(2027, 12, 31), model.EndDate);
        Assert.False(model.IsActive);
    }

    [Fact]
    public void IncomeSourceForm_Empty_ShowsTheErrors()
    {
        var cut = Render<IncomeSourceForm>(p => p
            .Add(f => f.Visible, true).Add(f => f.Model, new IncomeSourceInput()).Add(f => f.OnSave, OnSave));

        Submit(cut);

        Assert.Equal(0, saved);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("El nombre es obligatorio.", Errors(cut));
            Assert.Contains("El monto debe ser mayor que cero.", Errors(cut));
        });
    }

    [Fact]
    public void OneTimeIncomeForm_FilledIn_Saves()
    {
        var model = new OneTimeIncomeInput { Date = new DateOnly(2026, 11, 5) };
        var cut = Render<OneTimeIncomeForm>(p => p
            .Add(f => f.Visible, true).Add(f => f.Model, model).Add(f => f.OnSave, OnSave));

        Fill(cut, "Descripción", "Bono");
        Fill(cut, "Monto", "5,000");
        Submit(cut);

        Assert.Equal(1, saved);
        Assert.Equal("Bono", model.Description);
        Assert.Equal(5000m, model.Amount);
    }

    // ================= DebtForm / DebtPaymentForm =================

    private IRenderedComponent<DebtForm> RenderDebt(DebtInput model)
        => Render<DebtForm>(p => p
            .Add(f => f.Visible, true).Add(f => f.Model, model).Add(f => f.OnSave, OnSave).Add(f => f.OnCancel, OnCancel));

    [Fact]
    public void DebtForm_TheEnunciadoLoan_Saves()
    {
        var model = new DebtInput { StartDate = new DateOnly(2023, 1, 5) };
        var cut = RenderDebt(model);

        Fill(cut, "Nombre", "Préstamo banco");
        Fill(cut, "Monto original", "1,000,000");
        Fill(cut, "Ya pagado antes de usar la app", "500,000");
        Fill(cut, "Tasa anual", "5");
        Fill(cut, "Día de pago (1-28)", "5");
        Fill(cut, "Cuota mensual", "30,000");
        Submit(cut);

        Assert.Equal(1, saved);
        Assert.Equal(1_000_000m, model.OriginalAmount);
        Assert.Equal(500_000m, model.InitialPaidAmount);
        Assert.Equal(5m, model.AnnualInterestRate);
        Assert.Equal(5, model.PaymentDay);
        Assert.Equal(30_000m, model.MonthlyPayment);
        Assert.Null(model.TermMonths);
    }

    [Fact]
    public void DebtForm_TermInsteadOfInstallment_Saves()
    {
        var model = new DebtInput { StartDate = new DateOnly(2026, 11, 1) };
        var cut = RenderDebt(model);

        Fill(cut, "Nombre", "Carro");
        Fill(cut, "Monto original", "100000");
        Fill(cut, "Plazo (meses)", "12");
        Submit(cut);

        Assert.Equal(1, saved);
        Assert.Equal(12, model.TermMonths);
        Assert.Equal(0m, model.MonthlyPayment);
    }

    [Fact]
    public void DebtForm_TheDebtCurrency_AppliesToThePaidAndInstallmentFields()
    {
        var model = new DebtInput { StartDate = new DateOnly(2026, 11, 1) };
        var cut = RenderDebt(model);
        Assert.All(cut.FindAll(".field-group__affix--start"), a => Assert.Equal("RD$", a.TextContent));

        cut.Find("select[aria-label=Moneda]").Change("USD");

        Assert.Equal(Currency.USD, model.Currency);
        var symbols = cut.FindAll(".field-group__affix--start").Select(a => a.TextContent).ToList();
        Assert.Equal(new[] { "US$", "US$" }, symbols);
    }

    [Fact]
    public void DebtForm_OutOfRangeValues_ShowTheSpanishMessages()
    {
        var model = new DebtInput { Name = "X", OriginalAmount = 1000, StartDate = new DateOnly(2026, 1, 1) };
        var cut = RenderDebt(model);

        Fill(cut, "Día de pago (1-28)", "31");
        Fill(cut, "Tasa anual", "150");
        Submit(cut);

        Assert.Equal(0, saved);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("El día de pago debe estar entre 1 y 28.", Errors(cut));
            Assert.Contains("La tasa anual debe estar entre 0 y 100.", Errors(cut));
        });
    }

    [Fact]
    public void DebtPaymentForm_ShowsTheDebtCurrency_AndSaves()
    {
        var model = new DebtPaymentInput { Date = new DateOnly(2026, 11, 18) };
        var cut = Render<DebtPaymentForm>(p => p
            .Add(f => f.Visible, true).Add(f => f.Model, model).Add(f => f.Currency, Currency.USD)
            .Add(f => f.OnSave, OnSave).Add(f => f.OnCancel, OnCancel));

        Assert.Equal("US$", cut.Find(".field-group__affix--start").TextContent);

        Fill(cut, "Monto", "30,000");
        Fill(cut, "Notas", "Cuota de noviembre");
        Submit(cut);

        Assert.Equal(1, saved);
        Assert.Equal(30_000m, model.Amount);
        Assert.Equal("Cuota de noviembre", model.Notes);
    }

    [Fact]
    public void DebtPaymentForm_PrefilledSuggestedAmount_IsShownFormatted()
    {
        var cut = Render<DebtPaymentForm>(p => p
            .Add(f => f.Visible, true).Add(f => f.Model, new DebtPaymentInput { Amount = 30000m, Date = new DateOnly(2026, 11, 18) }));

        Assert.Equal("30,000.00", Control(cut, "Monto").GetAttribute("value"));
    }

    // ================= GoalForm =================

    private IRenderedComponent<GoalForm> RenderGoal(GoalInput model)
        => Render<GoalForm>(p => p
            .Add(f => f.Visible, true).Add(f => f.Model, model).Add(f => f.Categories, Categories)
            .Add(f => f.Currency, Currency.USD).Add(f => f.OnSave, OnSave).Add(f => f.OnCancel, OnCancel));

    [Fact]
    public void GoalForm_AnnualGoal_Saves()
    {
        var model = new GoalInput { Period = GoalPeriod.Annual, Year = 2026 };
        var cut = RenderGoal(model);
        Assert.Equal("US$", cut.Find(".field-group__affix--start").TextContent);

        Fill(cut, "Nombre", "Ahorro de 200,000 anualmente");
        Fill(cut, "Monto objetivo", "200,000");
        Fill(cut, "Categoría vinculada", "2");
        Submit(cut);

        Assert.Equal(1, saved);
        Assert.Equal(200_000m, model.TargetAmount);
        Assert.Equal(2, model.CategoryId);
        Assert.Equal(2026, model.Year);
    }

    [Fact]
    public void GoalForm_ChangingThePeriod_SwapsYearForDates()
    {
        var model = new GoalInput { Period = GoalPeriod.Annual, Year = 2026, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31) };
        var cut = RenderGoal(model);
        Assert.Contains(cut.FindAll("label"), l => l.TextContent.Trim() == "Año");

        Fill(cut, "Periodo", "Custom");

        Assert.DoesNotContain(cut.FindAll("label"), l => l.TextContent.Trim() == "Año");
        Fill(cut, "Inicio", "2026-03-01");
        Fill(cut, "Fin", "2026-08-31");
        Assert.Equal(new DateOnly(2026, 3, 1), model.StartDate);
        Assert.Equal(new DateOnly(2026, 8, 31), model.EndDate);

        Fill(cut, "Periodo", "Monthly");
        Assert.Contains("Se reinicia cada mes", cut.Find(".form-field__hint").TextContent);
    }

    [Fact]
    public void GoalForm_WithoutCategory_ShowsTheMessage()
    {
        var cut = RenderGoal(new GoalInput { Name = "x", TargetAmount = 100, Period = GoalPeriod.Annual, Year = 2026 });

        Submit(cut);

        Assert.Equal(0, saved);
        cut.WaitForAssertion(() => Assert.Contains("Seleccione la categoría vinculada.", Errors(cut)));
    }

    // ================= AllocationForm =================

    [Fact]
    public void AllocationForm_EditingAValue_UpdatesTheEquivalentAndTheTotal()
    {
        var rows = new List<AllocationRowDto>
        {
            new() { CategoryId = 1, CategoryName = "Comida", CategoryColor = "#E67E22", IsActive = true, Value = 0 },
            new() { CategoryId = 2, CategoryName = "Ahorro", CategoryColor = "#2E9E5B", IsActive = true,
                    AllocationType = AllocationType.PercentageOfIncome, Value = 10, IncomeSourceId = 1 }
        };
        var editor = new AllocationEditorDto
        {
            Rows = rows,
            IncomeBases = new IncomeBasesDto
            {
                RecurringTotal = 60_000m,
                Sources = new() { new IncomeBaseDto { SourceId = 1, Name = "Sueldo trabajo", MonthlyAmount = 50_000m, IsCurrent = true } }
            }
        };
        var cut = Render<AllocationForm>(p => p
            .Add(f => f.Visible, true).Add(f => f.Editor, editor).Add(f => f.Rows, rows)
            .Add(f => f.OnSave, OnSave).Add(f => f.OnCancel, OnCancel));

        Assert.Contains("RD$ 5,000.00", cut.Find("tbody tr:nth-child(2)").TextContent); // 10 % of 50,000

        cut.Find("input[aria-label='Valor de Comida']").Input("10000");

        Assert.Equal(10_000m, rows[0].Value);
        Assert.Contains("RD$ 15,000.00", cut.Find("tfoot").TextContent);

        cut.FindAll("button").First(b => b.TextContent.Contains("Guardar plan")).Click();
        Assert.Equal(1, saved);
    }
}
