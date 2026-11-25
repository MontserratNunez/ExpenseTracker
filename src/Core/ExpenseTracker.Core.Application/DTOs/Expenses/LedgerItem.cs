using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Expenses;

/// <summary>Spent: money that already left. Pending: committed for a later date of the month.</summary>
public enum LedgerStatus
{
    Spent = 1,
    Pending = 2
}

public enum LedgerSource
{
    OneTimeExpense = 1,
    RecurringExpense = 2,
    DebtPayment = 3,
    DebtInstallment = 4
}

/// <summary>One movement of a month (rule 5.2). Used by Expenses, Dashboard, Plan and Forecast.</summary>
public class LedgerItem
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public LedgerSource Source { get; set; }
    public LedgerStatus Status { get; set; }

    /// <summary>Id of the Expense (or the Debt for debt items) this item comes from.</summary>
    public int SourceId { get; set; }

    /// <summary>Null for virtual categories such as "Deudas".</summary>
    public int? CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = "#6C757D";

    /// <summary>Bootstrap Icons name of the category. Null when the category has none.</summary>
    public string? CategoryIcon { get; set; }

    /// <summary>Original amount and currency.</summary>
    public decimal Amount { get; set; }

    public Currency Currency { get; set; }

    /// <summary>Amount in the base currency.</summary>
    public decimal AmountInBase { get; set; }

    /// <summary>Only for recurring expenses.</summary>
    public Frequency? Frequency { get; set; }

    public string? Notes { get; set; }

    public bool IsRecurring => Source == LedgerSource.RecurringExpense;

    /// <summary>Debt items cannot be edited from the expenses page.</summary>
    public bool IsReadOnly => Source is LedgerSource.DebtPayment or LedgerSource.DebtInstallment;
}
