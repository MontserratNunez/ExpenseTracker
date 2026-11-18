using ExpenseTracker.Core.Domain.Entities;

namespace ExpenseTracker.Core.Application.Interfaces;

public interface IExpenseRepository : IRepository<Expense>
{
    /// <summary>One-time expenses with a date in [from, to], including their category.</summary>
    Task<List<Expense>> GetOneTimeByRangeAsync(DateOnly from, DateOnly to);

    /// <summary>Active recurring expense templates that may occur within [from, to], including their category.</summary>
    Task<List<Expense>> GetActiveRecurringAsync(DateOnly from, DateOnly to);
}
