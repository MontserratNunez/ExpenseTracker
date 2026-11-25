using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Goals;

public class GoalDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public GoalPeriod Period { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? Icon { get; set; }
}

public enum GoalState
{
    /// <summary>The projection reaches the target.</summary>
    OnTrack = 1,

    /// <summary>The projection falls short of the target.</summary>
    Behind = 2,

    /// <summary>Progress already reached the target.</summary>
    Completed = 3,

    /// <summary>The period has not started yet.</summary>
    NotStarted = 4,

    /// <summary>The period is over and the target was not reached.</summary>
    Missed = 5
}

public class GoalStatusDto
{
    public GoalDto Goal { get; set; } = new();

    /// <summary>Spent on the linked category within the goal period (base currency).</summary>
    public decimal Progress { get; set; }

    public decimal ProgressPercent { get; set; }

    /// <summary>Target minus progress (0 once reached).</summary>
    public decimal Remaining { get; set; }

    /// <summary>Whole months from today to the end of the period (at least 1).</summary>
    public int RemainingMonths { get; set; }

    /// <summary>What must be contributed each remaining month to reach the target.</summary>
    public decimal RequiredMonthly { get; set; }

    /// <summary>Average contributed per elapsed month so far.</summary>
    public decimal AverageMonthly { get; set; }

    /// <summary>Progress plus the average monthly contribution over the remaining months.</summary>
    public decimal Projection { get; set; }

    /// <summary>How much the projection misses the target by (0 when on track).</summary>
    public decimal Shortfall { get; set; }

    public GoalState State { get; set; }

    /// <summary>Monthly amount the Plan assigns to the goal's category, or null if it has no allocation.</summary>
    public decimal? PlannedMonthly { get; set; }

    /// <summary>True when the goal needs more per month than the Plan assigns to its category.</summary>
    public bool PlanBelowRequired { get; set; }
}
