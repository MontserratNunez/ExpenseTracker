using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Helpers;

namespace ExpenseTracker.Core.Application.Services;

/// <summary>
/// Sources and rates loaded once. Every answer uses the revision in force in the month asked and the
/// rate of the date, never "the current amount".
/// </summary>
public class IncomeSnapshot
{
    private readonly CurrencyConverter _converter;

    public IncomeSnapshot(List<IncomeSource> sources, CurrencyConverter converter)
    {
        Sources = sources.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        _converter = converter;
    }

    /// <summary>Every source, ordered by name.</summary>
    public List<IncomeSource> Sources { get; }

    public IncomeSourceRevision? Revision(int sourceId, int year, int month)
    {
        var source = Sources.FirstOrDefault(s => s.Id == sourceId);
        return source is not null && source.HasIncomeIn(year, month)
            ? source.GetRevision(new DateOnly(year, month, 1))
            : null;
    }

    /// <summary>
    /// Monthly equivalent of one source, or of all of them (null), in the base currency with the rate
    /// of day 1. A source without income that month counts 0.
    /// </summary>
    public decimal MonthlyBase(int? sourceId, int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        return Sources
            .Where(s => (sourceId is null || s.Id == sourceId) && s.HasIncomeIn(year, month))
            .Sum(s =>
            {
                var revision = s.GetRevision(first)!;
                return RecurrenceCalculator.ToMonthlyAmount(
                    _converter.ToBase(revision.Amount, revision.Currency, first), revision.Frequency);
            });
    }

    /// <summary>Recurring income dates inside [from, to].</summary>
    public List<IncomeOccurrenceDto> Occurrences(DateOnly from, DateOnly to)
    {
        var result = new List<IncomeOccurrenceDto>();
        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
        {
            var monthEnd = month.AddMonths(1).AddDays(-1);
            var start = from > month ? from : month;
            var end = to < monthEnd ? to : monthEnd;

            foreach (var source in Sources.Where(s => s.IsActive))
            {
                var revision = source.GetRevision(month);
                if (revision is null)
                    continue;

                foreach (var date in RecurrenceCalculator.GetOccurrences(source.StartDate, revision.Frequency, source.EndDate, start, end))
                {
                    result.Add(new IncomeOccurrenceDto
                    {
                        SourceId = source.Id,
                        Name = source.Name,
                        Date = date,
                        Amount = revision.Amount,
                        Currency = revision.Currency,
                        AmountInBase = _converter.ToBase(revision.Amount, revision.Currency, date)
                    });
                }
            }
        }

        return result;
    }

    public IncomeBasesDto Bases(int year, int month) => new()
    {
        Sources = Sources.Select(s => new IncomeBaseDto
        {
            SourceId = s.Id,
            Name = s.Name,
            MonthlyAmount = MonthlyBase(s.Id, year, month),
            HasIncome = s.HasIncomeIn(year, month)
        }).ToList(),
        RecurringTotal = MonthlyBase(null, year, month)
    };
}
