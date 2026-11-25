using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Budget;

/// <summary>Green: estimate up to 90% of the allocation. Yellow: 90-100%. Red: above 100%.</summary>
public enum PlanHealth
{
    Green = 1,
    Yellow = 2,
    Red = 3
}

public class PlanStatusDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    // ----- Summary (base currency) -----

    public decimal Income { get; set; }
    public decimal TotalAssigned { get; set; }

    /// <summary>Income minus total assigned; negative when more than 100% of income is assigned.</summary>
    public decimal Unassigned { get; set; }

    public decimal AssignedPercentOfIncome { get; set; }
    public bool IsOverAssigned { get; set; }

    public decimal TotalSpent { get; set; }
    public decimal TotalPending { get; set; }

    /// <summary>Estimated month-end spending: plan categories (estimate) plus out-of-plan (spent + pending).</summary>
    public decimal TotalEstimated { get; set; }

    public List<CategoryPlanDto> Categories { get; set; } = new();

    /// <summary>Spending in categories that have no allocation ("Fuera del plan").</summary>
    public List<CategoryTotalDto> OutOfPlan { get; set; } = new();

    public decimal OutOfPlanSpent { get; set; }
    public decimal OutOfPlanPending { get; set; }
}

public class CategoryPlanDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = string.Empty;
    public string? CategoryIcon { get; set; }
    public AllocationType AllocationType { get; set; }
    public decimal Value { get; set; }
    public int? IncomeSourceId { get; set; }

    public decimal Assigned { get; set; }
    public decimal Spent { get; set; }
    public decimal Pending { get; set; }

    /// <summary>Assigned minus spent minus pending. Negative when already over the allocation.</summary>
    public decimal Available { get; set; }

    /// <summary>Estimated spending at month end.</summary>
    public decimal Estimated { get; set; }

    /// <summary>Estimated amount above the allocation (0 when within it).</summary>
    public decimal EstimatedOverrun { get; set; }

    /// <summary>Estimated / assigned x 100.</summary>
    public decimal EstimatedPercent { get; set; }

    public PlanHealth Health { get; set; }
}
