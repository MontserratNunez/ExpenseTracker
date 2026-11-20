using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Tests.Fakes;

namespace ExpenseTracker.Tests.Application;

public class ExpenseServiceTests
{
    private const int Food = 1;
    private const int Leisure = 2;
    private const int Subscriptions = 3;
    private const int Services = 4;
    private const int Insurance = 5;

    private readonly FakeRepository<Category> _categories = new();
    private readonly FakeRepository<UserSettings> _settings = new();
    private readonly FakeRepository<ExchangeRate> _rates = new();
    private readonly FakeExpenseRepository _expenses;
    private readonly FakeDebtRepository _debts = new();
    private readonly FixedDateProvider _dates = new(new DateOnly(2026, 11, 18));
    private readonly CurrencyService _currency;
    private readonly ExpenseService _service;

    public ExpenseServiceTests()
    {
        _expenses = new FakeExpenseRepository(_categories);
        _settings.Items.Add(new UserSettings { Id = 1, BaseCurrency = Currency.DOP });
        _categories.Items.AddRange(new[]
        {
            new Category { Id = Food, Name = "Comida", Color = "#E67E22" },
            new Category { Id = Leisure, Name = "Entretenimiento", Color = "#F1C40F" },
            new Category { Id = Subscriptions, Name = "Suscripciones", Color = "#34495E" },
            new Category { Id = Services, Name = "Servicios", Color = "#3498DB" },
            new Category { Id = Insurance, Name = "Seguros", Color = "#16A085" },
        });

        _currency = new CurrencyService(_settings, _rates, _dates);
        var ledger = new MonthlyLedgerService(_expenses, _debts, _currency, _dates);
        _service = new ExpenseService(_expenses, _categories, _currency, ledger, _dates);
    }

    private static ExpenseInput OneTime(string name, decimal amount, DateOnly date, int category = Food, Currency currency = Currency.DOP)
        => new() { Name = name, Amount = amount, Date = date, CategoryId = category, Currency = currency };

    private static ExpenseInput Recurring(string name, decimal amount, DateOnly start, Frequency frequency, int category,
        Currency currency = Currency.DOP, DateOnly? end = null)
        => new() { Name = name, Amount = amount, Date = start, CategoryId = category, Currency = currency, IsRecurring = true, Frequency = frequency, EndDate = end };

    /// <summary>The seven test movements from the enunciado, placed in November 2026.</summary>
    private async Task SeedEnunciadoAsync()
    {
        await _currency.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60.50m });

        await _service.CreateAsync(OneTime("Supermercado", 4_885.40m, new DateOnly(2026, 11, 3)));
        await _service.CreateAsync(Recurring("Netflix", 10m, new DateOnly(2026, 1, 15), Frequency.Monthly, Subscriptions, Currency.USD));
        await _service.CreateAsync(OneTime("Supermercado", 700m, new DateOnly(2026, 11, 10)));
        await _service.CreateAsync(Recurring("Recibo luz", 1_300m, new DateOnly(2026, 1, 20), Frequency.Monthly, Services));
        await _service.CreateAsync(OneTime("Ticket cine", 381.20m, new DateOnly(2026, 11, 12), Leisure));
        await _service.CreateAsync(OneTime("Combo palomitas", 400m, new DateOnly(2026, 11, 12), Leisure));
        await _service.CreateAsync(Recurring("Seguro", 20_000m, new DateOnly(2025, 11, 25), Frequency.Annual, Insurance));
    }

    [Fact]
    public async Task Month_WithEnunciadoData_ShowsSevenItemsAndCorrectTotals()
    {
        await SeedEnunciadoAsync();

        var month = await _service.GetMonthAsync(2026, 11);

        Assert.Equal(7, month.Items.Count);
        // Spent (<= today, Nov 18): 4,885.40 + 605.00 (Netflix) + 700 + 381.20 + 400
        Assert.Equal(6_971.60m, month.TotalSpent);
        // Pending (> today): electricity on the 20th + annual insurance on the 25th
        Assert.Equal(21_300m, month.TotalPending);
    }

    [Fact]
    public async Task Month_RecurringBeforeToday_IsSpent_AndAfterToday_IsPending()
    {
        await SeedEnunciadoAsync();

        var items = (await _service.GetMonthAsync(2026, 11)).Items;

        Assert.Equal(LedgerStatus.Spent, items.Single(i => i.Name == "Netflix").Status);
        Assert.Equal(LedgerStatus.Pending, items.Single(i => i.Name == "Recibo luz").Status);
        Assert.Equal(LedgerStatus.Pending, items.Single(i => i.Name == "Seguro").Status);
    }

    [Fact]
    public async Task Month_ForeignCurrencyRecurring_ShowsOriginalAndConvertedAmount()
    {
        await SeedEnunciadoAsync();

        var netflix = (await _service.GetMonthAsync(2026, 11)).Items.Single(i => i.Name == "Netflix");

        Assert.Equal(10m, netflix.Amount);
        Assert.Equal(Currency.USD, netflix.Currency);
        Assert.Equal(605.00m, netflix.AmountInBase);
        Assert.Equal(Frequency.Monthly, netflix.Frequency);
        Assert.True(netflix.IsRecurring);
    }

    [Fact]
    public async Task AnnualInsurance_AppearsOnlyInItsAnniversaryMonth()
    {
        await SeedEnunciadoAsync();

        Assert.Contains((await _service.GetMonthAsync(2026, 11)).Items, i => i.Name == "Seguro");
        Assert.DoesNotContain((await _service.GetMonthAsync(2026, 12)).Items, i => i.Name == "Seguro");
        Assert.DoesNotContain((await _service.GetMonthAsync(2026, 4)).Items, i => i.Name == "Seguro");
        Assert.Contains((await _service.GetMonthAsync(2027, 11)).Items, i => i.Name == "Seguro");
    }

    [Fact]
    public async Task Netflix_AppearsEveryMonthFromItsStart_AndNotBefore()
    {
        await SeedEnunciadoAsync();

        for (var m = 1; m <= 12; m++)
            Assert.Contains((await _service.GetMonthAsync(2026, m)).Items, i => i.Name == "Netflix");

        Assert.DoesNotContain((await _service.GetMonthAsync(2025, 12)).Items, i => i.Name == "Netflix");
    }

    [Fact]
    public async Task PastMonths_AreReachable_AndFuturePendingStaysPending()
    {
        await SeedEnunciadoAsync();

        var october = await _service.GetMonthAsync(2026, 10);
        var december = await _service.GetMonthAsync(2026, 12);

        Assert.All(october.Items, i => Assert.Equal(LedgerStatus.Spent, i.Status));
        Assert.All(december.Items, i => Assert.Equal(LedgerStatus.Pending, i.Status));
    }

    [Fact]
    public async Task Month_GroupsTotalsByCategory()
    {
        await SeedEnunciadoAsync();

        var month = await _service.GetMonthAsync(2026, 11);

        var food = month.ByCategory.Single(c => c.CategoryId == Food);
        Assert.Equal(5_585.40m, food.Spent);
        Assert.Equal(0m, food.Pending);
        var insurance = month.ByCategory.Single(c => c.CategoryId == Insurance);
        Assert.Equal(20_000m, insurance.Pending);
        Assert.Equal("Seguros", insurance.CategoryName);
    }

    [Fact]
    public async Task Create_RecurringWithoutFrequency_Throws()
    {
        var input = Recurring("Gym", 500m, new DateOnly(2026, 1, 1), Frequency.Monthly, Food);
        input.Frequency = null;

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(input));
    }

    [Fact]
    public async Task Create_OneTime_IgnoresFrequencyAndEndDate()
    {
        var input = OneTime("Cena", 900m, new DateOnly(2026, 11, 1));
        input.Frequency = Frequency.Weekly;
        input.EndDate = new DateOnly(2026, 12, 1);

        var created = await _service.CreateAsync(input);

        Assert.False(created.IsRecurring);
        Assert.Null(created.Frequency);
        Assert.Null(created.EndDate);
    }

    [Fact]
    public async Task Create_ForeignCurrencyWithoutRate_Throws_AndSavesNothing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(OneTime("Amazon", 10m, new DateOnly(2026, 11, 1), currency: Currency.USD)));

        Assert.Empty(_expenses.Items);
    }

    [Fact]
    public async Task Create_WithMissingOrInactiveCategory_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(OneTime("X", 10m, new DateOnly(2026, 11, 1), category: 99)));

        _categories.Items.Single(c => c.Id == Food).IsActive = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(OneTime("X", 10m, new DateOnly(2026, 11, 1), Food)));
    }

    [Fact]
    public async Task Update_KeepingItsOwnInactiveCategory_IsAllowed()
    {
        var created = await _service.CreateAsync(OneTime("Super", 100m, new DateOnly(2026, 11, 1), Food));
        _categories.Items.Single(c => c.Id == Food).IsActive = false;

        var updated = await _service.UpdateAsync(created.Id, OneTime("Super 2", 150m, new DateOnly(2026, 11, 1), Food));

        Assert.Equal("Super 2", updated.Name);
        Assert.Equal(150m, updated.AmountInBase);
    }

    [Fact]
    public async Task End_StopsFutureOccurrences_ButKeepsHistory()
    {
        var created = await _service.CreateAsync(Recurring("Gym", 500m, new DateOnly(2026, 1, 5), Frequency.Monthly, Food));

        await _service.EndAsync(created.Id);

        Assert.Equal(new DateOnly(2026, 11, 18), _expenses.Items.Single().EndDate);
        Assert.Contains((await _service.GetMonthAsync(2026, 3)).Items, i => i.Name == "Gym");
        Assert.Contains((await _service.GetMonthAsync(2026, 11)).Items, i => i.Name == "Gym");
        Assert.DoesNotContain((await _service.GetMonthAsync(2026, 12)).Items, i => i.Name == "Gym");
    }

    [Fact]
    public async Task End_OnOneTimeOrNotStartedExpense_Throws()
    {
        var oneTime = await _service.CreateAsync(OneTime("Cena", 100m, new DateOnly(2026, 11, 1)));
        var future = await _service.CreateAsync(Recurring("Futuro", 100m, new DateOnly(2027, 1, 1), Frequency.Monthly, Food));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.EndAsync(oneTime.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.EndAsync(future.Id));
    }

    [Fact]
    public async Task Delete_Recurring_RemovesItsHistory()
    {
        var created = await _service.CreateAsync(Recurring("Gym", 500m, new DateOnly(2026, 1, 5), Frequency.Monthly, Food));

        await _service.DeleteAsync(created.Id);

        Assert.Empty((await _service.GetMonthAsync(2026, 3)).Items);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteAsync(created.Id));
    }

    [Fact]
    public async Task Restore_AfterDelete_BringsBackTheSameExpense_KeepingItsBaseAmount()
    {
        await _currency.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60.50m });
        var created = await _service.CreateAsync(OneTime("Spotify", 10m, new DateOnly(2026, 11, 4), Subscriptions, Currency.USD));
        var before = await _service.GetByIdAsync(created.Id);
        await _service.DeleteAsync(created.Id);
        Assert.Null(await _service.GetByIdAsync(created.Id));

        // The rate changes while the toast is on screen: the restored amount keeps the value it had.
        await _currency.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 70m });
        var restored = await _service.RestoreAsync(before!);

        var again = await _service.GetByIdAsync(restored.Id);
        Assert.NotNull(again);
        Assert.Equal("Spotify", again.Name);
        Assert.Equal(10m, again.Amount);
        Assert.Equal(Currency.USD, again.Currency);
        Assert.Equal(before!.AmountInBase, again.AmountInBase);
        Assert.Equal(new DateOnly(2026, 11, 4), again.Date);
        Assert.Equal(Subscriptions, again.CategoryId);
        Assert.Single((await _service.GetMonthAsync(2026, 11)).Items, i => i.Name == "Spotify");
    }

    [Fact]
    public async Task Restore_OfARecurringExpense_KeepsItsFrequencyAndEnd()
    {
        var created = await _service.CreateAsync(Recurring("Gym", 500m, new DateOnly(2026, 1, 5), Frequency.Monthly, Food, end: new DateOnly(2026, 12, 31)));
        var before = await _service.GetByIdAsync(created.Id);
        await _service.DeleteAsync(created.Id);

        var restored = await _service.RestoreAsync(before!);

        Assert.True(restored.IsRecurring);
        Assert.Equal(Frequency.Monthly, restored.Frequency);
        Assert.Equal(new DateOnly(2026, 12, 31), restored.EndDate);
    }

    [Fact]
    public async Task Restore_WithAnInactiveCategory_StillWorks_ButNotWithAMissingOne()
    {
        var created = await _service.CreateAsync(OneTime("Cine", 300m, new DateOnly(2026, 11, 6), Leisure));
        var before = await _service.GetByIdAsync(created.Id);
        await _service.DeleteAsync(created.Id);

        _categories.Items.Single(c => c.Id == Leisure).IsActive = false;
        await _service.RestoreAsync(before!);   // it already had that category

        before!.CategoryId = 999;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RestoreAsync(before));
    }

    [Fact]
    public async Task Update_ConvertingOneTimeToRecurring_ChangesItsType()
    {
        var created = await _service.CreateAsync(OneTime("Gym", 500m, new DateOnly(2026, 10, 5)));

        await _service.UpdateAsync(created.Id, Recurring("Gym", 500m, new DateOnly(2026, 10, 5), Frequency.Monthly, Food));

        var nov = await _service.GetMonthAsync(2026, 11);
        Assert.Equal(LedgerSource.RecurringExpense, nov.Items.Single(i => i.Name == "Gym").Source);
    }
}
