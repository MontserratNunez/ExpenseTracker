using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Domain.Entities;

public class IncomeSourceRevision : UserOwnedEntity
{
    public int IncomeSourceId { get; set; }

    /// <summary>Always day 1: a change applies to whole months.</summary>
    public DateOnly EffectiveFrom { get; set; }

    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public Frequency Frequency { get; set; } = Frequency.Monthly;
    public string? Note { get; set; }
}
