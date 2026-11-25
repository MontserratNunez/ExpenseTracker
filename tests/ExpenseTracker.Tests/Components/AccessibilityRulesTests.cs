using System.Globalization;
using System.Text.RegularExpressions;

namespace ExpenseTracker.Tests.Components;

/// <summary>
/// Rules of accessibility that can be checked without a browser: the contrast of the color tokens (WCAG AA), where the
/// light gray is allowed, and that every button with only an icon has a name for screen readers.
/// </summary>
public class AccessibilityRulesTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExpenseTracker.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró ExpenseTracker.slnx");
    }

    private static string WebProject => Path.Combine(RepositoryRoot(), "src", "Presentation", "ExpenseTracker.Web");

    // ---------- contrast ----------

    private static Dictionary<string, string> Tokens()
    {
        var css = File.ReadAllText(Path.Combine(WebProject, "wwwroot", "css", "tokens.css"));
        var root = css[css.IndexOf(":root", StringComparison.Ordinal)..];
        var end = root.IndexOf("[data-theme", StringComparison.Ordinal);
        if (end > 0)
            root = root[..end];

        return Regex.Matches(root, @"(--[a-z0-9-]+)\s*:\s*(#[0-9A-Fa-f]{6})\s*;")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);
    }

    private static double Luminance(string hex)
    {
        double Channel(int start)
        {
            var value = int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(1) + 0.7152 * Channel(3) + 0.0722 * Channel(5);
    }

    private static double Contrast(string foreground, string background)
    {
        var a = Luminance(foreground);
        var b = Luminance(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    public static IEnumerable<object[]> TextPairs() => new[]
    {
        // Body text and secondary text on every surface they are drawn on.
        new object[] { "--color-text", "--color-bg" },
        new object[] { "--color-text", "--color-bg-subtle" },
        new object[] { "--color-text", "--color-bg-hover" },
        new object[] { "--color-text", "--color-primary-50" },
        new object[] { "--color-text-muted", "--color-bg" },
        new object[] { "--color-text-muted", "--color-bg-subtle" },
        new object[] { "--color-text-muted", "--color-bg-hover" },
        new object[] { "--color-text-muted", "--color-primary-50" },
        new object[] { "--color-text-muted", "--color-committed-bg" },
        // Links and the blue accent used as text.
        new object[] { "--color-primary-600", "--color-bg" },
        new object[] { "--color-primary-600", "--color-bg-subtle" },
        new object[] { "--color-primary-600", "--color-bg-hover" },
        new object[] { "--color-primary-700", "--color-bg" },
        new object[] { "--color-primary-700", "--color-primary-50" },
        new object[] { "--color-primary-700", "--color-bg-hover" },
        // Text on the solid buttons.
        new object[] { "--color-on-accent", "--color-primary-500" },
        new object[] { "--color-on-accent", "--color-primary-600" },
        new object[] { "--color-on-accent", "--color-spent-strong" },
        // Financial meaning written as text (the soft variants are for backgrounds only).
        new object[] { "--color-available-strong", "--color-bg" },
        new object[] { "--color-available-strong", "--color-available-bg" },
        new object[] { "--color-available-strong", "--color-bg-hover" },
        new object[] { "--color-spent-strong", "--color-bg" },
        new object[] { "--color-spent-strong", "--color-spent-bg" },
        new object[] { "--color-spent-strong", "--color-bg-hover" },
        new object[] { "--color-warning-strong", "--color-bg" },
        new object[] { "--color-warning-strong", "--color-warning-bg" },
        new object[] { "--color-warning-strong", "--color-bg-hover" },
        new object[] { "--color-committed-strong", "--color-bg" },
        new object[] { "--color-committed-strong", "--color-committed-bg" },
    };

    [Theory]
    [MemberData(nameof(TextPairs))]
    public void TextColors_MeetWcagAa_4_5(string foreground, string background)
    {
        var tokens = Tokens();
        var ratio = Contrast(tokens[foreground], tokens[background]);
        Assert.True(ratio >= 4.5, $"{foreground} {tokens[foreground]} sobre {background} {tokens[background]} tiene contraste {ratio:0.00}:1 y debe ser de 4.5:1 como mínimo");
    }

    [Theory]
    [InlineData("--color-primary-500", "--color-bg")]       // borders and focus of the fields, accent fills
    [InlineData("--color-available", "--color-bg")]         // the green of bars and dots
    [InlineData("--color-spent", "--color-bg")]             // the red of bars and dots
    public void GraphicColors_MeetWcagAa_3(string foreground, string background)
    {
        var tokens = Tokens();
        var ratio = Contrast(tokens[foreground], tokens[background]);
        Assert.True(ratio >= 3.0, $"{foreground} sobre {background} tiene contraste {ratio:0.00}:1 y debe ser de 3:1 como mínimo");
    }

    [Fact]
    public void TheLightGray_IsOnlyUsedForPlaceholdersDisabledAndDecoration()
    {
        // --color-text-subtle (#9CA3AF) does not reach 4.5:1 on white, so it can never carry information.
        var allowed = new[] { "::placeholder", ":disabled", ".text-subtle", ".account-divider" };
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(WebProject, "wwwroot", "css"), "*.css").Where(f => !f.EndsWith("tokens.css")))
        {
            foreach (var line in File.ReadAllLines(file))
            {
                if (Regex.IsMatch(line, @"(^|[;{\s])color\s*:\s*var\(--color-text-subtle\)") && !allowed.Any(line.Contains))
                    offenders.Add($"{Path.GetFileName(file)}: {line.Trim()}");
            }
        }

        Assert.True(offenders.Count == 0, "Texto con el gris claro que no cumple el contraste:\n" + string.Join("\n", offenders));
    }

    // ---------- names of icon-only buttons ----------

    [Fact]
    public void EveryAppButtonWithOnlyAnIcon_HasAnAriaLabel()
    {
        var tag = new Regex(@"<AppButton\b(?<attrs>[^>]*?)(?<self>/>|>(?<body>.*?)</AppButton>)", RegexOptions.Singleline | RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(WebProject, "*.razor", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in tag.Matches(text))
            {
                var attrs = match.Groups["attrs"].Value;
                if (!attrs.Contains("Icon="))
                    continue;

                // The visible text is what is left of the body without markup or Razor expressions' tags.
                var body = Regex.Replace(match.Groups["body"].Value, "<[^>]+>", string.Empty).Trim();
                var hasText = body.Length > 0;
                if (!hasText && !attrs.Contains("AriaLabel="))
                    offenders.Add($"{Path.GetRelativePath(WebProject, file)}: {Regex.Replace(match.Value, @"\s+", " ").Trim()}");
            }
        }

        Assert.True(offenders.Count == 0, "Botones de solo ícono sin AriaLabel:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryRawButtonOrLink_ThatHasNoText_HasAnAccessibleName()
    {
        // <button ...><AppIcon .../></button> and the like must carry aria-label or a hidden text.
        var tag = new Regex(@"<(?<tag>button|a)\b(?<attrs>[^>]*)>(?<body>.*?)</\k<tag>>", RegexOptions.Singleline | RegexOptions.Compiled);
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(WebProject, "*.razor", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in tag.Matches(text))
            {
                var attrs = match.Groups["attrs"].Value;
                var body = match.Groups["body"].Value;
                var visible = Regex.Replace(Regex.Replace(body, @"<AppIcon[^>]*/>", string.Empty), "<[^>]+>", string.Empty).Trim();
                var named = attrs.Contains("aria-label") || attrs.Contains("title=") || body.Contains("sr-only") || body.Contains("Label=")
                            || visible.Length > 0 || body.Contains("@");
                if (!named)
                    offenders.Add($"{Path.GetRelativePath(WebProject, file)}: {Regex.Replace(match.Value, @"\s+", " ").Trim()}");
            }
        }

        Assert.True(offenders.Count == 0, "Botones o enlaces sin nombre accesible:\n" + string.Join("\n", offenders));
    }
}
