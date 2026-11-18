using ExpenseTracker.Core.Domain.Common;

namespace ExpenseTracker.Core.Domain.Entities;

public class DebtPayment : UserOwnedEntity
{
    public int DebtId { get; set; }
    public Debt? Debt { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public decimal InterestPortion { get; set; }
    public decimal PrincipalPortion { get; set; }
    public string? Notes { get; set; }
}
