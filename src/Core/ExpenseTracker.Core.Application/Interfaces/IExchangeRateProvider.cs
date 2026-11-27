using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.Interfaces;

public interface IExchangeRateProvider
{
    /// <summary>Rate in force on a date; 1 for the base currency.</summary>
    Task<decimal> GetRateAsync(Currency currency, DateOnly date);

    Task<decimal> ConvertToBaseAsync(decimal amount, Currency currency, DateOnly date);
}
