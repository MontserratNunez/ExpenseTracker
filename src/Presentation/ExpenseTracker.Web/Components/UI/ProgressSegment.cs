namespace ExpenseTracker.Web.Components.UI;

/// <summary>One colored part of a <see cref="ProgressBar"/>.</summary>
public class ProgressSegment
{
    /// <summary>Share of the whole bar, from 0 to 100.</summary>
    public decimal Value { get; set; }

    /// <summary>Name of a color token, for example "--color-available".</summary>
    public string ColorVar { get; set; } = "--color-available";

    /// <summary>Draws the segment striped (used for amounts that are still pending).</summary>
    public bool Striped { get; set; }
}
