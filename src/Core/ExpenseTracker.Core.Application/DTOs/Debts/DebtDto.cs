using ExpenseTracker.Core.Application.Helpers;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Debts;

public class DebtDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DebtType DebtType { get; set; }
    public Currency Currency { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal AnnualInterestRate { get; set; }
    public decimal InitialPaidAmount { get; set; }
    public decimal MonthlyPayment { get; set; }
    public int PaymentDay { get; set; }
    public DateOnly StartDate { get; set; }
    public int? TermMonths { get; set; }
    public bool IsClosed { get; set; }

    // ----- Calculated -----

    public decimal RemainingBalance { get; set; }
    public decimal RemainingBalanceInBase { get; set; }

    /// <summary>Original amount minus remaining balance.</summary>
    public decimal PaidAmount { get; set; }

    public decimal PaidPercent { get; set; }

    /// <summary>Date of the next pending installment, or null when settled.</summary>
    public DateOnly? NextPaymentDate { get; set; }

    /// <summary>Null when the debt is settled or the installment never pays it off.</summary>
    public DateOnly? EstimatedPayoffDate { get; set; }

    /// <summary>Installment for the payment form: the usual one, or less when the balance is smaller.</summary>
    public decimal SuggestedPayment { get; set; }
}

public class DebtPaymentDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public decimal InterestPortion { get; set; }
    public decimal PrincipalPortion { get; set; }
    public string? Notes { get; set; }
}

public class DebtDetailDto
{
    public DebtDto Debt { get; set; } = new();
    public List<DebtPaymentDto> Payments { get; set; } = new();
}

public class AmortizationDto
{
    public List<AmortizationRow> Rows { get; set; } = new();

    /// <summary>False when the installment does not cover the interest.</summary>
    public bool PaysOff { get; set; }

    /// <summary>True when the table was cut because the debt never pays off.</summary>
    public bool Truncated { get; set; }
}
