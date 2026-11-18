using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Incomes;

public class OneTimeIncomeInput
{
    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(200, ErrorMessage = "La descripción no puede superar {1} caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "El monto debe ser mayor que cero.")]
    public decimal Amount { get; set; }

    public Currency Currency { get; set; } = Currency.DOP;

    public DateOnly Date { get; set; }
}
