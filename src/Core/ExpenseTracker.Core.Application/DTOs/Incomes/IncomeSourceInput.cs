using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Incomes;

public class IncomeSourceInput
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

    [Display(Name = "Frecuencia")]
    public Frequency Frequency { get; set; } = Frequency.Monthly;

    [Display(Name = "Fecha de inicio")]
    public DateOnly StartDate { get; set; }

    [Display(Name = "Fecha de fin")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "Activa")]
    public bool IsActive { get; set; } = true;
}
