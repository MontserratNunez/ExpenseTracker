using ExpenseTracker.Core.Domain.Entities;

namespace ExpenseTracker.Core.Application.Interfaces;

public interface IBudgetPlanRepository : IRepository<BudgetPlan>
{
    /// <summary>Every plan of the user with its allocations.</summary>
    Task<List<BudgetPlan>> GetAllWithAllocationsAsync();

    /// <summary>
    /// Adds or updates a plan, replacing its allocations, and optionally stores the new end of another plan.
    /// Both changes are saved together, so two plans are never left covering the same month.
    /// </summary>
    Task SaveAsync(BudgetPlan plan, BudgetPlan? closedPlan = null);
}
