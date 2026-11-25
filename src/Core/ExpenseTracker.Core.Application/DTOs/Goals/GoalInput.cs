using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Goals;

public class GoalInput
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "El objetivo debe ser mayor que cero.")]
    [Display(Name = "Monto objetivo")]
    public decimal TargetAmount { get; set; }

    [Display(Name = "Periodo")]
    public GoalPeriod Period { get; set; } = GoalPeriod.Annual;

    /// <summary>Used when <see cref="Period"/> is Annual: the goal runs from 1 Jan to 31 Dec of this year.</summary>
    [Range(2000, 2200, ErrorMessage = "El año no es válido.")]
    [Display(Name = "Año")]
    public int Year { get; set; }

    /// <summary>Used when <see cref="Period"/> is Custom.</summary>
    [Display(Name = "Fecha de inicio")]
    public DateOnly StartDate { get; set; }

    /// <summary>Used when <see cref="Period"/> is Custom.</summary>
    [Display(Name = "Fecha de fin")]
    public DateOnly EndDate { get; set; }

    /// <summary>Category whose spending counts toward the goal (normally the savings category).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione la categoría vinculada.")]
    [Display(Name = "Categoría")]
    public int CategoryId { get; set; }

    /// <summary>Bootstrap Icons name, picked from a curated list in the form. Optional.</summary>
    [StringLength(50, ErrorMessage = "El ícono no es válido.")]
    [Display(Name = "Ícono")]
    public string? Icon { get; set; }
}
