using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Incomes;

/// <summary>New values of a source: a change from now on, or a correction of its history.</summary>
public class IncomeRevisionInput
{
    /// <summary>Any day of the first month the values apply to.</summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>History corrections only: last month they apply to. Null means up to the next change.</summary>
    public DateOnly? Until { get; set; }

    public decimal Amount { get; set; }
    public Currency Currency { get; set; } = Currency.DOP;
    public Frequency Frequency { get; set; } = Frequency.Monthly;
    public string? Note { get; set; }
}

/// <summary>The past months whose income would change.</summary>
public class PastImpactDto
{
    public List<DateOnly> Months { get; set; } = new();

    /// <summary>Warning to show before saving; null when no past month changes.</summary>
    public string? Warning { get; set; }
}
