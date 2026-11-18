using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;

namespace ExpenseTracker.Core.Application.Services;

public class CategoryService
{
    private readonly IRepository<Category> _categories;
    private readonly IRepository<Expense> _expenses;
    private readonly IRepository<BudgetAllocation> _allocations;
    private readonly IRepository<Goal> _goals;

    public CategoryService(
        IRepository<Category> categories,
        IRepository<Expense> expenses,
        IRepository<BudgetAllocation> allocations,
        IRepository<Goal> goals)
    {
        _categories = categories;
        _expenses = expenses;
        _allocations = allocations;
        _goals = goals;
    }

    public async Task<List<CategoryDto>> ListAsync(bool includeInactive = true)
    {
        var items = await _categories.ListAsync(includeInactive ? null : c => c.IsActive);
        return items.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).Select(ToDto).ToList();
    }

    public async Task<CategoryDto> CreateAsync(CategoryInput input)
    {
        var name = await ValidateAsync(input, currentId: null);

        var entity = new Category
        {
            Name = name,
            Color = input.Color,
            Icon = NormalizeIcon(input.Icon),
            IsSavings = input.IsSavings,
            IsActive = true
        };

        await _categories.AddAsync(entity);
        return ToDto(entity);
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryInput input)
    {
        var entity = await GetOrThrowAsync(id);
        entity.Name = await ValidateAsync(input, currentId: id);
        entity.Color = input.Color;
        entity.Icon = NormalizeIcon(input.Icon);
        entity.IsSavings = input.IsSavings;

        await _categories.UpdateAsync(entity);
        return ToDto(entity);
    }

    public async Task SetActiveAsync(int id, bool isActive)
    {
        var entity = await GetOrThrowAsync(id);
        if (entity.IsActive == isActive)
            return;

        entity.IsActive = isActive;
        await _categories.UpdateAsync(entity);
    }

    /// <summary>
    /// Deletes a category that has no movements. If it is in use (expenses, plan or goals)
    /// it is deactivated instead. Returns true when deleted, false when only deactivated.
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await GetOrThrowAsync(id);

        if (await IsInUseAsync(id))
        {
            await SetActiveAsync(id, false);
            return false;
        }

        await _categories.DeleteAsync(entity);
        return true;
    }

    private async Task<bool> IsInUseAsync(int categoryId)
    {
        if ((await _expenses.ListAsync(e => e.CategoryId == categoryId)).Count > 0)
            return true;
        if ((await _allocations.ListAsync(a => a.CategoryId == categoryId)).Count > 0)
            return true;
        return (await _goals.ListAsync(g => g.CategoryId == categoryId)).Count > 0;
    }

    private async Task<Category> GetOrThrowAsync(int id)
        => await _categories.GetByIdAsync(id)
           ?? throw new InvalidOperationException("La categoría no existe.");

    private async Task<string> ValidateAsync(CategoryInput input, int? currentId)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            throw new ArgumentException("El nombre es obligatorio.");
        if (name.Length > 100)
            throw new ArgumentException("El nombre no puede superar 100 caracteres.");

        var existing = await _categories.ListAsync();
        if (existing.Any(c => c.Id != currentId && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Ya existe una categoría con ese nombre.");

        return name;
    }

    private static string? NormalizeIcon(string? icon)
        => string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();

    private static CategoryDto ToDto(Category c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Color = c.Color,
        Icon = c.Icon,
        IsSavings = c.IsSavings,
        IsActive = c.IsActive
    };
}
