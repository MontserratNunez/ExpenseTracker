using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Persistence.Repositories;

public class ExpenseRepository : Repository<Expense>, IExpenseRepository
{
    public ExpenseRepository(IDbContextFactory<AppDbContext> factory) : base(factory)
    {
    }

    public async Task<List<Expense>> GetOneTimeByRangeAsync(DateOnly from, DateOnly to)
    {
        await using var context = await Factory.CreateDbContextAsync();
        return await context.Expenses
            .AsNoTracking()
            .Include(e => e.Category)
            .Where(e => e.Type == ExpenseType.OneTime && e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date)
            .ToListAsync();
    }

    public async Task<List<Expense>> GetActiveRecurringAsync(DateOnly from, DateOnly to)
    {
        await using var context = await Factory.CreateDbContextAsync();
        return await context.Expenses
            .AsNoTracking()
            .Include(e => e.Category)
            .Where(e => e.Type == ExpenseType.Recurring
                        && e.IsActive
                        && e.Date <= to
                        && (e.EndDate == null || e.EndDate >= from))
            .ToListAsync();
    }
}
