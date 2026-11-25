using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Web.Components.UI;

namespace ExpenseTracker.Web.Services;

/// <summary>
/// How a plan category is drawn on a <see cref="ProgressBar"/>: spent (solid), pending (striped), a marker for the
/// month-end estimate and a dashed line for the assigned limit, all on one scale. Shared by Inicio and Plan.
/// </summary>
public static class PlanBars
{
    // The bar spans the largest of the allocation and the amounts shown, so an overrun still fits.
    private static decimal Scale(CategoryPlanDto c) => Math.Max(Math.Max(c.Assigned, c.Spent + c.Pending), c.Estimated);

    private static decimal Share(CategoryPlanDto c, decimal value)
        => Scale(c) <= 0 ? 0m : Math.Clamp(value / Scale(c) * 100m, 0m, 100m);

    public static IReadOnlyList<ProgressSegment> Segments(CategoryPlanDto c) => new[]
    {
        new ProgressSegment { Value = Share(c, c.Spent), ColorVar = HealthVar(c.Health) },
        new ProgressSegment { Value = Share(c, c.Pending), ColorVar = "--color-committed", Striped = true }
    };

    public static decimal? EstimateMarker(CategoryPlanDto c) => c.Estimated > 0 ? Share(c, c.Estimated) : null;

    /// <summary>The assigned limit, only drawn when the amounts go past it (otherwise it is the end of the bar).</summary>
    public static decimal? LimitMarker(CategoryPlanDto c) => c.Assigned > 0 && Scale(c) > c.Assigned ? Share(c, c.Assigned) : null;

    public static string Label(CategoryPlanDto c)
        => $"{c.CategoryName}: gastado {c.Spent:N2}, pendiente {c.Pending:N2}, estimado {c.Estimated:N2} de {c.Assigned:N2} asignados";

    public static string HealthVar(PlanHealth health) => health switch
    {
        PlanHealth.Red => "--color-spent",
        PlanHealth.Yellow => "--color-warning",
        _ => "--color-available"
    };

    public static string HealthIcon(PlanHealth health) => health switch
    {
        PlanHealth.Red => Icons.Exceeded,
        PlanHealth.Yellow => Icons.AtRisk,
        _ => Icons.OnTrack
    };

    public static string HealthClass(PlanHealth health) => health switch
    {
        PlanHealth.Red => "meter__state--over",
        PlanHealth.Yellow => "meter__state--risk",
        _ => "meter__state--ok"
    };

    public static string HealthText(PlanHealth health) => health switch
    {
        PlanHealth.Red => "Excedido",
        PlanHealth.Yellow => "Cerca del límite",
        _ => "En plan"
    };
}
