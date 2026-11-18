using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Expenses;

public class ExpenseInput
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar {1} caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    public Currency Currency { get; set; } = Currency.DOP;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccione una categoría.")]
    public int CategoryId { get; set; }

    /// <summary>Date of a one-time expense, or first occurrence of a recurring one.</summary>
    public DateOnly Date { get; set; }

    public bool IsRecurring { get; set; }

    /// <summary>Required when <see cref="IsRecurring"/> is true.</summary>
    public Frequency? Frequency { get; set; }

    public DateOnly? EndDate { get; set; }

    [StringLength(500, ErrorMessage = "Las notas no pueden superar {1} caracteres.")]
    public string? Notes { get; set; }
}
