using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Tests.Fakes;

public class FakeBudgetPlanRepository : FakeRepository<BudgetPlan>, IBudgetPlanRepository
{
    public Task<List<BudgetPlan>> GetAllWithAllocationsAsync()
        => Task.FromResult(Items.Select(Clone).ToList());

    public async Task SaveAsync(BudgetPlan plan, BudgetPlan? closedPlan = null)
    {
        if (closedPlan is not null)
            Items.Single(p => p.Id == closedPlan.Id).EffectiveTo = closedPlan.EffectiveTo;

        if (plan.Id == 0)
            await AddAsync(Clone(plan));
        else
            await UpdateAsync(Clone(plan));

        if (plan.Id == 0)
            plan.Id = Items[^1].Id;
    }

    /// <summary>Adds an allocation to a plan without end that covers every month of the tests.</summary>
    public void Allocate(int categoryId, decimal value, AllocationType type = AllocationType.FixedAmount, int? source = null)
    {
        if (Items.Count == 0)
            AddAsync(new BudgetPlan { Name = "Plan", EffectiveFrom = new DateOnly(2020, 1, 1) });

        Items[0].Allocations.Add(new BudgetAllocation
        {
            CategoryId = categoryId, Value = value, AllocationType = type, IncomeSourceId = source
        });
    }

    // Copies, like the database: changing a loaded plan does not change the stored one.
    private static BudgetPlan Clone(BudgetPlan plan) => new()
    {
        Id = plan.Id,
        Name = plan.Name,
        EffectiveFrom = plan.EffectiveFrom,
        EffectiveTo = plan.EffectiveTo,
        Allocations = plan.Allocations.Select(a => new BudgetAllocation
        {
            CategoryId = a.CategoryId, Value = a.Value, AllocationType = a.AllocationType, IncomeSourceId = a.IncomeSourceId
        }).ToList()
    };
}
