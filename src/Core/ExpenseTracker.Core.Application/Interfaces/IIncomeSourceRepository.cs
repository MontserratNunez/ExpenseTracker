using ExpenseTracker.Core.Domain.Entities;

namespace ExpenseTracker.Core.Application.Interfaces;

public interface IIncomeSourceRepository : IRepository<IncomeSource>
{
    /// <summary>Every source of the user with its revisions.</summary>
    Task<List<IncomeSource>> GetAllWithRevisionsAsync();

    /// <summary>Adds or updates a source together with its revisions (matched by <c>EffectiveFrom</c>).</summary>
    Task SaveAsync(IncomeSource source);
}
