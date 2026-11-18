using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Domain.Entities;

public class Expense : UserOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public decimal AmountInBase { get; set; }
    public ExpenseType Type { get; set; } = ExpenseType.OneTime;

    /// <summary>Date of a one-time expense, or first occurrence of a recurring one.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Required when <see cref="Type"/> is Recurring.</summary>
    public Frequency? Frequency { get; set; }

    public DateOnly? EndDate { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
