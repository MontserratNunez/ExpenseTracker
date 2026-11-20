using ExpenseTracker.Web.Components.UI;

namespace ExpenseTracker.Web.Components.Layout;

/// <summary>One destination of the main navigation.</summary>
public sealed class NavItem
{
    public NavItem(string href, string text, string icon, string? activeIcon = null)
    {
        Href = href;
        Text = text;
        Icon = icon;
        ActiveIcon = activeIcon ?? icon;
    }

    /// <summary>Relative to the site root, without a leading slash. Empty for the home page.</summary>
    public string Href { get; }

    public string Text { get; }
    public string Icon { get; }

    /// <summary>Filled variant of the icon, used while the destination is the current page (same icon when there is none).</summary>
    public string ActiveIcon { get; }

    /// <summary>True when <paramref name="path"/> (relative, no query or fragment) is this destination or inside it.</summary>
    public bool IsCurrent(string path)
        => Href.Length == 0
            ? path.Length == 0
            : path == Href || path.StartsWith(Href + "/", StringComparison.Ordinal);
}

/// <summary>The destinations shown in the sidebar and in the bottom navigation, in order.</summary>
public static class NavItems
{
    public static readonly NavItem Home = new("", "Inicio", Icons.Home, Icons.HomeActive);
    public static readonly NavItem Incomes = new("ingresos", "Ingresos", Icons.Incomes);
    public static readonly NavItem Expenses = new("gastos", "Gastos", Icons.Expenses);
    public static readonly NavItem Debts = new("deudas", "Deudas", Icons.Debts);
    public static readonly NavItem Budget = new("plan", "Plan", Icons.Budget, Icons.BudgetActive);
    public static readonly NavItem Goals = new("metas", "Metas", Icons.Goals);
    public static readonly NavItem Forecast = new("estimaciones", "Estimaciones", Icons.Forecast);
    public static readonly NavItem Settings = new("configuracion", "Configuración", Icons.Settings, Icons.SettingsActive);

    /// <summary>The main list of the sidebar (Settings sits apart, at the bottom).</summary>
    public static readonly IReadOnlyList<NavItem> Main = new[] { Home, Incomes, Expenses, Debts, Budget, Goals, Forecast };

    /// <summary>What the "More" sheet of the bottom navigation lists.</summary>
    public static readonly IReadOnlyList<NavItem> More = new[] { Incomes, Debts, Goals, Forecast, Settings };
}
