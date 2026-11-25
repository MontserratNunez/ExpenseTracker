using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class ShortcutsTests
{
    private readonly AppFixture _app;

    public ShortcutsTests(AppFixture app) => _app = app;

    private static ILocator Dialog(IPage page, string name) => page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    private static ILocator Field(ILocator dialog, string label) => dialog.GetByLabel(label, new LocatorGetByLabelOptions { Exact = true });

    private static ILocator MonthLabel(IPage page) => page.Locator(".month-nav__label");

    /// <summary>Waits until the browser is listening (the circuit is connected) by pressing the key until it has an effect.</summary>
    private static async Task PressUntilAsync(IPage page, string key, ILocator expected, int attempts = 12)
    {
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            await page.Locator("body").PressAsync(key);
            try
            {
                await expected.WaitForAsync(new LocatorWaitForOptions { Timeout = 1200 });
                return;
            }
            catch (TimeoutException) when (attempt < attempts)
            {
                // the shortcut script was not registered yet
            }
        }
    }

    private static async Task OpenAppAsync(Tab tab, string path)
    {
        await tab.GotoAsync(path);
        await Expect(tab.Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Colapsar barra lateral" })).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task N_OpensTheNewExpensePanel_FromAnyPage_AndAnExpenseIsCreatedWithTheKeyboardInUnderTenSeconds()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await OpenAppAsync(tab, "/plan");

        var panel = Dialog(page, "Nuevo gasto");
        var clock = Stopwatch.StartNew();
        await PressUntilAsync(page, "n", panel);
        await page.WaitForURLAsync(new Regex(@"/gastos/\d+/\d+$"));   // N from another page goes to Gastos
        await Expect(Field(panel, "Nombre")).ToBeFocusedAsync();

        // Type the name, the amount and the category, then Enter from the amount field.
        await page.Keyboard.TypeAsync("Cena");
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("Tab");   // the currency comes before the amount
        await page.Keyboard.TypeAsync("1500");
        await Field(panel, "Categoría").SelectOptionAsync(new SelectOptionValue { Label = "Comida" });
        await Field(panel, "Monto").FocusAsync();
        await page.Keyboard.PressAsync("Enter");

        await Expect(panel).ToBeHiddenAsync();
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Cena" })).ToContainTextAsync("1,500.00");
        clock.Stop();
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"Crear el gasto con el teclado tardó {clock.Elapsed.TotalSeconds:0.0} s");

        // On Gastos itself it opens the panel of the month on screen, without leaving the page.
        var url = page.Url;
        await PressUntilAsync(page, "n", panel);
        Assert.Equal(url, page.Url);
        await page.Keyboard.PressAsync("Escape");
        await Expect(panel).ToBeHiddenAsync();
        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Arrows_ChangeTheMonth_InExpensesAndPlan_AndStopOneMonthAfterTheCurrent()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await OpenAppAsync(tab, "/gastos");

        var now = DateTime.Today;
        var current = await MonthLabel(page).InnerTextAsync();
        var previous = now.AddMonths(-1);
        var next = now.AddMonths(1);
        var cultureNames = System.Globalization.CultureInfo.GetCultureInfo("es-DO").DateTimeFormat;

        string Label(DateTime d) => char.ToUpper(cultureNames.GetMonthName(d.Month)[0]) + cultureNames.GetMonthName(d.Month)[1..] + " " + d.Year;
        Assert.Equal(Label(now), current);

        // Left goes back (retrying until the browser listens).
        for (var attempt = 0; attempt < 12 && await MonthLabel(page).InnerTextAsync() != Label(previous); attempt++)
        {
            await page.Locator("body").PressAsync("ArrowLeft");
            await page.WaitForTimeoutAsync(500);
        }
        await Expect(MonthLabel(page)).ToHaveTextAsync(Label(previous));
        await page.WaitForURLAsync(new Regex($@"/gastos/{previous.Year}/{previous.Month}$"));

        await page.Locator("body").PressAsync("ArrowRight");
        await Expect(MonthLabel(page)).ToHaveTextAsync(Label(now));
        await page.Locator("body").PressAsync("ArrowRight");
        await Expect(MonthLabel(page)).ToHaveTextAsync(Label(next));
        await page.Locator("body").PressAsync("ArrowRight");   // one month after the current one is the limit
        await page.WaitForTimeoutAsync(700);
        await Expect(MonthLabel(page)).ToHaveTextAsync(Label(next));

        // The same on the plan.
        await OpenAppAsync(tab, "/plan");
        await Expect(MonthLabel(page)).ToHaveTextAsync(Label(now));
        for (var attempt = 0; attempt < 12 && await MonthLabel(page).InnerTextAsync() != Label(previous); attempt++)
        {
            await page.Locator("body").PressAsync("ArrowLeft");
            await page.WaitForTimeoutAsync(500);
        }
        await Expect(MonthLabel(page)).ToHaveTextAsync(Label(previous));
        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task PlainKeys_AreIgnoredWhileTyping_OrWhileADialogIsOpen_AndSlashGoesToTheFilter()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await OpenAppAsync(tab, "/gastos");
        var url = page.Url;

        // "/" focuses the first filter of the list (once the browser listens).
        var filter = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Filtrar por categoría" });
        for (var attempt = 0; attempt < 12; attempt++)
        {
            await page.Locator("body").PressAsync("/");
            try
            {
                await Expect(filter).ToBeFocusedAsync(new() { Timeout = 1000 });
                break;
            }
            catch (PlaywrightException) when (attempt < 11)
            {
            }
        }
        await Expect(filter).ToBeFocusedAsync();

        // Typing in a field: N, ?, / and the arrows are just characters or cursor moves.
        var quickAdd = page.GetByLabel("Agregar gasto rápido");
        await quickAdd.FocusAsync();
        await page.Keyboard.TypeAsync("n?/");
        await page.Keyboard.PressAsync("ArrowLeft");
        await page.WaitForTimeoutAsync(600);
        await Expect(quickAdd).ToHaveValueAsync("n?/");
        Assert.Equal(url, page.Url);
        await Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);

        // With a dialog open, N does not open another one.
        await quickAdd.FillAsync("");
        await page.Locator("body").ClickAsync(new LocatorClickOptions { Position = new Position { X = 5, Y = 400 } });
        var help = Dialog(page, "Atajos de teclado");
        await PressUntilAsync(page, "?", help);
        await page.Locator("body").PressAsync("n");
        await page.WaitForTimeoutAsync(600);
        await Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(1);
        await Expect(help.GetByText("Nuevo gasto")).ToBeVisibleAsync();
        await tab.ShotAsync("atajos-ayuda", help);
        await page.Keyboard.PressAsync("Escape");
        await Expect(help).ToBeHiddenAsync();
    }

    [E2EFact]
    public async Task HelpDialog_ListsEveryShortcutWithKbdKeys()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await OpenAppAsync(tab, "/");

        var help = Dialog(page, "Atajos de teclado");
        await PressUntilAsync(page, "?", help);
        var keys = await help.Locator("kbd").AllInnerTextsAsync();
        Assert.Equal(new[] { "N", "←", "→", "/", "Ctrl + K", "⌘ + K", "Esc", "?" }, keys);
        await Expect(help.GetByRole(AriaRole.Row)).ToHaveCountAsync(7);   // the header and six shortcuts
    }

    [E2EFact]
    public async Task CommandPalette_GoesToSections_RunsActions_AndFindsAnExpenseByName()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        // An expense to find later.
        await OpenAppAsync(tab, "/gastos");
        var panel = Dialog(page, "Nuevo gasto");
        await PressUntilAsync(page, "n", panel);
        await Field(panel, "Nombre").FillAsync("Supermercado");
        await Field(panel, "Monto").FillAsync("700");
        await Field(panel, "Monto").PressAsync("Tab");
        await Field(panel, "Categoría").SelectOptionAsync(new SelectOptionValue { Label = "Comida" });
        await panel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(panel).ToBeHiddenAsync();

        await OpenAppAsync(tab, "/estimaciones");
        var palette = Dialog(page, "Buscar o ir a...");
        var input = palette.GetByRole(AriaRole.Combobox);

        // Ctrl+K opens it, with the box focused, and lists the sections and actions.
        await PressUntilAsync(page, "Control+K", palette);
        await Expect(input).ToBeFocusedAsync();
        await Expect(palette.GetByRole(AriaRole.Group, new LocatorGetByRoleOptions { Name = "Ir a" })).ToBeVisibleAsync();
        await Expect(palette.GetByRole(AriaRole.Group, new LocatorGetByRoleOptions { Name = "Acciones" })).ToBeVisibleAsync();
        await Expect(palette.GetByRole(AriaRole.Option, new LocatorGetByRoleOptions { Name = "Nuevo gasto" })).ToBeVisibleAsync();
        await tab.ShotAsync("paleta-comandos", palette);

        // Ctrl+K again closes it; Esc closes it too.
        await page.Keyboard.PressAsync("Control+K");
        await Expect(palette).ToBeHiddenAsync();
        await page.Keyboard.PressAsync("Control+K");
        await Expect(palette).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Escape");
        await Expect(palette).ToBeHiddenAsync();

        // Sections: filtered without caring about accents; the arrows move, Enter goes.
        await page.Keyboard.PressAsync("Control+K");
        await input.FillAsync("configuracion");
        await Expect(palette.GetByRole(AriaRole.Option)).ToHaveCountAsync(1);
        await Expect(palette.GetByRole(AriaRole.Option, new LocatorGetByRoleOptions { Name = "Configuración" })).ToHaveAttributeAsync("aria-selected", "true");
        await page.Keyboard.PressAsync("Enter");
        await Expect(page).ToHaveURLAsync($"{_app.BaseUrl}/configuracion");
        await Expect(palette).ToBeHiddenAsync();

        // Arrows: the highlighted option follows them and wraps around.
        await page.Keyboard.PressAsync("Control+K");
        await Expect(palette.GetByRole(AriaRole.Option).First).ToHaveAttributeAsync("aria-selected", "true");
        await page.Keyboard.PressAsync("ArrowDown");
        await Expect(palette.GetByRole(AriaRole.Option).Nth(1)).ToHaveAttributeAsync("aria-selected", "true");
        await page.Keyboard.PressAsync("ArrowUp");
        await page.Keyboard.PressAsync("ArrowUp");
        await Expect(palette.GetByRole(AriaRole.Option).Last).ToHaveAttributeAsync("aria-selected", "true");
        await page.Keyboard.PressAsync("Escape");

        // Actions: "Nuevo ingreso" opens the form of the one-time income.
        await page.Keyboard.PressAsync("Control+K");
        await input.FillAsync("nuevo ing");
        await page.Keyboard.PressAsync("Enter");
        await Expect(page).ToHaveURLAsync(new Regex(@"/ingresos$"));
        await Expect(Dialog(page, "Nuevo ingreso único")).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Escape");

        // The search for an expense: Ctrl+K, type, wait for the results, Enter opens it.
        await page.Keyboard.PressAsync("Control+K");
        await input.FillAsync("super");
        var found = palette.GetByRole(AriaRole.Option, new LocatorGetByRoleOptions { Name = "Supermercado" });
        await Expect(found).ToBeVisibleAsync();
        await Expect(palette.GetByRole(AriaRole.Group, new LocatorGetByRoleOptions { Name = "Gastos" })).ToBeVisibleAsync();
        await Expect(found).ToContainTextAsync("RD$ 700.00");
        await page.Keyboard.PressAsync("ArrowDown");   // from the first section ("Ir a") to the match is not needed: it is a different group
        await found.ClickAsync();
        var edit = Dialog(page, "Editar gasto");
        await Expect(edit).ToBeVisibleAsync();
        await Expect(Field(edit, "Nombre")).ToHaveValueAsync("Supermercado");
        await page.WaitForURLAsync(new Regex(@"/gastos/\d+/\d+$"));   // the query was dropped

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task CommandPalette_SearchWithKeyboardOnly_OpensTheFoundExpense()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        await OpenAppAsync(tab, "/gastos");
        var panel = Dialog(page, "Nuevo gasto");
        await PressUntilAsync(page, "n", panel);
        await Field(panel, "Nombre").FillAsync("Supermercado");
        await Field(panel, "Monto").FillAsync("700");
        await Field(panel, "Monto").PressAsync("Tab");
        await Field(panel, "Categoría").SelectOptionAsync(new SelectOptionValue { Label = "Comida" });
        await panel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(panel).ToBeHiddenAsync();

        await OpenAppAsync(tab, "/metas");
        var palette = Dialog(page, "Buscar o ir a...");
        await PressUntilAsync(page, "Control+K", palette);
        await page.Keyboard.TypeAsync("super");

        // Wait for the result to appear, move down to it (the sections that match come first) and press Enter.
        var found = palette.GetByRole(AriaRole.Option, new LocatorGetByRoleOptions { Name = "Supermercado" });
        await Expect(found).ToBeVisibleAsync();
        var options = await palette.GetByRole(AriaRole.Option).CountAsync();
        for (var i = 0; i < options - 1; i++)
            await page.Keyboard.PressAsync("ArrowDown");
        await Expect(found).ToHaveAttributeAsync("aria-selected", "true");
        await page.Keyboard.PressAsync("Enter");

        await Expect(Dialog(page, "Editar gasto")).ToBeVisibleAsync();
        await Expect(Field(Dialog(page, "Editar gasto"), "Nombre")).ToHaveValueAsync("Supermercado");
    }
}
