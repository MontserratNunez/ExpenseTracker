using ExpenseTracker.Core.Application.DTOs.Incomes;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Helpers;

namespace ExpenseTracker.Core.Application.Services;

public class IncomeService
{
    private readonly IRepository<IncomeSource> _sources;
    private readonly IRepository<OneTimeIncome> _oneTime;
    private readonly IRepository<BudgetAllocation> _allocations;
    private readonly CurrencyService _currency;
    private readonly IDateProvider _dateProvider;

    public IncomeService(
        IRepository<IncomeSource> sources,
        IRepository<OneTimeIncome> oneTime,
        IRepository<BudgetAllocation> allocations,
        CurrencyService currency,
        IDateProvider dateProvider)
    {
        _sources = sources;
        _oneTime = oneTime;
        _allocations = allocations;
        _currency = currency;
        _dateProvider = dateProvider;
    }

    // ---------- Recurring sources ----------

    public async Task<List<IncomeSourceDto>> ListSourcesAsync()
    {
        var items = await _sources.ListAsync();
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
        await _sources.AddAsync(entity);
        return ToDto(entity);
    }

    public async Task<IncomeSourceDto> UpdateSourceAsync(int id, IncomeSourceInput input)
    {
        Validate(input);

        var entity = await _sources.GetByIdAsync(id)
            ?? throw new InvalidOperationException("La fuente de ingreso no existe.");
        Apply(entity, input);
        await _sources.UpdateAsync(entity);
        return ToDto(entity);
    }

    public async Task DeleteSourceAsync(int id)
    {
        var entity = await _sources.GetByIdAsync(id)
            ?? throw new InvalidOperationException("La fuente de ingreso no existe.");

        // Plan allocations can be a percentage of this source; the database would reject the delete.
        if ((await _allocations.ListAsync(a => a.IncomeSourceId == id)).Count > 0)
            throw new InvalidOperationException("La fuente se usa en un plan y no se puede eliminar. Para que deje de contar, póngale fecha de fin.");

        await _sources.DeleteAsync(entity);
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

        var sources = await _sources.ListAsync(s => s.IsActive);
        foreach (var source in sources.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var occurrences = RecurrenceCalculator.GetOccurrences(source.StartDate, source.Frequency, source.EndDate, from, to);
            if (occurrences.Count == 0)
                continue;

            var unitInBase = await _currency.ConvertToBaseAsync(source.Amount, source.Currency);
            result.Lines.Add(new IncomeLineDto
            {
                Name = source.Name,
                IsRecurring = true,
                SourceId = source.Id,
                Amount = source.Amount,
                Currency = source.Currency,
                Occurrences = occurrences.Count,
                AmountInBase = unitInBase * occurrences.Count
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

    /// <summary>
    /// Monthly equivalent, in the base currency, of all recurring sources that are active today.
    /// Used by the Plan to resolve percentage allocations.
    /// </summary>
    public async Task<decimal> GetRecurringMonthlyTotalAsync()
        => (await GetIncomeBasesAsync()).RecurringTotal;

    /// <summary>
    /// Monthly equivalent of every source (for percentage allocations tied to one source) plus the
    /// total of the sources that are active today. Rates are loaded once.
    /// </summary>
    public async Task<IncomeBasesDto> GetIncomeBasesAsync()
    {
        var today = _dateProvider.Today;
        var sources = await _sources.ListAsync();
        var result = new IncomeBasesDto();
        if (sources.Count == 0)
            return result;

        var converter = await _currency.GetConverterAsync();
        foreach (var source in sources.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var monthly = RecurrenceCalculator.ToMonthlyAmount(converter.ToBase(source.Amount, source.Currency), source.Frequency);
            var isCurrent = source.IsActive && source.StartDate <= today && (source.EndDate is null || source.EndDate >= today);

            result.Sources.Add(new IncomeBaseDto { SourceId = source.Id, Name = source.Name, MonthlyAmount = monthly, IsCurrent = isCurrent });
            if (isCurrent)
                result.RecurringTotal += monthly;
        }

        return result;
    }

    /// <summary>
    /// Projected income for [from, to], keyed by the first day of each month it touches:
    /// recurring occurrences inside the range plus one-time income already registered in it.
    /// </summary>
    public async Task<Dictionary<DateOnly, decimal>> GetProjectedIncomeByMonthAsync(DateOnly from, DateOnly to)
    {
        var result = new Dictionary<DateOnly, decimal>();
        if (to < from)
            return result;

        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
            result[month] = 0m;

        var sources = await _sources.ListAsync(s => s.IsActive);
        if (sources.Count > 0)
        {
            var converter = await _currency.GetConverterAsync();
            foreach (var source in sources)
            {
                var unitInBase = converter.ToBase(source.Amount, source.Currency);
                foreach (var date in RecurrenceCalculator.GetOccurrences(source.StartDate, source.Frequency, source.EndDate, from, to))
                    result[new DateOnly(date.Year, date.Month, 1)] += unitInBase;
            }
        }

        foreach (var income in await _oneTime.ListAsync(i => i.Date >= from && i.Date <= to))
            result[new DateOnly(income.Date.Year, income.Date.Month, 1)] += income.AmountInBase;

        return result;
    }

    // ---------- Helpers ----------

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
        if (input.Amount <= 0)
            throw new ArgumentException("El monto debe ser mayor que cero.");
        if (!Enum.IsDefined(input.Currency) || !Enum.IsDefined(input.Frequency))
            throw new ArgumentException("La moneda o la frecuencia no son válidas.");
        if (input.EndDate is not null && input.EndDate < input.StartDate)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.");
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
        entity.Amount = input.Amount;
        entity.Currency = input.Currency;
        entity.Frequency = input.Frequency;
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
        entity.AmountInBase = await _currency.ConvertToBaseAsync(input.Amount, input.Currency);
    }

    private static IncomeSourceDto ToDto(IncomeSource s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Amount = s.Amount,
        Currency = s.Currency,
        Frequency = s.Frequency,
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        IsActive = s.IsActive
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
