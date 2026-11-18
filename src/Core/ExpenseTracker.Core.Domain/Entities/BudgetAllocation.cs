using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Domain.Entities;

public class BudgetAllocation : UserOwnedEntity
{
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public AllocationType AllocationType { get; set; }

    /// <summary>An amount (FixedAmount) or a percentage (PercentageOfIncome).</summary>
    public decimal Value { get; set; }

    /// <summary>For percentages: the source to take the percentage from. Null means total recurring income.</summary>
    public int? IncomeSourceId { get; set; }
    public IncomeSource? IncomeSource { get; set; }
}
