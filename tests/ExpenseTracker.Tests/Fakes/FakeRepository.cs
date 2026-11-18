using System.Linq.Expressions;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Common;

namespace ExpenseTracker.Tests.Fakes;

/// <summary>In-memory repository for service tests. Ids are assigned on add, like the database does.</summary>
public class FakeRepository<T> : IRepository<T> where T : UserOwnedEntity
{
    private int _nextId = 1;

    public List<T> Items { get; } = new();

    public Task<T?> GetByIdAsync(int id) => Task.FromResult(Items.FirstOrDefault(e => e.Id == id));

    public Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null)
    {
        IEnumerable<T> query = Items;
        if (predicate is not null)
            query = query.Where(predicate.Compile());
        return Task.FromResult(query.ToList());
    }

    public Task AddAsync(T entity)
    {
        entity.Id = _nextId++;
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T entity)
    {
        var index = Items.FindIndex(e => e.Id == entity.Id);
        if (index < 0)
            throw new InvalidOperationException("El registro no existe.");
        Items[index] = entity;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity)
    {
        Items.RemoveAll(e => e.Id == entity.Id);
        return Task.CompletedTask;
    }
}
