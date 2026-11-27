using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Core.Domain.Enums;

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

    // ----- Savings reserve: the minimum the user does not want to spend -----

    public AllocationType SavingsType { get; set; } = AllocationType.FixedAmount;

    /// <summary>An amount or a percentage of income, by <see cref="SavingsType"/>. Zero means no reserve.</summary>
    public decimal SavingsValue { get; set; }

    /// <summary>For percentages: the source to take it from. Null means all recurring income.</summary>
    public int? SavingsIncomeSourceId { get; set; }

    /// <summary>Warn when less than this percentage of the spendable money is left.</summary>
    public decimal AlertThresholdPercent { get; set; } = 15m;

    public bool AppliesTo(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return EffectiveFrom <= first && (EffectiveTo is null || EffectiveTo >= first);
    }
}
