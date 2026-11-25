using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class NoBootstrapTests
{
    private readonly AppFixture _app;

    public NoBootstrapTests(AppFixture app) => _app = app;

    private static readonly string[] Paths =
    {
        "/", "/gastos", "/ingresos", "/deudas", "/plan", "/metas", "/estimaciones", "/configuracion",
        "/Account/Manage", "/Account/Manage/Email", "/Account/Manage/ChangePassword", "/Account/Manage/TwoFactorAuthentication",
        "/Account/Manage/Passkeys", "/Account/Manage/PersonalData", "/dev/ui"
    };

    [E2EFact]
    public async Task EveryScreen_WorksWithoutBootstrap_At360And1440()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1440, height: 900);
        var page = tab.Page;
        var stylesheets = new List<string>();
        page.Response += (_, response) =>
        {
            if (response.Url.Contains(".css"))
                stylesheets.Add(response.Url);
        };
        await tab.RegisterAsync();

        foreach (var (width, height) in new[] { (1440, 900), (360, 800) })
        {
            await page.SetViewportSizeAsync(width, height);
            foreach (var path in Paths)
            {
                await tab.GotoAsync(path);
                await Expect(page.Locator("h1").First).ToBeVisibleAsync();
                await page.EvaluateAsync("async () => { await document.fonts.ready; }");
                await page.WaitForTimeoutAsync(200);

                var overflow = await page.EvaluateAsync<string>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth ? [...document.querySelectorAll('body *')].filter(e => e.getBoundingClientRect().right > document.documentElement.clientWidth + 1).slice(0, 4).map(e => e.tagName + '.' + e.className).join(' | ') : ''");
                Assert.True(overflow == string.Empty, $"{path} se desborda en {width} px: {overflow}");

                // The page uses the design system: Inter on the body, and no Bootstrap rule is left to restyle anything.
                var font = await page.EvaluateAsync<string>("() => getComputedStyle(document.body).fontFamily");
                Assert.StartsWith("Inter", font.Trim('"', ' '));

                if (width == 1440 && (path is "/" or "/gastos" or "/plan" or "/configuracion"))
                    await tab.ShotAsync("sin-bootstrap-" + (path == "/" ? "inicio" : path.Trim('/')), fullPage: true);
            }
        }

        Assert.DoesNotContain(stylesheets, url => url.Contains("bootstrap.min.css") || url.EndsWith("/app.css"));
        Assert.Contains(stylesheets, url => url.Contains("bootstrap-icons"));
        Assert.Empty(tab.FailedRequests);
        Assert.Empty(tab.ExternalRequests);
        Assert.Empty(tab.BadResponses);
        Assert.Empty(tab.ConsoleErrors.ToArray().Where(e => !e.Contains("invalid domain")));
    }
}
