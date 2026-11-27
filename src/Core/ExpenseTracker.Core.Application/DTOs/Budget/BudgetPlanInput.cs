using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Core.Application.DTOs.Budget;

public class BudgetPlanInput
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar 100 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Any day of the first month; it is stored as day 1.</summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Any day of the last month (stored as its last day), or null when the plan has no end.</summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>New plans only: plan whose allocations are copied.</summary>
    public int? CopyFromPlanId { get; set; }

    /// <summary>The allocations of the plan (value 0 = none). Null keeps the current or copied ones.</summary>
    public List<AllocationInput>? Allocations { get; set; }

    /// <summary>The savings reserve. Null keeps the current or copied one.</summary>
    public SavingsInput? Savings { get; set; }
}
