using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class IncomesGoalsForecastTests
{
    private readonly AppFixture _app;

    public IncomesGoalsForecastTests(AppFixture app) => _app = app;

    private static ILocator Dialog(IPage page, string name) => page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    private static ILocator Field(ILocator dialog, string label) => dialog.GetByLabel(label, new LocatorGetByLabelOptions { Exact = true });

    private static async Task AddIncomeSourceAsync(Tab tab, string name, string amount)
    {
        var page = tab.Page;
        await tab.GotoAsync("/ingresos");
        var dialog = Dialog(page, "Nueva fuente de ingreso");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva fuente" }).First, dialog);
        await Field(dialog, "Nombre").FillAsync(name);
        await Field(dialog, "Monto").FillAsync(amount);
        await Field(dialog, "Monto").PressAsync("Tab");
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();
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

    // ================= Ingresos =================

    [E2EFact]
    public async Task Incomes_ShowSourcesAsCards_WithTheMonthlyTotalOnTop_AndOneTimeIncomesGroupedByDay()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        await AddIncomeSourceAsync(tab, "Sueldo trabajo", "50000");
        await AddIncomeSourceAsync(tab, "Sueldo universidad", "10000");

        // The monthly equivalent total is a card on top; every source is a small card with its frequency.
        await Expect(page.Locator(".income-total .stat-card")).ToContainTextAsync("RD$ 60,000.00");
        var cards = page.Locator(".income-card");
        await Expect(cards).ToHaveCountAsync(2);
        var job = cards.Filter(new LocatorFilterOptions { HasText = "Sueldo trabajo" });
        await Expect(job).ToContainTextAsync("RD$ 50,000.00");
        await Expect(job).ToContainTextAsync("Mensual");
        await Expect(job).ToContainTextAsync("Activa");
        await tab.ShotAsync("ingresos-tarjetas", fullPage: true);

        // The three-dots menu edits and deletes.
        var editItem = page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Editar" });
        await Tab.ClickUntilAsync(job.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Acciones de Sueldo trabajo" }), editItem);
        await editItem.ClickAsync();
        await Expect(Dialog(page, "Editar fuente de ingreso")).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Escape");

        // One-time incomes: a real tab, the month navigator and the same grouped list as Gastos.
        var oneTimeTab = page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "Ingresos únicos" });
        await Expect(page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = "Fuentes recurrentes" })).ToHaveAttributeAsync("aria-selected", "true");
        var form = Dialog(page, "Nuevo ingreso único");
        await Tab.ClickUntilAsync(oneTimeTab, page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nuevo ingreso", Exact = true }).First);
        await Expect(oneTimeTab).ToHaveAttributeAsync("aria-selected", "true");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nuevo ingreso", Exact = true }).First, form);
        await Field(form, "Descripción").FillAsync("Bono");
        await Field(form, "Monto").FillAsync("5000");
        await Field(form, "Monto").PressAsync("Tab");
        await form.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(form).ToBeHiddenAsync();

        var today = page.Locator("section.exp-day").Filter(new LocatorFilterOptions { Has = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Hoy", Exact = true }) });
        await Expect(today.Locator(".exp-day__total")).ToHaveTextAsync("RD$ 5,000.00");
        await Expect(today.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Bono" })).ToContainTextAsync("Ingreso #");
        await Expect(page.GetByText("Ingresos de")).ToContainTextAsync("RD$ 65,000.00");
        await tab.ShotAsync("ingresos-unicos-por-dia", fullPage: true);

        // The very same list component serves Gastos.
        await AddExpenseAsync(tab, "Cena", "1500", "Comida");
        await Expect(page.Locator("section.exp-day .exp-day__head h2").First).ToHaveTextAsync("Hoy");

        Assert.Empty(tab.ConsoleErrors);
    }

    // ================= Metas =================

    [E2EFact]
    public async Task Goals_ShowARing_TheChosenIcon_TheState_AndCelebrateOnlyOnce()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        // 2,000 already saved against a goal of 1,000: it is reached from the start.
        await AddExpenseAsync(tab, "Depósito", "2000", "Otros");

        await tab.GotoAsync("/metas");
        var form = Dialog(page, "Nueva meta");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva meta" }).First, form);
        await Field(form, "Nombre").FillAsync("Vacaciones");
        await Field(form, "Monto objetivo").FillAsync("1000");
        await Field(form, "Monto objetivo").PressAsync("Tab");
        await Field(form, "Categoría vinculada").SelectOptionAsync(new SelectOptionValue { Label = "Otros" });

        // The icon picker is a keyboard-friendly radio group: arrows move and select.
        var picker = form.GetByRole(AriaRole.Radiogroup);
        await Expect(picker.GetByRole(AriaRole.Radio)).ToHaveCountAsync(40);
        var airplane = picker.GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "airplane" });
        await airplane.CheckAsync();
        await Expect(airplane).ToBeCheckedAsync();
        await airplane.PressAsync("ArrowRight");
        await Expect(picker.GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "suitcase" })).ToBeCheckedAsync();
        await picker.GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "airplane" }).CheckAsync();
        await tab.ShotAsync("metas-selector-de-icono", form);
        await form.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(form).ToBeHiddenAsync();

        var card = page.Locator("article.goal-card").Filter(new LocatorFilterOptions { HasText = "Vacaciones" });
        await Expect(card).ToBeVisibleAsync();
        await Expect(card.Locator("svg.donut__svg")).ToBeVisibleAsync();
        await Expect(card.Locator(".donut__center .bi-airplane")).ToBeVisibleAsync();
        await Expect(card).ToContainTextAsync("RD$ 2,000.00 de RD$ 1,000.00");
        await Expect(card.Locator(".badge-app")).ToContainTextAsync("Lograda");
        await Expect(card.Locator(".badge-app .bi-trophy")).ToBeVisibleAsync();

        // The celebration plays once and is remembered by the browser.
        await Expect(card.Locator(".confetti i")).ToHaveCountAsync(16);
        await tab.ShotAsync("metas-celebracion", card);
        await Expect(card.Locator(".confetti")).ToHaveCountAsync(0, new() { Timeout = 5000 });

        await tab.GotoAsync("/metas");
        await Expect(card.Locator("svg.donut__svg")).ToBeVisibleAsync();
        await page.WaitForTimeoutAsync(1200);
        await Expect(card.Locator(".confetti")).ToHaveCountAsync(0);

        await tab.ShotAsync("metas-tarjetas", fullPage: true);
        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Goals_InProgress_ShowTheMonthlyNeedWithAnIconState()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        await tab.GotoAsync("/metas");
        var form = Dialog(page, "Nueva meta");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva meta" }).First, form);
        await Field(form, "Nombre").FillAsync("Ahorro de 200,000 anualmente");
        await Field(form, "Monto objetivo").FillAsync("200000");
        await Field(form, "Monto objetivo").PressAsync("Tab");
        await Field(form, "Categoría vinculada").SelectOptionAsync(new SelectOptionValue { Label = "Otros" });
        await form.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();

        var card = page.Locator("article.goal-card");
        await Expect(card).ToContainTextAsync("Necesitas");
        await Expect(card).ToContainTextAsync("/mes");
        await Expect(card.Locator(".confetti")).ToHaveCountAsync(0);
        // Not reached: the state has an icon next to its words.
        await Expect(card.Locator(".badge-app i.bi")).ToHaveCountAsync(1);
        await Expect(card.Locator(".donut__center .bi-piggy-bank")).ToBeVisibleAsync();   // the default icon of a savings goal

        // Edit and delete live in the card menu.
        await card.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { NameRegex = new Regex("^Acciones de") }).ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Editar" }).ClickAsync();
        await Expect(Dialog(page, "Editar meta")).ToBeVisibleAsync();
        await Expect(Dialog(page, "Editar meta").GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "piggy-bank" })).ToBeCheckedAsync();
        Assert.Empty(tab.ConsoleErrors);
    }

    // ================= Estimaciones =================

    [E2EFact]
    public async Task Forecast_ShowsThreeBigCards_AFullYearBarChartWithATextAlternative_AndExpandableDetail()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        await AddIncomeSourceAsync(tab, "Sueldo", "50000");
        await tab.GotoAsync("/estimaciones");

        var cards = page.Locator(".forecast-card");
        await Expect(cards).ToHaveCountAsync(3);
        await Expect(cards.Nth(0)).ToContainTextAsync("Próximo mes");
        await Expect(cards.Nth(1)).ToContainTextAsync("Resto de");
        await Expect(cards.Nth(0)).ToContainTextAsync("Ingreso estimado");
        await Expect(cards.Nth(0)).ToContainTextAsync("RD$ 50,000.00");
        // The balance is a gain here, shown with an arrow and a color.
        await Expect(cards.Nth(0).Locator(".forecast-card__balance--positive .bi-arrow-up-short")).ToBeVisibleAsync();
        await Expect(cards.Nth(0).Locator(".stat-card__amount")).ToBeVisibleAsync();

        // The chart: 12 bars with a tooltip each, the income line, and a hidden table with the same numbers.
        var chart = page.Locator("figure.bar-chart");
        await Expect(chart.Locator("svg")).ToHaveAttributeAsync("role", "img");
        await Expect(chart.Locator(".bar-chart__bar")).ToHaveCountAsync(12);
        await Expect(chart.Locator(".bar-chart__bar title")).ToHaveCountAsync(12);
        await Expect(chart.Locator(".bar-chart__line")).ToHaveCountAsync(1);
        await Expect(chart.Locator("text.bar-chart__label")).ToHaveCountAsync(12);
        await Expect(chart.Locator("text.bar-chart__label").First).ToHaveTextAsync("Ene");
        await Expect(chart.Locator(".sr-only table tbody tr")).ToHaveCountAsync(12);
        await Expect(chart.Locator(".sr-only table tbody tr").First).ToContainTextAsync("RD$ 50,000.00");
        var fill = await chart.Locator(".bar-chart__bar rect").First.EvaluateAsync<string>("e => getComputedStyle(e).fill");
        Assert.NotEqual("rgb(0, 0, 0)", fill);   // the bars use the blue accent

        // The breakdown opens with a chevron that turns.
        var toggle = cards.Nth(1).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Ver desglose" });
        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");
        await Tab.ClickUntilAsync(toggle, cards.Nth(1).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "Recurrentes" }));
        await Expect(cards.Nth(1).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Ocultar desglose" })).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(cards.Nth(1).Locator(".forecast-card__chevron--open")).ToHaveCountAsync(1);
        await Expect(cards.Nth(1).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "Mes a mes" })).ToBeVisibleAsync();
        await tab.ShotAsync("estimaciones-rediseno", fullPage: true);

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task IncomesGoalsAndForecast_FitAPhone()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 360, height: 800);
        await tab.RegisterAsync();
        var page = tab.Page;

        await AddIncomeSourceAsync(tab, "Sueldo con un nombre bastante largo para probar", "50000");

        foreach (var path in new[] { "/ingresos", "/metas", "/estimaciones" })
        {
            await tab.GotoAsync(path);
            await Expect(page.Locator("main h1")).ToBeVisibleAsync();
            await page.WaitForTimeoutAsync(300);
            var overflow = await page.EvaluateAsync<string>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth ? [...document.querySelectorAll('main *')].filter(e => e.getBoundingClientRect().right > document.documentElement.clientWidth + 1).slice(0, 5).map(e => e.tagName + '.' + e.className).join(' | ') : ''");
            Assert.True(overflow == string.Empty, $"{path} se desborda en 360 px: " + overflow);
        }

        await tab.GotoAsync("/estimaciones");
        await Expect(page.Locator("figure.bar-chart svg")).ToBeVisibleAsync();
        await tab.ShotAsync("estimaciones-movil", fullPage: true);
        Assert.Empty(tab.ConsoleErrors);
    }
}
