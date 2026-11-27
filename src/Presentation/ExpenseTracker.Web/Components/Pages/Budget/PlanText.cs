using ExpenseTracker.Web.Services;

namespace ExpenseTracker.Web.Components.Pages.Budget;

public static class PlanText
{
    /// <summary>The validity of a plan: "desde ene 2027", "ene 2027 a dic 2027" or "solo nov 2026".</summary>
    public static string Period(DateOnly from, DateOnly? to)
    {
        if (to is not { } end)
            return $"desde {UiLabels.MonthYear(from)}";

        return from.Year == end.Year && from.Month == end.Month
            ? $"solo {UiLabels.MonthYear(from)}"
            : $"{UiLabels.MonthYear(from)} a {UiLabels.MonthYear(end)}";
    }
}
