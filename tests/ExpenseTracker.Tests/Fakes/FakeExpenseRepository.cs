using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Tests.Fakes;

public class FakeExpenseRepository : FakeRepository<Expense>, IExpenseRepository
{
    private readonly FakeRepository<Category> _categories;

    public FakeExpenseRepository(FakeRepository<Category> categories)
    {
        _categories = categories;
    }

    public Task<List<Expense>> GetOneTimeByRangeAsync(DateOnly from, DateOnly to)
    {
        var result = Items
            .Where(e => e.Type == ExpenseType.OneTime && e.Date >= from && e.Date <= to)
            .Select(WithCategory)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<Expense>> GetActiveRecurringAsync(DateOnly from, DateOnly to)
    {
        var result = Items
            .Where(e => e.Type == ExpenseType.Recurring && e.IsActive && e.Date <= to && (e.EndDate == null || e.EndDate >= from))
            .Select(WithCategory)
            .ToList();
        return Task.FromResult(result);
    }

    // The real repository includes the category; mimic it here.
    private Expense WithCategory(Expense e)
    {
        e.Category = _categories.Items.FirstOrDefault(c => c.Id == e.CategoryId);
        return e;
    }
}
