using System.Text.RegularExpressions;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

/// <summary>
/// axe-core on every screen, with data and with the overlays open (WCAG 2.1 A and AA). Nothing critical or serious may
/// be reported, and in practice the app has no violation of any kind, which these tests keep that way.
/// </summary>
[Collection(E2ECollection.Name)]
public class AccessibilityTests
{
    private static readonly string[] Tags = { "wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "best-practice" };

    private readonly AppFixture _app;

    public AccessibilityTests(AppFixture app) => _app = app;

    internal static ILocator Dialog(IPage page, string name) => page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    internal static ILocator Field(ILocator dialog, string label) => dialog.GetByLabel(label, new LocatorGetByLabelOptions { Exact = true });

    internal static async Task AssertNoViolationsAsync(IPage page, string where)
    {
        await page.EvaluateAsync("async () => { await document.fonts.ready; }");
        await page.WaitForTimeoutAsync(400);   // entrance animations finish: axe reads the colors it sees

        var result = await page.RunAxe(new AxeRunOptions { RunOnly = new RunOnlyOptions { Type = "tag", Values = Tags.ToList() } });
        var serious = result.Violations.Where(v => v.Impact is "critical" or "serious").ToList();
        Assert.True(serious.Count == 0, $"{where}: problemas críticos o serios de accesibilidad:\n" + Describe(serious));
        Assert.True(result.Violations.Length == 0, $"{where}: problemas de accesibilidad:\n" + Describe(result.Violations));
    }

    private static string Describe(IEnumerable<AxeResultItem> violations)
        => string.Join("\n", violations.Select(v => $"- [{v.Impact}] {v.Id}: {v.Help} ({v.Nodes.Length} elementos)\n    " +
            string.Join("\n    ", v.Nodes.Take(4).Select(n => n.Target + " :: " + (n.Any.FirstOrDefault()?.Message ?? n.Html)))));

    // ---------- seeding ----------

    internal static async Task SeedAsync(Tab tab)
    {
        var page = tab.Page;

        await tab.GotoAsync("/ingresos");
        var income = Dialog(page, "Nueva fuente de ingreso");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva fuente" }).First, income);
        await Field(income, "Nombre").FillAsync("Sueldo trabajo");
        await Field(income, "Monto").FillAsync("50000");
        await Field(income, "Monto").PressAsync("Tab");
        await income.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(income).ToBeHiddenAsync();

        await AddExpenseAsync(tab, "Supermercado", "4885.40", "Comida");
        await AddExpenseAsync(tab, "Ahorro mensual", "2000", "Ahorro");

        await tab.GotoAsync("/plan");
        var plan = Dialog(page, "Editar plan");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Editar plan" }).First, plan);
        await plan.GetByLabel("Valor de Comida").FillAsync("3000");
        await plan.GetByLabel("Valor de Ahorro").FillAsync("5000");
        await plan.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar plan" }).ClickAsync();
        await Expect(plan).ToBeHiddenAsync();

        await tab.GotoAsync("/deudas");
        var debt = Dialog(page, "Nueva deuda");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva deuda" }).First, debt);
        await Field(debt, "Nombre").FillAsync("Préstamo carro");
        await Field(debt, "Monto original").FillAsync("100000");
        await Field(debt, "Monto original").PressAsync("Tab");
        await Field(debt, "Cuota mensual").FillAsync("5000");
        await Field(debt, "Cuota mensual").PressAsync("Tab");
        await debt.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(debt).ToBeHiddenAsync();

        await tab.GotoAsync("/metas");
        var goal = Dialog(page, "Nueva meta");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva meta" }).First, goal);
        await Field(goal, "Nombre").FillAsync("Ahorro de 200,000 anualmente");
        await Field(goal, "Monto objetivo").FillAsync("200000");
        await Field(goal, "Monto objetivo").PressAsync("Tab");
        await goal.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(goal).ToBeHiddenAsync();
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

    // ---------- tests ----------

    [E2EFact]
    public async Task EveryScreen_HasNoAccessibilityViolations_WithData()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await SeedAsync(tab);

        var paths = new[]
        {
            "/", "/gastos", "/ingresos", "/deudas", "/plan", "/metas", "/estimaciones", "/configuracion",
            "/Account/Manage", "/Account/Manage/Email", "/Account/Manage/ChangePassword", "/Account/Manage/TwoFactorAuthentication",
            "/Account/Manage/Passkeys", "/Account/Manage/PersonalData", "/Account/Manage/DeletePersonalData", "/dev/ui", "/no-existe"
        };

        var problems = new List<string>();
        foreach (var path in paths)
        {
            await tab.GotoAsync(path);
            await Expect(page.Locator("h1").First).ToBeVisibleAsync();
            try
            {
                await AssertNoViolationsAsync(page, path);
            }
            catch (Xunit.Sdk.XunitException ex)
            {
                problems.Add(ex.Message);
            }
        }

        await tab.GotoAsync("/deudas");
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Préstamo carro" }).First.ClickAsync();
        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"/deudas/\d+"));
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Ver tabla de amortización" }), page.Locator(".amort__table"));
        try
        {
            await AssertNoViolationsAsync(page, "detalle de deuda con la tabla de amortización");
        }
        catch (Xunit.Sdk.XunitException ex)
        {
            problems.Add(ex.Message);
        }

        Assert.True(problems.Count == 0, string.Join("\n\n", problems));
    }

    [E2EFact]
    public async Task EveryPage_HasALanguage_AndAUniqueTitleEndingInTheAppName()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 800);
        await tab.RegisterAsync();
        var page = tab.Page;

        var titles = new Dictionary<string, string>();
        foreach (var path in new[] { "/", "/gastos", "/ingresos", "/deudas", "/plan", "/metas", "/estimaciones", "/configuracion", "/Account/Manage", "/Account/Manage/Email", "/Account/Manage/ChangePassword", "/Account/Manage/PersonalData", "/no-existe" })
        {
            await tab.GotoAsync(path);
            await Expect(page.Locator("h1").First).ToBeVisibleAsync();
            await Expect(page).ToHaveTitleAsync(new Regex("Expense Tracker$"));
            Assert.Equal("es", await page.EvaluateAsync<string>("() => document.documentElement.lang"));
            // The title is rewritten when the page becomes interactive; read it once it is stable.
            var title = await page.TitleAsync();
            for (var i = 0; i < 25 && !title.EndsWith(" · Expense Tracker"); i++)
            {
                await page.WaitForTimeoutAsync(200);
                title = await page.TitleAsync();
            }

            titles[path] = title;
        }

        Assert.All(titles, t => Assert.EndsWith(" · Expense Tracker", t.Value));
        Assert.Equal(titles.Count, titles.Values.Distinct().Count());
        Assert.Matches(@"^Gastos · [a-z]+ [0-9]{4} · Expense Tracker$", titles["/gastos"]);
        Assert.Matches(@"^Plan · [a-z]+ [0-9]{4} · Expense Tracker$", titles["/plan"]);
    }

    [E2EFact]
    public async Task LoggedOutPages_HaveNoAccessibilityViolations()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 800);
        var page = tab.Page;

        var problems = new List<string>();
        foreach (var path in new[] { "/Account/Login", "/Account/Register", "/Account/ForgotPassword", "/Account/ResendEmailConfirmation", "/Account/Lockout", "/Account/AccessDenied" })
        {
            await tab.GotoAsync(path);
            await Expect(page.Locator("h1").First).ToBeVisibleAsync();
            try
            {
                await AssertNoViolationsAsync(page, path);
            }
            catch (Xunit.Sdk.XunitException ex)
            {
                problems.Add(ex.Message);
            }
        }

        // The login with errors showing.
        await tab.GotoAsync("/Account/Login");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Iniciar sesión", Exact = true }).ClickAsync();
        await Expect(page.GetByText("El campo Correo electrónico es obligatorio.").First).ToBeVisibleAsync();
        try
        {
            await AssertNoViolationsAsync(page, "login con errores");
        }
        catch (Xunit.Sdk.XunitException ex)
        {
            problems.Add(ex.Message);
        }

        Assert.True(problems.Count == 0, string.Join("\n\n", problems));
    }

    [E2EFact]
    public async Task Overlays_HaveNoAccessibilityViolations()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await SeedAsync(tab);
        var problems = new List<string>();

        async Task Check(string where)
        {
            try
            {
                await AssertNoViolationsAsync(page, where);
            }
            catch (Xunit.Sdk.XunitException ex)
            {
                problems.Add(ex.Message);
            }
        }

        // New-expense panel, with its validation errors showing.
        await tab.GotoAsync("/gastos");
        var panel = Dialog(page, "Nuevo gasto");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nuevo gasto" }).First, panel);
        await panel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(panel.GetByText("El nombre es obligatorio.")).ToBeVisibleAsync();
        await Check("panel de nuevo gasto con errores");
        await page.Keyboard.PressAsync("Escape");
        await Expect(panel).ToBeHiddenAsync();

        // Calendar popover and a row menu.
        var calendar = page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Elegir mes" });
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Elegir mes y año" }), calendar);
        await Check("calendario");
        await page.Keyboard.PressAsync("Escape");
        await Expect(calendar).ToBeHiddenAsync();

        var row = page.Locator(".exp-row").First;
        await row.HoverAsync();
        var menu = page.GetByRole(AriaRole.Menu);
        await Tab.ClickUntilAsync(row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Más acciones" }), menu);
        await Check("menú de fila");
        await page.Keyboard.PressAsync("Escape");

        // Command palette (with a search showing) and the shortcuts help.
        var palette = Dialog(page, "Buscar o ir a...");
        for (var attempt = 0; attempt < 6 && !await palette.IsVisibleAsync(); attempt++)
        {
            await page.Keyboard.PressAsync("Control+K");
            try
            {
                await palette.WaitForAsync(new LocatorWaitForOptions { Timeout = 3500 });   // Ctrl+K toggles: do not press again too soon
            }
            catch (TimeoutException)
            {
            }
        }
        await Expect(palette.GetByRole(AriaRole.Combobox)).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync("super");
        await Expect(palette.GetByRole(AriaRole.Option, new LocatorGetByRoleOptions { Name = "Supermercado" })).ToBeVisibleAsync();
        await Check("paleta de comandos");
        await page.Keyboard.PressAsync("Escape");
        await Expect(palette).ToBeHiddenAsync();

        var help = Dialog(page, "Atajos de teclado");
        await page.Locator("body").ClickAsync(new LocatorClickOptions { Position = new Position { X = 5, Y = 500 } });
        for (var attempt = 0; attempt < 6 && !await help.IsVisibleAsync(); attempt++)
        {
            await page.Keyboard.PressAsync("?");
            try
            {
                await help.WaitForAsync(new LocatorWaitForOptions { Timeout = 3500 });
            }
            catch (TimeoutException)
            {
            }
        }
        await Check("ayuda de atajos");
        await page.Keyboard.PressAsync("Escape");

        // Plan: the inline edit and the plan editor.
        await tab.GotoAsync("/plan");
        var edit = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Editar lo asignado a Comida" });
        var input = page.GetByLabel("Monto asignado a Comida");
        await Tab.ClickUntilAsync(edit, input);
        await Check("plan con edición en línea");
        await input.PressAsync("Escape");

        // Goals: the form with the icon picker; Settings: the category editor with the color and icon pickers.
        await tab.GotoAsync("/metas");
        var goal = Dialog(page, "Nueva meta");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva meta" }).First, goal);
        await Check("formulario de meta");
        await page.Keyboard.PressAsync("Escape");

        await tab.GotoAsync("/configuracion");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva categoría" }), page.Locator("form.settings-editor"));
        await Check("editor de categoría");

        Assert.True(problems.Count == 0, string.Join("\n\n", problems));
    }
}
