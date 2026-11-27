using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class FeedbackTests
{
    private readonly AppFixture _app;

    public FeedbackTests(AppFixture app) => _app = app;

    private static ILocator Dialog(IPage page, string name) => page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    private static ILocator Toast(IPage page, string text) => page.Locator(".toast-app").Filter(new LocatorFilterOptions { HasText = text });

    [E2EFact]
    public async Task DeletingAnExpense_ShowsAToastWithUndo_AndUndoBringsItBack()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();
        var page = tab.Page;

        await tab.GotoAsync("/gastos");
        var form = Dialog(page, "Nuevo gasto");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nuevo gasto" }).First, form);
        await form.GetByLabel("Nombre", new LocatorGetByLabelOptions { Exact = true }).FillAsync("Cena");
        await form.GetByLabel("Monto", new LocatorGetByLabelOptions { Exact = true }).FillAsync("1250.5");
        await form.GetByLabel("Monto", new LocatorGetByLabelOptions { Exact = true }).PressAsync("Tab");
        await form.GetByLabel("Categoría", new LocatorGetByLabelOptions { Exact = true }).SelectOptionAsync(new SelectOptionValue { Label = "Comida" });
        await form.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(Toast(page, "Gasto guardado.")).ToBeVisibleAsync();

        var row = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Cena" });
        await Expect(row).ToContainTextAsync("1,250.50");

        // Delete it.
        await row.ClickAsync();
        var edit = Dialog(page, "Editar gasto");
        await edit.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Eliminar" }).ClickAsync();
        await Dialog(page, "Eliminar gasto").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Eliminar" }).ClickAsync();

        var deleted = Toast(page, "Gasto eliminado.");
        await Expect(deleted).ToBeVisibleAsync();
        await Expect(deleted.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Deshacer" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Cena" })).ToHaveCountAsync(0);
        await tab.ShotAsync("toast-con-deshacer", deleted);

        // Undo it: the row is back with the same amount, and the toast is gone.
        await deleted.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Deshacer" }).ClickAsync();
        await Expect(Toast(page, "Gasto restaurado.")).ToBeVisibleAsync();
        await Expect(deleted).ToHaveCountAsync(0);
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Cena" })).ToContainTextAsync("1,250.50");

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task DeletingADebt_ShowsAToast_EvenThoughThePageNavigatesAway()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();
        var page = tab.Page;

        await tab.GotoAsync("/deudas");
        var form = Dialog(page, "Nueva deuda");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva deuda" }).First, form);
        await form.GetByLabel("Nombre", new LocatorGetByLabelOptions { Exact = true }).FillAsync("Préstamo carro");
        await form.GetByLabel("Monto original", new LocatorGetByLabelOptions { Exact = true }).FillAsync("100000");
        await form.GetByLabel("Monto original", new LocatorGetByLabelOptions { Exact = true }).PressAsync("Tab");
        await form.GetByLabel("Cuota mensual", new LocatorGetByLabelOptions { Exact = true }).FillAsync("5000");
        await form.GetByLabel("Cuota mensual", new LocatorGetByLabelOptions { Exact = true }).PressAsync("Tab");
        await form.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(Toast(page, "Deuda creada.")).ToBeVisibleAsync();

        await page.GetByText("Préstamo carro").First.ClickAsync();
        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex(@"/deudas/\d+"));
        var confirm = Dialog(page, "Eliminar deuda");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Eliminar", Exact = true }).First, confirm);
        await confirm.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Eliminar" }).ClickAsync();

        await page.WaitForURLAsync($"{_app.BaseUrl}/deudas");
        await Expect(Toast(page, "Deuda eliminada.")).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task ATableThatIsEmpty_ShowsAnEmptyStateWithAWayToCreateTheFirstItem()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();
        var page = tab.Page;

        var cases = new (string Path, string Title, string Button, string Dialog)[]
        {
            ("/gastos", "No hay gastos en", "Nuevo gasto", "Nuevo gasto"),
            ("/deudas", "Aún no has registrado deudas", "Nueva deuda", "Nueva deuda"),
            ("/metas", "Aún no tienes metas", "Nueva meta", "Nueva meta"),
            ("/ingresos", "Aún no tienes fuentes de ingreso", "Nueva fuente", "Nueva fuente de ingreso"),
            ("/plan", "Aún no tienes un plan", "Crear mi plan", "Nuevo plan"),
        };

        foreach (var (path, title, button, dialogName) in cases)
        {
            await tab.GotoAsync(path);
            var empty = page.Locator(".empty-state").Filter(new LocatorFilterOptions { HasText = title });
            await Expect(empty).ToBeVisibleAsync();
            await Expect(page.Locator("[role=status][aria-busy=true]")).ToHaveCountAsync(0);   // no skeleton left behind

            if (path == "/gastos")
                await tab.ShotAsync("estado-vacio-gastos", empty);

            var dialog = Dialog(page, dialogName);
            await Tab.ClickUntilAsync(empty.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = button }), dialog);
            await page.WaitForFunctionAsync("() => document.activeElement?.closest('[role=dialog]') != null");
            await page.Keyboard.PressAsync("Escape");
            await Expect(dialog).ToBeHiddenAsync();
        }

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Catalog_Toasts_ShowAnActionThatRunsOnce_AndTheOtherStatesRender()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;
        await tab.GotoAsync("/dev/ui");

        var section = page.Locator("section[aria-labelledby=catalog-feedback]");
        await Expect(section).ToBeVisibleAsync();

        // Skeleton, empty state, progress bars and alerts.
        await Expect(section.GetByRole(AriaRole.Status).Filter(new LocatorFilterOptions { HasText = "Cargando..." })).ToHaveCountAsync(2);
        await Expect(section.GetByRole(AriaRole.Progressbar)).ToHaveCountAsync(3);
        await Expect(section.GetByRole(AriaRole.Alert)).ToHaveCountAsync(1);
        await Expect(section.Locator(".empty-state__title")).ToHaveTextAsync("Aún no tienes metas");

        // The close button of the dismissible alert is reachable by name.
        await Expect(section.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Cerrar aviso" })).ToBeVisibleAsync();

        // The toast with an action: Deshacer runs once and the toast closes.
        var withAction = Button(section, "Con Deshacer");
        await Tab.ClickUntilAsync(withAction, Toast(page, "Gasto eliminado."));
        await Toast(page, "Gasto eliminado.").GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Deshacer" }).ClickAsync();
        await Expect(page.GetByTestId("toast-result")).ToHaveTextAsync("Deshacer ejecutado");
        await Expect(Toast(page, "Gasto eliminado.")).ToHaveCountAsync(0);

        await tab.ShotAsync("catalogo-feedback", section);
        Assert.Empty(tab.ConsoleErrors);
    }

    private static ILocator Button(ILocator scope, string name)
        => scope.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = name, Exact = true });
}
