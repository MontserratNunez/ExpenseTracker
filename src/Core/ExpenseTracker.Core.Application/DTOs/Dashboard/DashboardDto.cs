using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Application.DTOs.Goals;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Dashboard;

public class DashboardDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    // ----- Month at a glance (rule 5.3, base currency) -----

    /// <summary>Income of the month. The pie chart total.</summary>
    public decimal Income { get; set; }

    /// <summary>Money that already left (red).</summary>
    public decimal Spent { get; set; }

    /// <summary>Pending expenses and installments of the month (gray).</summary>
    public decimal Committed { get; set; }

    /// <summary>Income minus spent minus committed, never below 0 (green).</summary>
    public decimal Available { get; set; }

    public bool IsDeficit { get; set; }

    /// <summary>How much spent + committed exceeds the income (0 when there is no deficit).</summary>
    public decimal DeficitAmount { get; set; }

    /// <summary>False for a month without income: the page shows the empty state.</summary>
    public bool HasIncome => Income > 0;

    // ----- Sections -----

    public List<UpcomingPaymentDto> UpcomingPayments { get; set; } = new();

    /// <summary>The (up to) 3 plan categories closest to exceeding their allocation.</summary>
    public List<CategoryPlanDto> PlanHighlights { get; set; } = new();

    public bool HasPlan { get; set; }

    public List<DashboardGoalDto> Goals { get; set; } = new();

    /// <summary>Estimated expenses for next month (from the forecast).</summary>
    public decimal NextMonthEstimate { get; set; }
}

public class UpcomingPaymentDto
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Amount in the base currency.</summary>
    public decimal Amount { get; set; }

    public decimal OriginalAmount { get; set; }
    public Currency Currency { get; set; }
    public bool IsDebt { get; set; }

    /// <summary>A debt installment whose due date already passed without a payment.</summary>
    public bool IsOverdue { get; set; }

    /// <summary>Debt id for installments, so the page can link to it.</summary>
    public int? DebtId { get; set; }
}

public class DashboardGoalDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal ProgressPercent { get; set; }
    public GoalState State { get; set; }
}
