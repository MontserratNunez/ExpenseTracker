using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Persistence.Repositories;

public class IncomeSourceRepository : Repository<IncomeSource>, IIncomeSourceRepository
{
    public IncomeSourceRepository(IDbContextFactory<AppDbContext> factory) : base(factory)
    {
    }

    public async Task<List<IncomeSource>> GetAllWithRevisionsAsync()
    {
        await using var context = await Factory.CreateDbContextAsync();
        return await context.IncomeSources
            .AsNoTracking()
            .Include(s => s.Revisions)
            .ToListAsync();
    }

    public async Task SaveAsync(IncomeSource source)
    {
        await using var context = await Factory.CreateDbContextAsync();

        IncomeSource target;
        if (source.Id == 0)
        {
            target = new IncomeSource();
            context.IncomeSources.Add(target);
        }
        else
        {
            // Loaded through the user filter: another user's source is "not found".
            target = await context.IncomeSources.Include(s => s.Revisions).FirstOrDefaultAsync(s => s.Id == source.Id)
                ?? throw new InvalidOperationException("La fuente de ingreso no existe.");
        }

        target.Name = source.Name;
        target.StartDate = source.StartDate;
        target.EndDate = source.EndDate;
        target.IsActive = source.IsActive;

        // Revisions are matched by month: there is one per month in each source.
        var wanted = source.Revisions.ToDictionary(r => r.EffectiveFrom);
        foreach (var current in target.Revisions.ToList())
        {
            if (!wanted.ContainsKey(current.EffectiveFrom))
                context.IncomeSourceRevisions.Remove(current);
        }

        foreach (var revision in wanted.Values)
        {
            var current = target.Revisions.FirstOrDefault(r => r.EffectiveFrom == revision.EffectiveFrom);
            if (current is null)
            {
                current = new IncomeSourceRevision { EffectiveFrom = revision.EffectiveFrom };
                target.Revisions.Add(current);
            }

            current.Amount = revision.Amount;
            current.Currency = revision.Currency;
            current.Frequency = revision.Frequency;
            current.Note = revision.Note;
        }

        await context.SaveChangesAsync();
        source.Id = target.Id;
    }
}
