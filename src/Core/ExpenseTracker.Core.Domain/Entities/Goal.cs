using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Domain.Entities;

public class Goal : UserOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public GoalPeriod Period { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Bootstrap Icons name chosen by the user from the curated list. Null shows a default icon.</summary>
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
}
