using System.Text.RegularExpressions;

namespace ExpenseTracker.Tests.Components;

/// <summary>
/// Bootstrap's CSS is gone (step 15 of phase 14). These tests keep it that way: no page may use its classes and
/// the app must not link or ship it, because without the stylesheet such a class would silently do nothing.
/// </summary>
public class NoBootstrapTests
{
    private static readonly string[] BannedClasses =
    {
        "btn", "btn-primary", "btn-secondary", "btn-danger", "btn-outline-primary", "btn-outline-secondary", "btn-outline-danger",
        "btn-link", "btn-sm", "btn-lg", "card", "card-body", "card-header", "card-footer", "form-control", "form-select",
        "form-label", "form-text", "form-floating", "form-check", "form-check-input", "form-check-label", "form-horizontal",
        "table", "table-responsive", "table-sm", "modal", "alert", "alert-info", "alert-warning", "alert-danger", "alert-success",
        "alert-light", "row", "col", "col-12", "col-md-4", "col-md-6", "col-lg-4", "col-lg-6", "col-xl-6", "d-flex", "d-grid",
        "d-inline-block", "d-none", "justify-content-between", "align-items-center", "align-items-start", "flex-wrap",
        "flex-column", "gap-1", "gap-2", "gap-3", "mb-0", "mb-1", "mb-2", "mb-3", "mb-4", "mt-1", "mt-2", "mt-3", "mt-4", "me-1", "me-2",
        "ms-1", "ms-2", "mx-auto", "p-2", "p-3", "py-2", "py-3", "w-100", "h-100", "fw-bold", "fw-semibold", "fs-5", "small",
        "text-end", "text-start", "text-center", "text-nowrap", "text-danger", "text-success", "text-secondary", "text-info",
        "text-warning", "text-bg-success", "text-bg-danger", "text-bg-secondary", "text-bg-light", "badge", "list-group",
        "list-group-item", "progress", "progress-bar", "nav-link", "nav-item", "nav-pills", "nav-tabs", "rounded", "rounded-circle",
        "border", "bg-light", "visually-hidden", "input-group", "checkbox"
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExpenseTracker.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró ExpenseTracker.slnx");
    }

    private static string WebProject => Path.Combine(RepositoryRoot(), "src", "Presentation", "ExpenseTracker.Web");

    [Fact]
    public void NoRazorFile_UsesABootstrapClass()
    {
        var banned = new HashSet<string>(BannedClasses);
        var classAttribute = new Regex("class=\"([^\"]*)\"", RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(WebProject, "*.razor", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                              && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in classAttribute.Matches(text))
            {
                // Only the fixed words count: drop what Razor computes (@(...) and @Name).
                var fixedPart = Regex.Replace(match.Groups[1].Value, @"@\([^)]*\)|@[A-Za-z_.]+", " ");
                foreach (var token in fixedPart.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (banned.Contains(token))
                        offenders.Add($"{Path.GetRelativePath(WebProject, file)}: {token}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Clases de Bootstrap que quedan: " + string.Join("; ", offenders.Distinct()));
    }

    [Fact]
    public void Bootstrap_IsNeitherLinkedNorShipped()
    {
        var app = File.ReadAllText(Path.Combine(WebProject, "Components", "App.razor"));
        Assert.DoesNotContain("lib/bootstrap/", app);
        Assert.DoesNotContain("bootstrap.min.css", app);
        Assert.False(Directory.Exists(Path.Combine(WebProject, "wwwroot", "lib", "bootstrap")), "wwwroot/lib/bootstrap debe estar borrada");
        Assert.True(Directory.Exists(Path.Combine(WebProject, "wwwroot", "lib", "bootstrap-icons")), "Bootstrap Icons se mantiene");
        Assert.False(File.Exists(Path.Combine(WebProject, "wwwroot", "app.css")), "app.css de la plantilla debe estar borrado");
    }

    [Fact]
    public void OurStylesheets_DoNotDependOnBootstrapVariables()
    {
        var css = Directory.EnumerateFiles(Path.Combine(WebProject, "wwwroot", "css"), "*.css")
            .Concat(Directory.EnumerateFiles(Path.Combine(WebProject, "Components"), "*.razor.css", SearchOption.AllDirectories));

        foreach (var file in css)
            Assert.DoesNotContain("--bs-", File.ReadAllText(file));
    }
}
