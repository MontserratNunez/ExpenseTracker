using Bunit;
using ExpenseTracker.Web.Components.Pages.Dev;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace ExpenseTracker.Tests.Components;

/// <summary>
/// Renders the whole /dev/ui catalog. It exercises every design-system component in every variant, so a
/// component that throws when rendered (a missing parameter, a field outside an EditForm) fails here.
/// </summary>
public class CatalogSmokeTests : UiTestContext
{
    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public FakeEnvironment(string name) => EnvironmentName = name;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "ExpenseTracker.Web";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void InDevelopment_RendersEverySectionWithoutErrors()
    {
        Services.AddSingleton<IWebHostEnvironment>(new FakeEnvironment("Development"));

        var cut = Render<UiCatalog>();

        var headings = cut.FindAll("section h2").Select(h => h.TextContent.Trim()).ToList();
        Assert.Contains("Paleta", headings);
        Assert.Contains("Tipografía", headings);
        Assert.Contains("Íconos", headings);
        Assert.Contains("AppButton", headings);
        Assert.Contains("Badge", headings);
        Assert.Contains("CategoryChip", headings);
        Assert.Contains("AppCard", headings);
        Assert.Contains("StatCard", headings);
        Assert.Contains("PageHeader", headings);
        Assert.Contains("Formularios", headings);
    }

    [Fact]
    public void InProduction_ShowsNotAvailable()
    {
        Services.AddSingleton<IWebHostEnvironment>(new FakeEnvironment("Production"));

        var cut = Render<UiCatalog>();

        Assert.Equal("No disponible.", cut.Find("p").TextContent);
        Assert.Empty(cut.FindAll("section"));
    }

    [Fact]
    public void TheExampleForm_ValidatesOnDemand_AndShowsMessagesUnderTheFields()
    {
        Services.AddSingleton<IWebHostEnvironment>(new FakeEnvironment("Development"));
        var cut = Render<UiCatalog>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Validar ahora")).Click();

        cut.WaitForAssertion(() =>
        {
            var messages = cut.FindAll(".validation-message").Select(m => m.TextContent.Trim()).ToList();
            Assert.Contains("El nombre es obligatorio.", messages);
            Assert.Contains("El monto debe ser mayor que cero.", messages);
        });
    }
}
