using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Tests.Components;

public class MoneyModel
{
    [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "El monto debe ser mayor que cero.")]
    [Display(Name = "Monto")]
    public decimal Amount { get; set; }

    [Display(Name = "Moneda")]
    public Currency Currency { get; set; } = Currency.DOP;
}

public class FieldsModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [Display(Name = "Nombre")]
    public string? Name { get; set; }

    [Range(typeof(decimal), "0", "100", ErrorMessage = "La tasa anual debe estar entre 0 y 100.")]
    [Display(Name = "Tasa")]
    public decimal Rate { get; set; }

    [Display(Name = "Fecha")]
    public DateOnly Date { get; set; }

    [Display(Name = "Fecha de fin")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "Frecuencia")]
    public Frequency? Frequency { get; set; }

    [Display(Name = "Notas")]
    public string? Notes { get; set; }

    [Display(Name = "Activa")]
    public bool Active { get; set; }
}
