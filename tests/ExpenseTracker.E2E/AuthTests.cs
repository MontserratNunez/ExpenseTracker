using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class AuthTests
{
    private const string SessionCookie = ".AspNetCore.Identity.Application";
    private readonly AppFixture _app;

    public AuthTests(AppFixture app) => _app = app;

    private static async Task<string[]> CookieNamesAsync(IPage page)
        => (await page.Context.CookiesAsync()).Select(c => c.Name).ToArray();

    [E2EFact]
    public async Task Logout_RemovesTheSessionCookie_AndTheSameUserCanLogInAgain()
    {
        await using var tab = await Tab.OpenAsync(_app);
        var email = await tab.RegisterAsync();
        var page = tab.Page;

        Assert.Contains(SessionCookie, await CookieNamesAsync(page));

        await tab.LogoutAsync();

        var after = await CookieNamesAsync(page);
        Assert.True(!after.Contains(SessionCookie), "Cookies left after logout: " + string.Join(", ", after));

        // Without clearing any cookie by hand, the same credentials log in again.
        await tab.GotoAsync("/Account/Login");
        await page.GetByLabel("Correo electrónico").FillAsync(email);
        await page.GetByLabel("Contraseña", new PageGetByLabelOptions { Exact = true }).FillAsync("Passw0rd!x");
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Iniciar sesión", Exact = true }).ClickAsync();

        await page.WaitForURLAsync($"{_app.BaseUrl}/");
        Assert.Contains(SessionCookie, await CookieNamesAsync(page));
        await Expect(page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = new System.Text.RegularExpressions.Regex("^Menú de ") })).ToBeVisibleAsync();
    }
}
