using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.Helpers;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Helpers;

namespace ExpenseTracker.Core.Application.Services;

/// <summary>
/// Central service (rule 5.2): every movement of a month, as Spent or Pending.
/// Used by Expenses, Dashboard, Plan and Forecast.
/// </summary>
public class MonthlyLedgerService
{
    public const string NoCategoryName = "Sin categoría";
    public const string DebtsCategoryName = "Deudas";
    private const string DebtsCategoryColor = "#6F42C1";
    private const string DebtsCategoryIcon = "bank";

    private readonly IExpenseRepository _expenses;
    private readonly IDebtRepository _debts;
    private readonly CurrencyService _currency;
    private readonly IDateProvider _dateProvider;

    public MonthlyLedgerService(
        IExpenseRepository expenses,
        IDebtRepository debts,
        CurrencyService currency,
        IDateProvider dateProvider)
    {
        _expenses = expenses;
        _debts = debts;
        _currency = currency;
        _dateProvider = dateProvider;
    }

    public async Task<List<LedgerItem>> GetMonthAsync(int year, int month)
    {
        if (year < 1 || year > 9999 || month < 1 || month > 12)
            throw new ArgumentException("El mes no es válido.");

        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var today = _dateProvider.Today;

        var oneTime = await _expenses.GetOneTimeByRangeAsync(from, to);
        var recurring = await _expenses.GetActiveRecurringAsync(from, to);
        var debts = await _debts.GetAllWithPaymentsAsync();

        // Rates are loaded once, and only when something needs converting. With everything in the
        // base currency this costs a single query (the settings), keeping a month at 4 queries.
        CurrencyConverter? converter = null;
        if (recurring.Count > 0 || debts.Count > 0)
        {
            var currencies = recurring.Select(e => e.Currency).Concat(debts.Select(d => d.Currency));
            converter = await _currency.GetConverterAsync(currencies);
        }

        var items = new List<LedgerItem>();

        foreach (var expense in oneTime)
        {
            items.Add(ToItem(expense, expense.Date, LedgerSource.OneTimeExpense, LedgerStatus.Spent, expense.AmountInBase));
        }

        foreach (var expense in recurring)
        {
            if (expense.Frequency is null)
                continue;


            var occurrences = RecurrenceCalculator.GetOccurrences(expense.Date, expense.Frequency.Value, expense.EndDate, from, to);
            foreach (var date in occurrences)
            {
                var status = date <= today ? LedgerStatus.Spent : LedgerStatus.Pending;
                // Recurring expenses in a foreign currency use the rate in force on each date.
                var amountInBase = converter!.ToBase(expense.Amount, expense.Currency, date);
                items.Add(ToItem(expense, date, LedgerSource.RecurringExpense, status, amountInBase));
            }
        }

        foreach (var debt in debts)
        {
            AddDebtItems(items, debt, from, to, today, converter!);
        }

        return items.OrderBy(i => i.Date).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    // Registered payments are Spent. If the month has no payment for the debt, its installment
    // is Pending (only from the current month on, following the amortization schedule).
    private static void AddDebtItems(List<LedgerItem> items, Debt debt, DateOnly from, DateOnly to, DateOnly today, CurrencyConverter converter)
    {
        var payments = debt.Payments.Where(p => p.Date >= from && p.Date <= to).ToList();
        foreach (var payment in payments)
        {
            items.Add(new LedgerItem
            {
                Date = payment.Date,
                Name = $"Pago: {debt.Name}",
                Source = LedgerSource.DebtPayment,
                Status = LedgerStatus.Spent,
                SourceId = debt.Id,
                CategoryName = DebtsCategoryName,
                CategoryColor = DebtsCategoryColor,
                CategoryIcon = DebtsCategoryIcon,
                Amount = payment.Amount,
                Currency = debt.Currency,
                AmountInBase = converter.ToBase(payment.Amount, debt.Currency, payment.Date),
                Notes = payment.Notes
            });
        }

        if (payments.Count > 0 || debt.IsClosed || to < new DateOnly(today.Year, today.Month, 1))
            return;

        var installment = AmortizationCalculator.ForDebt(debt, today)
            .Rows.FirstOrDefault(r => r.Date >= from && r.Date <= to);
        if (installment is null)
            return;

        items.Add(new LedgerItem
        {
            Date = installment.Date,
            Name = $"Cuota: {debt.Name}",
            Source = LedgerSource.DebtInstallment,
            Status = LedgerStatus.Pending,
            SourceId = debt.Id,
            CategoryName = DebtsCategoryName,
            CategoryColor = DebtsCategoryColor,
            CategoryIcon = DebtsCategoryIcon,
            Amount = installment.Payment,
            Currency = debt.Currency,
            AmountInBase = converter.ToBase(installment.Payment, debt.Currency, installment.Date)
        });
    }

    private static LedgerItem ToItem(Expense expense, DateOnly date, LedgerSource source, LedgerStatus status, decimal amountInBase) => new()
    {
        Date = date,
        Name = expense.Name,
        Source = source,
        Status = status,
        SourceId = expense.Id,
        CategoryId = expense.CategoryId,
        CategoryName = expense.Category?.Name ?? NoCategoryName,
        CategoryColor = expense.Category?.Color ?? "#6C757D",
        CategoryIcon = expense.Category?.Icon,
        Amount = expense.Amount,
        Currency = expense.Currency,
        AmountInBase = amountInBase,
        Frequency = source == LedgerSource.RecurringExpense ? expense.Frequency : null,
        Notes = expense.Notes
    };
}
