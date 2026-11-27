using ExpenseTracker.Core.Application.DTOs.Dashboard;
using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.Interfaces;

namespace ExpenseTracker.Core.Application.Services;

/// <summary>Everything the home page shows for the current month.</summary>
public class DashboardService
{
    private const int UpcomingDays = 7;
    private const int PlanHighlightCount = 3;

    private readonly IncomeService _income;
    private readonly MonthlyLedgerService _ledger;
    private readonly BudgetService _budget;
    private readonly GoalService _goals;
    private readonly ForecastService _forecast;
    private readonly IDateProvider _dateProvider;

    public DashboardService(
        IncomeService income,
        MonthlyLedgerService ledger,
        BudgetService budget,
        GoalService goals,
        ForecastService forecast,
        IDateProvider dateProvider)
    {
        _income = income;
        _ledger = ledger;
        _budget = budget;
        _goals = goals;
        _forecast = forecast;
        _dateProvider = dateProvider;
    }

    public async Task<DashboardDto> GetAsync()
    {
        var today = _dateProvider.Today;
        var dashboard = new DashboardDto { Year = today.Year, Month = today.Month };

        // Rule 5.3: income = spent + committed + available, unless spending exceeds the income.
        var ledger = await _ledger.GetMonthAsync(today.Year, today.Month);
        dashboard.Income = (await _income.GetMonthlyIncomeAsync(today.Year, today.Month)).Total;
        dashboard.Spent = ledger.Where(i => i.Status == LedgerStatus.Spent).Sum(i => i.AmountInBase);
        dashboard.Committed = ledger.Where(i => i.Status == LedgerStatus.Pending).Sum(i => i.AmountInBase);

        var available = dashboard.Income - dashboard.Spent - dashboard.Committed;
        dashboard.IsDeficit = available < 0;
        dashboard.Available = Math.Max(0m, available);
        dashboard.DeficitAmount = Math.Max(0m, -available);

        dashboard.UpcomingPayments = await GetUpcomingPaymentsAsync(today, ledger);

        var plan = await _budget.GetPlanStatusAsync(today.Year, today.Month);
        dashboard.HasPlan = plan.HasPlan && plan.Categories.Count > 0;
        dashboard.PlanHighlights = plan.Categories
            .Where(c => c.Assigned > 0)
            .OrderByDescending(c => c.EstimatedPercent)
            .ThenByDescending(c => c.Spent + c.Pending)
            .Take(PlanHighlightCount)
            .ToList();

        dashboard.Goals = (await _goals.GetGoalsStatusAsync())
            .Select(g => new DashboardGoalDto
            {
                Id = g.Goal.Id,
                Name = g.Goal.Name,
                ProgressPercent = g.ProgressPercent,
                State = g.State
            })
            .ToList();

        dashboard.NextMonthEstimate = (await _forecast.GetNextMonthAsync()).TotalExpenses;
        return dashboard;
    }

    /// <summary>
    /// Recurring expenses and debt installments due in the next 7 days, plus installments
    /// of this month that are overdue. The window can reach into next month.
    /// </summary>
    private async Task<List<UpcomingPaymentDto>> GetUpcomingPaymentsAsync(DateOnly today, List<LedgerItem> thisMonth)
    {
        var limit = today.AddDays(UpcomingDays);
        var items = thisMonth.AsEnumerable();

        if (limit.Month != today.Month)
            items = items.Concat(await _ledger.GetMonthAsync(limit.Year, limit.Month));

        return items
            .Where(i => i.Status == LedgerStatus.Pending && i.Source is LedgerSource.RecurringExpense or LedgerSource.DebtInstallment)
            .Where(i => i.Date <= limit)
            .Select(i => new UpcomingPaymentDto
            {
                Date = i.Date,
                Name = i.Name,
                CategoryName = i.CategoryName,
                Amount = i.AmountInBase,
                OriginalAmount = i.Amount,
                Currency = i.Currency,
                IsDebt = i.Source == LedgerSource.DebtInstallment,
                IsOverdue = i.Date <= today,
                DebtId = i.Source == LedgerSource.DebtInstallment ? i.SourceId : null
            })
            .OrderBy(p => p.Date)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
