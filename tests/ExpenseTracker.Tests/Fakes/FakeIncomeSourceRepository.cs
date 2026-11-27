using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;

namespace ExpenseTracker.Tests.Fakes;

public class FakeIncomeSourceRepository : FakeRepository<IncomeSource>, IIncomeSourceRepository
{
    public Task<List<IncomeSource>> GetAllWithRevisionsAsync()
        => Task.FromResult(Items.Select(Clone).ToList());

    public async Task SaveAsync(IncomeSource source)
    {
        if (source.Id == 0)
        {
            var added = Clone(source);
            await AddAsync(added);
            source.Id = added.Id;
        }
        else
        {
            await UpdateAsync(Clone(source));
        }
    }

    // Copies, like the database: changing a loaded source does not change the stored one.
    private static IncomeSource Clone(IncomeSource source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        StartDate = source.StartDate,
        EndDate = source.EndDate,
        IsActive = source.IsActive,
        Revisions = source.Revisions.Select(r => new IncomeSourceRevision
        {
            IncomeSourceId = source.Id,
            EffectiveFrom = r.EffectiveFrom,
            Amount = r.Amount,
            Currency = r.Currency,
            Frequency = r.Frequency,
            Note = r.Note
        }).ToList()
    };
}
