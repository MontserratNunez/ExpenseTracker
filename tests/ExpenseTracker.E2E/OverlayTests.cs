using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

/// <summary>Focus, keyboard and scroll behavior of modals, panels, menus and popovers, in a real browser.</summary>
[Collection(E2ECollection.Name)]
public class OverlayTests
{
    private readonly AppFixture _app;

    public OverlayTests(AppFixture app) => _app = app;

    private static ILocator Button(IPage page, string name) =>
        page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = name, Exact = true });

    private static ILocator Dialog(IPage page, string name) =>
        page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    private static Task<string> BodyOverflow(IPage page) =>
        page.EvaluateAsync<string>("() => document.body.style.overflow");

    private async Task<Tab> OpenCatalogAsync(int width = 1280, int height = 800)
    {
        var tab = await Tab.OpenAsync(_app, width, height);
        await tab.RegisterAsync();
        await tab.GotoAsync("/dev/ui");
        await tab.Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Modales, paneles, menús y popover" }).WaitForAsync();
        return tab;
    }

    // ================= Modal =================

    [E2EFact]
    public async Task Modal_FocusesTheFirstField_TrapsTab_LocksTheScroll_AndEscReturnsTheFocus()
    {
        await using var tab = await OpenCatalogAsync();
        var page = tab.Page;
        var opener = Button(page, "Abrir modal");
        var dialog = Dialog(page, "Un modal");

        await Tab.ClickUntilAsync(opener, dialog);

        // The focus moved inside, onto the first field (not the close button).
        await Expect(dialog.GetByLabel("Un campo")).ToBeFocusedAsync();
        Assert.Equal("hidden", await BodyOverflow(page));
        await tab.ShotAsync("overlay-modal", dialog);

        // Tab and Shift+Tab never leave the dialog.
        for (var i = 0; i < 12; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            await Expect(dialog.Locator(":focus")).ToHaveCountAsync(1);
        }

        // From the first control, Shift+Tab wraps to the last one.
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cerrar" }).FocusAsync();
        await page.Keyboard.PressAsync("Shift+Tab");
        await Expect(dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Aceptar" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        await Expect(dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cerrar" })).ToBeFocusedAsync();

        // Esc closes it, the focus goes back to the button that opened it, and the page scrolls again.
        await page.Keyboard.PressAsync("Escape");
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(opener).ToBeFocusedAsync();
        Assert.Equal(string.Empty, await BodyOverflow(page));
        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Modal_ClickOnTheDimmedArea_Closes_ButAClickInsideDoesNot()
    {
        await using var tab = await OpenCatalogAsync();
        var page = tab.Page;
        var dialog = Dialog(page, "Un modal");
        await Tab.ClickUntilAsync(Button(page, "Abrir modal"), dialog);

        await dialog.GetByText("Este es el modal normal").ClickAsync();
        await dialog.GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "Un modal" }).ClickAsync();
        await Expect(dialog).ToBeVisibleAsync();

        await page.Mouse.ClickAsync(8, 8); // far from the panel, on the dimmed area
        await Expect(dialog).ToBeHiddenAsync();
    }

    [E2EFact]
    public async Task Modal_WithLongContent_ScrollsInsideItself_KeepingHeaderAndFooterInView()
    {
        await using var tab = await OpenCatalogAsync(height: 600);
        var page = tab.Page;
        var dialog = Dialog(page, "Contenido largo");
        await Tab.ClickUntilAsync(Button(page, "Modal con contenido largo"), dialog);

        var body = dialog.Locator(".overlay__body");
        Assert.True(await body.EvaluateAsync<bool>("e => e.scrollHeight > e.clientHeight"), "el cuerpo debería desbordar y desplazarse");

        await body.EvaluateAsync("e => { e.scrollTop = e.scrollHeight; }");
        await Expect(dialog.GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = "Contenido largo" })).ToBeInViewportAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Entendido" })).ToBeInViewportAsync();
        await Expect(dialog.GetByText("Párrafo 30")).ToBeInViewportAsync();

        var panelBox = await dialog.BoundingBoxAsync();
        Assert.True(panelBox!.Height <= 600, "el modal no debe ser más alto que la ventana");
        await tab.ShotAsync("overlay-modal-largo");
    }

    [E2EFact]
    public async Task NestedOverlays_EscClosesOnlyTheTop_FocusReturnsStepByStep_ScrollUnlocksAtTheEnd()
    {
        await using var tab = await OpenCatalogAsync();
        var page = tab.Page;
        var first = Dialog(page, "Primer modal");
        var top = Dialog(page, "Confirmación anidada");
        var opener = Button(page, "Modal con otro encima");
        var openTop = Button(page, "Abrir confirmación");

        await Tab.ClickUntilAsync(opener, first);
        await Tab.ClickUntilAsync(openTop, top);

        // The safe button of the top dialog has the focus, and Tab stays in the top one.
        await Expect(top.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancelar" })).ToBeFocusedAsync();
        for (var i = 0; i < 6; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            await Expect(top.Locator(":focus")).ToHaveCountAsync(1);
        }

        await page.Keyboard.PressAsync("Escape");
        await Expect(top).ToBeHiddenAsync();
        await Expect(first).ToBeVisibleAsync();                  // only the top one closed
        await Expect(openTop).ToBeFocusedAsync();                // back to what opened it
        Assert.Equal("hidden", await BodyOverflow(page));        // still one overlay open

        await page.Keyboard.PressAsync("Escape");
        await Expect(first).ToBeHiddenAsync();
        await Expect(opener).ToBeFocusedAsync();
        Assert.Equal(string.Empty, await BodyOverflow(page));
    }

    [E2EFact]
    public async Task Esc_ClosesTheTopOverlay_EvenWhenTheFocusIsOutsideIt()
    {
        await using var tab = await OpenCatalogAsync();
        var page = tab.Page;
        var first = Dialog(page, "Primer modal");
        var top = Dialog(page, "Confirmación anidada");

        // One overlay, with the focus taken away from it.
        var single = Dialog(page, "Un modal");
        await Tab.ClickUntilAsync(Button(page, "Abrir modal"), single);
        await page.EvaluateAsync("() => document.activeElement?.blur()");
        Assert.Equal("BODY", await page.EvaluateAsync<string>("() => document.activeElement.tagName"));
        await page.Keyboard.PressAsync("Escape");
        await Expect(single).ToBeHiddenAsync();

        // Two stacked: Esc with the focus outside closes only the top one.
        await Tab.ClickUntilAsync(Button(page, "Modal con otro encima"), first);
        await Tab.ClickUntilAsync(Button(page, "Abrir confirmación"), top);
        await page.EvaluateAsync("() => document.activeElement?.blur()");
        await page.Keyboard.PressAsync("Escape");
        await Expect(top).ToBeHiddenAsync();
        await Expect(first).ToBeVisibleAsync();
    }

    // ================= Confirm dialog =================

    [E2EFact]
    public async Task Confirm_StartsOnTheSafeButton_SoEnterDoesNotDelete()
    {
        await using var tab = await OpenCatalogAsync();
        var page = tab.Page;
        var dialog = Dialog(page, "Eliminar elemento");

        await Tab.ClickUntilAsync(Button(page, "Eliminar…"), dialog);
        await Expect(dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancelar" })).ToBeFocusedAsync();
        await tab.ShotAsync("overlay-confirmar", dialog);

        await page.Keyboard.PressAsync("Enter");
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(page.GetByText("Última acción: ninguna")).ToBeVisibleAsync();

        await Button(page, "Eliminar…").ClickAsync();
        await Expect(dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancelar" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        await Expect(dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Eliminar" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(page.GetByText("Última acción: Eliminado (confirmado)")).ToBeVisibleAsync();
    }

    // ================= Side panel =================

    [E2EFact]
    public async Task SidePanel_SlidesInFromTheRight_KeepsThePageVisible_AndEscCloses()
    {
        await using var tab = await OpenCatalogAsync();
        var page = tab.Page;
        var panel = Dialog(page, "Panel lateral");
        var opener = Button(page, "Panel lateral");

        await Tab.ClickUntilAsync(opener, panel);
        await Expect(panel.GetByLabel("Nombre")).ToBeFocusedAsync();
        await page.WaitForTimeoutAsync(300); // let the slide-in animation finish

        var box = await panel.BoundingBoxAsync();
        var viewport = page.ViewportSize!;
        Assert.InRange(box!.X + box.Width, viewport.Width - 1, viewport.Width + 1); // flush with the right edge
        Assert.True(box.Width <= 421);
        Assert.Equal(viewport.Height, (int)Math.Round(box.Height));
        await tab.ShotAsync("overlay-panel");

        await page.Keyboard.PressAsync("Escape");
        await Expect(panel).ToBeHiddenAsync();
        await Expect(opener).ToBeFocusedAsync();
    }

    // ================= Dropdown menu =================

    [E2EFact]
    public async Task Menu_IsFullyOperableWithTheKeyboard()
    {
        await using var tab = await OpenCatalogAsync();
        var page = tab.Page;
        var trigger = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Acciones de Suscripción Netflix" });
        var menu = page.GetByRole(AriaRole.Menu);
        ILocator Item(string name) => menu.GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = name });

        await Expect(trigger).ToHaveAttributeAsync("aria-expanded", "false");
        await Tab.ClickUntilAsync(trigger, menu);
        await page.Keyboard.PressAsync("Escape");
        await Expect(menu).ToBeHiddenAsync();
        await Expect(trigger).ToBeFocusedAsync();

        // Enter opens it and focuses the first option.
        await page.Keyboard.PressAsync("Enter");
        await Expect(menu).ToBeVisibleAsync();
        await Expect(trigger).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Item("Editar")).ToBeFocusedAsync();
        await page.WaitForTimeoutAsync(250);
        await tab.ShotAsync("overlay-menu");

        // Arrows move and wrap, skipping the disabled option; Home and End jump.
        await page.Keyboard.PressAsync("ArrowDown");
        await Expect(Item("Duplicar")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await Expect(Item("Eliminar")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        await Expect(Item("Editar")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("ArrowUp");
        await Expect(Item("Eliminar")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Home");
        await Expect(Item("Editar")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("End");
        await Expect(Item("Eliminar")).ToBeFocusedAsync();

        // Esc closes only the menu and returns to the button.
        await page.Keyboard.PressAsync("Escape");
        await Expect(menu).ToBeHiddenAsync();
        await Expect(trigger).ToBeFocusedAsync();

        // From the button, ArrowDown opens on the first option and ArrowUp on the last.
        await page.Keyboard.PressAsync("ArrowDown");
        await Expect(Item("Editar")).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Escape");
        await Expect(trigger).ToBeFocusedAsync();   // wait for the focus to be back on the button
        await page.Keyboard.PressAsync("ArrowUp");
        await Expect(Item("Eliminar")).ToBeFocusedAsync();

        // Tab closes it.
        await page.Keyboard.PressAsync("Tab");
        await Expect(menu).ToBeHiddenAsync();

        // Choosing with Enter runs the action, closes the menu and returns to the button.
        await trigger.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Expect(Item("Editar")).ToBeFocusedAsync();   // wait until the menu has moved the focus in
        await page.Keyboard.PressAsync("Enter");
        await Expect(menu).ToBeHiddenAsync();
        await Expect(page.GetByText("Última acción: Editar")).ToBeVisibleAsync();
        await Expect(trigger).ToBeFocusedAsync();

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Menu_DisabledOptionIsSkipped_AndAClickOutsideCloses()
    {
        await using var tab = await OpenCatalogAsync();
        var page = tab.Page;
        var trigger = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Acciones de Suscripción Netflix" });
        var menu = page.GetByRole(AriaRole.Menu);

        await Tab.ClickUntilAsync(trigger, menu);
        var disabled = menu.GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = "No disponible" });
        await Expect(disabled).ToBeDisabledAsync();

        await page.Mouse.ClickAsync(8, 8);
        await Expect(menu).ToBeHiddenAsync();
    }

    [E2EFact]
    public async Task Menu_ChoosingDelete_OpensAConfirm_WhoseCloseReturnsTheFocusToTheMenuButton()
    {
        await using var tab = await OpenCatalogAsync();
        var page = tab.Page;
        var trigger = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Acciones de Suscripción Netflix" });
        var menu = page.GetByRole(AriaRole.Menu);
        var confirm = Dialog(page, "Eliminar elemento");

        await Tab.ClickUntilAsync(trigger, menu);
        await menu.GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = "Eliminar" }).ClickAsync();

        await Expect(menu).ToBeHiddenAsync();
        await Expect(confirm).ToBeVisibleAsync();
        await Expect(confirm.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cancelar" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Escape");
        await Expect(confirm).ToBeHiddenAsync();
        await Expect(trigger).ToBeFocusedAsync();
    }

    // ================= Popover =================

    [E2EFact]
    public async Task Popover_OpensUnderItsButton_KeepsTheFocusInside_AndEscReturnsIt()
    {
        await using var tab = await OpenCatalogAsync();
        var page = tab.Page;
        var opener = Button(page, "Elegir mes");
        var popover = page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Elegir mes" });

        await Tab.ClickUntilAsync(opener, popover);
        await Expect(popover.Locator(":focus")).ToHaveCountAsync(1);

        await page.WaitForTimeoutAsync(300); // the opening animation and the scroll the focus causes
        await tab.ShotAsync("overlay-popover");

        // Both boxes are read in the same instant: the page may scroll when the focus enters the popover.
        var boxes = await page.EvaluateAsync<double[]>(
            "() => { const o = document.querySelector('.popover-anchor > button').getBoundingClientRect(); " +
            "const p = document.querySelector('.popover-app').getBoundingClientRect(); return [o.x, o.y, o.height, p.x, p.y]; }");
        Assert.True(boxes[4] >= boxes[1] + boxes[2] - 1, $"el popover va debajo del botón (botón y={boxes[1]} alto={boxes[2]}; popover y={boxes[4]})");
        Assert.InRange(boxes[3], boxes[0] - 2, boxes[0] + 2);
        Assert.Equal(string.Empty, await BodyOverflow(page)); // a popover does not lock the page

        for (var i = 0; i < 6; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            await Expect(popover.Locator(":focus")).ToHaveCountAsync(1);
        }

        await page.Keyboard.PressAsync("Escape");
        await Expect(popover).ToBeHiddenAsync();
        await Expect(opener).ToBeFocusedAsync();
    }

    [E2EFact]
    public async Task Popover_ClickOutsideCloses_AndChoosingAnOptionClosesIt()
    {
        await using var tab = await OpenCatalogAsync();
        var page = tab.Page;
        var opener = Button(page, "Elegir mes");
        var popover = page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Elegir mes" });

        await Tab.ClickUntilAsync(opener, popover);
        await page.Mouse.ClickAsync(8, 8);
        await Expect(popover).ToBeHiddenAsync();

        await opener.ClickAsync();
        await popover.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Noviembre 2026" }).ClickAsync();
        await Expect(popover).ToBeHiddenAsync();
        await Expect(page.GetByText("Última acción: Noviembre 2026 elegido")).ToBeVisibleAsync();
    }

    // ================= Real forms use the same overlays =================

    [E2EFact]
    public async Task ExpenseForm_FocusesTheName_EscCancels_AndReturnsTheFocusToNewExpense()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();
        var page = tab.Page;
        await tab.GotoAsync("/gastos");
        var opener = Button(page, "Nuevo gasto").First;   // the one in the page header; an empty page has a second one
        var dialog = Dialog(page, "Nuevo gasto");

        await Tab.ClickUntilAsync(opener, dialog);

        await Expect(dialog.GetByLabel("Nombre", new LocatorGetByLabelOptions { Exact = true })).ToBeFocusedAsync();
        Assert.Equal("hidden", await BodyOverflow(page));

        await page.Keyboard.PressAsync("Escape");
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(opener).ToBeFocusedAsync();
        Assert.Equal(string.Empty, await BodyOverflow(page));
    }
}
