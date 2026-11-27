using System.Globalization;
using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.Services;

/// <summary>
/// Plans and their validity. A plan rules whole months, two plans never share a month, and the months
/// before the current one keep the plan they were evaluated with.
/// </summary>
public class BudgetPlanService
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-DO");

    private readonly IBudgetPlanRepository _plans;
    private readonly IRepository<Category> _categories;
    private readonly IRepository<IncomeSource> _sources;
    private readonly IncomeService _income;
    private readonly IDateProvider _dateProvider;

    public BudgetPlanService(
        IBudgetPlanRepository plans,
        IRepository<Category> categories,
        IRepository<IncomeSource> sources,
        IncomeService income,
        IDateProvider dateProvider)
    {
        _plans = plans;
        _categories = categories;
        _sources = sources;
        _income = income;
        _dateProvider = dateProvider;
    }

    private DateOnly CurrentMonth => FirstDay(_dateProvider.Today);

    // ---------- Queries ----------

    /// <summary>The plan in force that month, or null.</summary>
    public async Task<BudgetPlan?> GetPlanForMonthAsync(int year, int month)
        => (await _plans.GetAllWithAllocationsAsync()).FirstOrDefault(p => p.AppliesTo(year, month));

    /// <summary>Every plan, oldest first.</summary>
    public async Task<List<BudgetPlanDto>> GetTimelineAsync()
        => (await _plans.GetAllWithAllocationsAsync())
            .OrderBy(p => p.EffectiveFrom)
            .Select(ToDto)
            .ToList();

    /// <summary>Data for the "Edit plan" table: every category with its allocation in that plan (value 0 = none).</summary>
    public async Task<AllocationEditorDto> GetEditorAsync(int planId)
    {
        var plan = Find(await _plans.GetAllWithAllocationsAsync(), planId);
        var categories = await _categories.ListAsync();
        var byCategory = plan.Allocations.ToDictionary(a => a.CategoryId);

        var month = plan.EffectiveFrom > CurrentMonth ? plan.EffectiveFrom : CurrentMonth;
        var editor = new AllocationEditorDto { IncomeBases = await _income.GetIncomeBasesAsync(month.Year, month.Month) };
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

    /// <summary>What saving this period would do: which plan gets closed and whether it overlaps another one.</summary>
    public async Task<PlanPeriodPreview> PreviewAsync(BudgetPlanInput input, int? editingId = null)
    {
        var plans = await _plans.GetAllWithAllocationsAsync();
        var editing = editingId is { } id ? Find(plans, id) : null;
        var from = editing is not null && editing.EffectiveFrom < CurrentMonth ? CurrentMonth : FirstDay(input.EffectiveFrom);
        var to = input.EffectiveTo is { } end ? LastDay(end) : (DateOnly?)null;

        var toClose = editing is null ? OpenPlanStartedBefore(plans, from) : null;
        var preview = new PlanPeriodPreview();
        if (toClose is not null)
            preview.Notice = $"El plan «{toClose.Name}» terminará en {MonthText(from.AddMonths(-1))}.";

        var conflict = FindOverlap(plans, from, to, editing, toClose);
        if (conflict is null)
            return preview;

        if (conflict.EffectiveFrom > from)
        {
            preview.SuggestedEnd = conflict.EffectiveFrom.AddDays(-1);
            preview.Conflict = $"Se cruza con «{conflict.Name}», que empieza en {MonthText(conflict.EffectiveFrom)}. " +
                               $"Puedes recortar este plan hasta {MonthText(preview.SuggestedEnd.Value)} o cancelar.";
        }
        else
        {
            preview.Conflict = OverlapMessage(conflict);
        }

        return preview;
    }

    // ---------- Changes ----------

    /// <summary>
    /// Creates a plan from the current month on. An earlier plan without end is closed the month before;
    /// any other overlap is rejected. Returns the new id.
    /// </summary>
    public async Task<int> CreateAsync(BudgetPlanInput input)
    {
        var plans = await _plans.GetAllWithAllocationsAsync();
        var name = ValidateName(input.Name);
        var from = FirstDay(input.EffectiveFrom);
        var to = input.EffectiveTo is { } end ? LastDay(end) : (DateOnly?)null;
        ValidatePeriod(from, to);

        var toClose = OpenPlanStartedBefore(plans, from);
        var conflict = FindOverlap(plans, from, to, null, toClose);
        if (conflict is not null)
            throw new InvalidOperationException(OverlapMessage(conflict));

        var allocations = input.Allocations is not null
            ? await BuildAllocationsAsync(input.Allocations)
            : input.CopyFromPlanId is { } sourceId
                ? Copy(Find(plans, sourceId).Allocations)
                : new List<BudgetAllocation>();

        if (toClose is not null)
            toClose.EffectiveTo = from.AddDays(-1);

        var plan = new BudgetPlan { Name = name, EffectiveFrom = from, EffectiveTo = to, Allocations = allocations };
        await _plans.SaveAsync(plan, toClose);
        return plan.Id;
    }

    /// <summary>
    /// A plan that started in a past month is not modified: a new version is created from the current month
    /// and the old one is closed the month before. Current-month and future plans are modified directly.
    /// Returns the id of the plan that now holds the changes.
    /// </summary>
    public async Task<int> UpdateAsync(int id, BudgetPlanInput input)
    {
        var plans = await _plans.GetAllWithAllocationsAsync();
        var plan = Find(plans, id);
        EnsureNotPast(plan);

        var name = ValidateName(input.Name);
        var needsVersion = plan.EffectiveFrom < CurrentMonth;
        var from = needsVersion ? CurrentMonth : FirstDay(input.EffectiveFrom);
        var to = input.EffectiveTo is { } end ? LastDay(end) : (DateOnly?)null;
        ValidatePeriod(from, to);

        var conflict = FindOverlap(plans, from, to, plan, null);
        if (conflict is not null)
            throw new InvalidOperationException(OverlapMessage(conflict));

        var allocations = input.Allocations is not null
            ? await BuildAllocationsAsync(input.Allocations)
            : Copy(plan.Allocations);

        if (!needsVersion)
        {
            plan.Name = name;
            plan.EffectiveFrom = from;
            plan.EffectiveTo = to;
            plan.Allocations = allocations;
            await _plans.SaveAsync(plan);
            return plan.Id;
        }

        // Saving without changes must not split the history.
        if (name == plan.Name && to == plan.EffectiveTo && Same(allocations, plan.Allocations))
            return plan.Id;

        var version = new BudgetPlan { Name = name, EffectiveFrom = from, EffectiveTo = to, Allocations = allocations };
        plan.EffectiveTo = from.AddDays(-1);
        await _plans.SaveAsync(version, plan);
        return version.Id;
    }

    /// <summary>
    /// Changes only the amount or percentage of a category already in the plan (the inline edit of the Plan page),
    /// keeping its type and income source. Returns the id of the plan that now holds the change.
    /// </summary>
    public async Task<int> SetValueAsync(int planId, int categoryId, decimal value)
    {
        var plan = Find(await _plans.GetAllWithAllocationsAsync(), planId);
        var inputs = plan.Allocations.Select(a => new AllocationInput
        {
            CategoryId = a.CategoryId,
            AllocationType = a.AllocationType,
            Value = a.Value,
            IncomeSourceId = a.IncomeSourceId
        }).ToList();

        var allocation = inputs.FirstOrDefault(a => a.CategoryId == categoryId)
            ?? throw new InvalidOperationException("Esa categoría no está en el plan.");
        if (value <= 0)
            throw new ArgumentException("El valor debe ser mayor que cero.");
        allocation.Value = value;

        return await UpdateAsync(planId, new BudgetPlanInput
        {
            Name = plan.Name,
            EffectiveFrom = plan.EffectiveFrom,
            EffectiveTo = plan.EffectiveTo,
            Allocations = inputs
        });
    }

    /// <summary>Finishes the plan in force at the end of the given month (the current one or a later one).</summary>
    public async Task EndAsync(int id, DateOnly endMonth)
    {
        var plans = await _plans.GetAllWithAllocationsAsync();
        var plan = Find(plans, id);
        EnsureNotPast(plan);
        if (plan.EffectiveFrom > CurrentMonth)
            throw new InvalidOperationException("Un plan futuro no se finaliza: elimínelo.");

        var end = LastDay(endMonth);
        if (end < CurrentMonth)
            throw new ArgumentException("El plan no puede terminar antes del mes actual.");

        var conflict = FindOverlap(plans, plan.EffectiveFrom, end, plan, null);
        if (conflict is not null)
            throw new InvalidOperationException(OverlapMessage(conflict));

        plan.EffectiveTo = end;
        await _plans.SaveAsync(plan);
    }

    /// <summary>Only future plans can be deleted: the others are part of the history.</summary>
    public async Task DeleteAsync(int id)
    {
        var plan = Find(await _plans.GetAllWithAllocationsAsync(), id);
        if (plan.EffectiveFrom <= CurrentMonth)
            throw new InvalidOperationException("Solo se pueden eliminar planes futuros. Un plan vigente se finaliza.");

        await _plans.DeleteAsync(plan);
    }

    // ---------- Rules ----------

    private void ValidatePeriod(DateOnly from, DateOnly? to)
    {
        if (from < CurrentMonth)
            throw new ArgumentException("El plan no puede empezar antes del mes actual.");
        if (to < from)
            throw new ArgumentException("El fin no puede ser anterior al inicio.");
    }

    private void EnsureNotPast(BudgetPlan plan)
    {
        if (plan.EffectiveTo < CurrentMonth)
            throw new InvalidOperationException("Los planes pasados no se modifican.");
    }

    private static string ValidateName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length == 0)
            throw new ArgumentException("El nombre es obligatorio.");
        if (name.Length > 100)
            throw new ArgumentException("El nombre no puede superar 100 caracteres.");
        return name;
    }

    private static BudgetPlan? OpenPlanStartedBefore(List<BudgetPlan> plans, DateOnly from)
        => plans.FirstOrDefault(p => p.EffectiveTo is null && p.EffectiveFrom < from);

    /// <summary>The first plan that shares a month with the period, ignoring the edited and the closing ones.</summary>
    private static BudgetPlan? FindOverlap(
        List<BudgetPlan> plans, DateOnly from, DateOnly? to, BudgetPlan? editing, BudgetPlan? closing)
        => plans
            .Where(p => p.Id != editing?.Id && p.Id != closing?.Id)
            .Where(p => p.EffectiveFrom <= (to ?? DateOnly.MaxValue) && (p.EffectiveTo ?? DateOnly.MaxValue) >= from)
            .OrderBy(p => p.EffectiveFrom)
            .FirstOrDefault();

    private static string OverlapMessage(BudgetPlan other)
    {
        var period = other.EffectiveTo is { } end
            ? $"de {MonthText(other.EffectiveFrom)} a {MonthText(end)}"
            : $"desde {MonthText(other.EffectiveFrom)}";
        return $"Se cruza con «{other.Name}» ({period}). Dos planes no pueden cubrir el mismo mes.";
    }

    // ---------- Allocations ----------

    /// <summary>A value above 0 is an allocation; 0 leaves the category out. Nothing is saved if a row is invalid.</summary>
    private async Task<List<BudgetAllocation>> BuildAllocationsAsync(List<AllocationInput> inputs)
    {
        if (inputs.GroupBy(i => i.CategoryId).Any(g => g.Count() > 1))
            throw new ArgumentException("Hay categorías repetidas en el plan.");

        var result = new List<BudgetAllocation>();
        foreach (var input in inputs.Where(i => i.Value > 0))
        {
            var isPercent = input.AllocationType == AllocationType.PercentageOfIncome;
            if (!Enum.IsDefined(input.AllocationType))
                throw new ArgumentException("El tipo de asignación no es válido.");
            if (isPercent && input.Value > 100)
                throw new ArgumentException("El porcentaje no puede superar 100.");
            if (await _categories.GetByIdAsync(input.CategoryId) is null)
                throw new InvalidOperationException("La categoría no existe.");
            if (isPercent && input.IncomeSourceId is { } sourceId && await _sources.GetByIdAsync(sourceId) is null)
                throw new InvalidOperationException("La fuente de ingreso no existe.");

            result.Add(new BudgetAllocation
            {
                CategoryId = input.CategoryId,
                AllocationType = input.AllocationType,
                Value = input.Value,
                // The source only applies to percentages.
                IncomeSourceId = isPercent ? input.IncomeSourceId : null
            });
        }

        return result;
    }

    private static List<BudgetAllocation> Copy(IEnumerable<BudgetAllocation> allocations)
        => allocations.Select(a => new BudgetAllocation
        {
            CategoryId = a.CategoryId,
            AllocationType = a.AllocationType,
            Value = a.Value,
            IncomeSourceId = a.IncomeSourceId
        }).ToList();

    private static bool Same(List<BudgetAllocation> left, List<BudgetAllocation> right)
        => left.Select(Key).ToHashSet().SetEquals(right.Select(Key));

    private static (int, AllocationType, decimal, int?) Key(BudgetAllocation a)
        => (a.CategoryId, a.AllocationType, a.Value, a.IncomeSourceId);

    // ---------- Helpers ----------

    private BudgetPlanDto ToDto(BudgetPlan plan)
    {
        var current = CurrentMonth;
        var isPast = plan.EffectiveTo < current;
        return new BudgetPlanDto
        {
            Id = plan.Id,
            Name = plan.Name,
            EffectiveFrom = plan.EffectiveFrom,
            EffectiveTo = plan.EffectiveTo,
            IsFuture = plan.EffectiveFrom > current,
            IsPast = isPast,
            EditCreatesVersion = !isPast && plan.EffectiveFrom < current
        };
    }

    private static BudgetPlan Find(List<BudgetPlan> plans, int id)
        => plans.FirstOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("El plan no existe.");

    private static DateOnly FirstDay(DateOnly date) => new(date.Year, date.Month, 1);

    private static DateOnly LastDay(DateOnly date) => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    private static string MonthText(DateOnly date) => date.ToString("MMMM yyyy", Culture);
}
