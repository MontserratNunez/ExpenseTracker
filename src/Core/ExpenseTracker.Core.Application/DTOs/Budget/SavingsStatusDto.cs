namespace ExpenseTracker.Core.Application.DTOs.Budget;

public enum SavingsState
{
    /// <summary>Neither income nor spending: nothing to evaluate.</summary>
    NoData = 0,
    OnPlan = 1,
    NearSavings = 2,
    UsingSavings = 3,
    Deficit = 4
}

/// <summary>The month against the savings reserve (base currency).</summary>
public class SavingsStatusDto
{
    public decimal Income { get; set; }
    public decimal Reserve { get; set; }

    /// <summary>Income minus reserve.</summary>
    public decimal Spendable { get; set; }

    public decimal Used { get; set; }
    public decimal Pending { get; set; }

    /// <summary>What can still be spent without touching the savings; negative when already touching them.</summary>
    public decimal Margin { get; set; }

    /// <summary>Income minus used and pending.</summary>
    public decimal RealSavings { get; set; }

    /// <summary>Savings above the reserve.</summary>
    public decimal Extra { get; set; }

    /// <summary>How much of the reserve is already consumed.</summary>
    public decimal UsedSavings { get; set; }

    /// <summary>Savings expected at month end at the current pace; the real ones for a past month.</summary>
    public decimal Projection { get; set; }

    /// <summary>How much of the reserve the current pace would use, while there is still margin today.</summary>
    public decimal? ProjectedSavingsUse { get; set; }

    public SavingsState State { get; set; }

    /// <summary>The month is over, so the reserve was met or not.</summary>
    public bool IsClosed { get; set; }

    public bool IsMet => RealSavings >= Reserve;

    /// <summary>What was missing to meet the reserve.</summary>
    public decimal Shortfall => Math.Max(0m, Reserve - RealSavings);
}

/// <summary>What saving an expense does to the month's savings.</summary>
public class SavingsImpactDto
{
    /// <summary>Asked before saving, when the expense starts using the savings.</summary>
    public string? Warning { get; set; }

    /// <summary>Added to the "saved" message.</summary>
    public string? Effect { get; set; }
}
