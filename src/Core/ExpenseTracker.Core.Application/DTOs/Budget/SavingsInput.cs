using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Budget;

/// <summary>The savings reserve of a plan, as it is edited.</summary>
public class SavingsInput
{
    public AllocationType Type { get; set; } = AllocationType.FixedAmount;

    /// <summary>An amount or a percentage from 0 to 100. Zero means no reserve.</summary>
    public decimal Value { get; set; }

    /// <summary>For percentages: the source to take it from. Null means all recurring income.</summary>
    public int? IncomeSourceId { get; set; }

    public decimal AlertThresholdPercent { get; set; } = 15m;
}
