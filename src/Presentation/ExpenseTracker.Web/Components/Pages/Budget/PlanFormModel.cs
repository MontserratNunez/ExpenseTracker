using ExpenseTracker.Core.Application.DTOs.Budget;

namespace ExpenseTracker.Web.Components.Pages.Budget;

public enum PlanStart
{
    ThisMonth = 1,
    NextMonth = 2,
    FromMonth = 3
}

/// <summary>What the "Nuevo plan" / "Nombre y vigencia" form edits. Months are kept as their first day.</summary>
public class PlanFormModel
{
    public string Name { get; set; } = string.Empty;
    public PlanStart Start { get; set; } = PlanStart.ThisMonth;
    public DateOnly FromMonth { get; set; }
    public bool HasEnd { get; set; }
    public DateOnly EndMonth { get; set; }
    public int? CopyFromPlanId { get; set; }

    public DateOnly StartMonth(DateOnly currentMonth) => Start switch
    {
        PlanStart.ThisMonth => currentMonth,
        PlanStart.NextMonth => currentMonth.AddMonths(1),
        _ => FromMonth
    };

    public BudgetPlanInput ToInput(DateOnly currentMonth) => new()
    {
        Name = Name,
        EffectiveFrom = StartMonth(currentMonth),
        EffectiveTo = HasEnd ? EndMonth : null,
        CopyFromPlanId = CopyFromPlanId
    };
}
