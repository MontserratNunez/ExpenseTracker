namespace ExpenseTracker.Core.Application.DTOs.Incomes;

/// <summary>Monthly income amounts of one month, used to resolve percentage allocations (base currency).</summary>
public class IncomeBasesDto
{
    public List<IncomeBaseDto> Sources { get; set; } = new();

    /// <summary>Monthly equivalent of all sources with income in that month.</summary>
    public decimal RecurringTotal { get; set; }
}

public class IncomeBaseDto
{
    public int SourceId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Monthly equivalent of the source in that month (0 when it had no income).</summary>
    public decimal MonthlyAmount { get; set; }

    public bool HasIncome { get; set; }
}
