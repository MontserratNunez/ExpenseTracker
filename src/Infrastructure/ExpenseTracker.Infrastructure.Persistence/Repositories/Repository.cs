using System.Linq.Expressions;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic repository. Each operation uses its own DbContext (D4) so it is safe in a Blazor Server circuit.
/// Every lookup goes through the global user filter, so another user's rows are never found.
/// </summary>
public class Repository<T> : IRepository<T> where T : UserOwnedEntity
{
    protected readonly IDbContextFactory<AppDbContext> Factory;

    public Repository(IDbContextFactory<AppDbContext> factory)
    {
        Factory = factory;
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        await using var context = await Factory.CreateDbContextAsync();
        return await context.Set<T>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    }

    public virtual async Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null)
    {
        await using var context = await Factory.CreateDbContextAsync();
        IQueryable<T> query = context.Set<T>().AsNoTracking();
        if (predicate is not null)
            query = query.Where(predicate);
        return await query.ToListAsync();
    }

    public virtual async Task AddAsync(T entity)
    {
        await using var context = await Factory.CreateDbContextAsync();
        context.Set<T>().Add(entity);
        await context.SaveChangesAsync();
    }

    public virtual async Task UpdateAsync(T entity)
    {
        await using var context = await Factory.CreateDbContextAsync();

        // Loaded through the user filter: an id from another user is "not found".
        var existing = await context.Set<T>().FirstOrDefaultAsync(e => e.Id == entity.Id)
            ?? throw new InvalidOperationException("El registro no existe.");

        // Copy scalar values only; ignore navigation properties and ownership fields.
        var userId = existing.UserId;
        var createdAt = existing.CreatedAtUtc;
        context.Entry(existing).CurrentValues.SetValues(entity);
        existing.UserId = userId;
        existing.CreatedAtUtc = createdAt;

        await context.SaveChangesAsync();
    }

    public virtual async Task DeleteAsync(T entity)
    {
        await using var context = await Factory.CreateDbContextAsync();

        var existing = await context.Set<T>().FirstOrDefaultAsync(e => e.Id == entity.Id)
            ?? throw new InvalidOperationException("El registro no existe.");

        context.Set<T>().Remove(existing);
        await context.SaveChangesAsync();
    }
}
