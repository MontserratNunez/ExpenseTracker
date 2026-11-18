using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Incomes;

public class MonthlyIncomeDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>Total income of the month, in the base currency.</summary>
    public decimal Total { get; set; }

    public decimal RecurringTotal { get; set; }
    public decimal OneTimeTotal { get; set; }

    public List<IncomeLineDto> Lines { get; set; } = new();
}

public class IncomeLineDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsRecurring { get; set; }

    /// <summary>Id of the IncomeSource or OneTimeIncome this line comes from.</summary>
    public int SourceId { get; set; }

    /// <summary>Amount of a single occurrence, in its original currency.</summary>
    public decimal Amount { get; set; }

    public Currency Currency { get; set; }

    /// <summary>How many times it occurs in the month (always 1 for one-time income).</summary>
    public int Occurrences { get; set; }

    /// <summary>Amount of the line (all occurrences) in the base currency.</summary>
    public decimal AmountInBase { get; set; }
}
