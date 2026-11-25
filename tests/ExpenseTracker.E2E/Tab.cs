using Microsoft.Playwright;

namespace ExpenseTracker.E2E;

/// <summary>
/// One browser window of a test: an isolated session (its own cookies) that records everything that
/// goes wrong in the page (console errors, failed requests, requests to other hosts).
/// </summary>
public sealed class Tab : IAsyncDisposable
{
    private readonly IBrowserContext _context;
    private readonly AppFixture _app;

    private Tab(AppFixture app, IBrowserContext context, IPage page)
    {
        _app = app;
        _context = context;
        Page = page;
    }

    public IPage Page { get; }

    public List<string> ConsoleErrors { get; } = new();
    /// <summary>Network failures. Requests the browser cancels itself while navigating (ERR_ABORTED) are not counted.</summary>
    public List<string> FailedRequests { get; } = new();

    /// <summary>Responses with an HTTP error status (404, 500...).</summary>
    public List<string> BadResponses { get; } = new();
    public List<string> ExternalRequests { get; } = new();

    public static async Task<Tab> OpenAsync(AppFixture app, int width = 1280, int height = 800)
    {
        var context = await app.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = width, Height = height },
            Locale = "es-DO"
        });
        var tab = new Tab(app, context, await context.NewPageAsync());
        tab.Watch();
        return tab;
    }

    private void Watch()
    {
        Page.Console += (_, message) =>
        {
            if (message.Type == "error")
                ConsoleErrors.Add(message.Text);
        };
        Page.PageError += (_, error) => ConsoleErrors.Add("Excepción: " + error);
        Page.RequestFailed += (_, request) =>
        {
            // The browser cancels in-flight requests of the previous page when the test navigates: not a failure.
            if (request.Failure != "net::ERR_ABORTED")
                FailedRequests.Add($"{request.Method} {request.Url} ({request.Failure})");
        };
        Page.Response += (_, response) =>
        {
            if (response.Status >= 400)
                BadResponses.Add($"{response.Status} {response.Request.Method} {response.Url}");
        };
        Page.Request += (_, request) =>
        {
            if (!request.Url.StartsWith(_app.BaseUrl, StringComparison.OrdinalIgnoreCase)
                && !request.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                && !request.Url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase)
                && !request.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                ExternalRequests.Add(request.Url);
        };
    }

    public Task GotoAsync(string path) => Page.GotoAsync(_app.BaseUrl + path);

    /// <summary>Registers a brand-new user through the UI and leaves the browser logged in on the home page.</summary>
    /// <summary>Signs out through the user menu at the bottom of the sidebar.</summary>
    public async Task LogoutAsync()
    {
        var menu = Page.GetByRole(AriaRole.Menu);
        await ClickUntilAsync(Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { NameRegex = new System.Text.RegularExpressions.Regex("^Menú de ") }), menu);
        await Page.RunAndWaitForResponseAsync(
            () => menu.GetByRole(AriaRole.Menuitem, new LocatorGetByRoleOptions { Name = "Cerrar sesión" }).ClickAsync(),
            r => r.Url.Contains("/Account/Logout") && r.Request.Method == "POST");
        await Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/Account/Login"));   // signed out, the home page sends you here
    }

    public async Task<string> RegisterAsync()
    {
        var email = $"e2e-{Guid.NewGuid():N}@test.com";
        const string password = "Passw0rd!x";

        await GotoAsync("/Account/Register");
        await Page.GetByLabel("Correo electrónico").FillAsync(email);
        await Page.GetByLabel("Contraseña", new PageGetByLabelOptions { Exact = true }).FillAsync(password);
        await Page.GetByLabel("Confirmar contraseña").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Registrarse" }).ClickAsync();
        await Page.WaitForURLAsync($"{_app.BaseUrl}/");
        return email;
    }

    /// <summary>
    /// Clicks until <paramref name="expected"/> shows up. A Blazor Server page is rendered by the server first and
    /// only reacts to clicks once its SignalR circuit is connected, so an early click can be lost.
    /// </summary>
    public static async Task ClickUntilAsync(ILocator target, ILocator expected, int attempts = 10)
    {
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            await target.ClickAsync();
            try
            {
                await expected.WaitForAsync(new LocatorWaitForOptions { Timeout = 1500 });
                return;
            }
            catch (Exception ex) when ((ex is TimeoutException || ex is PlaywrightException) && attempt < attempts)
            {
                // the circuit was not ready yet; try again
            }
        }
    }

    /// <summary>Saves a screenshot of the page (or of an element) into the artifacts folder and returns its path.</summary>
    public async Task<string> ShotAsync(string name, ILocator? element = null, bool fullPage = false)
    {
        var path = Path.Combine(_app.ArtifactsDir, name + ".png");
        if (element is null)
            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = fullPage });
        else
        {
            // A page rendered by the server is swapped for the interactive one a moment after it appears; if the
            // element is replaced while it is being captured, capture it again.
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await element.ScreenshotAsync(new LocatorScreenshotOptions { Path = path });
                    break;
                }
                catch (PlaywrightException ex) when (attempt < 3 && ex.Message.Contains("not attached"))
                {
                    await Task.Delay(300);
                }
            }
        }
        return path;
    }

    public async ValueTask DisposeAsync() => await _context.CloseAsync();
}
