using Microsoft.Playwright;

namespace ExpenseTracker.E2E;

[Collection(E2ECollection.Name)]
public class CatalogTests
{
    private readonly AppFixture _app;

    public CatalogTests(AppFixture app) => _app = app;

    [E2EFact]
    public async Task Catalog_LoadsInter_AndTheIcons_FromThisSiteOnly_WithoutErrors()
    {
        await using var tab = await Tab.OpenAsync(_app);
        await tab.RegisterAsync();

        await tab.GotoAsync("/dev/ui");
        await tab.Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Catálogo de componentes" }).WaitForAsync();

        // The fonts are only downloaded when something uses them: ask the browser to load and check them.
        var inter = await tab.Page.EvaluateAsync<bool>("async () => { await document.fonts.load('15px Inter'); return document.fonts.check('15px Inter'); }");
        var icons = await tab.Page.EvaluateAsync<bool>("async () => { await document.fonts.load('16px bootstrap-icons'); return document.fonts.check('16px bootstrap-icons'); }");
        var bodyFont = await tab.Page.EvaluateAsync<string>("() => getComputedStyle(document.body).fontFamily");

        Assert.True(inter, "Inter no se cargó");
        Assert.True(icons, "Bootstrap Icons no se cargó");
        Assert.StartsWith("Inter", bodyFont.Trim('"', ' '));
        Assert.True(tab.ExternalRequests.Count == 0, "Peticiones a otros sitios: " + string.Join(" | ", tab.ExternalRequests));
        Assert.True(tab.FailedRequests.Count == 0, "Peticiones fallidas: " + string.Join(" | ", tab.FailedRequests));
        Assert.True(tab.BadResponses.Count == 0, "Respuestas con error HTTP: " + string.Join(" | ", tab.BadResponses));
        Assert.True(tab.ConsoleErrors.Count == 0, "Errores de consola: " + string.Join(" | ", tab.ConsoleErrors));
    }

    [E2EFact]
    public async Task Catalog_Screenshots()
    {
        await using var tab = await Tab.OpenAsync(_app, width: 1280, height: 900);
        await tab.RegisterAsync();
        await tab.GotoAsync("/dev/ui");
        await tab.Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Catálogo de componentes" }).WaitForAsync();
        await tab.Page.EvaluateAsync("async () => { await document.fonts.ready; }");

        await tab.ShotAsync("catalogo-completo", fullPage: true);

        foreach (var section in new[] { "colors", "type", "buttons", "badges", "chips", "cards", "stats", "header", "forms", "icons", "feedback" })
        {
            var locator = tab.Page.Locator($"section[aria-labelledby=catalog-{section}]");
            if (await locator.CountAsync() > 0)
                await tab.ShotAsync($"catalogo-{section}", locator);
        }

        await using var phone = await Tab.OpenAsync(_app, width: 390, height: 844);
        await phone.RegisterAsync();
        await phone.GotoAsync("/dev/ui");
        await phone.Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Catálogo de componentes" }).WaitForAsync();
        await phone.Page.EvaluateAsync("async () => { await document.fonts.ready; }");
        await phone.ShotAsync("catalogo-movil", fullPage: true);

        Assert.True(File.Exists(Path.Combine(_app.ArtifactsDir, "catalogo-completo.png")));
    }
}
