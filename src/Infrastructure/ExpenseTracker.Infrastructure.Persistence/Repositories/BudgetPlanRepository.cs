using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Persistence.Repositories;

public class BudgetPlanRepository : Repository<BudgetPlan>, IBudgetPlanRepository
{
    public BudgetPlanRepository(IDbContextFactory<AppDbContext> factory) : base(factory)
    {
    }

    public async Task<List<BudgetPlan>> GetAllWithAllocationsAsync()
    {
        await using var context = await Factory.CreateDbContextAsync();
        return await context.BudgetPlans
            .AsNoTracking()
            .Include(p => p.Allocations)
            .ToListAsync();
    }

    public async Task SaveAsync(BudgetPlan plan, BudgetPlan? closedPlan = null)
    {
        await using var context = await Factory.CreateDbContextAsync();

        // Loaded through the user filter: another user's plan is "not found".
        if (closedPlan is not null)
        {
            var closed = await context.BudgetPlans.FirstOrDefaultAsync(p => p.Id == closedPlan.Id)
                ?? throw new InvalidOperationException("El plan no existe.");
            closed.EffectiveTo = closedPlan.EffectiveTo;
        }

        BudgetPlan target;
        if (plan.Id == 0)
        {
            target = new BudgetPlan();
            context.BudgetPlans.Add(target);
        }
        else
        {
            target = await context.BudgetPlans.Include(p => p.Allocations).FirstOrDefaultAsync(p => p.Id == plan.Id)
                ?? throw new InvalidOperationException("El plan no existe.");
        }

        target.Name = plan.Name;
        target.EffectiveFrom = plan.EffectiveFrom;
        target.EffectiveTo = plan.EffectiveTo;
        target.SavingsType = plan.SavingsType;
        target.SavingsValue = plan.SavingsValue;
        target.SavingsIncomeSourceId = plan.SavingsIncomeSourceId;
        target.AlertThresholdPercent = plan.AlertThresholdPercent;

        // Allocations are matched by category: there is one per category in each plan.
        var wanted = plan.Allocations.ToDictionary(a => a.CategoryId);
        foreach (var current in target.Allocations.ToList())
        {
            if (!wanted.ContainsKey(current.CategoryId))
                context.BudgetAllocations.Remove(current);
        }

        foreach (var allocation in wanted.Values)
        {
            var current = target.Allocations.FirstOrDefault(a => a.CategoryId == allocation.CategoryId);
            if (current is null)
            {
                current = new BudgetAllocation { CategoryId = allocation.CategoryId };
                target.Allocations.Add(current);
            }

            current.AllocationType = allocation.AllocationType;
            current.Value = allocation.Value;
            current.IncomeSourceId = allocation.IncomeSourceId;
        }

        await context.SaveChangesAsync();
        plan.Id = target.Id;
    }
}
