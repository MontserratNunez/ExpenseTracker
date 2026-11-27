using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Infrastructure.Persistence.Context;
using ExpenseTracker.Infrastructure.Persistence.Repositories;
using ExpenseTracker.Infrastructure.Persistence.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Tests.Persistence;

public class UserIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly TestUser _user = new();
    private readonly TestFactory _factory;

    public UserIsolationTests()
    {
        _connection.Open();
        _factory = new TestFactory(_connection, _user);
        using var context = _factory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private async Task<int> AddCategoryAsync(string userId, string name)
    {
        _user.UserId = userId;
        var repo = new Repository<Category>(_factory);
        var category = new Category { Name = name };
        await repo.AddAsync(category);
        return category.Id;
    }

    [Fact]
    public async Task List_ReturnsOnlyCurrentUserRows()
    {
        await AddCategoryAsync("user-a", "Comida");
        await AddCategoryAsync("user-b", "Comida");
        await AddCategoryAsync("user-b", "Salud");

        var repo = new Repository<Category>(_factory);

        _user.UserId = "user-a";
        Assert.Single(await repo.ListAsync());

        _user.UserId = "user-b";
        Assert.Equal(2, (await repo.ListAsync()).Count);
    }

    [Fact]
    public async Task GetById_OfAnotherUser_ReturnsNull()
    {
        var idOfA = await AddCategoryAsync("user-a", "Comida");

        _user.UserId = "user-b";
        var repo = new Repository<Category>(_factory);

        Assert.Null(await repo.GetByIdAsync(idOfA));
    }

    [Fact]
    public async Task Update_AndDelete_OfAnotherUser_Throw()
    {
        var idOfA = await AddCategoryAsync("user-a", "Comida");

        _user.UserId = "user-b";
        var repo = new Repository<Category>(_factory);
        var foreign = new Category { Id = idOfA, Name = "Hackeado" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UpdateAsync(foreign));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.DeleteAsync(foreign));

        _user.UserId = "user-a";
        Assert.Equal("Comida", (await repo.GetByIdAsync(idOfA))!.Name);
    }

    [Fact]
    public async Task Add_AssignsUserIdAndAuditDates()
    {
        var id = await AddCategoryAsync("user-a", "Comida");

        var saved = await new Repository<Category>(_factory).GetByIdAsync(id);

        Assert.Equal("user-a", saved!.UserId);
        Assert.NotEqual(default, saved.CreatedAtUtc);
        Assert.Null(saved.UpdatedAtUtc);
    }

    [Fact]
    public async Task Update_KeepsOwnerAndSetsUpdatedDate()
    {
        var id = await AddCategoryAsync("user-a", "Comida");
        var repo = new Repository<Category>(_factory);

        var entity = (await repo.GetByIdAsync(id))!;
        entity.Name = "Supermercado";
        entity.UserId = "user-b"; // tampering must be ignored
        await repo.UpdateAsync(entity);

        var saved = (await repo.GetByIdAsync(id))!;
        Assert.Equal("Supermercado", saved.Name);
        Assert.Equal("user-a", saved.UserId);
        Assert.NotNull(saved.UpdatedAtUtc);
    }

    [Fact]
    public async Task AddPayment_ToAnotherUsersDebt_Throws()
    {
        _user.UserId = "user-a";
        var debtRepo = new DebtRepository(_factory);
        var debt = new Debt { Name = "Prestamo", OriginalAmount = 1000m };
        await debtRepo.AddAsync(debt);

        _user.UserId = "user-b";
        var payment = new DebtPayment { DebtId = debt.Id, Amount = 10m, Date = new DateOnly(2026, 1, 1) };

        await Assert.ThrowsAsync<InvalidOperationException>(() => debtRepo.AddPaymentAsync(payment));
    }

    [Fact]
    public async Task ExpenseRepository_FiltersByRangeAndType()
    {
        var categoryId = await AddCategoryAsync("user-a", "Comida");
        var repo = new ExpenseRepository(_factory);

        await repo.AddAsync(new Expense { Name = "Super", Amount = 100, AmountInBase = 100, CategoryId = categoryId, Date = new DateOnly(2026, 11, 5) });
        await repo.AddAsync(new Expense { Name = "Old", Amount = 100, AmountInBase = 100, CategoryId = categoryId, Date = new DateOnly(2026, 10, 5) });
        await repo.AddAsync(new Expense
        {
            Name = "Netflix", Amount = 10, AmountInBase = 605, CategoryId = categoryId, Type = ExpenseType.Recurring,
            Frequency = Frequency.Monthly, Date = new DateOnly(2026, 1, 15)
        });
        await repo.AddAsync(new Expense
        {
            Name = "Ended", Amount = 10, AmountInBase = 10, CategoryId = categoryId, Type = ExpenseType.Recurring,
            Frequency = Frequency.Monthly, Date = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 1)
        });

        var oneTime = await repo.GetOneTimeByRangeAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));
        var recurring = await repo.GetActiveRecurringAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        Assert.Equal("Super", Assert.Single(oneTime).Name);
        Assert.Equal("Comida", oneTime[0].Category!.Name);
        Assert.Equal("Netflix", Assert.Single(recurring).Name);
    }

    [Fact]
    public async Task Seeder_CreatesSettingsAndDefaultCategories_Once()
    {
        _user.UserId = string.Empty; // a just-registered user is not the current user
        var seeder = new UserDataSeeder(_factory);

        await seeder.SeedDefaultsForUserAsync("new-user");
        await seeder.SeedDefaultsForUserAsync("new-user");

        _user.UserId = "new-user";
        await using var context = _factory.CreateDbContext();
        var categories = await context.Categories.ToListAsync();
        var settings = await context.UserSettings.SingleAsync();

        Assert.Equal(9, categories.Count);
        Assert.DoesNotContain(categories, c => c.Name == "Ahorro"); // savings is not an expense
        Assert.All(categories, c => Assert.False(string.IsNullOrWhiteSpace(c.Icon)));
        Assert.Equal(Currency.DOP, settings.BaseCurrency);
        Assert.Equal("es-DO", settings.CultureName);
    }

    private sealed class TestUser : ICurrentUserService
    {
        public string UserId { get; set; } = string.Empty;
    }

    private sealed class TestFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly ICurrentUserService _user;

        public TestFactory(SqliteConnection connection, ICurrentUserService user)
        {
            _user = user;
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        }

        public AppDbContext CreateDbContext() => new(_options, _user);
    }
}
