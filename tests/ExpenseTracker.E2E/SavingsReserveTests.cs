using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

/// <summary>Savings as a reserve of the plan (phase 16), with the numbers of the example in 16.4.</summary>
[Collection(E2ECollection.Name)]
public class SavingsReserveTests
{
    private readonly AppFixture _app;

    public SavingsReserveTests(AppFixture app) => _app = app;

    private static ILocator Dialog(IPage page, string name) => page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    private static ILocator Button(ILocator scope, string name)
        => scope.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = name, Exact = true });

    private static ILocator Field(ILocator scope, string label)
        => scope.GetByLabel(label, new LocatorGetByLabelOptions { Exact = true });

    private static ILocator Toast(IPage page, string text) => page.Locator(".toast-app").Filter(new LocatorFilterOptions { HasText = text });

    private static async Task<ILocator> FillExpenseAsync(Tab tab, string name, string amount)
    {
        var page = tab.Page;
        await tab.GotoAsync("/gastos");
        var dialog = Dialog(page, "Nuevo gasto");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nuevo gasto" }).First, dialog);
        await Field(dialog, "Nombre").FillAsync(name);
        await Field(dialog, "Monto").FillAsync(amount);
        await Field(dialog, "Monto").PressAsync("Tab");
        await Field(dialog, "Categoría").SelectOptionAsync(new SelectOptionValue { Label = "Comida" });
        await Button(dialog, "Guardar").ClickAsync();
        return dialog;
    }

    private static async Task SetUpAsync(Tab tab)
    {
        var page = tab.Page;
        await tab.GotoAsync("/ingresos");
        var income = Dialog(page, "Nueva fuente de ingreso");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva fuente" }).First, income);
        await Field(income, "Nombre").FillAsync("Sueldo");
        await Field(income, "Monto").FillAsync("30000");
        await Field(income, "Monto").PressAsync("Tab");
        await Button(income, "Guardar").ClickAsync();
        await Expect(income).ToBeHiddenAsync();

        var editor = await tab.OpenPlanEditorAsync();
        await editor.GetByLabel("Valor del ahorro").FillAsync("5000");
        await Button(editor, "Guardar plan").ClickAsync();
        await Expect(editor).ToBeHiddenAsync();
    }

    [E2EFact]
    public async Task Reserve_IsShownInThePlan_AndSpendingIntoItAsksFirstButSaves()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await SetUpAsync(tab);

        var card = page.Locator(".savings-card");
        await Expect(card).ToContainTextAsync("Puedes gastar RD$ 25,000.00 sin tocar tu ahorro.");
        await Expect(card).ToContainTextAsync("Meta de ahorro RD$ 5,000.00");

        // Within the plan: no question, and the message says what is left.
        var expense = await FillExpenseAsync(tab, "Compra grande", "24000");
        await Expect(expense).ToBeHiddenAsync();
        await Expect(Toast(page, "Gasto guardado. Te quedan RD$ 1,000.00 dentro del plan.")).ToBeVisibleAsync();

        await tab.GotoAsync("/plan");
        await Expect(card).ToContainTextAsync("Te quedan RD$ 1,000.00 antes de usar tu ahorro.");
        await Expect(card.Locator(".savings-card__state .bi-exclamation-triangle").First).ToBeVisibleAsync();
        await Expect(card.GetByRole(AriaRole.Progressbar)).ToBeVisibleAsync();

        // Crossing into the reserve asks first; cancelling keeps the form.
        expense = await FillExpenseAsync(tab, "Cena", "1300");
        var question = Dialog(page, "Este gasto usa tu ahorro");
        await Expect(question).ToContainTextAsync("Este gasto usa RD$ 300.00 de tu ahorro. ¿Guardar igual?");
        await tab.ShotAsync("ahorro-aviso-antes-de-guardar", question);
        await Button(question, "Cancelar").ClickAsync();
        await Expect(question).ToBeHiddenAsync();
        await Expect(expense).ToBeVisibleAsync();

        await Button(expense, "Guardar").ClickAsync();
        await Button(question, "Guardar").ClickAsync();
        await Expect(expense).ToBeHiddenAsync();
        await Expect(Toast(page, "Has usado RD$ 300.00 de tus RD$ 5,000.00 de ahorro.")).ToBeVisibleAsync();

        await tab.GotoAsync("/plan");
        await Expect(card).ToContainTextAsync("Has usado RD$ 300.00 de tus RD$ 5,000.00 de ahorro.");
        await Expect(card.Locator(".savings-card__state .bi-x-octagon")).ToBeVisibleAsync();
        await tab.ShotAsync("ahorro-del-mes", fullPage: true);

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task SavingsCard_AndEditor_FitAPhone()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 360, height: 800);
        await tab.RegisterAsync();
        var page = tab.Page;
        await SetUpAsync(tab);
        await Expect(page.Locator(".savings-card")).ToBeVisibleAsync();

        var overflows = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        Assert.False(overflows, "/plan se desborda en 360 px");

        var editor = await tab.OpenPlanEditorAsync();
        await tab.ShotAsync("ahorro-editor-movil", editor);
        Assert.Empty(tab.ConsoleErrors);
    }
}
