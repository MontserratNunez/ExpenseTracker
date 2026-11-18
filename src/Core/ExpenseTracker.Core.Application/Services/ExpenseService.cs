using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.Services;

public class ExpenseService
{
    private readonly IExpenseRepository _expenses;
    private readonly IRepository<Category> _categories;
    private readonly CurrencyService _currency;
    private readonly MonthlyLedgerService _ledger;
    private readonly IDateProvider _dateProvider;

    public ExpenseService(
        IExpenseRepository expenses,
        IRepository<Category> categories,
        CurrencyService currency,
        MonthlyLedgerService ledger,
        IDateProvider dateProvider)
    {
        _expenses = expenses;
        _categories = categories;
        _currency = currency;
        _ledger = ledger;
        _dateProvider = dateProvider;
    }

    public async Task<ExpenseDto?> GetByIdAsync(int id)
    {
        var expense = await _expenses.GetByIdAsync(id);
        return expense is null ? null : ToDto(expense);
    }

    public async Task<ExpenseDto> CreateAsync(ExpenseInput input)
    {
        Validate(input);
        await EnsureCategoryAsync(input.CategoryId, previousCategoryId: null);

        var entity = new Expense();
        await ApplyAsync(entity, input);
        await _expenses.AddAsync(entity);
        return ToDto(entity);
    }

    public async Task<ExpenseDto> UpdateAsync(int id, ExpenseInput input)
    {
        Validate(input);

        var entity = await GetOrThrowAsync(id);
        await EnsureCategoryAsync(input.CategoryId, previousCategoryId: entity.CategoryId);

        await ApplyAsync(entity, input);
        await _expenses.UpdateAsync(entity);
        return ToDto(entity);
    }

    /// <summary>Deletes the expense completely. For a recurring one this also removes its past occurrences.</summary>
    public async Task DeleteAsync(int id)
    {
        var entity = await GetOrThrowAsync(id);
        await _expenses.DeleteAsync(entity);
    }

    /// <summary>Stops a recurring expense today and keeps its history.</summary>
    public async Task EndAsync(int id)
    {
        var entity = await GetOrThrowAsync(id);
        if (entity.Type != ExpenseType.Recurring)
            throw new InvalidOperationException("Solo se pueden finalizar los gastos recurrentes.");

        var today = _dateProvider.Today;
        if (entity.Date > today)
            throw new InvalidOperationException("El gasto aún no ha comenzado; elimínelo en lugar de finalizarlo.");

        if (entity.EndDate is not null && entity.EndDate <= today)
            return;

        entity.EndDate = today;
        await _expenses.UpdateAsync(entity);
    }

    public async Task<MonthExpensesDto> GetMonthAsync(int year, int month)
    {
        var items = await _ledger.GetMonthAsync(year, month);

        var result = new MonthExpensesDto
        {
            Year = year,
            Month = month,
            Items = items,
            TotalSpent = items.Where(i => i.Status == LedgerStatus.Spent).Sum(i => i.AmountInBase),
            TotalPending = items.Where(i => i.Status == LedgerStatus.Pending).Sum(i => i.AmountInBase)
        };

        result.ByCategory = items
            .GroupBy(i => new { i.CategoryId, i.CategoryName, i.CategoryColor })
            .Select(g => new CategoryTotalDto
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                CategoryColor = g.Key.CategoryColor,
                Spent = g.Where(i => i.Status == LedgerStatus.Spent).Sum(i => i.AmountInBase),
                Pending = g.Where(i => i.Status == LedgerStatus.Pending).Sum(i => i.AmountInBase)
            })
            .OrderByDescending(c => c.Spent + c.Pending)
            .ToList();

        return result;
    }

    // ---------- Helpers ----------

    private async Task<Expense> GetOrThrowAsync(int id)
        => await _expenses.GetByIdAsync(id) ?? throw new InvalidOperationException("El gasto no existe.");

    private async Task EnsureCategoryAsync(int categoryId, int? previousCategoryId)
    {
        var category = await _categories.GetByIdAsync(categoryId)
            ?? throw new InvalidOperationException("La categoría no existe.");

        // An expense may keep an inactive category it already had, but new choices must be active.
        if (!category.IsActive && categoryId != previousCategoryId)
            throw new InvalidOperationException("La categoría está inactiva.");
    }

    private static void Validate(ExpenseInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("El nombre es obligatorio.");
        if (input.Amount <= 0)
            throw new ArgumentException("El monto debe ser mayor que cero.");
        if (!Enum.IsDefined(input.Currency))
            throw new ArgumentException("La moneda no es válida.");

        if (input.IsRecurring)
        {
            if (input.Frequency is null || !Enum.IsDefined(input.Frequency.Value))
                throw new ArgumentException("Un gasto recurrente requiere una frecuencia.");
            if (input.EndDate is not null && input.EndDate < input.Date)
                throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.");
        }
    }

    private async Task ApplyAsync(Expense entity, ExpenseInput input)
    {
        entity.Name = input.Name.Trim();
        entity.Amount = input.Amount;
        entity.Currency = input.Currency;
        entity.AmountInBase = await _currency.ConvertToBaseAsync(input.Amount, input.Currency);
        entity.CategoryId = input.CategoryId;
        entity.Date = input.Date;
        entity.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();

        if (input.IsRecurring)
        {
            entity.Type = ExpenseType.Recurring;
            entity.Frequency = input.Frequency;
            entity.EndDate = input.EndDate;
        }
        else
        {
            entity.Type = ExpenseType.OneTime;
            entity.Frequency = null;
            entity.EndDate = null;
        }
    }

    private static ExpenseDto ToDto(Expense e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Amount = e.Amount,
        Currency = e.Currency,
        AmountInBase = e.AmountInBase,
        IsRecurring = e.Type == ExpenseType.Recurring,
        Date = e.Date,
        Frequency = e.Frequency,
        EndDate = e.EndDate,
        CategoryId = e.CategoryId,
        Notes = e.Notes,
        IsActive = e.IsActive
    };
}
