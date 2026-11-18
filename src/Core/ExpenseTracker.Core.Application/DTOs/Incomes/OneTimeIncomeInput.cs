using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Incomes;

public class OneTimeIncomeInput
{
    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(200, ErrorMessage = "La descripción no puede superar {1} caracteres.")]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "El monto debe ser mayor que cero.")]
    [Display(Name = "Monto")]
    public decimal Amount { get; set; }

    [Display(Name = "Moneda")]
    public Currency Currency { get; set; } = Currency.DOP;

    [Display(Name = "Fecha")]
    public DateOnly Date { get; set; }
}
