using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Debts;

public class DebtInput
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar {1} caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Tipo")]
    public DebtType DebtType { get; set; } = DebtType.Loan;

    [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "El monto original debe ser mayor que cero.")]
    [Display(Name = "Monto original")]
    public decimal OriginalAmount { get; set; }

    [Range(typeof(decimal), "0", "100", ErrorMessage = "La tasa anual debe estar entre 0 y 100.")]
    [Display(Name = "Tasa anual")]
    public decimal AnnualInterestRate { get; set; }

    [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "El monto pagado no puede ser negativo.")]
    [Display(Name = "Monto ya pagado")]
    public decimal InitialPaidAmount { get; set; }

    /// <summary>Leave at 0 to calculate it from <see cref="TermMonths"/>.</summary>
    [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "La cuota no puede ser negativa.")]
    [Display(Name = "Cuota mensual")]
    public decimal MonthlyPayment { get; set; }

    [Range(1, 28, ErrorMessage = "El día de pago debe estar entre 1 y 28.")]
    [Display(Name = "Día de pago")]
    public int PaymentDay { get; set; } = 1;

    [Display(Name = "Fecha de inicio")]
    public DateOnly StartDate { get; set; }

    [Range(1, 1200, ErrorMessage = "El plazo debe estar entre 1 y 1200 meses.")]
    [Display(Name = "Plazo")]
    public int? TermMonths { get; set; }

    [Display(Name = "Moneda")]
    public Currency Currency { get; set; } = Currency.DOP;
}
