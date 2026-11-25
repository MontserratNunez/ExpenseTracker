using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Expenses;

/// <summary>One expense found by the quick search (the command palette).</summary>
public class ExpenseSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }

    /// <summary>The day of a one-time expense, or the first occurrence of a recurring one.</summary>
    public DateOnly Date { get; set; }

    public bool IsRecurring { get; set; }

    /// <summary>The month the user should be taken to: the expense's own month, or the current one for a recurring expense.</summary>
    public DateOnly MonthToShow { get; set; }
}
