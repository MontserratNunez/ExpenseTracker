using System.Data.Common;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Infrastructure.Persistence.Context;
using ExpenseTracker.Infrastructure.Persistence.Repositories;
using ExpenseTracker.Tests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ExpenseTracker.Tests.Persistence;

/// <summary>Guards the performance rule: a month of the ledger must stay at about 4 database queries.</summary>
public class QueryCountTests : IDisposable
{
    private const string UserId = "user-a";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly CountingInterceptor _counter = new();
    private readonly FactoryWithInterceptor _factory;
    private readonly FixedDateProvider _dates = new(new DateOnly(2026, 11, 18));

    public QueryCountTests()
    {
        _connection.Open();
        _factory = new FactoryWithInterceptor(_connection, _counter, new FixedUser(UserId));

        using var context = _factory.CreateDbContext();
        context.Database.EnsureCreated();
        context.UserSettings.Add(new UserSettings { BaseCurrency = Currency.DOP });
        context.Categories.Add(new Category { Name = "Comida", Color = "#E67E22" });
        context.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private MonthlyLedgerService CreateLedger()
    {
        var currency = new CurrencyService(new Repository<UserSettings>(_factory), new Repository<ExchangeRate>(_factory), _dates);
        return new MonthlyLedgerService(new ExpenseRepository(_factory), new DebtRepository(_factory), currency, _dates);
    }

    private async Task SeedBaseCurrencyDataAsync()
    {
        var expenses = new ExpenseRepository(_factory);
        var debts = new DebtRepository(_factory);

        await expenses.AddAsync(new Expense { Name = "Super", Currency = Currency.DOP, Amount = 100, AmountInBase = 100, CategoryId = 1, Date = new DateOnly(2026, 11, 3) });
        await expenses.AddAsync(new Expense { Name = "Cine", Currency = Currency.DOP, Amount = 50, AmountInBase = 50, CategoryId = 1, Date = new DateOnly(2026, 11, 8) });
        await expenses.AddAsync(new Expense
        {
            Name = "Luz", Currency = Currency.DOP, Amount = 1300, AmountInBase = 1300, CategoryId = 1, Type = ExpenseType.Recurring,
            Frequency = Frequency.Monthly, Date = new DateOnly(2026, 1, 20)
        });

        for (var i = 1; i <= 3; i++)
        {
            var debt = new Debt { Name = $"Deuda {i}", Currency = Currency.DOP, OriginalAmount = 10_000, MonthlyPayment = 1_000, PaymentDay = 10, StartDate = new DateOnly(2026, 1, 1) };
            await debts.AddAsync(debt);
            await debts.AddPaymentAsync(new DebtPayment { DebtId = debt.Id, Amount = 500, PrincipalPortion = 500, Date = new DateOnly(2026, 10, 10) });
        }
    }

    [Fact]
    public async Task LedgerMonth_WithEverythingInTheBaseCurrency_UsesFourQueries()
    {
        await SeedBaseCurrencyDataAsync();
        var ledger = CreateLedger();

        _counter.Reset();
        var items = await ledger.GetMonthAsync(2026, 11);

        Assert.NotEmpty(items);
        Assert.Equal(4, _counter.Count); // one-time, recurring, debts with payments (no N+1) and settings
    }

    [Fact]
    public async Task LedgerMonth_QueryCount_DoesNotGrowWithTheNumberOfDebts()
    {
        await SeedBaseCurrencyDataAsync();
        var ledger = CreateLedger();

        _counter.Reset();
        await ledger.GetMonthAsync(2026, 11);
        var withThreeDebts = _counter.Count;

        var debts = new DebtRepository(_factory);
        for (var i = 4; i <= 10; i++)
            await debts.AddAsync(new Debt { Name = $"Deuda {i}", Currency = Currency.DOP, OriginalAmount = 5_000, MonthlyPayment = 500, PaymentDay = 5, StartDate = new DateOnly(2026, 1, 1) });

        _counter.Reset();
        await ledger.GetMonthAsync(2026, 11);

        Assert.Equal(withThreeDebts, _counter.Count);
    }

    [Fact]
    public async Task LedgerMonth_WithAForeignCurrencyItem_AddsOnlyTheRatesQuery()
    {
        await SeedBaseCurrencyDataAsync();
        using (var context = _factory.CreateDbContext())
        {
            context.ExchangeRates.Add(new ExchangeRate { Currency = Currency.USD, RateToBase = 60m, EffectiveFrom = new DateOnly(2026, 11, 1) });
            context.SaveChanges();
        }
        await new ExpenseRepository(_factory).AddAsync(new Expense
        {
            Name = "Netflix", Amount = 10, Currency = Currency.USD, AmountInBase = 600, CategoryId = 1, Type = ExpenseType.Recurring,
            Frequency = Frequency.Monthly, Date = new DateOnly(2026, 1, 15)
        });
        var ledger = CreateLedger();

        _counter.Reset();
        await ledger.GetMonthAsync(2026, 11);

        Assert.Equal(5, _counter.Count);
    }

    [Fact]
    public async Task EmptyMonth_ForANewUser_UsesThreeQueries()
    {
        var ledger = CreateLedger();

        _counter.Reset();
        Assert.Empty(await ledger.GetMonthAsync(2026, 11));

        Assert.Equal(3, _counter.Count);
    }

    private sealed class CountingInterceptor : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public void Reset() => Count = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Count++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FixedUser : ICurrentUserService
    {
        public FixedUser(string userId) => UserId = userId;
        public string UserId { get; }
    }

    private sealed class FactoryWithInterceptor : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly ICurrentUserService _user;

        public FactoryWithInterceptor(SqliteConnection connection, DbCommandInterceptor interceptor, ICurrentUserService user)
        {
            _user = user;
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(interceptor).Options;
        }

        public AppDbContext CreateDbContext() => new(_options, _user);
    }
}
