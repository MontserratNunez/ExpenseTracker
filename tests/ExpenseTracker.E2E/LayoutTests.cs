using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class LayoutTests
{
    private readonly AppFixture _app;

    public LayoutTests(AppFixture app) => _app = app;

    private static ILocator Sidebar(IPage page) => page.Locator("aside.sidebar");

    private static ILocator BottomNav(IPage page) => page.Locator("nav.bottom-nav");

    // The element can be replaced a moment after load (server page swapped for the interactive one), so ask again if it is gone.
    private static async Task<double> WidthAsync(ILocator locator)
    {
        for (var attempt = 1; ; attempt++)
        {
            var box = await locator.BoundingBoxAsync();
            if (box is not null || attempt == 10)
                return box!.Width;

            await Task.Delay(200);
        }
    }

    private static ILocator UserMenuButton(IPage page)
        => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = new System.Text.RegularExpressions.Regex("^Menú de ") });

    // ================= Desktop =================

    [E2EFact]
    public async Task Desktop_ShowsTheSidebar_NotTheBottomNav_AndMarksTheCurrentPage()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 800);
        await tab.RegisterAsync();
        var page = tab.Page;

        await tab.GotoAsync("/gastos");
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Colapsar barra lateral" })).ToBeVisibleAsync();   // the circuit is connected
        await Expect(Sidebar(page)).ToBeVisibleAsync();
        await Expect(BottomNav(page)).ToBeHiddenAsync();
        Assert.InRange(await WidthAsync(Sidebar(page)), 239, 241);

        var current = Sidebar(page).Locator("a[aria-current=page]");
        await Expect(current).ToHaveCountAsync(1);
        await Expect(current).ToHaveAttributeAsync("aria-label", "Gastos");
        await Expect(page.Locator("main#main")).ToBeVisibleAsync();
        await tab.ShotAsync("layout-escritorio");

        // Going to another page moves the mark.
        await Sidebar(page).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Deudas" }).ClickAsync();
        await page.WaitForURLAsync($"{_app.BaseUrl}/deudas");
        await Expect(Sidebar(page).Locator("a[aria-current=page]")).ToHaveAttributeAsync("aria-label", "Deudas");
        await Expect(page.Locator("main#main")).ToBeVisibleAsync();

        // The content is never wider than 1100 px and never overflows the page sideways.
        Assert.True(await WidthAsync(page.Locator("main#main")) <= 1100.5);
        Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth"));
        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task EveryPage_HasTheSamePageHeader_WithOneLevelOneHeading()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();
        var page = tab.Page;

        foreach (var path in new[] { "/", "/ingresos", "/gastos", "/deudas", "/plan", "/metas", "/estimaciones", "/configuracion" })
        {
            await tab.GotoAsync(path);
            await Expect(page.Locator("main#main header.page-header")).ToHaveCountAsync(1);
            await Expect(page.Locator("main#main h1")).ToHaveCountAsync(1);
        }

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task SkipLink_IsTheFirstElement_AppearsOnlyWithTheFocus_AndMovesToTheContent()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();
        var page = tab.Page;
        await tab.GotoAsync("/gastos");
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Colapsar barra lateral" })).ToBeVisibleAsync();   // the circuit is connected
        var skip = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Saltar al contenido" });

        // It is the first focusable element of the page, and off screen until it has the focus. (The router moves the
        // focus to the page title on load so the new page is announced; Shift+Tab from there is not the way to it,
        // which is why the test focuses it directly.)
        Assert.Equal("Saltar al contenido", await page.EvaluateAsync<string>(
            "() => document.querySelector('a[href], button:not([disabled]), input, select, textarea, [tabindex]:not([tabindex=\"-1\"])').textContent.trim()"));
        Assert.True((await skip.BoundingBoxAsync())!.Y < 0);
        await skip.FocusAsync();
        await Expect(skip).ToBeFocusedAsync();
        Assert.True((await skip.BoundingBoxAsync())!.Y >= 0);

        await page.Keyboard.PressAsync("Enter");
        await Expect(page.Locator("main#main")).ToBeFocusedAsync();
    }

    // ================= Collapsing the sidebar =================

    [E2EFact]
    public async Task TheSidebar_Collapses_ToIcons_AndRemembersItAfterReloading()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();
        var page = tab.Page;
        await tab.GotoAsync("/gastos");
        var toggle = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Colapsar barra lateral" });

        await Tab.ClickUntilAsync(toggle, page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Expandir barra lateral" }));

        Assert.InRange(await WidthAsync(Sidebar(page)), 63, 65);
        await Expect(Sidebar(page).GetByText("Gastos")).ToBeHiddenAsync();
        // Icons only, but each link keeps its name for screen readers and a tooltip.
        var gastos = Sidebar(page).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Gastos" });
        await Expect(gastos).ToHaveAttributeAsync("title", "Gastos");
        await tab.ShotAsync("layout-colapsado");

        // After a reload it is collapsed from the first paint, not after the circuit connects.
        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        Assert.Equal("collapsed", await page.EvaluateAsync<string>("() => document.documentElement.dataset.sidebar"));
        Assert.InRange(await WidthAsync(Sidebar(page)), 63, 65);

        // Expanding it forgets the preference.
        var expand = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Expandir barra lateral" });
        await Tab.ClickUntilAsync(expand, page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Colapsar barra lateral" }));
        Assert.InRange(await WidthAsync(Sidebar(page)), 239, 241);
        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        Assert.InRange(await WidthAsync(Sidebar(page)), 239, 241);
        Assert.Empty(tab.ConsoleErrors);
    }

    // ================= User menu =================

    [E2EFact]
    public async Task UserMenu_OpensAboveTheButton_WithTheKeyboard_AndEscReturnsTheFocus()
    {
        await using var tab = await Tab.OpenAsync(_app);
        var email = await tab.RegisterAsync();
        var page = tab.Page;
        var button = UserMenuButton(page);
        var menu = page.GetByRole(AriaRole.Menu);

        await Expect(button).ToContainTextAsync(email);
        await button.FocusAsync();
        await Tab.ClickUntilAsync(button, menu);
        await Expect(menu.GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = "Mi cuenta" })).ToBeFocusedAsync();

        var box = (await menu.BoundingBoxAsync())!;
        var trigger = (await button.BoundingBoxAsync())!;
        Assert.True(box.Y + box.Height <= trigger.Y + 1, "the menu should be above the button");
        await tab.ShotAsync("menu-usuario", fullPage: false);

        await page.Keyboard.PressAsync("ArrowDown");
        await Expect(menu.GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = "Cerrar sesión" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Escape");
        await Expect(menu).ToBeHiddenAsync();
        await Expect(button).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task UserMenu_MyAccount_GoesToTheAccountPage()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();
        var page = tab.Page;
        var menu = page.GetByRole(AriaRole.Menu);

        await Tab.ClickUntilAsync(UserMenuButton(page), menu);
        await menu.GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = "Mi cuenta" }).ClickAsync();

        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/Account/Manage"));
    }

    // ================= Phone =================

    [E2EFact]
    public async Task Phone_ShowsTheBottomNav_NotTheSidebar_WithoutSidewaysScroll()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 360, height: 740);
        await tab.RegisterAsync();
        var page = tab.Page;

        foreach (var path in new[] { "/", "/gastos", "/plan", "/ingresos", "/deudas", "/metas", "/estimaciones", "/configuracion" })
        {
            await tab.GotoAsync(path);
            await Expect(page.Locator("main#main h1")).ToHaveCountAsync(1);
            await Expect(Sidebar(page)).ToBeHiddenAsync();
            await Expect(BottomNav(page)).ToBeVisibleAsync();
            Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth"), $"{path} scrolls sideways");
        }

        await tab.GotoAsync("/gastos");
        await Expect(BottomNav(page).GetByRole(AriaRole.Link)).ToHaveCountAsync(4);   // Inicio, Gastos, +, Plan
        await Expect(BottomNav(page).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Más" })).ToBeVisibleAsync();
        await Expect(BottomNav(page).Locator("a[aria-current=page]")).ToHaveAttributeAsync("href", "gastos");
        await tab.ShotAsync("layout-movil");
        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Phone_TheCentralPlusButton_OpensTheNewExpenseForm_FromAnyPage()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 360, height: 740);
        await tab.RegisterAsync();
        var page = tab.Page;
        var form = page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Nuevo gasto" });

        await tab.GotoAsync("/metas");
        await BottomNav(page).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Nuevo gasto" }).ClickAsync(new LocatorClickOptions { NoWaitAfter = true });

        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"/gastos/\d+/\d+$"));   // the query is dropped
        await Expect(form).ToBeVisibleAsync();
        await tab.ShotAsync("movil-nuevo-gasto", form);

        // Closing it and reloading does not reopen it.
        await page.WaitForFunctionAsync("() => document.activeElement?.closest('[role=dialog]') != null");
        await page.Keyboard.PressAsync("Escape");
        await Expect(form).ToBeHiddenAsync();
        await page.ReloadAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Gastos", Level = 1 })).ToBeVisibleAsync();
        await Expect(form).ToBeHiddenAsync();

        // From the expenses page itself too.
        await BottomNav(page).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Nuevo gasto" }).ClickAsync(new LocatorClickOptions { NoWaitAfter = true });
        await Expect(form).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task Phone_More_OpensASheet_ThatListsTheRest_ClosesWithEsc_AndNavigates()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 360, height: 740);
        await tab.RegisterAsync();
        var page = tab.Page;
        await tab.GotoAsync("/gastos");
        var more = BottomNav(page).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Más" });
        var sheet = page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Más" });

        await Tab.ClickUntilAsync(more, sheet);
        foreach (var name in new[] { "Ingresos", "Deudas", "Metas", "Estimaciones", "Configuración" })
            await Expect(sheet.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = name })).ToBeVisibleAsync();
        await Expect(sheet.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cerrar sesión" })).ToBeVisibleAsync();

        // It rises from the bottom edge and covers the full width.
        var box = (await sheet.BoundingBoxAsync())!;
        Assert.InRange(box.Width, 359, 361);
        Assert.True(box.Y > 200, "the sheet should sit at the bottom");
        await tab.ShotAsync("movil-hoja-mas", sheet);

        await page.WaitForFunctionAsync("() => document.activeElement?.closest('[role=dialog]') != null");
        await page.Keyboard.PressAsync("Escape");
        await Expect(sheet).ToBeHiddenAsync();
        await Expect(more).ToBeFocusedAsync();

        await Tab.ClickUntilAsync(more, sheet);
        await sheet.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Metas" }).ClickAsync();
        await page.WaitForURLAsync($"{_app.BaseUrl}/metas");
        await Expect(sheet).ToBeHiddenAsync();
    }

    [E2EFact]
    public async Task Phone_SigningOutFromTheSheet_Works()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 360, height: 740);
        await tab.RegisterAsync();
        var page = tab.Page;
        var sheet = page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Más" });

        await Tab.ClickUntilAsync(BottomNav(page).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Más" }), sheet);
        await page.RunAndWaitForResponseAsync(
            () => sheet.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cerrar sesión" }).ClickAsync(),
            r => r.Url.Contains("/Account/Logout") && r.Request.Method == "POST");

        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/Account/Login"));
        await Expect(BottomNav(page)).ToHaveCountAsync(0);   // signed out: no navigation bar
    }
}
