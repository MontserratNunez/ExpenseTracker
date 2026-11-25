namespace ExpenseTracker.Web.Services;

/// <summary>Remembers, per browser session (circuit), which charts already played their entrance animation.</summary>
public class ChartAnimationState
{
    private readonly HashSet<string> _played = new();

    /// <summary>True the first time it is called for a chart name; false afterwards.</summary>
    public bool ShouldAnimate(string chart) => _played.Add(chart);
}
