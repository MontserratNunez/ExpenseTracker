namespace ExpenseTracker.Web.Components.Shared;

/// <summary>One segment of the <see cref="PieChart"/>.</summary>
public class PieSlice
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }

    /// <summary>CSS color, e.g. "#2E9E5B".</summary>
    public string Color { get; set; } = "#9AA0A6";
}
