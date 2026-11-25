using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class PlanDebtsTests
{
    private readonly AppFixture _app;

    public PlanDebtsTests(AppFixture app) => _app = app;

    private static ILocator Dialog(IPage page, string name) => page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    private static ILocator Field(ILocator dialog, string label) => dialog.GetByLabel(label, new LocatorGetByLabelOptions { Exact = true });

    private static ILocator Toast(IPage page, string text) => page.Locator(".toast-app").Filter(new LocatorFilterOptions { HasText = text });

    private static async Task AddIncomeAsync(Tab tab, string amount)
    {
        var page = tab.Page;
        await tab.GotoAsync("/ingresos");
        var dialog = Dialog(page, "Nueva fuente de ingreso");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva fuente" }).First, dialog);
        await Field(dialog, "Nombre").FillAsync("Sueldo");
        await Field(dialog, "Monto").FillAsync(amount);
        await Field(dialog, "Monto").PressAsync("Tab");
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(page.GetByText("Fuente de ingreso creada.")).ToBeVisibleAsync();
    }

    private static async Task AddExpenseAsync(Tab tab, string name, string amount, string category)
    {
        var page = tab.Page;
        await tab.GotoAsync("/gastos");
        var dialog = Dialog(page, "Nuevo gasto");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nuevo gasto" }).First, dialog);
        await Field(dialog, "Nombre").FillAsync(name);
        await Field(dialog, "Monto").FillAsync(amount);
        await Field(dialog, "Monto").PressAsync("Tab");
        await Field(dialog, "Categoría").SelectOptionAsync(new SelectOptionValue { Label = category });
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();
    }

    private static async Task AssignAsync(Tab tab, params (string Category, string Value)[] rows)
    {
        var page = tab.Page;
        await tab.GotoAsync("/plan");
        var dialog = Dialog(page, "Editar plan");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Editar plan" }).First, dialog);
        foreach (var (category, value) in rows)
            await dialog.GetByLabel($"Valor de {category}").FillAsync(value);
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar plan" }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(page.GetByText("Plan guardado.").First).ToBeVisibleAsync();
    }

    private static ILocator PlanRow(IPage page, string category)
        => page.Locator(".plan-row").Filter(new LocatorFilterOptions { HasText = category });

    [E2EFact]
    public async Task Plan_ShowsSummary_StatesWithIconAndText_AndOutOfPlan()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        await AddIncomeAsync(tab, "50000");
        await AddExpenseAsync(tab, "Arroz", "700", "Comida");
        await AddExpenseAsync(tab, "Cine", "900", "Entretenimiento");
        await AddExpenseAsync(tab, "Pienso", "250", "Transporte");   // not in the plan
        await AssignAsync(tab, ("Comida", "10000"), ("Entretenimiento", "500"));

        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Plan", Exact = true })).ToBeVisibleAsync();

        // Two summary cards: what is assigned (as a share of income) and the month-end estimate.
        var cards = page.Locator(".plan-summary .stat-card");
        await Expect(cards).ToHaveCountAsync(2);
        await Expect(cards.Nth(0)).ToContainTextAsync("Asignado");
        await Expect(cards.Nth(0)).ToContainTextAsync("RD$ 10,500.00");
        await Expect(cards.Nth(0)).ToContainTextAsync("21 % de tus ingresos");
        await Expect(cards.Nth(1)).ToContainTextAsync("Estimado a fin de mes");

        // Each state is shown with an icon and words, never color alone.
        var food = PlanRow(page, "Comida");
        await Expect(food).ToContainTextAsync("RD$ 9,300.00 disponibles");
        await Expect(food.Locator(".plan-row__status .bi-check-circle")).ToBeVisibleAsync();
        var leisure = PlanRow(page, "Entretenimiento");
        await Expect(leisure).ToContainTextAsync("Ya te pasaste por RD$ 400.00");
        await Expect(leisure.Locator(".plan-row__status .bi-x-octagon")).ToBeVisibleAsync();
        await Expect(leisure.GetByRole(AriaRole.Progressbar)).ToBeVisibleAsync();

        // Spending in a category with no allocation is listed apart.
        var outside = page.Locator("section.plan-outside");
        await Expect(outside.GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "Fuera del plan" })).ToBeVisibleAsync();
        await Expect(outside).ToContainTextAsync("Transporte");
        await Expect(outside).ToContainTextAsync("RD$ 250.00");

        await tab.ShotAsync("plan-rediseno", fullPage: true);
        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Plan_EditsTheAssignedAmountInline_WithEnter_Esc_AndErrors_WithoutOpeningAModal()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        await AddIncomeAsync(tab, "50000");
        await AssignAsync(tab, ("Comida", "10000"));
        await Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);

        var row = PlanRow(page, "Comida");
        var edit = row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Editar lo asignado a Comida" });
        var input = page.GetByLabel("Monto asignado a Comida");

        // Esc cancels and keeps the old value.
        await Tab.ClickUntilAsync(edit, input);
        await Expect(input).ToBeFocusedAsync();
        await Expect(input).ToHaveValueAsync("10,000.00");
        await input.FillAsync("99999");
        await input.PressAsync("Escape");
        await Expect(input).ToHaveCountAsync(0);
        await Expect(row).ToContainTextAsync("RD$ 10,000.00");

        // An invalid amount shows the error under the row and keeps editing.
        await edit.ClickAsync();
        await input.FillAsync("5000,5");
        await input.PressAsync("Enter");
        await Expect(row.GetByRole(AriaRole.Alert)).ToContainTextAsync("Escriba un monto válido");
        await Expect(input).ToBeVisibleAsync();
        await tab.ShotAsync("plan-edicion-en-linea", row);

        // Enter saves.
        await input.FillAsync("12,000");
        await input.PressAsync("Enter");
        await Expect(input).ToHaveCountAsync(0);
        await Expect(row).ToContainTextAsync("RD$ 12,000.00");
        await Expect(page.GetByText("Plan guardado.").First).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
        await Expect(page.Locator(".plan-summary .stat-card").First).ToContainTextAsync("RD$ 12,000.00");

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Debts_ShowCardsWithPercent_Payoff_AndTheDetailWithPaymentsAndAmortization()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        await tab.GotoAsync("/deudas");
        var form = Dialog(page, "Nueva deuda");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva deuda" }).First, form);
        await Field(form, "Nombre").FillAsync("Préstamo carro");
        await Field(form, "Monto original").FillAsync("100000");
        await Field(form, "Monto original").PressAsync("Tab");
        await Field(form, "Cuota mensual").FillAsync("5000");
        await Field(form, "Cuota mensual").PressAsync("Tab");
        await form.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(Toast(page, "Deuda creada.")).ToBeVisibleAsync();

        // The card: total, big percentage, thick bar and the payoff phrase.
        await Expect(page.Locator(".debt-total .stat-card")).ToContainTextAsync("RD$ 100,000.00");
        var card = page.Locator("article.debt-card");
        await Expect(card.Locator(".debt-card__percent-value")).ToHaveTextAsync("0 %");
        await Expect(card.GetByRole(AriaRole.Progressbar)).ToHaveCSSAsync("height", "8px");
        await Expect(card).ToContainTextAsync("Libre de deuda en");
        await Expect(card.Locator(".bi-calendar-check")).ToBeVisibleAsync();
        await tab.ShotAsync("deudas-tarjeta", card);

        await card.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Préstamo carro" }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/deudas/\d+$"));

        // Amortization: the current month is marked (with text, not only color) and the header is sticky.
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Ver tabla de amortización" }), page.Locator(".amort__table"));
        var current = page.Locator("tr.amort__current");
        await Expect(current).ToHaveCountAsync(1);
        await Expect(current).ToContainTextAsync("Este mes");
        await Expect(page.Locator(".amort__table th").First).ToHaveCSSAsync("position", "sticky");
        await tab.ShotAsync("deudas-amortizacion", page.Locator(".amort"));

        // Register a payment in a side panel with the suggested amount.
        var panel = Dialog(page, "Registrar pago");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Registrar pago" }).First, panel);
        await Expect(Field(panel, "Monto")).ToHaveValueAsync("5,000.00");
        await tab.ShotAsync("deudas-registrar-pago", panel);
        await panel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Registrar", Exact = true }).ClickAsync();
        await Expect(panel).ToBeHiddenAsync();
        await Expect(Toast(page, "Pago registrado.")).ToBeVisibleAsync();

        // The history is a list, the percentage moved and the payment can be removed.
        var payment = page.Locator("li.payment-row");
        await Expect(payment).ToHaveCountAsync(1);
        await Expect(payment).ToContainTextAsync("RD$ 5,000.00");
        await Expect(page.Locator(".debt-summary .debt-card__percent-value")).Not.ToHaveTextAsync("0 %");
        await Expect(page.Locator("table").First).ToBeVisibleAsync();   // only the amortization table remains
        await tab.ShotAsync("deudas-detalle", fullPage: true);

        await payment.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = new Regex("^Eliminar el pago del") }).ClickAsync();
        await Dialog(page, "Eliminar pago").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Eliminar" }).ClickAsync();
        await Expect(page.Locator("li.payment-row")).ToHaveCountAsync(0);

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Plan_AndDebts_FitAPhone()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 360, height: 800);
        await tab.RegisterAsync();
        var page = tab.Page;

        await AddIncomeAsync(tab, "50000");
        await AddExpenseAsync(tab, "Arroz", "700", "Comida");
        await AssignAsync(tab, ("Comida", "10000"));
        await Expect(PlanRow(page, "Comida")).ToBeVisibleAsync();

        foreach (var name in new[] { "plan", "deudas" })
        {
            await tab.GotoAsync("/" + name);
            await Expect(page.Locator("main h1")).ToBeVisibleAsync();
            var overflow = await page.EvaluateAsync<string>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth ? [...document.querySelectorAll('main *')].filter(e => e.getBoundingClientRect().right > document.documentElement.clientWidth + 1).slice(0, 5).map(e => e.tagName + '.' + e.className).join(' | ') : ''");
            Assert.True(overflow == string.Empty, $"/{name} se desborda en 360 px: " + overflow);
        }

        await tab.GotoAsync("/plan");
        await tab.ShotAsync("plan-movil", fullPage: true);
        Assert.Empty(tab.ConsoleErrors);
    }
}
