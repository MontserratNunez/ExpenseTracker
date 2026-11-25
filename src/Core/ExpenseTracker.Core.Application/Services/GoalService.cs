using ExpenseTracker.Core.Application.DTOs.Goals;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Core.Domain.Helpers;

namespace ExpenseTracker.Core.Application.Services;

public class GoalService
{
    private readonly IRepository<Goal> _goals;
    private readonly IRepository<Category> _categories;
    private readonly IExpenseRepository _expenses;
    private readonly CurrencyService _currency;
    private readonly BudgetService _budget;
    private readonly IDateProvider _dateProvider;

    public GoalService(
        IRepository<Goal> goals,
        IRepository<Category> categories,
        IExpenseRepository expenses,
        CurrencyService currency,
        BudgetService budget,
        IDateProvider dateProvider)
    {
        _goals = goals;
        _categories = categories;
        _expenses = expenses;
        _currency = currency;
        _budget = budget;
        _dateProvider = dateProvider;
    }

    // ---------- CRUD ----------

    public async Task<GoalDto?> GetByIdAsync(int id)
    {
        var goal = await _goals.GetByIdAsync(id);
        if (goal is null)
            return null;

        var category = goal.CategoryId is { } categoryId ? await _categories.GetByIdAsync(categoryId) : null;
        return ToDto(goal, category?.Name);
    }

    public async Task<GoalDto> CreateAsync(GoalInput input)
    {
        var category = await ValidateAsync(input);

        var goal = new Goal { IsActive = true };
        Apply(goal, input);
        await _goals.AddAsync(goal);
        return ToDto(goal, category.Name);
    }

    public async Task<GoalDto> UpdateAsync(int id, GoalInput input)
    {
        var category = await ValidateAsync(input);

        var goal = await _goals.GetByIdAsync(id) ?? throw new InvalidOperationException("La meta no existe.");
        Apply(goal, input);
        await _goals.UpdateAsync(goal);
        return ToDto(goal, category.Name);
    }

    public async Task DeleteAsync(int id)
    {
        var goal = await _goals.GetByIdAsync(id) ?? throw new InvalidOperationException("La meta no existe.");
        await _goals.DeleteAsync(goal);
    }

    // ---------- Status (rule 5.6) ----------

    public async Task<List<GoalStatusDto>> GetGoalsStatusAsync()
    {
        var goals = await _goals.ListAsync(g => g.IsActive);
        if (goals.Count == 0)
            return new List<GoalStatusDto>();

        var today = _dateProvider.Today;
        var periods = goals.ToDictionary(g => g.Id, g => GetEffectivePeriod(g, today));

        // One pair of queries covers every goal; each goal then filters in memory.
        var from = periods.Values.Min(p => p.Start);
        var to = periods.Values.Max(p => p.End);
        var oneTime = await _expenses.GetOneTimeByRangeAsync(from, to);

        var recurringTo = to < today ? to : today;
        var recurring = recurringTo >= from
            ? await _expenses.GetActiveRecurringAsync(from, recurringTo)
            : new List<Expense>();
        var converter = recurring.Count > 0 ? await _currency.GetConverterAsync() : null;

        var categories = (await _categories.ListAsync()).ToDictionary(c => c.Id);
        var assigned = await _budget.GetAssignedByCategoryAsync();

        var result = new List<GoalStatusDto>();
        foreach (var goal in goals)
        {
            var (start, end) = periods[goal.Id];
            var progress = goal.CategoryId is { } categoryId
                ? SumProgress(categoryId, start, end, today, oneTime, recurring, converter)
                : 0m;

            categories.TryGetValue(goal.CategoryId ?? 0, out var category);
            decimal? planned = goal.CategoryId is { } id && assigned.TryGetValue(id, out var amount) ? amount : null;

            var dto = Evaluate(goal, start, end, today, progress, planned);
            dto.Goal.CategoryName = category?.Name;
            result.Add(dto);
        }

        return result
            .OrderBy(s => s.State is GoalState.Completed or GoalState.Missed)
            .ThenBy(s => s.Goal.EndDate)
            .ThenBy(s => s.Goal.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    // ---------- Calculation ----------

    /// <summary>A monthly goal restarts every month; the others use their stored dates.</summary>
    private static (DateOnly Start, DateOnly End) GetEffectivePeriod(Goal goal, DateOnly today)
    {
        if (goal.Period == GoalPeriod.Monthly)
            return (new DateOnly(today.Year, today.Month, 1), new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)));

        return (goal.StartDate, goal.EndDate);
    }

    // Spent so far: one-time expenses in the period, plus recurring occurrences that already happened.
    private static decimal SumProgress(
        int categoryId, DateOnly start, DateOnly end, DateOnly today,
        List<Expense> oneTime, List<Expense> recurring, CurrencyConverter? converter)
    {
        var total = oneTime
            .Where(e => e.CategoryId == categoryId && e.Date >= start && e.Date <= end)
            .Sum(e => e.AmountInBase);

        var upTo = end < today ? end : today;
        if (upTo < start)
            return total;

        foreach (var expense in recurring.Where(e => e.CategoryId == categoryId && e.Frequency is not null))
        {
            var count = RecurrenceCalculator.GetOccurrences(expense.Date, expense.Frequency!.Value, expense.EndDate, start, upTo).Count;
            if (count > 0)
                total += converter!.ToBase(expense.Amount, expense.Currency) * count;
        }

        return total;
    }

    private static GoalStatusDto Evaluate(Goal goal, DateOnly start, DateOnly end, DateOnly today, decimal progress, decimal? planned)
    {
        var target = goal.TargetAmount;
        var remaining = Math.Max(0m, target - progress);

        var dto = new GoalStatusDto
        {
            Goal = ToDto(goal, null),
            Progress = progress,
            ProgressPercent = target > 0 ? Math.Round(progress / target * 100m, 1) : 0m,
            Remaining = remaining,
            Projection = progress,
            PlannedMonthly = planned
        };
        // For monthly goals the dates shown are the current month, not the stored ones.
        dto.Goal.StartDate = start;
        dto.Goal.EndDate = end;

        if (progress >= target)
        {
            dto.State = GoalState.Completed;
            dto.RemainingMonths = Math.Max(1, CompleteMonths(today > start ? today : start, end));
            return dto;
        }

        if (start > today)
        {
            dto.State = GoalState.NotStarted;
            dto.RemainingMonths = Math.Max(1, CompleteMonths(start, end));
            dto.RequiredMonthly = Round(remaining / dto.RemainingMonths);
            return dto;
        }

        if (end < today)
        {
            dto.State = GoalState.Missed;
            dto.Shortfall = remaining;
            return dto;
        }

        if (goal.Period == GoalPeriod.Monthly)
        {
            // Within a month there are no "remaining months": project the current pace to month end.
            dto.RemainingMonths = 1;
            dto.RequiredMonthly = remaining;
            dto.AverageMonthly = progress;
            dto.Projection = Round(progress / today.Day * DateTime.DaysInMonth(today.Year, today.Month));
        }
        else
        {
            var totalMonths = MonthSpan(start, end);
            var elapsed = Math.Clamp(MonthSpan(start, today), 1, totalMonths);

            dto.RemainingMonths = Math.Max(1, CompleteMonths(today, end));
            dto.RequiredMonthly = Round(remaining / dto.RemainingMonths);
            dto.AverageMonthly = Round(progress / elapsed);
            dto.Projection = Round(progress + dto.AverageMonthly * dto.RemainingMonths);
        }

        dto.State = dto.Projection >= target ? GoalState.OnTrack : GoalState.Behind;
        dto.Shortfall = Math.Max(0m, target - dto.Projection);
        dto.PlanBelowRequired = planned is not null && dto.RequiredMonthly > planned.Value;
        return dto;
    }

    /// <summary>Whole months between two dates (a month only counts once its day has been reached).</summary>
    private static int CompleteMonths(DateOnly from, DateOnly to)
    {
        var months = (to.Year - from.Year) * 12 + to.Month - from.Month;
        if (to.Day < from.Day)
            months--;
        return Math.Max(0, months);
    }

    /// <summary>Calendar months touched from one date to another, both included.</summary>
    private static int MonthSpan(DateOnly from, DateOnly to)
        => (to.Year - from.Year) * 12 + to.Month - from.Month + 1;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    // ---------- Helpers ----------

    private async Task<Category> ValidateAsync(GoalInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("El nombre es obligatorio.");
        if (input.TargetAmount <= 0)
            throw new ArgumentException("El objetivo debe ser mayor que cero.");
        if (!Enum.IsDefined(input.Period))
            throw new ArgumentException("El periodo no es válido.");

        switch (input.Period)
        {
            case GoalPeriod.Annual when input.Year < 2000 || input.Year > 2200:
                throw new ArgumentException("El año no es válido.");
            case GoalPeriod.Custom when input.EndDate < input.StartDate:
                throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.");
        }

        return await _categories.GetByIdAsync(input.CategoryId)
            ?? throw new InvalidOperationException("La categoría vinculada no existe.");
    }

    private void Apply(Goal goal, GoalInput input)
    {
        goal.Name = input.Name.Trim();
        goal.TargetAmount = input.TargetAmount;
        goal.Period = input.Period;
        goal.CategoryId = input.CategoryId;
        goal.Icon = string.IsNullOrWhiteSpace(input.Icon) ? null : input.Icon.Trim();

        switch (input.Period)
        {
            case GoalPeriod.Annual:
                goal.StartDate = new DateOnly(input.Year, 1, 1);
                goal.EndDate = new DateOnly(input.Year, 12, 31);
                break;
            case GoalPeriod.Monthly:
                var today = _dateProvider.Today;
                goal.StartDate = new DateOnly(today.Year, today.Month, 1);
                goal.EndDate = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                break;
            default:
                goal.StartDate = input.StartDate;
                goal.EndDate = input.EndDate;
                break;
        }
    }

    private static GoalDto ToDto(Goal goal, string? categoryName) => new()
    {
        Id = goal.Id,
        Name = goal.Name,
        TargetAmount = goal.TargetAmount,
        Period = goal.Period,
        StartDate = goal.StartDate,
        EndDate = goal.EndDate,
        CategoryId = goal.CategoryId,
        CategoryName = categoryName,
        Icon = goal.Icon
    };
}
