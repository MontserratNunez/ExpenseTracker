using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Budget;

/// <summary>Everything the "Edit plan" table needs: one row per category plus the income bases.</summary>
public class AllocationEditorDto
{
    public List<AllocationRowDto> Rows { get; set; } = new();
    public IncomeBasesDto IncomeBases { get; set; } = new();
}

public class AllocationRowDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public AllocationType AllocationType { get; set; } = AllocationType.FixedAmount;

    /// <summary>Zero means the category has no allocation.</summary>
    public decimal Value { get; set; }

    public int? IncomeSourceId { get; set; }
}
