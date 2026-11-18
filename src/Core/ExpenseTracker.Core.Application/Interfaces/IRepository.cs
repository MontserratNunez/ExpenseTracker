using System.Linq.Expressions;
using ExpenseTracker.Core.Domain.Common;

namespace ExpenseTracker.Core.Application.Interfaces;

public interface IRepository<T> where T : UserOwnedEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
