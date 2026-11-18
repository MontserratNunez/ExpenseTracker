using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Helpers;

namespace ExpenseTracker.Core.Application.Services;

/// <summary>
/// Central service (rule 5.2): every movement of a month, as Spent or Pending.
/// Used by Expenses, Dashboard, Plan and Forecast. Debt installments are added in the debts module.
/// </summary>
public class MonthlyLedgerService
{
    private const string NoCategoryName = "Sin categoría";

    private readonly IExpenseRepository _expenses;
    private readonly CurrencyService _currency;
    private readonly IDateProvider _dateProvider;

    public MonthlyLedgerService(IExpenseRepository expenses, CurrencyService currency, IDateProvider dateProvider)
    {
        _expenses = expenses;
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

        var items = new List<LedgerItem>();

        foreach (var expense in await _expenses.GetOneTimeByRangeAsync(from, to))
        {
            items.Add(ToItem(expense, expense.Date, LedgerSource.OneTimeExpense, LedgerStatus.Spent, expense.AmountInBase));
        }

        var recurring = await _expenses.GetActiveRecurringAsync(from, to);
        if (recurring.Count > 0)
        {
            // D7: recurring expenses in a foreign currency use the rate in force when calculating.
            var converter = await _currency.GetConverterAsync();

            foreach (var expense in recurring)
            {
                if (expense.Frequency is null)
                    continue;

                var amountInBase = converter.ToBase(expense.Amount, expense.Currency);
                var occurrences = RecurrenceCalculator.GetOccurrences(expense.Date, expense.Frequency.Value, expense.EndDate, from, to);
                foreach (var date in occurrences)
                {
                    var status = date <= today ? LedgerStatus.Spent : LedgerStatus.Pending;
                    items.Add(ToItem(expense, date, LedgerSource.RecurringExpense, status, amountInBase));
                }
            }
        }

        return items.OrderBy(i => i.Date).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
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
        Amount = expense.Amount,
        Currency = expense.Currency,
        AmountInBase = amountInBase,
        Frequency = source == LedgerSource.RecurringExpense ? expense.Frequency : null,
        Notes = expense.Notes
    };
}
