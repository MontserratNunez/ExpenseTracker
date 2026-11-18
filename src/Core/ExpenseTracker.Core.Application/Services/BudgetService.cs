using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.DTOs.Incomes;
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

    private readonly IRepository<BudgetAllocation> _allocations;
    private readonly IRepository<Category> _categories;
    private readonly IRepository<IncomeSource> _sources;
    private readonly IExpenseRepository _expenses;
    private readonly MonthlyLedgerService _ledger;
    private readonly IncomeService _income;
    private readonly IDateProvider _dateProvider;

    public BudgetService(
        IRepository<BudgetAllocation> allocations,
        IRepository<Category> categories,
        IRepository<IncomeSource> sources,
        IExpenseRepository expenses,
        MonthlyLedgerService ledger,
        IncomeService income,
        IDateProvider dateProvider)
    {
        _allocations = allocations;
        _categories = categories;
        _sources = sources;
        _expenses = expenses;
        _ledger = ledger;
        _income = income;
        _dateProvider = dateProvider;
    }

    // ---------- Allocations (CRUD) ----------

    public async Task<BudgetAllocation> CreateAsync(AllocationInput input)
    {
        await ValidateAsync(input);
        if ((await _allocations.ListAsync(a => a.CategoryId == input.CategoryId)).Count > 0)
            throw new InvalidOperationException("Esa categoría ya tiene una asignación en el plan.");

        var entity = new BudgetAllocation();
        Apply(entity, input);
        await _allocations.AddAsync(entity);
        return entity;
    }

    public async Task<BudgetAllocation> UpdateAsync(int id, AllocationInput input)
    {
        await ValidateAsync(input);

        var entity = await _allocations.GetByIdAsync(id)
            ?? throw new InvalidOperationException("La asignación no existe.");
        if (entity.CategoryId != input.CategoryId
            && (await _allocations.ListAsync(a => a.CategoryId == input.CategoryId)).Count > 0)
            throw new InvalidOperationException("Esa categoría ya tiene una asignación en el plan.");

        Apply(entity, input);
        await _allocations.UpdateAsync(entity);
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _allocations.GetByIdAsync(id)
            ?? throw new InvalidOperationException("La asignación no existe.");
        await _allocations.DeleteAsync(entity);
    }

    /// <summary>Data for the "Edit plan" table: every category with its allocation (value 0 = none).</summary>
    public async Task<AllocationEditorDto> GetEditorAsync()
    {
        var categories = await _categories.ListAsync();
        var allocations = await _allocations.ListAsync();
        var byCategory = allocations.ToDictionary(a => a.CategoryId);

        var editor = new AllocationEditorDto { IncomeBases = await _income.GetIncomeBasesAsync() };
        foreach (var category in categories.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            byCategory.TryGetValue(category.Id, out var allocation);

            // Inactive categories only matter if they still have an allocation.
            if (!category.IsActive && allocation is null)
                continue;

            editor.Rows.Add(new AllocationRowDto
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                CategoryColor = category.Color,
                IsActive = category.IsActive,
                AllocationType = allocation?.AllocationType ?? AllocationType.FixedAmount,
                Value = allocation?.Value ?? 0m,
                IncomeSourceId = allocation?.IncomeSourceId
            });
        }

        return editor;
    }

    /// <summary>
    /// Saves the whole plan: a value above 0 creates or updates the category's allocation,
    /// a value of 0 removes it. Nothing is saved if any row is invalid.
    /// </summary>
    public async Task SaveAllAsync(List<AllocationInput> inputs)
    {
        if (inputs.GroupBy(i => i.CategoryId).Any(g => g.Count() > 1))
            throw new ArgumentException("Hay categorías repetidas en el plan.");

        foreach (var input in inputs.Where(i => i.Value > 0))
            await ValidateAsync(input);

        var existing = (await _allocations.ListAsync()).ToDictionary(a => a.CategoryId);
        foreach (var input in inputs)
        {
            existing.TryGetValue(input.CategoryId, out var current);

            if (input.Value <= 0)
            {
                if (current is not null)
                    await _allocations.DeleteAsync(current);
            }
            else if (current is null)
            {
                var entity = new BudgetAllocation();
                Apply(entity, input);
                await _allocations.AddAsync(entity);
            }
            else
            {
                Apply(current, input);
                await _allocations.UpdateAsync(current);
            }
        }
    }

    // ---------- Plan status (rule 5.5) ----------

    public async Task<PlanStatusDto> GetPlanStatusAsync(int year, int month)
    {
        if (year < 1 || year > 9999 || month < 1 || month > 12)
            throw new ArgumentException("El mes no es válido.");

        var today = _dateProvider.Today;
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var ledger = await _ledger.GetMonthAsync(year, month);
        var allocations = await _allocations.ListAsync();
        var categories = (await _categories.ListAsync()).ToDictionary(c => c.Id);
        var incomeBases = allocations.Any(a => a.AllocationType == AllocationType.PercentageOfIncome)
            ? await _income.GetIncomeBasesAsync()
            : new IncomeBasesDto();
        var income = (await _income.GetMonthlyIncomeAsync(year, month)).Total;

        var variableByCategory = ledger
            .Where(i => i.Source == LedgerSource.OneTimeExpense && i.CategoryId is not null)
            .GroupBy(i => i.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.AmountInBase));

        var dailyHistory = await GetDailyAverageByCategoryAsync(year, month);

        var status = new PlanStatusDto { Year = year, Month = month, Income = income };

        foreach (var allocation in allocations)
        {
            if (!categories.TryGetValue(allocation.CategoryId, out var category))
                continue;

            var items = ledger.Where(i => i.CategoryId == allocation.CategoryId).ToList();
            var spent = items.Where(i => i.Status == LedgerStatus.Spent).Sum(i => i.AmountInBase);
            var pending = items.Where(i => i.Status == LedgerStatus.Pending).Sum(i => i.AmountInBase);
            var assigned = ResolveAssigned(allocation, incomeBases);

            variableByCategory.TryGetValue(allocation.CategoryId, out var variable);
            dailyHistory.TryGetValue(allocation.CategoryId, out var historicDaily);
            var estimated = Estimate(year, month, today, daysInMonth, spent, pending, variable, historicDaily);

            status.Categories.Add(new CategoryPlanDto
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                CategoryColor = category.Color,
                AllocationType = allocation.AllocationType,
                Value = allocation.Value,
                IncomeSourceId = allocation.IncomeSourceId,
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
        status.IsOverAssigned = status.TotalAssigned > income;
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

    private static decimal ResolveAssigned(BudgetAllocation allocation, IncomeBasesDto bases)
    {
        if (allocation.AllocationType == AllocationType.FixedAmount)
            return allocation.Value;

        var baseAmount = allocation.IncomeSourceId is { } sourceId
            ? bases.Sources.FirstOrDefault(s => s.SourceId == sourceId)?.MonthlyAmount ?? 0m
            : bases.RecurringTotal;

        return Math.Round(baseAmount * allocation.Value / 100m, 2, MidpointRounding.AwayFromZero);
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

    private async Task ValidateAsync(AllocationInput input)
    {
        if (!Enum.IsDefined(input.AllocationType))
            throw new ArgumentException("El tipo de asignación no es válido.");
        if (input.Value <= 0)
            throw new ArgumentException("El valor debe ser mayor que cero.");
        if (input.AllocationType == AllocationType.PercentageOfIncome && input.Value > 100)
            throw new ArgumentException("El porcentaje no puede superar 100.");

        if (await _categories.GetByIdAsync(input.CategoryId) is null)
            throw new InvalidOperationException("La categoría no existe.");

        if (input.AllocationType == AllocationType.PercentageOfIncome && input.IncomeSourceId is { } sourceId
            && await _sources.GetByIdAsync(sourceId) is null)
            throw new InvalidOperationException("La fuente de ingreso no existe.");
    }

    private static void Apply(BudgetAllocation entity, AllocationInput input)
    {
        entity.CategoryId = input.CategoryId;
        entity.AllocationType = input.AllocationType;
        entity.Value = input.Value;
        // The source only applies to percentages.
        entity.IncomeSourceId = input.AllocationType == AllocationType.PercentageOfIncome ? input.IncomeSourceId : null;
    }
}
