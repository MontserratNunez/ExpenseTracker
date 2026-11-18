using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Settings;

public class ExchangeRateDto
{
    public Currency Currency { get; set; }
    public decimal RateToBase { get; set; }
    public DateOnly UpdatedOn { get; set; }
}
