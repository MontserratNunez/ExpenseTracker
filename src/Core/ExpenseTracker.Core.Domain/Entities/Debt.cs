using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Domain.Entities;

public class Debt : UserOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public DebtType DebtType { get; set; }
    public decimal OriginalAmount { get; set; }

    /// <summary>Annual interest rate as a percentage (5 means 5%).</summary>
    public decimal AnnualInterestRate { get; set; }

    /// <summary>Amount already paid before using the app.</summary>
    public decimal InitialPaidAmount { get; set; }

    public decimal MonthlyPayment { get; set; }

    /// <summary>Day of the month the installment is due (1-28).</summary>
    public int PaymentDay { get; set; }

    public DateOnly StartDate { get; set; }
    public int? TermMonths { get; set; }
    public Currency Currency { get; set; }
    public bool IsClosed { get; set; }
    public List<DebtPayment> Payments { get; set; } = new();

    public decimal GetRemainingBalance()
        => OriginalAmount - InitialPaidAmount - Payments.Sum(p => p.PrincipalPortion);

    public decimal GetMonthlyInterest(decimal balance)
        => balance * AnnualInterestRate / 100m / 12m;
}
