using ExpenseTracker.Core.Domain.Common;

namespace ExpenseTracker.Core.Domain.Entities;

/// <summary>
/// A recurring income. Its amount, currency and frequency live in dated revisions, so a past month is
/// always calculated with the values it had.
/// </summary>
public class IncomeSource : UserOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public List<IncomeSourceRevision> Revisions { get; set; } = new();

    /// <summary>The revision in force in the month of <paramref name="date"/>, or null before the first one.</summary>
    public IncomeSourceRevision? GetRevision(DateOnly date)
    {
        var month = new DateOnly(date.Year, date.Month, 1);
        return Revisions.Where(r => r.EffectiveFrom <= month).MaxBy(r => r.EffectiveFrom);
    }

    /// <summary>True when the source has income in that month: active, started and not finished.</summary>
    public bool HasIncomeIn(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return IsActive
               && StartDate <= first.AddMonths(1).AddDays(-1)
               && (EndDate is null || EndDate >= first)
               && GetRevision(first) is not null;
    }
}
