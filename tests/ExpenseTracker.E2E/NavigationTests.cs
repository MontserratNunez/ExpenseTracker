using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class NavigationTests
{
    private readonly AppFixture _app;

    public NavigationTests(AppFixture app) => _app = app;

    [E2EFact]
    public async Task TheMenu_StaysResponsive_AcrossManyVisits_AndAfterLoggingOutAndIn()
    {
        await using var tab = await Tab.OpenAsync(_app);
        var email = await tab.RegisterAsync();
        var page = tab.Page;
        var paths = new[] { "/", "/gastos", "/ingresos", "/plan", "/deudas", "/metas", "/estimaciones", "/configuracion" };

        async Task VisitAllAsync(int rounds)
        {
            for (var round = 0; round < rounds; round++)
                foreach (var path in paths)
                {
                    var response = await page.GotoAsync(_app.BaseUrl + path);
                    Assert.True(response is { Ok: true }, $"{path} answered {response?.Status}");
                    await Expect(page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 }).First).ToBeVisibleAsync(new() { Timeout = 15000 });
                    await Expect(page.Locator(".inline-alert--danger, #blazor-error-ui:visible")).ToHaveCountAsync(0);
                }
        }

        await VisitAllAsync(3);

        await tab.LogoutAsync();
        await tab.GotoAsync("/Account/Login");
        await page.GetByLabel("Correo electrónico").FillAsync(email);
        await page.GetByLabel("Contraseña", new PageGetByLabelOptions { Exact = true }).FillAsync("Passw0rd!x");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Iniciar sesión", Exact = true }).ClickAsync();
        await page.WaitForURLAsync($"{_app.BaseUrl}/");

        await VisitAllAsync(3);
        Assert.DoesNotContain(tab.ConsoleErrors, e => e.Contains("timeout", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("SqlException", _app.AppLog);
    }
}
