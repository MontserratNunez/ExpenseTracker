using System.Globalization;
using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Core.Domain.Helpers;

namespace ExpenseTracker.Core.Application.Services;

public class IncomeService : IIncomeResolver
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-DO");

    private readonly IIncomeSourceRepository _sources;
    private readonly IRepository<OneTimeIncome> _oneTime;
    private readonly IBudgetPlanRepository _plans;
    private readonly CurrencyService _currency;
    private readonly IDateProvider _dateProvider;

    public IncomeService(
        IIncomeSourceRepository sources,
        IRepository<OneTimeIncome> oneTime,
        IBudgetPlanRepository plans,
        CurrencyService currency,
        IDateProvider dateProvider)
    {
        _sources = sources;
        _oneTime = oneTime;
        _plans = plans;
        _currency = currency;
        _dateProvider = dateProvider;
    }

    private DateOnly CurrentMonth => FirstDay(_dateProvider.Today);

    // ---------- Recurring sources ----------

    public async Task<List<IncomeSourceDto>> ListSourcesAsync()
    {
        var items = await _sources.GetAllWithRevisionsAsync();
        return items
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(ToDto)
            .ToList();
    }

    public async Task<IncomeSourceDto> CreateSourceAsync(IncomeSourceInput input)
    {
        Validate(input);

        var entity = new IncomeSource();
        Apply(entity, input);
        entity.Revisions.Add(new IncomeSourceRevision
        {
            EffectiveFrom = FirstDay(input.StartDate),
            Amount = input.Amount,
            Currency = input.Currency,
            Frequency = input.Frequency
        });
        await _sources.SaveAsync(entity);
        return ToDto(entity);
    }

    /// <summary>
    /// A new amount, currency or frequency becomes a revision from <c>ChangeFrom</c> (this month or later).
    /// Name changes affect nothing. Start, end and "active" changes that reach past months need
    /// <c>ConfirmPastChanges</c>.
    /// </summary>
    public async Task<IncomeSourceDto> UpdateSourceAsync(int id, IncomeSourceInput input)
    {
        var (source, impact) = await PrepareUpdateAsync(id, input);
        if (impact.Warning is not null && !input.ConfirmPastChanges)
            throw new InvalidOperationException(impact.Warning);

        await _sources.SaveAsync(source);
        return ToDto(source);
    }

    /// <summary>Past months that saving this edit would change.</summary>
    public async Task<PastImpactDto> PreviewUpdateAsync(int id, IncomeSourceInput input)
        => (await PrepareUpdateAsync(id, input)).Impact;

    /// <summary>New values from this month or a later one. Earlier revisions are not touched.</summary>
    public async Task CreateRevisionAsync(int sourceId, IncomeRevisionInput input, DateOnly effectiveFrom)
    {
        var source = await FindAsync(sourceId);
        ValidateValues(input.Amount, input.Currency, input.Frequency);
        SetRevision(source, ChangeMonth(source.StartDate, effectiveFrom), input.Amount, input.Currency, input.Frequency, input.Note);
        await _sources.SaveAsync(source);
    }

    /// <summary>Past months that a history correction would change.</summary>
    public async Task<PastImpactDto> PreviewCorrectionAsync(int sourceId, IncomeRevisionInput input)
        => (await PrepareCorrectionAsync(sourceId, input)).Impact;

    /// <summary>
    /// The only way to give past months other values: to record an earlier salary or fix a typo.
    /// Returns the months that changed.
    /// </summary>
    public async Task<PastImpactDto> CorrectHistoryAsync(int sourceId, IncomeRevisionInput input)
    {
        var (source, impact) = await PrepareCorrectionAsync(sourceId, input);
        await _sources.SaveAsync(source);
        return impact;
    }

    /// <summary>Only a source that never counted in a past month can be deleted; the others are finished.</summary>
    public async Task DeleteSourceAsync(int id)
    {
        var entity = await FindAsync(id);

        // Allocations and reserves can be a percentage of this source; the database would reject the delete.
        var plans = await _plans.GetAllWithAllocationsAsync();
        if (plans.Any(p => p.SavingsIncomeSourceId == id || p.Allocations.Any(a => a.IncomeSourceId == id)))
            throw new InvalidOperationException("La fuente se usa en un plan y no se puede eliminar. Para que deje de contar, póngale fecha de fin.");
        if (entity.IsActive && FirstDay(entity.StartDate) < CurrentMonth)
            throw new InvalidOperationException("La fuente ya cuenta en meses pasados y no se puede eliminar. Para que deje de contar, póngale fecha de fin.");

        await _sources.DeleteAsync(entity);
    }

    // ---------- IIncomeResolver ----------

    public async Task<IncomeSnapshot> LoadSnapshotAsync()
    {
        var sources = await _sources.GetAllWithRevisionsAsync();
        var converter = await _currency.GetConverterAsync(sources.SelectMany(s => s.Revisions).Select(r => r.Currency));
        return new IncomeSnapshot(sources, converter);
    }

    public async Task<IncomeSourceRevision?> GetRevisionAsync(int incomeSourceId, int year, int month)
        => (await LoadSnapshotAsync()).Revision(incomeSourceId, year, month);

    public async Task<decimal> GetMonthlyBaseAsync(int? incomeSourceId, int year, int month)
        => (await LoadSnapshotAsync()).MonthlyBase(incomeSourceId, year, month);

    public async Task<List<IncomeOccurrenceDto>> GetOccurrencesAsync(int year, int month)
    {
        var (from, to) = GetMonthRange(year, month);
        return (await LoadSnapshotAsync()).Occurrences(from, to);
    }

    // ---------- One-time income ----------

    public async Task<List<OneTimeIncomeDto>> ListOneTimeAsync(int year, int month)
    {
        var (from, to) = GetMonthRange(year, month);
        var items = await _oneTime.ListAsync(i => i.Date >= from && i.Date <= to);
        return items.OrderBy(i => i.Date).ThenBy(i => i.Id).Select(ToDto).ToList();
    }

    public async Task<OneTimeIncomeDto> CreateOneTimeAsync(OneTimeIncomeInput input)
    {
        Validate(input);

        var entity = new OneTimeIncome();
        await ApplyAsync(entity, input);
        await _oneTime.AddAsync(entity);
        return ToDto(entity);
    }

    public async Task<OneTimeIncomeDto> UpdateOneTimeAsync(int id, OneTimeIncomeInput input)
    {
        Validate(input);

        var entity = await _oneTime.GetByIdAsync(id)
            ?? throw new InvalidOperationException("El ingreso no existe.");
        await ApplyAsync(entity, input);
        await _oneTime.UpdateAsync(entity);
        return ToDto(entity);
    }

    public async Task DeleteOneTimeAsync(int id)
    {
        var entity = await _oneTime.GetByIdAsync(id)
            ?? throw new InvalidOperationException("El ingreso no existe.");
        await _oneTime.DeleteAsync(entity);
    }

    // ---------- Totals ----------

    /// <summary>Income of a month (rule 5.1): recurring occurrences plus one-time income, in the base currency.</summary>
    public async Task<MonthlyIncomeDto> GetMonthlyIncomeAsync(int year, int month)
    {
        var (from, to) = GetMonthRange(year, month);
        var result = new MonthlyIncomeDto { Year = year, Month = month };

        foreach (var group in (await GetOccurrencesAsync(year, month)).GroupBy(o => o.SourceId))
        {
            var first = group.First();
            result.Lines.Add(new IncomeLineDto
            {
                Name = first.Name,
                IsRecurring = true,
                SourceId = first.SourceId,
                Amount = first.Amount,
                Currency = first.Currency,
                Occurrences = group.Count(),
                AmountInBase = group.Sum(o => o.AmountInBase)
            });
        }

        var oneTime = await _oneTime.ListAsync(i => i.Date >= from && i.Date <= to);
        foreach (var income in oneTime.OrderBy(i => i.Date).ThenBy(i => i.Id))
        {
            result.Lines.Add(new IncomeLineDto
            {
                Name = income.Description,
                IsRecurring = false,
                SourceId = income.Id,
                Amount = income.Amount,
                Currency = income.Currency,
                Occurrences = 1,
                AmountInBase = income.AmountInBase
            });
        }

        result.RecurringTotal = result.Lines.Where(l => l.IsRecurring).Sum(l => l.AmountInBase);
        result.OneTimeTotal = result.Lines.Where(l => !l.IsRecurring).Sum(l => l.AmountInBase);
        result.Total = result.RecurringTotal + result.OneTimeTotal;
        return result;
    }

    /// <summary>Monthly equivalent, in the base currency, of the recurring sources with income this month.</summary>
    public async Task<decimal> GetRecurringMonthlyTotalAsync()
        => await GetMonthlyBaseAsync(null, CurrentMonth.Year, CurrentMonth.Month);

    /// <summary>Monthly equivalent of every source in a month, plus their total.</summary>
    public async Task<IncomeBasesDto> GetIncomeBasesAsync(int year, int month)
        => (await LoadSnapshotAsync()).Bases(year, month);

    /// <summary>
    /// Projected income for [from, to], keyed by the first day of each month it touches: recurring
    /// occurrences (with their scheduled changes) plus one-time income already registered in it.
    /// </summary>
    public async Task<Dictionary<DateOnly, decimal>> GetProjectedIncomeByMonthAsync(DateOnly from, DateOnly to)
    {
        var result = new Dictionary<DateOnly, decimal>();
        if (to < from)
            return result;

        for (var month = FirstDay(from); month <= to; month = month.AddMonths(1))
            result[month] = 0m;

        foreach (var occurrence in (await LoadSnapshotAsync()).Occurrences(from, to))
            result[FirstDay(occurrence.Date)] += occurrence.AmountInBase;

        foreach (var income in await _oneTime.ListAsync(i => i.Date >= from && i.Date <= to))
            result[FirstDay(income.Date)] += income.AmountInBase;

        return result;
    }

    // ---------- Changes to a source ----------

    private async Task<(IncomeSource Source, PastImpactDto Impact)> PrepareUpdateAsync(int id, IncomeSourceInput input)
    {
        Validate(input);

        var source = await FindAsync(id);
        var before = await FindAsync(id);
        var shown = ShownRevision(source);
        var valuesChanged = shown.Amount != input.Amount || shown.Currency != input.Currency || shown.Frequency != input.Frequency;

        Apply(source, input);

        // A start moved back takes the first values with it.
        var first = source.Revisions.MinBy(r => r.EffectiveFrom)!;
        if (FirstDay(source.StartDate) < first.EffectiveFrom)
            first.EffectiveFrom = FirstDay(source.StartDate);

        if (valuesChanged)
        {
            var month = ChangeMonth(source.StartDate, input.ChangeFrom ?? CurrentMonth);
            SetRevision(source, month, input.Amount, input.Currency, input.Frequency, input.ChangeNote);
        }

        return (source, Impact(before, source));
    }

    private async Task<(IncomeSource Source, PastImpactDto Impact)> PrepareCorrectionAsync(int sourceId, IncomeRevisionInput input)
    {
        ValidateValues(input.Amount, input.Currency, input.Frequency);
        var from = FirstDay(input.EffectiveFrom);
        var until = input.Until is { } end ? FirstDay(end) : (DateOnly?)null;
        if (until < from)
            throw new ArgumentException("El último mes no puede ser anterior al primero.");

        var source = await FindAsync(sourceId);
        var before = await FindAsync(sourceId);

        if (until is { } last)
        {
            // What applied after the corrected range must stay as it was.
            var next = last.AddMonths(1);
            var kept = source.GetRevision(next) ?? source.Revisions.MinBy(r => r.EffectiveFrom)!;
            var (amount, currency, frequency, note) = (kept.Amount, kept.Currency, kept.Frequency, kept.Note);

            source.Revisions.RemoveAll(r => r.EffectiveFrom >= from && r.EffectiveFrom <= last);
            if (source.Revisions.All(r => r.EffectiveFrom != next))
                SetRevision(source, next, amount, currency, frequency, note);
        }

        SetRevision(source, from, input.Amount, input.Currency, input.Frequency, input.Note);

        // Recording values from before the start means the income started then.
        if (from < FirstDay(source.StartDate))
        {
            var day = Math.Min(source.StartDate.Day, DateTime.DaysInMonth(from.Year, from.Month));
            source.StartDate = new DateOnly(from.Year, from.Month, day);
        }

        return (source, Impact(before, source));
    }

    /// <summary>Revisions start this month or later; a source that has not started changes from its start.</summary>
    private DateOnly ChangeMonth(DateOnly startDate, DateOnly requested)
    {
        var month = FirstDay(requested);
        if (month < CurrentMonth)
            throw new ArgumentException("Para cambiar meses pasados usa «Corregir historial».");

        var start = FirstDay(startDate);
        return month < start ? start : month;
    }

    private static void SetRevision(
        IncomeSource source, DateOnly month, decimal amount, Currency currency, Frequency frequency, string? note)
    {
        var revision = source.Revisions.FirstOrDefault(r => r.EffectiveFrom == month);
        if (revision is null)
        {
            revision = new IncomeSourceRevision { IncomeSourceId = source.Id, EffectiveFrom = month };
            source.Revisions.Add(revision);
        }

        revision.Amount = amount;
        revision.Currency = currency;
        revision.Frequency = frequency;
        revision.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    private PastImpactDto Impact(IncomeSource before, IncomeSource after)
    {
        var impact = new PastImpactDto();
        var start = before.StartDate < after.StartDate ? before.StartDate : after.StartDate;

        for (var month = FirstDay(start); month < CurrentMonth; month = month.AddMonths(1))
        {
            if (Signature(before, month) != Signature(after, month))
                impact.Months.Add(month);
        }

        if (impact.Months.Count > 0)
        {
            var count = impact.Months.Count == 1 ? "1 mes pasado" : $"{impact.Months.Count} meses pasados";
            var range = impact.Months.Count == 1
                ? MonthText(impact.Months[0])
                : $"{MonthText(impact.Months[0])} a {MonthText(impact.Months[^1])}";
            impact.Warning = $"Esto cambiará la evaluación de {count} ({range}).";
        }

        return impact;
    }

    /// <summary>Everything a month's calculations read from a source.</summary>
    private static string Signature(IncomeSource source, DateOnly month)
    {
        if (!source.HasIncomeIn(month.Year, month.Month))
            return string.Empty;

        var revision = source.GetRevision(month)!;
        var dates = RecurrenceCalculator.GetOccurrences(
            source.StartDate, revision.Frequency, source.EndDate, month, month.AddMonths(1).AddDays(-1));
        return FormattableString.Invariant(
            $"{revision.Amount:0.00}|{revision.Currency}|{revision.Frequency}|{string.Join(",", dates.Select(d => d.Day))}");
    }

    // ---------- Helpers ----------

    private async Task<IncomeSource> FindAsync(int id)
        => (await _sources.GetAllWithRevisionsAsync()).FirstOrDefault(s => s.Id == id)
           ?? throw new InvalidOperationException("La fuente de ingreso no existe.");

    /// <summary>The values the list shows: those of this month, or the first ones.</summary>
    private IncomeSourceRevision ShownRevision(IncomeSource source)
        => source.GetRevision(CurrentMonth) ?? source.Revisions.MinBy(r => r.EffectiveFrom)!;

    private static DateOnly FirstDay(DateOnly date) => new(date.Year, date.Month, 1);

    private static string MonthText(DateOnly date) => date.ToString("MMMM yyyy", Culture);

    private static (DateOnly From, DateOnly To) GetMonthRange(int year, int month)
    {
        if (year < 1 || year > 9999 || month < 1 || month > 12)
            throw new ArgumentException("El mes no es válido.");

        var from = new DateOnly(year, month, 1);
        return (from, new DateOnly(year, month, DateTime.DaysInMonth(year, month)));
    }

    private static void Validate(IncomeSourceInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("El nombre es obligatorio.");
        ValidateValues(input.Amount, input.Currency, input.Frequency);
        if (input.EndDate is not null && input.EndDate < input.StartDate)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.");
    }

    private static void ValidateValues(decimal amount, Currency currency, Frequency frequency)
    {
        if (amount <= 0)
            throw new ArgumentException("El monto debe ser mayor que cero.");
        if (!Enum.IsDefined(currency) || !Enum.IsDefined(frequency))
            throw new ArgumentException("La moneda o la frecuencia no son válidas.");
    }

    private static void Validate(OneTimeIncomeInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Description))
            throw new ArgumentException("La descripción es obligatoria.");
        if (input.Amount <= 0)
            throw new ArgumentException("El monto debe ser mayor que cero.");
        if (!Enum.IsDefined(input.Currency))
            throw new ArgumentException("La moneda no es válida.");
    }

    private static void Apply(IncomeSource entity, IncomeSourceInput input)
    {
        entity.Name = input.Name.Trim();
        entity.StartDate = input.StartDate;
        entity.EndDate = input.EndDate;
        entity.IsActive = input.IsActive;
    }

    private async Task ApplyAsync(OneTimeIncome entity, OneTimeIncomeInput input)
    {
        entity.Description = input.Description.Trim();
        entity.Amount = input.Amount;
        entity.Currency = input.Currency;
        entity.Date = input.Date;
        entity.AmountInBase = await _currency.ConvertToBaseAsync(input.Amount, input.Currency, input.Date);
    }

    private IncomeSourceDto ToDto(IncomeSource source)
    {
        var shown = ShownRevision(source);
        var revisions = source.Revisions.OrderBy(r => r.EffectiveFrom).Select(ToDto).ToList();
        return new IncomeSourceDto
        {
            Id = source.Id,
            Name = source.Name,
            Amount = shown.Amount,
            Currency = shown.Currency,
            Frequency = shown.Frequency,
            StartDate = source.StartDate,
            EndDate = source.EndDate,
            IsActive = source.IsActive,
            NextRevision = revisions.FirstOrDefault(r => r.EffectiveFrom > shown.EffectiveFrom && r.EffectiveFrom > CurrentMonth),
            Revisions = revisions
        };
    }

    private static IncomeRevisionDto ToDto(IncomeSourceRevision r) => new()
    {
        EffectiveFrom = r.EffectiveFrom,
        Amount = r.Amount,
        Currency = r.Currency,
        Frequency = r.Frequency,
        Note = r.Note
    };

    private static OneTimeIncomeDto ToDto(OneTimeIncome i) => new()
    {
        Id = i.Id,
        Description = i.Description,
        Amount = i.Amount,
        Currency = i.Currency,
        AmountInBase = i.AmountInBase,
        Date = i.Date
    };
}
