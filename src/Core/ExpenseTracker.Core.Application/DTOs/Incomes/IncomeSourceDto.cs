using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Incomes;

public class IncomeSourceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Values in force this month (or the first ones, for a source that has not started).
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public Frequency Frequency { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; }

    /// <summary>The first change scheduled after the values shown, if any.</summary>
    public IncomeRevisionDto? NextRevision { get; set; }

    /// <summary>History of the source, oldest first.</summary>
    public List<IncomeRevisionDto> Revisions { get; set; } = new();
}

public class IncomeRevisionDto
{
    public DateOnly EffectiveFrom { get; set; }
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public Frequency Frequency { get; set; }
    public string? Note { get; set; }
}

public class IncomeOccurrenceDto
{
    public int SourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public decimal AmountInBase { get; set; }
}
