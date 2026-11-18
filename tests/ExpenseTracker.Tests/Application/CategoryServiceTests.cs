using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Tests.Fakes;

namespace ExpenseTracker.Tests.Application;

public class CategoryServiceTests
{
    private readonly FakeRepository<Category> _categories = new();
    private readonly FakeRepository<Expense> _expenses = new();
    private readonly FakeRepository<BudgetAllocation> _allocations = new();
    private readonly FakeRepository<Goal> _goals = new();
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        _service = new CategoryService(_categories, _expenses, _allocations, _goals);
    }

    private static CategoryInput Input(string name, bool savings = false) => new() { Name = name, Color = "#112233", IsSavings = savings };

    [Fact]
    public async Task Create_TrimsNameAndActivates()
    {
        var created = await _service.CreateAsync(Input("  Mascotas  "));

        Assert.Equal("Mascotas", created.Name);
        Assert.True(created.IsActive);
        Assert.Single(_categories.Items);
    }

    [Fact]
    public async Task Create_DuplicateName_IgnoringCase_Throws()
    {
        await _service.CreateAsync(Input("Comida"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(Input("comida")));
        Assert.Equal("Ya existe una categoría con ese nombre.", ex.Message);
    }

    [Fact]
    public async Task Create_EmptyName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(Input("   ")));
    }

    [Fact]
    public async Task Update_RenamesAndRecolors_AllowsKeepingOwnName()
    {
        var created = await _service.CreateAsync(Input("Comida"));

        var input = new CategoryInput { Name = "Comida", Color = "#AABBCC", IsSavings = true };
        var updated = await _service.UpdateAsync(created.Id, input);

        Assert.Equal("#AABBCC", updated.Color);
        Assert.True(updated.IsSavings);

        await _service.UpdateAsync(created.Id, Input("Supermercado"));
        Assert.Equal("Supermercado", _categories.Items.Single().Name);
    }

    [Fact]
    public async Task Update_ToAnotherCategorysName_Throws()
    {
        await _service.CreateAsync(Input("Comida"));
        var other = await _service.CreateAsync(Input("Salud"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateAsync(other.Id, Input("Comida")));
    }

    [Fact]
    public async Task Delete_WithoutMovements_RemovesIt()
    {
        var created = await _service.CreateAsync(Input("Temporal"));

        var deleted = await _service.DeleteAsync(created.Id);

        Assert.True(deleted);
        Assert.Empty(_categories.Items);
    }

    [Fact]
    public async Task Delete_WithExpenses_DeactivatesInstead()
    {
        var created = await _service.CreateAsync(Input("Comida"));
        await _expenses.AddAsync(new Expense { Name = "Super", CategoryId = created.Id });

        var deleted = await _service.DeleteAsync(created.Id);

        Assert.False(deleted);
        Assert.False(_categories.Items.Single().IsActive);
    }

    [Fact]
    public async Task Delete_WithGoalOrAllocation_DeactivatesInstead()
    {
        var a = await _service.CreateAsync(Input("Ahorro", savings: true));
        var b = await _service.CreateAsync(Input("Ocio"));
        await _goals.AddAsync(new Goal { Name = "Meta", CategoryId = a.Id });
        await _allocations.AddAsync(new BudgetAllocation { CategoryId = b.Id });

        Assert.False(await _service.DeleteAsync(a.Id));
        Assert.False(await _service.DeleteAsync(b.Id));
        Assert.All(_categories.Items, c => Assert.False(c.IsActive));
    }

    [Fact]
    public async Task List_CanExcludeInactive_AndIsSortedByName()
    {
        await _service.CreateAsync(Input("Zeta"));
        var b = await _service.CreateAsync(Input("Beta"));
        await _service.CreateAsync(Input("Alfa"));
        await _service.SetActiveAsync(b.Id, false);

        var all = await _service.ListAsync();
        var active = await _service.ListAsync(includeInactive: false);

        Assert.Equal(new[] { "Alfa", "Beta", "Zeta" }, all.Select(c => c.Name));
        Assert.Equal(new[] { "Alfa", "Zeta" }, active.Select(c => c.Name));
    }

    [Fact]
    public async Task Update_NonExistent_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateAsync(99, Input("X")));
    }
}
