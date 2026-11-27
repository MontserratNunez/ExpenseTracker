using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class ThemeTests
{
    private const string ThemeKey = "et.theme";

    private readonly AppFixture _app;

    public ThemeTests(AppFixture app) => _app = app;

    private static ILocator Dialog(IPage page, string name) => AccessibilityTests.Dialog(page, name);

    private static Task<string> ThemeAsync(IPage page) => page.EvaluateAsync<string>("() => document.documentElement.dataset.theme");

    private static Task<string> BackgroundAsync(IPage page) => page.EvaluateAsync<string>("() => getComputedStyle(document.body).backgroundColor");

    private static Task<string?> StoredAsync(IPage page) => page.EvaluateAsync<string?>($"() => localStorage.getItem('{ThemeKey}')");

    [E2EFact]
    public async Task Settings_OffersLightDarkAndSystem_ChangesTheThemeAtOnce_AndRemembersIt()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Light });

        await tab.GotoAsync("/configuracion");
        var group = page.GetByRole(AriaRole.Radiogroup, new PageGetByRoleOptions { Name = "Tema" });
        await Expect(group.GetByRole(AriaRole.Radio)).ToHaveCountAsync(3);
        await Expect(group.GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "Sistema" })).ToBeCheckedAsync();   // nothing saved yet
        foreach (var icon in new[] { "bi-sun", "bi-moon", "bi-circle-half" })
            await Expect(group.Locator("." + icon)).ToHaveCountAsync(1);
        Assert.Equal("light", await ThemeAsync(page));
        Assert.Equal("rgb(255, 255, 255)", await BackgroundAsync(page));

        // Dark: the page changes without reloading and the choice is saved.
        var dark = group.GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "Oscuro" });
        await Tab.ClickUntilAsync(page.Locator(".theme-picker__option").Nth(1), page.Locator("html[data-theme=dark]"));
        await Expect(dark).ToBeCheckedAsync();
        Assert.Equal("dark", await ThemeAsync(page));
        Assert.Equal("rgb(15, 23, 41)", await BackgroundAsync(page));
        Assert.Equal("dark", await StoredAsync(page));
        await tab.ShotAsync("tema-configuracion-oscuro", fullPage: true);

        // It survives a reload and applies before the page is painted: the script is the first thing of the head.
        await tab.GotoAsync("/configuracion");
        Assert.Equal("dark", await ThemeAsync(page));
        await Expect(group.GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "Oscuro" })).ToBeCheckedAsync();
        var html = await (await page.APIRequest.GetAsync(_app.BaseUrl + "/Account/Login")).TextAsync();
        var script = html.IndexOf(ThemeKey, StringComparison.Ordinal);
        var firstStylesheet = html.IndexOf("rel=\"stylesheet\"", StringComparison.Ordinal);
        Assert.True(script >= 0 && script < firstStylesheet, "El script del tema debe ir antes de las hojas de estilo para que no haya parpadeo blanco");

        // Light on a dark system: the choice wins over the system.
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Dark });
        await Tab.ClickUntilAsync(page.Locator(".theme-picker__option").Nth(0), page.Locator("html[data-theme=light]"));
        Assert.Equal("light", await ThemeAsync(page));
        Assert.Equal("light", await StoredAsync(page));

        // System: follows the device, and follows it live when it changes.
        await Tab.ClickUntilAsync(page.Locator(".theme-picker__option").Nth(2), page.Locator("html[data-theme=dark]"));
        Assert.Null(await StoredAsync(page));
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Light });
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "light");
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Dark });
        await Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "dark");

        // Native controls and scrollbars follow too.
        Assert.Equal("dark", await page.EvaluateAsync<string>("() => getComputedStyle(document.documentElement).colorScheme"));
        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task TheTheme_SurvivesNavigatingBetweenTheAccountPages_InEverySetting()
    {
        // Moving between the account pages is an enhanced navigation: the server sends a new <html> without the
        // theme attribute, and the page must not fall back to the light theme until the next reload.
        foreach (var (saved, system, expected) in new[]
        {
            ((string?)null, ColorScheme.Dark, "dark"),   // "System" on a dark device
            ("dark", ColorScheme.Light, "dark"),
            ("light", ColorScheme.Dark, "light"),
        })
        {
            await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 800);
            var page = tab.Page;
            await page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = system });
            if (saved is not null)
                await page.AddInitScriptAsync($"localStorage.setItem('{ThemeKey}', '{saved}')");
            await tab.RegisterAsync();

            await tab.GotoAsync("/Account/Manage");
            Assert.Equal(expected, await ThemeAsync(page));

            var submenu = page.GetByRole(AriaRole.Navigation, new PageGetByRoleOptions { Name = "Secciones de la cuenta" });
            foreach (var (link, heading) in new[]
            {
                ("Correo electrónico", "Correo electrónico"), ("Contraseña", "Cambiar contraseña"),
                ("Verificación en dos pasos", "Verificación en dos pasos (2FA)"), ("Passkeys", "Passkeys"),
                ("Datos personales", "Datos personales"), ("Perfil", "Perfil"),
            })
            {
                await submenu.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = link, Exact = true }).ClickAsync();
                await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = heading, Level = 2 })).ToBeVisibleAsync();
                await page.WaitForTimeoutAsync(300);
                Assert.True(expected == await ThemeAsync(page), $"[{saved ?? "sistema"}] al abrir '{link}' el tema pasó a '{await ThemeAsync(page)}' y debía seguir en '{expected}'");
                Assert.Equal(expected == "dark" ? "rgb(15, 23, 41)" : "rgb(255, 255, 255)", await BackgroundAsync(page));
            }
        }
    }

    [E2EFact]
    public async Task TheCollapsedSidebar_SurvivesNavigatingBetweenTheAccountPages()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 800);
        var page = tab.Page;
        await page.AddInitScriptAsync("localStorage.setItem('et.sidebar', 'collapsed')");
        await tab.RegisterAsync();

        await tab.GotoAsync("/Account/Manage");
        Assert.Equal("collapsed", await page.EvaluateAsync<string>("() => document.documentElement.dataset.sidebar"));
        var submenu = page.GetByRole(AriaRole.Navigation, new PageGetByRoleOptions { Name = "Secciones de la cuenta" });
        await submenu.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Contraseña", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Cambiar contraseña", Level = 2 })).ToBeVisibleAsync();
        await page.WaitForTimeoutAsync(300);
        Assert.Equal("collapsed", await page.EvaluateAsync<string>("() => document.documentElement.dataset.sidebar"));
    }

    [E2EFact]
    public async Task TheLoginPage_FollowsTheSystemTheme_WithoutAnySavedChoice()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 800);
        var page = tab.Page;

        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Dark });
        await tab.GotoAsync("/Account/Login");
        Assert.Equal("dark", await ThemeAsync(page));
        await tab.ShotAsync("tema-login-oscuro");

        // The brand panel keeps its blue in both themes.
        var hero = await page.Locator(".account-shell__hero").EvaluateAsync<string>("e => getComputedStyle(e).backgroundImage");
        Assert.Contains("rgb(47, 111, 235)", hero);
        Assert.Contains("rgb(28, 71, 160)", hero);

        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Light });
        await tab.GotoAsync("/Account/Login");
        Assert.Equal("light", await ThemeAsync(page));
    }

    [E2EFact]
    public async Task DarkMode_HasNoContrastOrOtherAccessibilityViolations_OnEveryScreen_AndInTheOverlays()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        var page = tab.Page;
        await page.AddInitScriptAsync($"localStorage.setItem('{ThemeKey}', 'dark')");
        await tab.RegisterAsync();
        await AccessibilityTests.SeedAsync(tab);

        var problems = new List<string>();
        async Task Check(string where)
        {
            Assert.Equal("dark", await ThemeAsync(page));
            try
            {
                await AccessibilityTests.AssertNoViolationsAsync(page, "[oscuro] " + where);
            }
            catch (Xunit.Sdk.XunitException ex)
            {
                problems.Add(ex.Message);
            }
        }

        var paths = new[]
        {
            "/", "/gastos", "/ingresos", "/deudas", "/plan", "/metas", "/estimaciones", "/configuracion",
            "/Account/Manage", "/Account/Manage/Email", "/Account/Manage/ChangePassword", "/Account/Manage/PersonalData", "/dev/ui"
        };
        foreach (var path in paths)
        {
            await tab.GotoAsync(path);
            await Expect(page.Locator("h1").First).ToBeVisibleAsync();
            await Check(path);
            if (path is "/" or "/gastos" or "/plan" or "/metas" or "/estimaciones")
                await tab.ShotAsync("tema-oscuro-" + (path == "/" ? "inicio" : path.Trim('/')), fullPage: true);
        }

        // The debt with its amortization table, and the overlays.
        await tab.GotoAsync("/deudas");
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Préstamo carro" }).First.ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/deudas/\d+"));
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Ver tabla de amortización" }), page.Locator(".amort__table"));
        await Check("detalle de deuda");

        await tab.GotoAsync("/gastos");
        var panel = Dialog(page, "Nuevo gasto");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nuevo gasto" }).First, panel);
        await panel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(panel.GetByText("El nombre es obligatorio.")).ToBeVisibleAsync();
        await Check("panel con errores");
        await tab.ShotAsync("tema-oscuro-panel", panel);
        await page.Keyboard.PressAsync("Escape");

        var calendar = page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Elegir mes" });
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Elegir mes y año" }), calendar);
        await Check("calendario");
        await page.Keyboard.PressAsync("Escape");

        var palette = Dialog(page, "Buscar o ir a...");
        for (var attempt = 0; attempt < 6 && !await palette.IsVisibleAsync(); attempt++)
        {
            await page.Keyboard.PressAsync("Control+K");
            try
            {
                await palette.WaitForAsync(new LocatorWaitForOptions { Timeout = 3500 });
            }
            catch (TimeoutException)
            {
            }
        }
        await Check("paleta de comandos");

        Assert.True(problems.Count == 0, string.Join("\n\n", problems));
    }

    [E2EFact]
    public async Task DarkMode_RecolorsTheCharts_AndKeepsTheStatesReadable()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        var page = tab.Page;
        await page.AddInitScriptAsync($"localStorage.setItem('{ThemeKey}', 'dark')");
        await tab.RegisterAsync();
        await AccessibilityTests.SeedAsync(tab);

        await tab.GotoAsync("/");
        await Expect(page.Locator(".donut__slice").First).ToBeAttachedAsync();
        // The donut uses the dark theme's green and red, not the light ones.
        string[] strokes = Array.Empty<string>();
        for (var attempt = 0; attempt < 25 && strokes.Length < 3; attempt++)   // the page is swapped for the interactive one a moment after it appears
        {
            await page.WaitForTimeoutAsync(200);
            strokes = await page.Locator(".donut__slice").EvaluateAllAsync<string[]>("els => els.map(e => getComputedStyle(e).stroke)");
        }

        Assert.Contains("rgb(61, 214, 140)", strokes);    // --color-available in dark
        Assert.Contains("rgb(255, 99, 105)", strokes);    // --color-spent in dark
        Assert.DoesNotContain("rgb(34, 160, 107)", strokes);

        // The cards are dark surfaces with light text.
        var card = await page.Locator(".stat-card").First.EvaluateAsync<string[]>("e => { const s = getComputedStyle(e); return [s.backgroundColor, s.color]; }");
        Assert.Equal("rgb(15, 23, 41)", card[0]);
        Assert.Equal("rgb(229, 233, 240)", card[1]);

        await tab.GotoAsync("/estimaciones");
        await Expect(page.Locator(".bar-chart__bar rect").First).ToHaveCSSAsync("fill", "rgb(76, 141, 255)");   // the blue accent of the dark theme
    }
}
