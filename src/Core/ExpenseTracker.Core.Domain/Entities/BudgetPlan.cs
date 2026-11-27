using ExpenseTracker.Core.Domain.Common;

namespace ExpenseTracker.Core.Domain.Entities;

/// <summary>The allocations that rule a range of whole months. Two plans never cover the same month.</summary>
public class BudgetPlan : UserOwnedEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>First day of the first month the plan applies to.</summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Last day of the last month the plan applies to, or null when it has no end.</summary>
    public DateOnly? EffectiveTo { get; set; }

    public List<BudgetAllocation> Allocations { get; set; } = new();

    public bool AppliesTo(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return EffectiveFrom <= first && (EffectiveTo is null || EffectiveTo >= first);
    }
}
