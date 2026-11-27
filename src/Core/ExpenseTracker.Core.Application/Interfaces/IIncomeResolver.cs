using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;

namespace ExpenseTracker.Core.Application.Interfaces;

/// <summary>The only place that answers "how much was the income that month".</summary>
public interface IIncomeResolver
{
    /// <summary>Revision of a source in force in a month, or null when it does not apply.</summary>
    Task<IncomeSourceRevision?> GetRevisionAsync(int incomeSourceId, int year, int month);

    /// <summary>Monthly equivalent in the base currency (base of percentages). Null source = all of them.</summary>
    Task<decimal> GetMonthlyBaseAsync(int? incomeSourceId, int year, int month);

    /// <summary>Recurring income occurrences of a month, with the revision and rate of that month.</summary>
    Task<List<IncomeOccurrenceDto>> GetOccurrencesAsync(int year, int month);

    /// <summary>Loads sources and rates once, to resolve many months without more queries.</summary>
    Task<IncomeSnapshot> LoadSnapshotAsync();
}
