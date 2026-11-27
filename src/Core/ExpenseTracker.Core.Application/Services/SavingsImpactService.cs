using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.Helpers;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Core.Domain.Helpers;

namespace ExpenseTracker.Core.Application.Services;

/// <summary>
/// Tells, before saving an expense, what it does to the savings of its month. It only informs:
/// the expense can always be saved.
/// </summary>
public class SavingsImpactService
{
    private readonly BudgetService _budget;
    private readonly IExpenseRepository _expenses;
    private readonly CurrencyService _currency;
    private readonly IDateProvider _dateProvider;

    public SavingsImpactService(
        BudgetService budget, IExpenseRepository expenses, CurrencyService currency, IDateProvider dateProvider)
    {
        _budget = budget;
        _expenses = expenses;
        _currency = currency;
        _dateProvider = dateProvider;
    }

    /// <param name="editingId">The expense being edited, whose current amount no longer counts.</param>
    public async Task<SavingsImpactDto> PreviewAsync(ExpenseInput input, int? editingId = null)
    {
        var result = new SavingsImpactDto();
        var today = _dateProvider.Today;
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        var month = new DateOnly(input.Date.Year, input.Date.Month, 1);

        // A recurring expense that started before affects what is left of this month.
        if (input.IsRecurring && month < currentMonth)
            month = currentMonth;
        if (month < currentMonth)
            return result; // a closed month has nothing left to protect

        var status = await _budget.GetPlanStatusAsync(month.Year, month.Month);
        if (status.Savings is not { } before)
            return result;

        var converter = await _currency.GetConverterAsync();
        var monthEnd = month.AddMonths(1).AddDays(-1);

        // What an expense adds to the month, split like the ledger does.
        (decimal Spent, decimal Pending) Contribution(
            bool recurring, decimal amount, Currency currency, DateOnly date, Frequency? frequency, DateOnly? end, decimal? amountInBase)
        {
            if (!recurring)
            {
                return date >= month && date <= monthEnd
                    ? (amountInBase ?? converter.ToBase(amount, currency, date), 0m)
                    : (0m, 0m);
            }

            if (frequency is null)
                return (0m, 0m);

            var dates = RecurrenceCalculator.GetOccurrences(date, frequency.Value, end, month, monthEnd);
            return (dates.Where(d => d <= today).Sum(d => converter.ToBase(amount, currency, d)),
                    dates.Where(d => d > today).Sum(d => converter.ToBase(amount, currency, d)));
        }

        var added = Contribution(input.IsRecurring, input.Amount, input.Currency, input.Date, input.Frequency, input.EndDate, null);
        var old = editingId is { } id ? await _expenses.GetByIdAsync(id) : null;
        var removed = old is null
            ? (Spent: 0m, Pending: 0m)
            : Contribution(old.Type == ExpenseType.Recurring, old.Amount, old.Currency, old.Date, old.Frequency, old.EndDate, old.AmountInBase);

        var after = SavingsReserveCalculator.Calculate(
            before.Income, before.Reserve,
            before.Used - removed.Spent + added.Spent,
            before.Pending - removed.Pending + added.Pending,
            status.AlertThresholdPercent);

        if (after.Income == 0m)
            return result;

        var baseCurrency = converter.BaseCurrency;
        if (before.Margin >= 0m && after.UsedSavings > 0m)
            result.Warning = $"Este gasto usa {MoneyFormatter.Format(after.UsedSavings, baseCurrency)} de tu ahorro. ¿Guardar igual?";

        result.Effect = after.State is SavingsState.OnPlan or SavingsState.NearSavings
            ? $"Te quedan {MoneyFormatter.Format(after.Margin, baseCurrency)} dentro del plan."
            : SavingsReserveCalculator.Message(after, baseCurrency);
        return result;
    }
}
