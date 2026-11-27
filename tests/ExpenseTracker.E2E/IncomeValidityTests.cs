using System.Globalization;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

/// <summary>Income and exchange rates with validity dates (phase 15B). Dates follow the real clock, like the app.</summary>
[Collection(E2ECollection.Name)]
public class IncomeValidityTests
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-DO");

    private readonly AppFixture _app;

    public IncomeValidityTests(AppFixture app) => _app = app;

    private static ILocator Dialog(IPage page, string name) => page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    private static ILocator Button(ILocator scope, string name)
        => scope.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = name, Exact = true });

    private static ILocator Field(ILocator scope, string label)
        => scope.GetByLabel(label, new LocatorGetByLabelOptions { Exact = true });

    private static string ShortMonth(DateTime date) => $"{date.ToString("MMMM", Culture)[..3]} {date.Year}";

    private static async Task OpenMenuAsync(Tab tab, string item, ILocator expected)
    {
        var page = tab.Page;
        var menu = page.GetByRole(AriaRole.Menu);
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Acciones de Sueldo" }), menu);
        await menu.GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = item }).ClickAsync();
        await Expect(expected).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task Salary_ChangesFromAMonth_AndItsHistoryIsCorrectedExplicitly()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        var now = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        await tab.GotoAsync("/ingresos");
        var create = Dialog(page, "Nueva fuente de ingreso");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva fuente" }).First, create);
        await Field(create, "Nombre").FillAsync("Sueldo");
        await Field(create, "Monto").FillAsync("50000");
        await Field(create, "Monto").PressAsync("Tab");
        await Button(create, "Guardar").ClickAsync();
        await Expect(create).ToBeHiddenAsync();

        // A raise asks since when; the card keeps today's amount and announces the next one.
        var edit = Dialog(page, "Editar fuente de ingreso");
        await OpenMenuAsync(tab, "Editar", edit);
        await Expect(Field(edit, "¿Desde cuándo?")).ToHaveCountAsync(0);
        await Field(edit, "Monto").FillAsync("60000");
        await Field(edit, "Monto").PressAsync("Tab");
        await Field(edit, "¿Desde cuándo?").SelectOptionAsync("next");
        await tab.ShotAsync("ingreso-desde-cuando", edit);
        await Button(edit, "Guardar").ClickAsync();
        await Expect(edit).ToBeHiddenAsync();

        var card = page.Locator(".income-card");
        await Expect(card.Locator(".income-card__amount")).ToContainTextAsync("RD$ 50,000.00");
        await Expect(card.Locator(".income-card__next")).ToContainTextAsync($"desde {ShortMonth(now.AddMonths(1))}: RD$ 60,000.00");

        // Correcting the history shows the effect before it can be saved.
        var past = now.AddMonths(-2);
        var history = Dialog(page, "Historial de Sueldo");
        await OpenMenuAsync(tab, "Historial", history);
        await Expect(history.Locator(".income-history li")).ToHaveCountAsync(2);
        await Button(history, "Corregir historial").ClickAsync();
        await Field(history, "Corregir desde: año").SelectOptionAsync(past.Year.ToString());
        await Field(history, "Corregir desde: mes").SelectOptionAsync(past.Month.ToString());
        await Field(history, "Monto correcto").FillAsync("45000");
        await Expect(Button(history, "Guardar corrección")).ToHaveCountAsync(0);
        await Button(history, "Ver efecto").ClickAsync();
        await Expect(history).ToContainTextAsync("2 meses pasados");
        await tab.ShotAsync("ingreso-corregir-historial", history);
        await Button(history, "Guardar corrección").ClickAsync();
        await Expect(history.Locator(".income-history li")).ToHaveCountAsync(3);
        await Button(history, "Listo").ClickAsync();
        await Expect(history).ToBeHiddenAsync();
        await Expect(card.Locator(".income-card__amount")).ToContainTextAsync("RD$ 50,000.00");

        // Turning the source off would reach those months: it has to be accepted.
        await OpenMenuAsync(tab, "Editar", edit);
        await edit.GetByRole(AriaRole.Switch, new LocatorGetByRoleOptions { Name = "Activa" }).UncheckAsync();
        await Button(edit, "Guardar").ClickAsync();
        await Expect(edit).ToContainTextAsync("Esto cambiará la evaluación de 2 meses pasados");
        await tab.ShotAsync("ingreso-aviso-meses-pasados", edit);
        await page.Keyboard.PressAsync("Escape");
        await Expect(edit).ToBeHiddenAsync();

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Rates_ApplyFromADate_AndKeepTheirHistory()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        await tab.GotoAsync("/configuracion");
        var edit = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Editar la tasa de USD" });
        var rate = page.GetByLabel("Tasa de USD", new PageGetByLabelOptions { Exact = true });
        var from = page.GetByLabel("Rige desde, USD");

        await Tab.ClickUntilAsync(edit, rate);
        await rate.FillAsync("60.5");
        await from.FillAsync(DateTime.Today.AddMonths(-1).ToString("yyyy-MM-dd"));
        await rate.PressAsync("Enter");
        await Expect(rate).ToHaveCountAsync(0);

        await edit.ClickAsync();
        await rate.FillAsync("61");
        await from.FillAsync(DateTime.Today.ToString("yyyy-MM-dd"));
        await rate.PressAsync("Enter");
        await Expect(rate).ToHaveCountAsync(0);

        var row = page.Locator(".settings-row").Filter(new LocatorFilterOptions { HasText = "USD" });
        await Expect(row).ToContainTextAsync("1 USD = 61 DOP");
        var history = page.Locator(".rate-history");
        await history.GetByText("Historial de USD").ClickAsync();
        await Expect(history.Locator("li")).ToHaveCountAsync(2);
        await tab.ShotAsync("tasas-historial", page.Locator("section[aria-labelledby=settings-rates]"));

        Assert.Empty(tab.ConsoleErrors);
    }
}
