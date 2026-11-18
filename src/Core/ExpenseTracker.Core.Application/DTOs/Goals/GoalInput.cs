using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Goals;

public class GoalInput
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "El objetivo debe ser mayor que cero.")]
    public decimal TargetAmount { get; set; }

    public GoalPeriod Period { get; set; } = GoalPeriod.Annual;

    /// <summary>Used when <see cref="Period"/> is Annual: the goal runs from 1 Jan to 31 Dec of this year.</summary>
    [Range(2000, 2200, ErrorMessage = "El año no es válido.")]
    public int Year { get; set; }

    /// <summary>Used when <see cref="Period"/> is Custom.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Used when <see cref="Period"/> is Custom.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Category whose spending counts toward the goal (normally the savings category).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione la categoría vinculada.")]
    public int CategoryId { get; set; }
}
