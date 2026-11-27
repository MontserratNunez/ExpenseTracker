using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Settings;

public class ExchangeRateDto
{
    public Currency Currency { get; set; }
    public decimal RateToBase { get; set; }

    /// <summary>Date from which the rate applies.</summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Every rate of the currency, newest first (only filled by the list).</summary>
    public List<ExchangeRateDto> History { get; set; } = new();
}
