using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class FormsTests
{
    private readonly AppFixture _app;

    public FormsTests(AppFixture app) => _app = app;

    private static ILocator Dialog(IPage page, string name) => page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = name });

    private static ILocator Field(ILocator dialog, string label) => dialog.GetByLabel(label, new LocatorGetByLabelOptions { Exact = true });

    [E2EFact]
    public async Task EveryForm_Opens_WithoutAnErrorAlert_AndWithEmptyFields()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();
        var page = tab.Page;

        var cases = new (string Path, string? PreClick, string Button, string Title, string Shot)[]
        {
            ("/gastos", null, "Nuevo gasto", "Nuevo gasto", "form-gasto"),
            ("/ingresos", null, "Nueva fuente", "Nueva fuente de ingreso", "form-fuente-ingreso"),
            ("/ingresos", "Ingresos únicos", "Nuevo ingreso", "Nuevo ingreso único", "form-ingreso-unico"),
            ("/deudas", null, "Nueva deuda", "Nueva deuda", "form-deuda"),
            ("/metas", null, "Nueva meta", "Nueva meta", "form-meta"),
            ("/plan", null, "Nuevo plan", "Nuevo plan", "form-plan"),
        };

        foreach (var (path, preClick, button, title, shot) in cases)
        {
            await tab.GotoAsync(path);
            await page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 }).WaitForAsync();

            if (preClick is not null)
                await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Tab, new PageGetByRoleOptions { Name = preClick }),
                    page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = button }).First);

            var dialog = Dialog(page, title);
            await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = button, Exact = true }).First, dialog);

            await Expect(dialog.Locator(".inline-alert--danger")).ToHaveCountAsync(0);   // no stray "formError" text
            await Expect(dialog.Locator(".validation-message")).ToHaveCountAsync(0);
            await tab.ShotAsync(shot, dialog);

            // Esc closes every form, and the focus goes back to the button that opened it.
            await page.Keyboard.PressAsync("Escape");
            await Expect(dialog).ToBeHiddenAsync();
            await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = button, Exact = true }).First).ToBeFocusedAsync();
        }

        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task NewExpense_FromEmptyForm_ToSavedEditedAndDeleted()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();
        var page = tab.Page;

        await tab.GotoAsync("/gastos");
        var dialog = Dialog(page, "Nuevo gasto");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nuevo gasto" }).First, dialog);

        // The text fields start empty (they once showed the literal word "Value").
        await Expect(Field(dialog, "Nombre")).ToHaveValueAsync("");
        await Expect(Field(dialog, "Notas")).ToHaveValueAsync("");

        // Submitting empty shows every missing field under the field, in Spanish.
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();
        await Expect(dialog.GetByText("El nombre es obligatorio.")).ToBeVisibleAsync();
        await Expect(dialog.GetByText("El monto debe ser mayor que cero.")).ToBeVisibleAsync();
        await Expect(dialog.GetByText("Seleccione una categoría.")).ToBeVisibleAsync();
        await tab.ShotAsync("form-gasto-con-errores", dialog);

        // A decimal comma is rejected, not read as thousands.
        await Field(dialog, "Monto").FillAsync("5000,5");
        await Field(dialog, "Monto").PressAsync("Tab");
        await Expect(dialog.GetByText("Escriba un monto válido, por ejemplo 5,000.50.")).ToBeVisibleAsync();
        await Expect(dialog.GetByText("El monto debe ser mayor que cero.")).ToHaveCountAsync(0);

        // A valid amount is formatted when the field loses focus.
        await Field(dialog, "Nombre").FillAsync("Supermercado");
        await Field(dialog, "Monto").FillAsync("4885.4");
        await Field(dialog, "Monto").PressAsync("Tab");
        await Expect(Field(dialog, "Monto")).ToHaveValueAsync("4,885.40");
        await Field(dialog, "Categoría").SelectOptionAsync(new SelectOptionValue { Label = "Comida" });
        await Expect(dialog.Locator(".validation-message")).ToHaveCountAsync(0);
        await tab.ShotAsync("form-gasto-lleno", dialog);

        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();

        await Expect(dialog).ToBeHiddenAsync();
        await Expect(page.GetByText("Gasto guardado.")).ToBeVisibleAsync();
        var row = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Supermercado" });
        await Expect(row).ToContainTextAsync("RD$ 4,885.40");
        await Expect(row).ToContainTextAsync("Comida");
        await tab.ShotAsync("gastos-con-un-gasto");

        // Editing shows the saved values.
        var editDialog = Dialog(page, "Editar gasto");
        await row.ClickAsync();
        await Expect(editDialog).ToBeVisibleAsync();
        await Expect(Field(editDialog, "Nombre")).ToHaveValueAsync("Supermercado");
        await Expect(Field(editDialog, "Monto")).ToHaveValueAsync("4,885.40");
        await Expect(Field(editDialog, "Categoría")).ToHaveValueAsync(new System.Text.RegularExpressions.Regex(@"^\d+$"));
        await tab.ShotAsync("form-gasto-editar", editDialog);

        // Deleting asks for confirmation first.
        await editDialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Eliminar" }).ClickAsync();
        var confirm = Dialog(page, "Eliminar gasto");
        await Expect(confirm).ToBeVisibleAsync();
        await confirm.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Eliminar" }).ClickAsync();

        await Expect(page.GetByText("Gasto eliminado.")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Supermercado" })).ToHaveCountAsync(0);

        Assert.Empty(tab.ConsoleErrors);
        Assert.Empty(tab.FailedRequests);
    }

    [E2EFact]
    public async Task NewIncomeSource_Saves_AndAppearsAsACard()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();
        var page = tab.Page;

        await tab.GotoAsync("/ingresos");
        var dialog = Dialog(page, "Nueva fuente de ingreso");
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva fuente" }).First, dialog);

        await Field(dialog, "Nombre").FillAsync("Sueldo trabajo");
        await Field(dialog, "Monto").FillAsync("50,000");
        await Field(dialog, "Monto").PressAsync("Tab");
        await Field(dialog, "Frecuencia").SelectOptionAsync(new SelectOptionValue { Label = "Quincenal" });
        await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Guardar" }).ClickAsync();

        await Expect(dialog).ToBeHiddenAsync();
        await Expect(page.GetByText("Fuente de ingreso creada.")).ToBeVisibleAsync();
        var row = page.Locator(".income-card").Filter(new LocatorFilterOptions { HasText = "Sueldo trabajo" });
        await Expect(row).ToContainTextAsync("RD$ 50,000.00");
        await Expect(row).ToContainTextAsync("Quincenal");
        await tab.ShotAsync("ingresos-con-una-fuente");
    }
}
