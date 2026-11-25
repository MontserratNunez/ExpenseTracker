using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class KeyboardAndMotionTests
{
    private readonly AppFixture _app;

    public KeyboardAndMotionTests(AppFixture app) => _app = app;

    private static ILocator Dialog(IPage page, string name) => page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    private static async Task PressUntilAsync(IPage page, string key, ILocator expected, int attempts = 12)
    {
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            await page.Locator("body").PressAsync(key);
            try
            {
                await expected.WaitForAsync(new LocatorWaitForOptions { Timeout = 3500 });
                return;
            }
            catch (TimeoutException) when (attempt < attempts)
            {
            }
        }
    }

    // What the user sees as "focus": a ring (box-shadow or outline) on the element, or on the visible face of a hidden radio.
    private const string FocusRingScript = @"() => {
        const el = document.activeElement;
        if (!el || el === document.body) return 'none';
        const target = (el.matches('input[type=radio], input[type=checkbox]') && getComputedStyle(el).opacity === '0')
            ? (el.nextElementSibling || el.parentElement) : el;
        const s = getComputedStyle(target);
        const ring = (s.boxShadow && s.boxShadow !== 'none') || (s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) > 0)
            || (s.borderColor !== '' && el.matches('.field, .field-group__control, .quick-add__input'));
        const parentRing = el.closest('.quick-add, .field-group, .palette__search');
        const parentStyle = parentRing ? getComputedStyle(parentRing) : null;
        const ok = ring || (parentStyle && (parentStyle.boxShadow !== 'none' || parentStyle.borderBottomColor !== parentStyle.borderTopColor));
        return ok ? 'ok' : (el.tagName + '.' + el.className + ' ' + (el.getAttribute('aria-label') || el.textContent || '').trim().slice(0, 30));
    }";

    [E2EFact]
    public async Task EveryStopOfTheKeyboardTour_ShowsAVisibleFocus()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        var problems = new List<string>();
        foreach (var path in new[] { "/", "/gastos", "/plan", "/configuracion", "/Account/Manage" })
        {
            await tab.GotoAsync(path);
            await Expect(page.Locator("h1").First).ToBeVisibleAsync();
            await page.WaitForTimeoutAsync(400);
            await page.Locator("body").ClickAsync(new LocatorClickOptions { Position = new Position { X = 2, Y = 2 } });

            var seen = new HashSet<string>();
            for (var step = 0; step < 40; step++)
            {
                await page.Keyboard.PressAsync("Tab");
                var signature = await page.EvaluateAsync<string>("() => { const e = document.activeElement; return e ? e.tagName + '|' + e.className + '|' + (e.getAttribute('aria-label') || e.textContent || '').trim().slice(0, 40) : ''; }");
                if (signature.StartsWith("BODY|") || !seen.Add(signature))
                    break;   // the tour left the page or came back to where it started

                var result = await page.EvaluateAsync<string>(FocusRingScript);
                if (result != "ok")
                    problems.Add($"{path}: sin foco visible en {result}");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems.Distinct()));
    }

    [E2EFact]
    public async Task TheMainFlows_CanBeDoneWithTheKeyboardAlone()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await tab.GotoAsync("/gastos");
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Colapsar barra lateral" })).ToBeVisibleAsync();

        // Register an expense: N, name, amount, category, Enter. No mouse.
        var panel = Dialog(page, "Nuevo gasto");
        await PressUntilAsync(page, "n", panel);
        await page.Keyboard.TypeAsync("Cena");
        await page.Keyboard.PressAsync("Tab");   // currency
        await page.Keyboard.PressAsync("Tab");   // amount
        await page.Keyboard.TypeAsync("1500");
        await page.Keyboard.PressAsync("Tab");   // category
        await page.Keyboard.TypeAsync("Comida"); // a closed select picks the option that starts with what is typed
        await page.Keyboard.PressAsync("Shift+Tab");
        await page.Keyboard.PressAsync("Enter");
        await Expect(panel).ToBeHiddenAsync();
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Cena" })).ToContainTextAsync("1,500.00");

        // Change the month with the arrows and come back.
        await page.Locator("body").ClickAsync(new LocatorClickOptions { Position = new Position { X = 2, Y = 2 } });
        var month = page.Locator(".month-nav__label");
        var current = await month.InnerTextAsync();
        await page.Keyboard.PressAsync("ArrowLeft");
        await Expect(month).Not.ToHaveTextAsync(current);
        await page.Keyboard.PressAsync("ArrowRight");
        await Expect(month).ToHaveTextAsync(current);

        // Open the row menu with the keyboard, then see the plan through the palette.
        var row = page.Locator(".exp-row").First;
        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Más acciones" }).FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        var menu = page.GetByRole(AriaRole.Menu);
        await Expect(menu).ToBeVisibleAsync();
        await Expect(menu.GetByRole(AriaRole.Menuitem).First).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Escape");
        await Expect(menu).ToBeHiddenAsync();
        await Expect(row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Más acciones" })).ToBeFocusedAsync();

        var palette = Dialog(page, "Buscar o ir a...");
        await page.Keyboard.PressAsync("Control+K");
        await Expect(palette).ToBeVisibleAsync();
        await page.Keyboard.TypeAsync("plan");
        await Expect(palette.GetByRole(AriaRole.Option, new LocatorGetByRoleOptions { Name = "Plan" })).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Enter");
        await Expect(page).ToHaveURLAsync(new Regex("/plan$"));
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Plan", Level = 1 })).ToBeVisibleAsync();

        // The skip link is the first stop of the page and takes the focus to the content.
        await tab.GotoAsync("/plan");
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Colapsar barra lateral" })).ToBeVisibleAsync();
        // Blazor puts the focus on the page heading after loading, so the skip link is one Shift+Tab away.
        await page.Keyboard.PressAsync("Shift+Tab");
        var skip = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Saltar al contenido" });
        for (var i = 0; i < 40 && !await skip.EvaluateAsync<bool>("e => e === document.activeElement"); i++)
            await page.Keyboard.PressAsync("Shift+Tab");
        await Expect(skip).ToBeFocusedAsync();
        await Expect(skip).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Enter");
        await Expect(page.Locator("main#main")).ToBeFocusedAsync();

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task WithReducedMotion_NothingAnimatesAnywhere()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });

        // A page with a donut, then the open overlays: a panel, the palette and a toast.
        await tab.GotoAsync("/gastos");
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Colapsar barra lateral" })).ToBeVisibleAsync();
        var panel = Dialog(page, "Nuevo gasto");
        await PressUntilAsync(page, "n", panel);

        // Every running animation and transition lasts (almost) nothing: the page is at its end state at once.
        var longest = await page.EvaluateAsync<double>(@"() => {
            const toSeconds = v => v.split(',').map(x => x.trim()).map(x => x.endsWith('ms') ? parseFloat(x) / 1000 : parseFloat(x));
            let max = 0;
            for (const el of document.querySelectorAll('*')) {
                const s = getComputedStyle(el);
                if (s.animationName !== 'none') max = Math.max(max, ...toSeconds(s.animationDuration));
                if (s.transitionProperty !== 'none') max = Math.max(max, ...toSeconds(s.transitionDuration));
            }
            return max;
        }");
        Assert.True(longest <= 0.001, $"Con movimiento reducido todavía hay una animación o transición de {longest}s");

        // The same page without the preference does animate (the check above is not vacuous).
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.NoPreference });
        var normal = await page.EvaluateAsync<double>(@"() => {
            const toSeconds = v => v.split(',').map(x => x.trim()).map(x => x.endsWith('ms') ? parseFloat(x) / 1000 : parseFloat(x));
            let max = 0;
            for (const el of document.querySelectorAll('*')) {
                const s = getComputedStyle(el);
                if (s.animationName !== 'none') max = Math.max(max, ...toSeconds(s.animationDuration));
                if (s.transitionProperty !== 'none') max = Math.max(max, ...toSeconds(s.transitionDuration));
            }
            return max;
        }");
        Assert.True(normal >= 0.1, "Sin la preferencia debería haber animaciones o transiciones");
    }
}
