using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Domain.Entities;

public class OneTimeIncome : UserOwnedEntity
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public decimal AmountInBase { get; set; }
    public DateOnly Date { get; set; }
}
