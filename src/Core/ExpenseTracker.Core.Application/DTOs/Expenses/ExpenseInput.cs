using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Expenses;

public class ExpenseInput
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "El monto debe ser mayor que cero.")]
    [Display(Name = "Monto")]
    public decimal Amount { get; set; }

    [Display(Name = "Moneda")]
    public Currency Currency { get; set; } = Currency.DOP;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccione una categoría.")]
    [Display(Name = "Categoría")]
    public int CategoryId { get; set; }

    /// <summary>Date of a one-time expense, or first occurrence of a recurring one.</summary>
    [Display(Name = "Fecha")]
    public DateOnly Date { get; set; }

    [Display(Name = "Es recurrente")]
    public bool IsRecurring { get; set; }

    /// <summary>Required when <see cref="IsRecurring"/> is true.</summary>
    [Display(Name = "Frecuencia")]
    public Frequency? Frequency { get; set; }

    [Display(Name = "Fecha de fin")]
    public DateOnly? EndDate { get; set; }

    [StringLength(500, ErrorMessage = "Las notas no pueden superar {1} caracteres.")]
    [Display(Name = "Notas")]
    public string? Notes { get; set; }
}
