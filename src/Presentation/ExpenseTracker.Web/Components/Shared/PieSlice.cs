namespace ExpenseTracker.Web.Components.Shared;

/// <summary>One segment of the <see cref="PieChart"/>.</summary>
public class PieSlice
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }

    /// <summary>CSS color. Use a design token, e.g. "var(--color-available)".</summary>
    public string Color { get; set; } = "var(--color-committed)";
}
