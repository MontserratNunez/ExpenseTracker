using System.Globalization;
using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.Services;

public class BudgetService
{
    /// <summary>Months of history used to estimate variable spending early in the month.</summary>
    private const int HistoryMonths = 3;

    /// <summary>Below this many elapsed days, the daily pace of the month is not reliable.</summary>
    private const int MinDaysForPace = 3;

    private readonly IBudgetPlanRepository _plans;
    private readonly IRepository<Category> _categories;
    private readonly IExpenseRepository _expenses;
    private readonly MonthlyLedgerService _ledger;
    private readonly IncomeService _income;
    private readonly IDateProvider _dateProvider;

    public BudgetService(
        IBudgetPlanRepository plans,
        IRepository<Category> categories,
        IExpenseRepository expenses,
        MonthlyLedgerService ledger,
        IncomeService income,
        IDateProvider dateProvider)
    {
        _plans = plans;
        _categories = categories;
        _expenses = expenses;
        _ledger = ledger;
        _income = income;
        _dateProvider = dateProvider;
    }

    // ---------- Assigned amounts ----------

    /// <summary>
    /// Monthly amount assigned to each category by the plan in force in each month of the range
    /// (percentages already resolved). The keys are the first day of each month; a month without plan is empty.
    /// </summary>
    public async Task<Dictionary<DateOnly, Dictionary<int, decimal>>> GetAssignedByMonthAsync(DateOnly from, DateOnly to)
    {
        var plans = await _plans.GetAllWithAllocationsAsync();
        var incomes = plans.Any(p => p.Allocations.Any(a => a.AllocationType == AllocationType.PercentageOfIncome))
            ? await _income.LoadSnapshotAsync()
            : null;

        var result = new Dictionary<DateOnly, Dictionary<int, decimal>>();
        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            var plan = plans.FirstOrDefault(p => p.AppliesTo(month.Year, month.Month));
            result[month] = plan?.Allocations.ToDictionary(a => a.CategoryId, a => ResolveAssigned(a, incomes, month.Year, month.Month))
                            ?? new Dictionary<int, decimal>();
        }

        return result;
    }

    /// <summary>Monthly amount assigned to each category by the plan in force today.</summary>
    public async Task<Dictionary<int, decimal>> GetAssignedByCategoryAsync()
    {
        var today = _dateProvider.Today;
        var byMonth = await GetAssignedByMonthAsync(today, today);
        return byMonth[new DateOnly(today.Year, today.Month, 1)];
    }

    // ---------- Plan status (rule 5.5) ----------

    public async Task<PlanStatusDto> GetPlanStatusAsync(int year, int month)
    {
        if (year < 1 || year > 9999 || month < 1 || month > 12)
            throw new ArgumentException("El mes no es válido.");

        var today = _dateProvider.Today;
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var ledger = await _ledger.GetMonthAsync(year, month);
        var plan = (await _plans.GetAllWithAllocationsAsync()).FirstOrDefault(p => p.AppliesTo(year, month));
        var allocations = plan?.Allocations ?? new List<BudgetAllocation>();
        var categories = (await _categories.ListAsync()).ToDictionary(c => c.Id);
        var incomes = allocations.Any(a => a.AllocationType == AllocationType.PercentageOfIncome)
            ? await _income.LoadSnapshotAsync()
            : null;
        var income = (await _income.GetMonthlyIncomeAsync(year, month)).Total;

        var variableByCategory = ledger
            .Where(i => i.Source == LedgerSource.OneTimeExpense && i.CategoryId is not null)
            .GroupBy(i => i.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.AmountInBase));

        var dailyHistory = await GetDailyAverageByCategoryAsync(year, month);

        var status = new PlanStatusDto
        {
            Year = year,
            Month = month,
            Income = income,
            HasPlan = plan is not null,
            PlanId = plan?.Id,
            PlanName = plan?.Name,
            PlanFrom = plan?.EffectiveFrom,
            PlanTo = plan?.EffectiveTo
        };

        foreach (var allocation in allocations)
        {
            if (!categories.TryGetValue(allocation.CategoryId, out var category))
                continue;

            var items = ledger.Where(i => i.CategoryId == allocation.CategoryId).ToList();
            var spent = items.Where(i => i.Status == LedgerStatus.Spent).Sum(i => i.AmountInBase);
            var pending = items.Where(i => i.Status == LedgerStatus.Pending).Sum(i => i.AmountInBase);
            var assigned = ResolveAssigned(allocation, incomes, year, month);
            var isPercent = allocation.AllocationType == AllocationType.PercentageOfIncome;
            var source = isPercent ? incomes!.Sources.FirstOrDefault(s => s.Id == allocation.IncomeSourceId) : null;
            if (source is not null && !source.HasIncomeIn(year, month))
                AddNoIncomeWarning(status, source.Name, year, month, today);

            variableByCategory.TryGetValue(allocation.CategoryId, out var variable);
            dailyHistory.TryGetValue(allocation.CategoryId, out var historicDaily);
            var estimated = Estimate(year, month, today, daysInMonth, spent, pending, variable, historicDaily);

            status.Categories.Add(new CategoryPlanDto
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                CategoryColor = category.Color,
                CategoryIcon = category.Icon,
                AllocationType = allocation.AllocationType,
                Value = allocation.Value,
                IncomeSourceId = allocation.IncomeSourceId,
                BaseAmount = isPercent ? incomes!.MonthlyBase(allocation.IncomeSourceId, year, month) : 0m,
                BaseName = source?.Name,
                Assigned = assigned,
                Spent = spent,
                Pending = pending,
                Available = assigned - spent - pending,
                Estimated = estimated,
                EstimatedOverrun = Math.Max(0m, estimated - assigned),
                EstimatedPercent = assigned > 0 ? Math.Round(estimated / assigned * 100m, 1) : 0m,
                Health = GetHealth(estimated, assigned)
            });
        }

        status.Categories = status.Categories
            .OrderByDescending(c => c.Assigned)
            .ThenBy(c => c.CategoryName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // Spending in categories without an allocation (including virtual ones such as "Deudas").
        var planned = allocations.Select(a => a.CategoryId).ToHashSet();
        status.OutOfPlan = ledger
            .Where(i => i.CategoryId is null || !planned.Contains(i.CategoryId.Value))
            .GroupBy(i => new { i.CategoryId, i.CategoryName, i.CategoryColor })
            .Select(g => new CategoryTotalDto
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                CategoryColor = g.Key.CategoryColor,
                Spent = g.Where(i => i.Status == LedgerStatus.Spent).Sum(i => i.AmountInBase),
                Pending = g.Where(i => i.Status == LedgerStatus.Pending).Sum(i => i.AmountInBase)
            })
            .OrderByDescending(c => c.Spent + c.Pending)
            .ToList();
        status.OutOfPlanSpent = status.OutOfPlan.Sum(c => c.Spent);
        status.OutOfPlanPending = status.OutOfPlan.Sum(c => c.Pending);

        status.TotalAssigned = status.Categories.Sum(c => c.Assigned);
        status.Unassigned = income - status.TotalAssigned;
        status.IsOverAssigned = plan is not null && status.TotalAssigned > income;
        status.AssignedPercentOfIncome = income > 0 ? Math.Round(status.TotalAssigned / income * 100m, 1) : 0m;
        status.TotalSpent = ledger.Where(i => i.Status == LedgerStatus.Spent).Sum(i => i.AmountInBase);
        status.TotalPending = ledger.Where(i => i.Status == LedgerStatus.Pending).Sum(i => i.AmountInBase);
        status.TotalEstimated = status.Categories.Sum(c => c.Estimated) + status.OutOfPlanSpent + status.OutOfPlanPending;

        return status;
    }

    // ---------- Helpers ----------

    /// <summary>
    /// Estimated month-end spending: spent + pending + daily pace x remaining days.
    /// The pace comes from this month's one-time spending, or from the last 3 months'
    /// daily average when fewer than 3 days have elapsed (or the month has not started).
    /// </summary>
    private static decimal Estimate(
        int year, int month, DateOnly today, int daysInMonth,
        decimal spent, decimal pending, decimal variable, decimal historicDaily)
    {
        var monthIndex = year * 12 + month;
        var currentIndex = today.Year * 12 + today.Month;

        if (monthIndex < currentIndex)
            return spent + pending; // closed month: nothing left to project

        int elapsed = monthIndex > currentIndex ? 0 : today.Day;
        var remaining = daysInMonth - elapsed;
        var dailyPace = elapsed >= MinDaysForPace ? variable / elapsed : historicDaily;

        return Math.Round(spent + pending + dailyPace * remaining, 2, MidpointRounding.AwayFromZero);
    }

    private static PlanHealth GetHealth(decimal estimated, decimal assigned)
    {
        if (assigned <= 0)
            return estimated > 0 ? PlanHealth.Red : PlanHealth.Green;

        var ratio = estimated / assigned;
        if (ratio <= 0.9m) return PlanHealth.Green;
        if (ratio <= 1m) return PlanHealth.Yellow;
        return PlanHealth.Red;
    }

    private static decimal ResolveAssigned(BudgetAllocation allocation, IncomeSnapshot? incomes, int year, int month)
    {
        if (allocation.AllocationType == AllocationType.FixedAmount)
            return allocation.Value;

        var baseAmount = incomes!.MonthlyBase(allocation.IncomeSourceId, year, month);
        return Math.Round(baseAmount * allocation.Value / 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static void AddNoIncomeWarning(PlanStatusDto status, string name, int year, int month, DateOnly today)
    {
        var verb = year * 12 + month < today.Year * 12 + today.Month ? "no tenía" : "no tiene";
        var monthText = new DateOnly(year, month, 1).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-DO"));
        var warning = $"{name} {verb} ingreso en {monthText}.";
        if (!status.Warnings.Contains(warning))
            status.Warnings.Add(warning);
    }

    /// <summary>Daily average of one-time spending per category over the 3 full months before the given one.</summary>
    private async Task<Dictionary<int, decimal>> GetDailyAverageByCategoryAsync(int year, int month)
    {
        var target = new DateOnly(year, month, 1);
        var from = target.AddMonths(-HistoryMonths);
        var to = target.AddDays(-1);
        var days = to.DayNumber - from.DayNumber + 1;

        var history = await _expenses.GetOneTimeByRangeAsync(from, to);
        return history
            .GroupBy(e => e.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.AmountInBase) / days);
    }
}
