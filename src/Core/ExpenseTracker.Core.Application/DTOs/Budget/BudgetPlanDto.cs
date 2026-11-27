namespace ExpenseTracker.Core.Application.DTOs.Budget;

public class BudgetPlanDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>Starts after the current month: it can be edited freely and deleted.</summary>
    public bool IsFuture { get; set; }

    /// <summary>Ended before the current month: it cannot change.</summary>
    public bool IsPast { get; set; }

    /// <summary>Applies to the current month.</summary>
    public bool IsCurrent => !IsFuture && !IsPast;

    /// <summary>Started before the current month and is still running: editing it creates a new version.</summary>
    public bool EditCreatesVersion { get; set; }
}

/// <summary>What saving a period would do to the other plans, shown before saving.</summary>
public class PlanPeriodPreview
{
    /// <summary>An open plan that will be closed.</summary>
    public string? Notice { get; set; }

    /// <summary>Why the period cannot be saved as it is. Null when there is no overlap.</summary>
    public string? Conflict { get; set; }

    /// <summary>Last month the plan could be cut to so the conflict disappears, when there is one.</summary>
    public DateOnly? SuggestedEnd { get; set; }
}
