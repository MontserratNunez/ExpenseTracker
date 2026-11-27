using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class AccountSettingsTests
{
    private readonly AppFixture _app;

    public AccountSettingsTests(AppFixture app) => _app = app;

    private static Task<string> OverflowAsync(IPage page)
        => page.EvaluateAsync<string>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth ? [...document.querySelectorAll('body *')].filter(e => e.getBoundingClientRect().right > document.documentElement.clientWidth + 1).slice(0, 5).map(e => e.tagName + '.' + e.className).join(' | ') : ''");

    // ================= Login and registration =================

    [E2EFact]
    public async Task Login_IsSplitInTwo_HasNoSidebar_AndShowsValidationInSpanish()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 800);
        var page = tab.Page;

        await tab.GotoAsync("/Account/Login");
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Iniciar sesión", Level = 1 })).ToBeVisibleAsync();

        // Not the app shell: no sidebar, no bottom navigation. The form on white and the blue panel beside it.
        await Expect(page.Locator("aside.sidebar")).ToHaveCountAsync(0);
        await Expect(page.Locator("nav.bottom-nav")).ToHaveCountAsync(0);
        var hero = page.Locator(".account-shell__hero");
        await Expect(hero).ToBeVisibleAsync();
        await Expect(hero).ToContainTextAsync("Expense Tracker");
        await Expect(hero).ToContainTextAsync("Tu dinero, claro y bajo control.");
        var image = await hero.EvaluateAsync<string>("e => getComputedStyle(e).backgroundImage");
        Assert.Contains("linear-gradient", image);
        var inner = await page.Locator(".account-shell__inner").BoundingBoxAsync();
        Assert.True(inner!.Width <= 381, $"el formulario mide {inner.Width}px y debe ser de 380 como máximo");
        var formBox = await page.Locator(".account-shell__form").BoundingBoxAsync();
        var heroBox = await hero.BoundingBoxAsync();
        Assert.True(heroBox!.X >= formBox!.X + formBox.Width - 1, "el panel azul va a la derecha del formulario");
        await tab.ShotAsync("cuenta-login");

        // Submitting empty shows the messages, in Spanish, under the fields.
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Iniciar sesión", Exact = true }).ClickAsync();
        await Expect(page.GetByText("El campo Correo electrónico es obligatorio.").First).ToBeVisibleAsync();
        await Expect(page.GetByText("El campo Contraseña es obligatorio.").First).ToBeVisibleAsync();

        // Wrong credentials: a clear alert, not the template's English text.
        await page.GetByLabel("Correo electrónico").FillAsync("nadie@test.com");
        await page.GetByLabel("Contraseña", new PageGetByLabelOptions { Exact = true }).FillAsync("Passw0rd!x");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Iniciar sesión", Exact = true }).ClickAsync();
        await Expect(page.Locator(".inline-alert--danger")).ToContainTextAsync("Inicio de sesión no válido");

        // Every link of the flow is in Spanish.
        await Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "¿Olvidaste tu contraseña?" })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Crear una cuenta nueva" })).ToBeVisibleAsync();
        // The passkey script cannot use an IP address as its domain; that message is expected on 127.0.0.1.
        Assert.Empty(tab.ConsoleErrors.ToArray().Where(e => !e.Contains("invalid domain")));
    }

    [E2EFact]
    public async Task AccountPages_OnAPhone_ShowOnlyTheFormWithTheLogoOnTop_AndFit()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 360, height: 740);
        var page = tab.Page;

        foreach (var path in new[] { "/Account/Login", "/Account/Register", "/Account/ForgotPassword" })
        {
            await tab.GotoAsync(path);
            await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 })).ToBeVisibleAsync();
            await Expect(page.Locator(".account-shell__hero")).ToBeHiddenAsync();
            await Expect(page.Locator(".account-brand")).ToBeVisibleAsync();
            var overflow = await OverflowAsync(page);
            Assert.True(overflow == string.Empty, $"{path} se desborda en 360 px: " + overflow);
        }

        await tab.GotoAsync("/Account/Register");
        await tab.ShotAsync("cuenta-registro-movil");
    }

    [E2EFact]
    public async Task ForgotPassword_LeadsToAConfirmation_InSpanish()
    {
        await using var tab = await Tab.OpenAsync(_app);
        var page = tab.Page;

        await tab.GotoAsync("/Account/ForgotPassword");
        await page.GetByLabel("Correo electrónico").FillAsync("alguien@test.com");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Restablecer contraseña" }).ClickAsync();
        await page.WaitForURLAsync(new Regex("/Account/ForgotPasswordConfirmation"));
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Revisa tu correo" })).ToBeVisibleAsync();
    }

    // ================= Manage account =================

    [E2EFact]
    public async Task ManageAccount_UsesTheAppShell_WithASimpleSubmenu_AndChangesThePassword()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 800);
        await tab.RegisterAsync();
        var page = tab.Page;

        await tab.GotoAsync("/Account/Manage");
        await Expect(page.Locator("aside.sidebar")).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Mi cuenta", Level = 1 })).ToBeVisibleAsync();

        var submenu = page.GetByRole(AriaRole.Navigation, new PageGetByRoleOptions { Name = "Secciones de la cuenta" });
        foreach (var name in new[] { "Perfil", "Correo electrónico", "Contraseña", "Datos personales" })
            await Expect(submenu.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = name })).ToBeVisibleAsync();
        await Expect(submenu.Locator("a.active")).ToHaveTextAsync("Perfil");
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Perfil", Level = 2 })).ToBeVisibleAsync();
        await tab.ShotAsync("cuenta-administrar");

        await submenu.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Contraseña" }).ClickAsync();
        await page.WaitForURLAsync(new Regex("/Account/Manage/ChangePassword"));
        await Expect(submenu.Locator("a.active")).ToHaveTextAsync("Contraseña");
        await page.GetByLabel("Contraseña actual").FillAsync("Passw0rd!x");
        await page.GetByLabel("Contraseña nueva", new PageGetByLabelOptions { Exact = true }).FillAsync("Passw0rd!y");
        await page.GetByLabel("Confirmar contraseña nueva").FillAsync("Passw0rd!z");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Actualizar contraseña" }).ClickAsync();
        await Expect(page.GetByText("La contraseña nueva y su confirmación no coinciden.").First).ToBeVisibleAsync();

        await page.GetByLabel("Contraseña actual").FillAsync("Passw0rd!x");
        await page.GetByLabel("Contraseña nueva", new PageGetByLabelOptions { Exact = true }).FillAsync("Passw0rd!y");
        await page.GetByLabel("Confirmar contraseña nueva").FillAsync("Passw0rd!y");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Actualizar contraseña" }).ClickAsync();
        await Expect(page.Locator(".inline-alert--success")).ToContainTextAsync("Tu contraseña se cambió.");

        await submenu.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Datos personales" }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Datos personales", Level = 2 })).ToBeVisibleAsync();
        await Expect(page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Eliminar mis datos" })).ToBeVisibleAsync();
        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task MyAccount_FromAnInteractivePage_ShowsTheAccountPage_NotPageNotFound()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 800);
        await tab.RegisterAsync();
        var page = tab.Page;

        // Through the user menu of the sidebar, without reloading by hand.
        await tab.GotoAsync("/gastos");
        var menu = page.GetByRole(AriaRole.Menu);
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = new Regex("^Menú de ") }), menu);
        await menu.GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = "Mi cuenta" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/Account/Manage$"));
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Mi cuenta", Level = 1 })).ToBeVisibleAsync();
        await Expect(page.GetByText("Página no encontrada")).ToHaveCountAsync(0);

        // And through the command palette.
        await tab.GotoAsync("/plan");
        var palette = page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions { Name = "Buscar o ir a..." });
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
        await Expect(palette.GetByRole(AriaRole.Combobox)).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync("mi cuenta");
        await page.Keyboard.PressAsync("Enter");
        await Expect(page).ToHaveURLAsync(new Regex("/Account/Manage$"));
        await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Mi cuenta", Level = 1 })).ToBeVisibleAsync();
        await Expect(page.GetByText("Página no encontrada")).ToHaveCountAsync(0);
    }

    // ================= Configuración =================

    [E2EFact]
    public async Task Settings_HasSectionsWithTitles_NoCards_EditableRates_AndCategoriesWithColorAndIcon()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        var page = tab.Page;

        await tab.GotoAsync("/configuracion");
        foreach (var title in new[] { "Moneda base", "Tasas de cambio", "Categorías", "Apariencia" })
            await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true })).ToBeVisibleAsync();
        await Expect(page.Locator("main .card, main .card-app, main .card-body")).ToHaveCountAsync(0);   // sections are separated by titles, not boxes
        await Expect(page.GetByRole(AriaRole.Radiogroup, new PageGetByRoleOptions { Name = "Tema" })).ToBeVisibleAsync();   // Apariencia: the theme picker

        // Rates: click, type, Enter. Esc cancels; an invalid number shows an error and keeps editing.
        var edit = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Editar la tasa de USD" });
        var input = page.GetByLabel("Tasa de USD", new PageGetByLabelOptions { Exact = true });
        await Tab.ClickUntilAsync(edit, input);
        await Expect(input).ToBeFocusedAsync();
        await input.FillAsync("99");
        await input.PressAsync("Escape");
        await Expect(input).ToHaveCountAsync(0);
        await Expect(page.GetByText("Sin configurar").First).ToBeVisibleAsync();

        await edit.ClickAsync();
        await input.FillAsync("sesenta");
        await input.PressAsync("Enter");
        await Expect(page.GetByRole(AriaRole.Alert).Filter(new LocatorFilterOptions { HasText = "Escriba una tasa válida" })).ToBeVisibleAsync();
        await input.FillAsync("60.5");
        await input.PressAsync("Enter");
        await Expect(input).ToHaveCountAsync(0);
        await Expect(page.Locator(".settings-row").Filter(new LocatorFilterOptions { HasText = "USD" })).ToContainTextAsync("1 USD = 60.5 DOP");
        await Expect(page.GetByText("Tasa de USD guardada.").First).ToBeVisibleAsync();

        // Categories: a list, a color from 8 swatches and an icon from the curated list.
        await Tab.ClickUntilAsync(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Nueva categoría" }), page.GetByLabel("Nombre", new PageGetByLabelOptions { Exact = true }));
        var editor = page.Locator("form.settings-editor");
        await Expect(editor.Locator(".color-swatches input[type=radio]")).ToHaveCountAsync(8);
        await Expect(editor.Locator(".icon-picker__grid input[type=radio]")).ToHaveCountAsync(40);
        await editor.GetByLabel("Nombre", new LocatorGetByLabelOptions { Exact = true }).FillAsync("Mascotas");
        await editor.GetByLabel("Color #9B59B6").CheckAsync();
        await editor.GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "gift" }).CheckAsync();
        await tab.ShotAsync("configuracion-categoria", editor);
        await editor.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Crear" }).ClickAsync();

        var row = page.Locator(".settings-row").Filter(new LocatorFilterOptions { HasText = "Mascotas" });
        await Expect(row).ToBeVisibleAsync();
        await Expect(row.Locator(".bi-gift")).ToBeVisibleAsync();

        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Acciones de Mascotas" }).ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Editar" }).ClickAsync();
        await Expect(editor.GetByLabel("Color #9B59B6")).ToBeCheckedAsync();
        await Expect(editor.GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = "gift" })).ToBeCheckedAsync();

        await tab.ShotAsync("configuracion-rediseno", fullPage: true);
        Assert.Empty(tab.ConsoleErrors);
    }

    [E2EFact]
    public async Task Settings_AndManage_FitAPhone()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 360, height: 800);
        await tab.RegisterAsync();
        var page = tab.Page;

        foreach (var path in new[] { "/configuracion", "/Account/Manage", "/Account/Manage/Email" })
        {
            await tab.GotoAsync(path);
            await Expect(page.Locator("main h1")).ToBeVisibleAsync();
            await page.WaitForTimeoutAsync(300);
            var overflow = await OverflowAsync(page);
            Assert.True(overflow == string.Empty, $"{path} se desborda en 360 px: " + overflow);
        }
    }
}
