namespace ExpenseTracker.Core.Application.DTOs.Incomes;

/// <summary>Monthly income amounts the Plan uses to resolve percentage allocations (base currency).</summary>
public class IncomeBasesDto
{
    public List<IncomeBaseDto> Sources { get; set; } = new();

    /// <summary>Monthly equivalent of all sources that are active today.</summary>
    public decimal RecurringTotal { get; set; }
}

public class IncomeBaseDto
{
    public int SourceId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Monthly equivalent of the source, whether or not it is active today.</summary>
    public decimal MonthlyAmount { get; set; }

    public bool IsCurrent { get; set; }
}
