using System.ComponentModel.DataAnnotations;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Settings;

public class ExchangeRateInput
{
    [Display(Name = "Moneda")]
    public Currency Currency { get; set; }

    [Range(typeof(decimal), "0.000001", "999999999", ErrorMessage = "La tasa debe ser mayor que cero.")]
    [Display(Name = "Tasa")]
    public decimal RateToBase { get; set; }

    /// <summary>Date from which the rate applies; null means today.</summary>
    public DateOnly? EffectiveFrom { get; set; }
}
