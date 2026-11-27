using ExpenseTracker.Core.Application.DTOs.Forecast;
using ExpenseTracker.Core.Application.Helpers;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Helpers;

namespace ExpenseTracker.Core.Application.Services;

/// <summary>Expense estimates (rule 5.7): Estimate = Recurring + Debts + Variables, with projected income.</summary>
public class ForecastService
{
    private const int HistoryMonths = 3;

    private readonly IExpenseRepository _expenses;
    private readonly IDebtRepository _debts;
    private readonly IRepository<Category> _categories;
    private readonly CurrencyService _currency;
    private readonly BudgetService _budget;
    private readonly IncomeService _income;
    private readonly IDateProvider _dateProvider;

    public ForecastService(
        IExpenseRepository expenses,
        IDebtRepository debts,
        IRepository<Category> categories,
        CurrencyService currency,
        BudgetService budget,
        IncomeService income,
        IDateProvider dateProvider)
    {
        _expenses = expenses;
        _debts = debts;
        _categories = categories;
        _currency = currency;
        _budget = budget;
        _income = income;
        _dateProvider = dateProvider;
    }

    // ---------- Shortcuts ----------

    /// <summary>The whole next calendar month.</summary>
    public Task<ForecastDto> GetNextMonthAsync()
    {
        var next = FirstOfMonth(_dateProvider.Today).AddMonths(1);
        return EstimateAsync(next, next.AddMonths(1).AddDays(-1));
    }

    /// <summary>From tomorrow to 31 December. Of the current month only what is still pending counts.</summary>
    public Task<ForecastDto> GetRestOfYearAsync()
    {
        var today = _dateProvider.Today;
        return EstimateAsync(today.AddDays(1), new DateOnly(today.Year, 12, 31));
    }

    /// <summary>1 January to 31 December of next year.</summary>
    public Task<ForecastDto> GetNextYearAsync()
    {
        var year = _dateProvider.Today.Year + 1;
        return EstimateAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
    }

    // ---------- Estimate ----------

    public async Task<ForecastDto> EstimateAsync(DateOnly from, DateOnly to)
    {
        var result = new ForecastDto { From = from, To = to };
        if (to < from)
            return result; // e.g. "rest of the year" on 31 December

        var today = _dateProvider.Today;
        var thisMonth = FirstOfMonth(today);

        // A range that starts later in the current month also counts the installments of that
        // month that are still unpaid, even when their due date has already passed.
        var includeUnpaidThisMonth = from > today && FirstOfMonth(from) == thisMonth;

        var recurring = await _expenses.GetActiveRecurringAsync(from, to);
        var debts = await _debts.GetAllWithPaymentsAsync();
        var categories = (await _categories.ListAsync()).ToDictionary(c => c.Id);
        var converter = recurring.Count > 0 || debts.Count > 0 ? await _currency.GetConverterAsync() : null;

        var historyRates = await GetHistoryMonthlyAmountsAsync(thisMonth);
        var assignedByMonth = await _budget.GetAssignedByMonthAsync(from, to);
        var incomeByMonth = await _income.GetProjectedIncomeByMonthAsync(from, to);

        // Installments are simulated once per debt; each month then picks its own rows.
        var schedules = debts
            .Select(d => (Debt: d, Rows: AmortizationCalculator.ForDebt(d, today).Rows))
            .Where(s => s.Rows.Count > 0)
            .ToList();

        var recurringItems = new Dictionary<int, ForecastItemDto>();
        var debtItems = new Dictionary<int, ForecastItemDto>();
        var variableTotals = new Dictionary<int, decimal>();
        var variableBasis = new Dictionary<int, VariableBasis>();

        for (var monthStart = FirstOfMonth(from); monthStart <= to; monthStart = monthStart.AddMonths(1))
        {
            var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var monthEnd = new DateOnly(monthStart.Year, monthStart.Month, daysInMonth);
            var windowStart = from > monthStart ? from : monthStart;
            var windowEnd = to < monthEnd ? to : monthEnd;

            var month = new ForecastMonthDto
            {
                Year = monthStart.Year,
                Month = monthStart.Month,
                IsPartial = windowStart != monthStart || windowEnd != monthEnd
            };

            // Recurring expenses: every occurrence inside the window.
            foreach (var expense in recurring.Where(e => e.Frequency is not null))
            {
                var count = RecurrenceCalculator.GetOccurrences(
                    expense.Date, expense.Frequency!.Value, expense.EndDate, windowStart, windowEnd).Count;
                if (count == 0)
                    continue;

                var amount = converter!.ToBase(expense.Amount, expense.Currency) * count;
                month.Recurring += amount;
                AddItem(recurringItems, expense.Id, expense.Name, CategoryName(categories, expense.CategoryId), count, amount);
            }

            // Debts: simulated installments, only until each debt is paid off.
            foreach (var (debt, rows) in schedules)
            {
                foreach (var row in rows)
                {
                    var inWindow = row.Date >= windowStart && row.Date <= windowEnd;
                    var unpaidThisMonth = includeUnpaidThisMonth && monthStart == thisMonth
                                          && FirstOfMonth(row.Date) == thisMonth && row.Date < windowStart;
                    if (!inWindow && !unpaidThisMonth)
                        continue;

                    var amount = converter!.ToBase(row.Payment, debt.Currency);
                    month.Debts += amount;
                    AddItem(debtItems, debt.Id, debt.Name, MonthlyLedgerService.DebtsCategoryName, 1, amount);
                }
            }

            // Variables: monthly estimate scaled by the share of the month the window covers.
            var share = (decimal)(windowEnd.DayNumber - windowStart.DayNumber + 1) / daysInMonth;
            // Without history, the plan in force that month is used (a month without plan adds nothing).
            foreach (var (categoryId, rate) in AddPlanRates(historyRates, assignedByMonth[monthStart], recurring, converter))
            {
                variableBasis[categoryId] = rate.Basis;
                var amount = rate.Monthly * share;
                month.Variables += amount;
                variableTotals[categoryId] = variableTotals.GetValueOrDefault(categoryId) + amount;
            }

            month.Recurring = Round(month.Recurring);
            month.Debts = Round(month.Debts);
            month.Variables = Round(month.Variables);
            month.TotalExpenses = month.Recurring + month.Debts + month.Variables;
            month.Income = Round(incomeByMonth.GetValueOrDefault(monthStart));
            month.Balance = month.Income - month.TotalExpenses;
            result.Months.Add(month);
        }

        result.RecurringItems = recurringItems.Values.OrderByDescending(i => i.Amount).ToList();
        result.DebtItems = debtItems.Values.OrderByDescending(i => i.Amount).ToList();
        result.VariableItems = variableTotals
            .Where(v => Round(v.Value) > 0)
            .Select(v => new ForecastVariableDto
            {
                CategoryId = v.Key,
                CategoryName = CategoryName(categories, v.Key),
                CategoryColor = categories.TryGetValue(v.Key, out var c) ? c.Color : "#6C757D",
                Amount = Round(v.Value),
                Basis = variableBasis[v.Key]
            })
            .OrderByDescending(v => v.Amount)
            .ToList();

        result.RecurringTotal = result.Months.Sum(m => m.Recurring);
        result.DebtsTotal = result.Months.Sum(m => m.Debts);
        result.VariablesTotal = result.Months.Sum(m => m.Variables);
        result.TotalExpenses = result.RecurringTotal + result.DebtsTotal + result.VariablesTotal;
        result.ProjectedIncome = result.Months.Sum(m => m.Income);
        result.Balance = result.ProjectedIncome - result.TotalExpenses;
        return result;
    }

    // ---------- Variables ----------

    private sealed class VariableRate
    {
        public decimal Monthly { get; init; }
        public VariableBasis Basis { get; init; }
    }

    /// <summary>Monthly average of variable (one-time) spending per category over the last 3 full months.</summary>
    private async Task<Dictionary<int, VariableRate>> GetHistoryMonthlyAmountsAsync(DateOnly thisMonth)
    {
        var historyFrom = thisMonth.AddMonths(-HistoryMonths);
        var history = await _expenses.GetOneTimeByRangeAsync(historyFrom, thisMonth.AddDays(-1));

        return history
            .GroupBy(e => e.CategoryId)
            .ToDictionary(
                g => g.Key,
                g => new VariableRate { Monthly = g.Sum(e => e.AmountInBase) / HistoryMonths, Basis = VariableBasis.History });
    }

    /// <summary>
    /// History rates plus, for categories without history, the Plan amount minus what the category's
    /// recurring expenses already cover so they are not counted twice.
    /// </summary>
    private static Dictionary<int, VariableRate> AddPlanRates(
        Dictionary<int, VariableRate> history, Dictionary<int, decimal> assigned,
        List<Expense> recurring, CurrencyConverter? converter)
    {
        var rates = new Dictionary<int, VariableRate>(history);
        foreach (var (categoryId, amount) in assigned)
        {
            if (rates.ContainsKey(categoryId))
                continue;

            var covered = recurring
                .Where(e => e.CategoryId == categoryId && e.Frequency is not null)
                .Sum(e => RecurrenceCalculator.ToMonthlyAmount(converter!.ToBase(e.Amount, e.Currency), e.Frequency!.Value));

            var monthly = Math.Max(0m, amount - covered);
            if (monthly > 0)
                rates[categoryId] = new VariableRate { Monthly = monthly, Basis = VariableBasis.Plan };
        }

        return rates;
    }

    // ---------- Helpers ----------

    private static void AddItem(Dictionary<int, ForecastItemDto> items, int id, string name, string category, int occurrences, decimal amount)
    {
        if (!items.TryGetValue(id, out var item))
            items[id] = item = new ForecastItemDto { Name = name, CategoryName = category };

        item.Occurrences += occurrences;
        item.Amount = Round(item.Amount + amount);
    }

    private static string CategoryName(Dictionary<int, Category> categories, int id)
        => categories.TryGetValue(id, out var category) ? category.Name : MonthlyLedgerService.NoCategoryName;

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
