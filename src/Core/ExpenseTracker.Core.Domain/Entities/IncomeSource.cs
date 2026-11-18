using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Domain.Entities;

public class IncomeSource : UserOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public Frequency Frequency { get; set; } = Frequency.Monthly;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}
