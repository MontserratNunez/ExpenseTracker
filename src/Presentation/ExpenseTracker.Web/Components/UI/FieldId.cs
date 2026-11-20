namespace ExpenseTracker.Web.Components.UI;

/// <summary>Unique ids for form controls that were not given one, so every label can point to its control.</summary>
internal static class FieldId
{
    private static int _next;

    public static string Next(string prefix = "f") => $"{prefix}-{Interlocked.Increment(ref _next)}";
}
