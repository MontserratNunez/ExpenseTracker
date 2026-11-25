using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class HomeTests
{
    private readonly AppFixture _app;

    public HomeTests(AppFixture app) => _app = app;

    private static ILocator Dialog(IPage page, string name) => page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    private static ILocator Field(ILocator dialog, string label) => dialog.GetByLabel(label, new LocatorGetByLabelOptions { Exact = true });

    private static async Task AddIncomeAsync(Tab tab, string name, string amount)
    {
        var page = tab.Page;
        await tab.GotoAsync("/ingresos");
        var income = Dialog(page, "Nueva fuente de ingreso");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva fuente" }).First, income);
        await Field(income, "Nombre").FillAsync(name);
        await Field(income, "Monto").FillAsync(amount);
        await Field(income, "Monto").PressAsync("Tab");
        await income.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(page.GetByText("Fuente de ingreso creada.")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task NewUser_SeesTheGreeting_AndTheEmptyStateWithThreeSteps()
    {
        await using var tab = await Tab.OpenAsync(_app);
        var email = await tab.RegisterAsync();
        var page = tab.Page;

        var name = email.Split('@')[0];
        var greeting = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 });
        await Expect(greeting).ToContainTextAsync(", " + name);
        await Expect(greeting).ToContainTextAsync(new System.Text.RegularExpressions.Regex("^(Buenos días|Buenas tardes|Buenas noches)"));

        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Empecemos" })).ToBeVisibleAsync();
        var steps = page.Locator(".start-steps li");
        await Expect(steps).ToHaveCountAsync(3);
        await Expect(steps.Nth(0)).ToContainTextAsync("Configura tus ingresos");
        await Expect(steps.Nth(1)).ToContainTextAsync("Registra tus gastos");
        await Expect(steps.Nth(2)).ToContainTextAsync("Arma tu plan");
        await Expect(page.Locator(".donut")).ToHaveCountAsync(0);
        await tab.ShotAsync("inicio-vacio");

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task WithIncomeAndExpenses_ShowsTheDonut_TheThreeCards_AndTheSections()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        await AddIncomeAsync(tab, "Sueldo trabajo", "50000");

        await tab.GotoAsync("/gastos");
        var form = Dialog(page, "Nuevo gasto");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nuevo gasto" }).First, form);
        await Field(form, "Nombre").FillAsync("Supermercado");
        await Field(form, "Monto").FillAsync("4885.40");
        await Field(form, "Monto").PressAsync("Tab");
        await Field(form, "Categoría").SelectOptionAsync(new SelectOptionValue { Label = "Comida" });
        await form.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(page.GetByText("Gasto guardado.")).ToBeVisibleAsync();

        await tab.GotoAsync("/");
        await page.EvaluateAsync("async () => { await document.fonts.ready; }");

        // Donut: 220 px, the available amount in the center and a text alternative.
        var donut = page.Locator(".donut__chart");
        await Expect(donut).ToBeVisibleAsync();
        await Expect(donut).ToHaveCSSAsync("width", "220px");   // retries if the server-rendered page is swapped for the interactive one
        await Expect(page.Locator(".donut__center")).ToContainTextAsync("Disponible");
        await Expect(page.Locator("svg.donut__svg")).ToHaveAttributeAsync("role", "img");
        await Expect(page.Locator("svg.donut__svg")).ToHaveAttributeAsync("aria-label", new System.Text.RegularExpressions.Regex("Gastado RD\\$ 4,885.40"));
        await Expect(page.Locator(".donut__slice title").First).ToBeAttachedAsync();

        // Three cards next to it, with their financial meaning.
        var cards = page.Locator(".home-summary__cards .stat-card");
        await Expect(cards).ToHaveCountAsync(3);
        await Expect(cards.Nth(0)).ToContainTextAsync("Disponible");
        await Expect(cards.Nth(1)).ToContainTextAsync("Gastado");
        await Expect(cards.Nth(1)).ToContainTextAsync("RD$ 4,885.40");
        await Expect(cards.Nth(2)).ToContainTextAsync("Comprometido");

        // Sections with their "Ver todo" links.
        foreach (var title in new[] { "Próximos pagos", "Plan", "Metas" })
            await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Ver todo" })).ToHaveCountAsync(3);

        await tab.ShotAsync("inicio-con-datos", fullPage: true);

        // Narrow screen: the cards go under the chart and nothing scrolls sideways.
        await page.SetViewportSizeAsync(360, 800);
        var chartBox = await donut.BoundingBoxAsync();
        var cardBox = await cards.First.BoundingBoxAsync();
        Assert.True(cardBox!.Y > chartBox!.Y + chartBox.Height - 1, "Las tarjetas deben quedar debajo del gráfico en móvil");
        var overflow = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        var wide = await page.EvaluateAsync<string>("() => [...document.querySelectorAll('body *')].filter(e => e.getBoundingClientRect().right > document.documentElement.clientWidth + 1).slice(0, 6).map(e => e.tagName + '.' + e.className + ' ' + Math.round(e.getBoundingClientRect().right)).join(' | ')");
        Assert.False(overflow, "La página se desborda en 360 px: " + wide);
        await tab.ShotAsync("inicio-movil", fullPage: true);

        Assert.Empty(tab.ConsoleErrors);
        Assert.Empty(tab.FailedRequests);
    }

    [E2EFact]
    public async Task Donut_Animates_OnlyOnTheFirstLoad_AndNotWithReducedMotion()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();
        var page = tab.Page;

        await AddIncomeAsync(tab, "Sueldo", "30000");

        // Client-side navigation keeps the same circuit: the first visit animates, the second one does not.
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Inicio", Exact = true }).First.ClickAsync();
        await Expect(page.Locator(".donut__slice--animate").First).ToBeAttachedAsync();

        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Ingresos", Exact = true }).First.ClickAsync();
        await page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Inicio", Exact = true }).First.ClickAsync();
        await Expect(page.Locator(".donut__track")).ToBeAttachedAsync();
        await Expect(page.Locator(".donut__slice--animate")).ToHaveCountAsync(0);

        // With reduced motion the animation is off.
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
        await tab.GotoAsync("/");
        await Expect(page.Locator(".donut__slice--animate").First).ToBeAttachedAsync();   // a new circuit animates again, unless motion is reduced
        await Expect(page.Locator(".donut__slice--animate").First).ToHaveCSSAsync("animation-name", "none");
    }
}
