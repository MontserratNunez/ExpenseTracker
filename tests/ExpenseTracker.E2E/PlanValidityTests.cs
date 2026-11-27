using System.Globalization;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

/// <summary>A plan only rules the months it was in force (phase 15). Dates follow the real clock, like the app.</summary>
[Collection(E2ECollection.Name)]
public class PlanValidityTests
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-DO");

    private readonly AppFixture _app;

    public PlanValidityTests(AppFixture app) => _app = app;

    private static ILocator Dialog(IPage page, string name) => page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    private static ILocator Button(IPage page, string name)
        => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = true });

    private static ILocator Button(ILocator scope, string name)
        => scope.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = name, Exact = true });

    private static ILocator Field(ILocator scope, string label)
        => scope.GetByLabel(label, new LocatorGetByLabelOptions { Exact = true });

    private static string ShortMonth(DateTime date) => $"{date.ToString("MMMM", Culture)[..3]} {date.Year}";

    private static string LongMonth(DateTime date) => $"{date.ToString("MMMM", Culture)} {date.Year}";

    [E2EFact]
    public async Task Plans_RuleOnlyTheirMonths_AndAreCreatedFinishedAndDeleted()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        var now = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var badge = page.Locator(".plan-toolbar__plan .badge-app");
        var timeline = page.Locator(".plan-timeline__row");

        // The first plan starts this month and goes straight to its categories.
        var editor = await tab.OpenPlanEditorAsync();
        await editor.GetByLabel("Valor de Comida").FillAsync("10000");
        await Button(editor, "Guardar plan").ClickAsync();
        await Expect(editor).ToBeHiddenAsync();
        await Expect(badge).ToContainTextAsync($"desde {ShortMonth(now)}");
        await Expect(timeline).ToHaveCountAsync(1);

        // The month before is not evaluated with it, and cannot be edited.
        await Button(page, "Mes anterior").ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = $"No tenías un plan en {LongMonth(now.AddMonths(-1))}" })).ToBeVisibleAsync();
        await Expect(badge).ToContainTextAsync("Sin plan");
        await Expect(Button(page, "Editar plan")).ToHaveCountAsync(0);
        await Expect(page.Locator(".plan-row")).ToHaveCountAsync(0);
        await tab.ShotAsync("plan-mes-sin-plan", fullPage: true);
        await Button(page, "Mes siguiente").ClickAsync();
        await Expect(page.Locator(".plan-row")).ToHaveCountAsync(1);

        // A plan from next January closes the current one in December, and says so before saving.
        var january = new DateTime(now.Year + 1, 1, 1);
        var form = Dialog(page, "Nuevo plan");
        await Tab.ClickUntilAsync(Button(page, "Nuevo plan"), form);
        await Field(form, "Nombre").FillAsync("Plan siguiente");
        await Field(form, "Inicio").SelectOptionAsync("FromMonth");
        await Field(form, "Mes de inicio: año").SelectOptionAsync(january.Year.ToString());
        await Field(form, "Mes de inicio: mes").SelectOptionAsync("1");
        await Field(form, "Copiar asignaciones de…").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Expect(form).ToContainTextAsync($"terminará en {LongMonth(january.AddMonths(-1))}");
        await tab.ShotAsync("plan-nuevo", form);
        await Button(form, "Crear plan").ClickAsync();
        await Expect(form).ToBeHiddenAsync();

        // The page shows the month where it starts, with the copied allocation.
        await Expect(badge).ToContainTextAsync($"Plan siguiente · desde {ShortMonth(january)}");
        await Expect(page.Locator(".plan-row")).ToContainTextAsync("RD$ 10,000.00");
        await Expect(timeline).ToHaveCountAsync(2);
        await tab.ShotAsync("plan-linea-de-tiempo", fullPage: true);

        // Overlapping a future plan blocks the save and offers to trim the new one.
        await Tab.ClickUntilAsync(Button(page, "Nuevo plan"), form);
        await Field(form, "Inicio").SelectOptionAsync("ThisMonth");
        await Expect(form).ToContainTextAsync("Se cruza con");
        await Expect(Button(form, "Crear plan")).ToBeDisabledAsync();
        await page.Keyboard.PressAsync("Escape");
        await Expect(form).ToBeHiddenAsync();

        // A future plan can be deleted.
        await Button(page, "Eliminar").ClickAsync();
        await Button(Dialog(page, "Eliminar plan"), "Eliminar").ClickAsync();
        await Expect(timeline).ToHaveCountAsync(1);

        // The timeline goes back to the plan that remains, which now ends in December.
        await timeline.First.ClickAsync();
        await Expect(badge).ToContainTextAsync($"a {ShortMonth(january.AddMonths(-1))}");

        // The running plan is finished, not deleted.
        await Expect(Button(page, "Eliminar")).ToHaveCountAsync(0);
        await Button(page, "Finalizar").ClickAsync();
        var finish = Dialog(page, "Finalizar plan");
        await Field(finish, "Último mes: año").SelectOptionAsync(now.Year.ToString());
        await Field(finish, "Último mes: mes").SelectOptionAsync(now.Month.ToString());
        await Button(finish, "Finalizar").ClickAsync();
        await Expect(badge).ToContainTextAsync($"solo {ShortMonth(now)}");

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task PlanPage_WithTimeline_FitsAPhone()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 360, height: 800);
        await tab.RegisterAsync();
        var page = tab.Page;

        var editor = await tab.OpenPlanEditorAsync();
        await editor.GetByLabel("Valor de Comida").FillAsync("10000");
        await Button(editor, "Guardar plan").ClickAsync();
        await Expect(editor).ToBeHiddenAsync();
        await Expect(page.Locator(".plan-timeline")).ToBeVisibleAsync();

        var overflows = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        Assert.False(overflows, "/plan se desborda en 360 px");
        await tab.ShotAsync("plan-vigencia-movil", fullPage: true);
        Assert.Empty(tab.ConsoleErrors);
    }
}
