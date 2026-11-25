using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class ExpensesTests
{
    private readonly AppFixture _app;

    public ExpensesTests(AppFixture app) => _app = app;

    private static ILocator Panel(IPage page, string name) => page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    private static ILocator Field(ILocator dialog, string label) => dialog.GetByLabel(label, new LocatorGetByLabelOptions { Exact = true });

    private static ILocator QuickAdd(IPage page) => page.GetByLabel("Agregar gasto rápido");

    private static ILocator Day(IPage page, string heading) => page.Locator("section.exp-day").Filter(new LocatorFilterOptions { Has = page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = heading, Exact = true }) });

    private static async Task OpenExpensesAsync(Tab tab)
    {
        await tab.GotoAsync("/gastos");
        await Expect(tab.Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Colapsar barra lateral" })).ToBeVisibleAsync();   // the circuit is connected
    }

    /// <summary>Types in the quick-add row and presses Enter until the side panel opens (the first presses can be lost before the circuit is ready).</summary>
    private static async Task QuickAddAsync(IPage page, string text, ILocator panel)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await QuickAdd(page).FillAsync(text);
            await QuickAdd(page).PressAsync("Enter");
            try
            {
                await panel.WaitForAsync(new LocatorWaitForOptions { Timeout = 1500 });
                return;
            }
            catch (TimeoutException)
            {
            }
        }
    }

    private static async Task SaveAsync(ILocator panel, string category, string? date = null)
    {
        await Field(panel, "Categoría").SelectOptionAsync(new SelectOptionValue { Label = category });
        if (date is not null)
            await Field(panel, "Fecha").FillAsync(date);
        await panel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(panel).ToBeHiddenAsync();
    }

    [E2EFact]
    public async Task QuickAdd_SplitsNameAndAmount_OpensThePanelPrefilled_AndRemembersTheCategory()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await OpenExpensesAsync(tab);
        var url = page.Url;

        var panel = Panel(page, "Nuevo gasto");
        await QuickAddAsync(page, "Supermercado 700", panel);
        await Expect(Field(panel, "Nombre")).ToHaveValueAsync("Supermercado");
        await Expect(Field(panel, "Monto")).ToHaveValueAsync("700.00");
        await Expect(Field(panel, "Fecha")).ToHaveValueAsync(DateTime.Today.ToString("yyyy-MM-dd"));
        await Expect(QuickAdd(page)).ToHaveValueAsync("");
        await tab.ShotAsync("gastos-agregar-rapido", panel);
        await SaveAsync(panel, "Comida");

        // Today's group, with the row and the day total.
        var today = Day(page, "Hoy");
        await Expect(today.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Supermercado" })).ToContainTextAsync("RD$ 700.00");
        await Expect(today.Locator(".exp-day__total")).ToHaveTextAsync("RD$ 700.00");

        // The same name again remembers the category used last time.
        await QuickAddAsync(page, "supermercado 4,885.40", panel);
        await Expect(Field(panel, "Monto")).ToHaveValueAsync("4,885.40");
        await Expect(Field(panel, "Categoría").Locator("option:checked")).ToHaveTextAsync("Comida");
        await SaveAsync(panel, "Comida", DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd"));

        // Two days now, the latest first, each with its own total.
        await Expect(Day(page, "Ayer").Locator(".exp-day__total")).ToHaveTextAsync("RD$ 4,885.40");
        await Expect(today.Locator(".exp-day__total")).ToHaveTextAsync("RD$ 700.00");
        var headings = await page.Locator("section.exp-day h2").AllInnerTextsAsync();
        Assert.Equal(new[] { "Hoy", "Ayer" }, headings);

        // Without a recognizable amount the whole text is the name.
        await QuickAddAsync(page, "Regalo de cumpleaños", panel);
        await Expect(Field(panel, "Nombre")).ToHaveValueAsync("Regalo de cumpleaños");
        await Expect(Field(panel, "Monto")).ToHaveValueAsync("");
        await page.Keyboard.PressAsync("Escape");
        await Expect(panel).ToBeHiddenAsync();

        Assert.Equal(url, page.Url);
        await tab.ShotAsync("gastos-agrupados-por-dia", fullPage: true);
        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Calendar_OpensAsAPopover_ShowsIntensity_GoesToPastYears_AndWorksWithTheKeyboard()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await OpenExpensesAsync(tab);

        // One expense this month so that month has a bar and the others do not.
        var panel = Panel(page, "Nuevo gasto");
        await QuickAddAsync(page, "Cena 1500", panel);
        await SaveAsync(panel, "Comida");
        await Expect(page.GetByText("Gasto guardado.")).ToBeVisibleAsync();

        var opener = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Elegir mes y año" });
        var popover = page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Elegir mes" });
        await Tab.ClickUntilAsync(opener, popover);
        await Expect(popover.Locator(".ymp__month")).ToHaveCountAsync(12);

        // Not a modal: the page behind is not locked, and the popover opens under the button.
        var boxes = await page.EvaluateAsync<double[]>(
            "() => { const o = document.querySelector('.popover-anchor > button').getBoundingClientRect(); " +
            "const p = document.querySelector('.popover-app').getBoundingClientRect(); return [o.y, o.height, p.y]; }");
        Assert.True(boxes[2] >= boxes[0] + boxes[1] - 1, "el calendario va debajo del botón");
        Assert.Equal(string.Empty, await page.EvaluateAsync<string>("() => document.body.style.overflow"));

        // The current month has the ring, is selected and has the strongest bar; an empty month has an invisible one.
        var now = DateTime.Today;
        var current = popover.Locator(".ymp__month--current");
        await Expect(current).ToHaveCountAsync(1);
        await Expect(current).ToHaveClassAsync(new Regex("ymp__month--selected"));
        await Expect(current.Locator(".ymp__bar")).ToHaveCSSAsync("opacity", "1");
        var other = popover.Locator(".ymp__month").Nth(now.Month == 1 ? 1 : 0);
        await Expect(other.Locator(".ymp__bar")).ToHaveCSSAsync("opacity", "0");
        await page.WaitForTimeoutAsync(250);
        await tab.ShotAsync("gastos-calendario", popover);

        // Past years: the previous year has no expenses, so every bar is faint, and the next year is not reachable.
        await popover.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Año anterior" }).ClickAsync();
        await Expect(popover.Locator(".ymp__year strong")).ToHaveTextAsync((now.Year - 1).ToString());
        await Expect(popover.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Año siguiente" })).ToBeEnabledAsync();

        // Keyboard: arrows move between months, Enter chooses.
        // Keyboard: arrows move between months (Up goes back one row), Enter chooses.
        var start = now.Month - 1;
        await popover.Locator(".ymp__month[tabindex='0']").FocusAsync();
        await page.Keyboard.PressAsync("ArrowLeft");
        await Expect(popover.Locator(".ymp__month").Nth(Math.Max(0, start - 1))).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("ArrowRight");
        await Expect(popover.Locator(".ymp__month").Nth(start)).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("ArrowUp");
        var target = Math.Max(0, start - 3);
        await Expect(popover.Locator(".ymp__month").Nth(target)).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");

        await page.WaitForURLAsync(new Regex($@"/gastos/{now.Year - 1}/{target + 1}$"));
        await Expect(popover).ToBeHiddenAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Gastos", Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText(new Regex("^No hay gastos en "))).ToBeVisibleAsync();

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task RowMenu_AppearsOnHover_Edits_Duplicates_AndDeletes_WithoutLeavingTheList()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await OpenExpensesAsync(tab);
        var url = page.Url;

        var panel = Panel(page, "Nuevo gasto");
        await QuickAddAsync(page, "Gasolina 2000", panel);
        await SaveAsync(panel, "Transporte");

        var row = page.Locator(".exp-row").Filter(new LocatorFilterOptions { HasText = "Gasolina" });
        var menuButton = row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Más acciones" });
        await page.Mouse.MoveAsync(5, 5);
        await Expect(row.Locator(".exp-row__menu")).ToHaveCSSAsync("opacity", "0");
        await row.HoverAsync();
        await Expect(row.Locator(".exp-row__menu")).ToHaveCSSAsync("opacity", "1");
        await tab.ShotAsync("gastos-menu-de-fila", row);

        // Edit: the panel opens over the list and the page does not change.
        await menuButton.ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Editar" }).ClickAsync();
        var edit = Panel(page, "Editar gasto");
        await Expect(edit).ToBeVisibleAsync();
        await Expect(Field(edit, "Nombre")).ToHaveValueAsync("Gasolina");
        await Field(edit, "Nombre").FillAsync("Gasolina del carro");
        await edit.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(edit).ToBeHiddenAsync();
        await Expect(page.GetByText("Gasto actualizado.")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Gasolina del carro" })).ToContainTextAsync("RD$ 2,000.00");
        Assert.Equal(url, page.Url);

        // Duplicate: a new expense with the same values.
        row = page.Locator(".exp-row").Filter(new LocatorFilterOptions { HasText = "Gasolina del carro" });
        await row.HoverAsync();
        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Más acciones" }).ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Duplicar" }).ClickAsync();
        var copy = Panel(page, "Nuevo gasto");
        await Expect(Field(copy, "Nombre")).ToHaveValueAsync("Gasolina del carro");
        await Expect(Field(copy, "Monto")).ToHaveValueAsync("2,000.00");
        await copy.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(page.Locator(".exp-row").Filter(new LocatorFilterOptions { HasText = "Gasolina del carro" })).ToHaveCountAsync(2);
        await Expect(Day(page, "Hoy").Locator(".exp-day__total")).ToHaveTextAsync("RD$ 4,000.00");

        // Delete from the menu: confirmation, toast with undo.
        row = page.Locator(".exp-row").Filter(new LocatorFilterOptions { HasText = "Gasolina del carro" }).First;
        await row.HoverAsync();
        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Más acciones" }).ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Eliminar" }).ClickAsync();
        await Panel(page, "Eliminar gasto").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Eliminar" }).ClickAsync();
        await Expect(page.GetByText("Gasto eliminado.")).ToBeVisibleAsync();
        await Expect(page.Locator(".exp-row").Filter(new LocatorFilterOptions { HasText = "Gasolina del carro" })).ToHaveCountAsync(1);

        Assert.Equal(url, page.Url);
        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Filters_NarrowTheList_AndAreClearable_AndThePhoneLayoutFits()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 360, height: 800);
        await tab.RegisterAsync();
        var page = tab.Page;
        await tab.GotoAsync("/gastos");
        await Expect(page.GetByLabel("Agregar gasto rápido")).ToBeVisibleAsync();

        var panel = Panel(page, "Nuevo gasto");
        await QuickAddAsync(page, "Arroz 300", panel);
        await SaveAsync(panel, "Comida");
        await QuickAddAsync(page, "Cine 400", panel);
        await SaveAsync(panel, "Entretenimiento");
        await Expect(page.Locator(".exp-row")).ToHaveCountAsync(2);

        var categoryFilter = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Filtrar por categoría" });
        await categoryFilter.ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Comida" }).ClickAsync();
        await Expect(page.Locator(".exp-row")).ToHaveCountAsync(1);
        await Expect(page.Locator(".exp-row")).ToContainTextAsync("Arroz");
        await Expect(categoryFilter).ToContainTextAsync("Comida");

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Quitar filtros" }).ClickAsync();
        await Expect(page.Locator(".exp-row")).ToHaveCountAsync(2);

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Filtrar por tipo" }).ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Recurrentes" }).ClickAsync();
        await Expect(page.Locator(".exp-row")).ToHaveCountAsync(0);
        await Expect(page.GetByText("Ningún gasto coincide con los filtros.")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Quitar filtros" }).ClickAsync();

        var overflow = await page.EvaluateAsync<string>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth ? [...document.querySelectorAll('main *')].filter(e => e.getBoundingClientRect().right > document.documentElement.clientWidth + 1).slice(0, 5).map(e => e.tagName + '.' + e.className).join(' | ') : ''");
        Assert.True(overflow == string.Empty, "La página se desborda en 360 px: " + overflow);
        await tab.ShotAsync("gastos-movil", fullPage: true);
        Assert.Empty(tab.ConsoleErrors);
    }
}
