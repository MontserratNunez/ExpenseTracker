using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.Core.Application.DTOs.Debts;

public class DebtPaymentInput
{
    [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "El monto debe ser mayor que cero.")]
    [Display(Name = "Monto")]
    public decimal Amount { get; set; }

    [Display(Name = "Fecha")]
    public DateOnly Date { get; set; }

    [StringLength(500, ErrorMessage = "Las notas no pueden superar {1} caracteres.")]
    [Display(Name = "Notas")]
    public string? Notes { get; set; }
}
