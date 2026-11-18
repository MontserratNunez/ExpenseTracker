using ExpenseTracker.Core.Application.DTOs.Debts;
using ExpenseTracker.Core.Application.DTOs.Expenses;
using ExpenseTracker.Core.Application.DTOs.Settings;
using ExpenseTracker.Core.Application.Services;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Tests.Fakes;

namespace ExpenseTracker.Tests.Application;

public class DebtServiceTests
{
    private readonly FakeDebtRepository _debts = new();
    private readonly FakeRepository<Category> _categories = new();
    private readonly FakeRepository<UserSettings> _settings = new();
    private readonly FakeRepository<ExchangeRate> _rates = new();
    private readonly FixedDateProvider _dates = new(new DateOnly(2026, 11, 18));
    private readonly CurrencyService _currency;
    private readonly DebtService _service;
    private readonly MonthlyLedgerService _ledger;

    public DebtServiceTests()
    {
        _settings.Items.Add(new UserSettings { Id = 1, BaseCurrency = Currency.DOP });
        _currency = new CurrencyService(_settings, _rates, _dates);
        _service = new DebtService(_debts, _currency, _dates);
        _ledger = new MonthlyLedgerService(new FakeExpenseRepository(_categories), _debts, _currency, _dates);
    }

    /// <summary>The enunciado loan: 1,000,000 at 5% with 500,000 already paid.</summary>
    private static DebtInput Loan(decimal installment = 30_000m) => new()
    {
        Name = "Préstamo",
        DebtType = DebtType.Loan,
        OriginalAmount = 1_000_000m,
        AnnualInterestRate = 5m,
        InitialPaidAmount = 500_000m,
        MonthlyPayment = installment,
        PaymentDay = 5,
        StartDate = new DateOnly(2023, 1, 5)
    };

    private static DebtInput Small(decimal original = 3_000m, decimal installment = 1_000m) => new()
    {
        Name = "Tarjeta",
        DebtType = DebtType.CreditCard,
        OriginalAmount = original,
        AnnualInterestRate = 0m,
        MonthlyPayment = installment,
        PaymentDay = 10,
        StartDate = new DateOnly(2026, 1, 1)
    };

    private static DebtPaymentInput Pay(decimal amount, DateOnly date) => new() { Amount = amount, Date = date };

    [Fact]
    public async Task Summary_MatchesEnunciadoExample_BalanceAndPercentPaid()
    {
        await _service.CreateAsync(Loan());

        var debt = Assert.Single(await _service.GetSummaryAsync());

        Assert.Equal(500_000m, debt.RemainingBalance);
        Assert.Equal(50m, debt.PaidPercent);
        Assert.Equal(500_000m, debt.RemainingBalanceInBase);
    }

    [Fact]
    public async Task RegisterPayment_SplitsInterestAndPrincipal_AsInEnunciado()
    {
        var debt = await _service.CreateAsync(Loan());

        var payment = await _service.RegisterPaymentAsync(debt.Id, Pay(30_000m, new DateOnly(2023, 10, 5)));

        Assert.Equal(2_083.33m, payment.InterestPortion);
        Assert.Equal(27_916.67m, payment.PrincipalPortion);
        Assert.Equal(472_083.33m, (await _service.GetDetailAsync(debt.Id)).Debt.RemainingBalance);
    }

    [Fact]
    public async Task RegisterPayment_AboveBalancePlusInterest_Throws()
    {
        var debt = await _service.CreateAsync(Small(original: 3_000m));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RegisterPaymentAsync(debt.Id, Pay(3_000.01m, new DateOnly(2026, 11, 1))));
    }

    [Fact]
    public async Task RegisterPayment_ThatSettlesTheBalance_ClosesTheDebt_AndBlocksFurtherPayments()
    {
        var debt = await _service.CreateAsync(Small(original: 3_000m));

        await _service.RegisterPaymentAsync(debt.Id, Pay(3_000m, new DateOnly(2026, 11, 1)));

        var detail = await _service.GetDetailAsync(debt.Id);
        Assert.True(detail.Debt.IsClosed);
        Assert.Equal(0m, detail.Debt.RemainingBalance);
        Assert.Equal(100m, detail.Debt.PaidPercent);
        Assert.Null(detail.Debt.NextPaymentDate);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RegisterPaymentAsync(debt.Id, Pay(1m, new DateOnly(2026, 11, 2))));
    }

    [Fact]
    public async Task DeletePayment_RestoresBalance_AndReopensAClosedDebt()
    {
        var debt = await _service.CreateAsync(Small(original: 3_000m));
        var payment = await _service.RegisterPaymentAsync(debt.Id, Pay(3_000m, new DateOnly(2026, 11, 1)));

        await _service.DeletePaymentAsync(debt.Id, payment.Id);

        var detail = await _service.GetDetailAsync(debt.Id);
        Assert.False(detail.Debt.IsClosed);
        Assert.Equal(3_000m, detail.Debt.RemainingBalance);
        Assert.Empty(detail.Payments);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeletePaymentAsync(debt.Id, payment.Id));
    }

    [Fact]
    public async Task Create_WithTermAndNoInstallment_CalculatesIt()
    {
        var input = new DebtInput
        {
            Name = "Carro", DebtType = DebtType.CarLoan, OriginalAmount = 100_000m, AnnualInterestRate = 12m,
            PaymentDay = 15, StartDate = new DateOnly(2026, 11, 1), TermMonths = 12
        };

        var debt = await _service.CreateAsync(input);

        Assert.Equal(8_884.88m, debt.MonthlyPayment);
    }

    [Fact]
    public async Task Create_TermCountsFromStartDate_SoRemainingMonthsShrink()
    {
        // Started 6 months ago with a 12 month term: 6 months left.
        var input = new DebtInput
        {
            Name = "Carro", DebtType = DebtType.CarLoan, OriginalAmount = 60_000m, AnnualInterestRate = 0m,
            PaymentDay = 15, StartDate = new DateOnly(2026, 5, 1), TermMonths = 12
        };

        Assert.Equal(10_000m, (await _service.CreateAsync(input)).MonthlyPayment);
    }

    [Fact]
    public async Task Create_WithoutInstallmentNorTerm_Throws()
    {
        var input = Small();
        input.MonthlyPayment = 0m;

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(input));
    }

    [Fact]
    public async Task Create_InvalidValues_Throw()
    {
        var day = Small(); day.PaymentDay = 31;
        var paid = Small(); paid.InitialPaidAmount = 3_000m;
        var rate = Small(); rate.AnnualInterestRate = 150m;

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(day));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(paid));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(rate));
    }

    [Fact]
    public async Task Summary_NextPaymentAndEstimatedPayoff_FollowTheSchedule()
    {
        var debt = await _service.CreateAsync(Small(original: 3_000m, installment: 1_000m));

        var before = (await _service.GetSummaryAsync()).Single();
        Assert.Equal(new DateOnly(2026, 11, 10), before.NextPaymentDate);
        Assert.Equal(new DateOnly(2027, 1, 10), before.EstimatedPayoffDate);

        await _service.RegisterPaymentAsync(debt.Id, Pay(1_000m, new DateOnly(2026, 11, 10)));

        var after = (await _service.GetSummaryAsync()).Single();
        Assert.Equal(new DateOnly(2026, 12, 10), after.NextPaymentDate);
        Assert.Equal(new DateOnly(2027, 1, 10), after.EstimatedPayoffDate);
    }

    [Fact]
    public async Task Summary_InstallmentBelowInterest_HasNoPayoffDate()
    {
        var input = Loan(installment: 1_000m); // interest alone is 2,083.33
        await _service.CreateAsync(input);

        Assert.Null((await _service.GetSummaryAsync()).Single().EstimatedPayoffDate);
    }

    [Fact]
    public async Task SuggestedPayment_IsInstallment_OrLessWhenBalanceIsSmaller()
    {
        var big = await _service.CreateAsync(Loan());
        var tiny = await _service.CreateAsync(Small(original: 400m, installment: 1_000m));

        Assert.Equal(30_000m, big.SuggestedPayment);
        Assert.Equal(400m, tiny.SuggestedPayment);
    }

    [Fact]
    public async Task Amortization_ListsRemainingInstallments_AndEndsAtZero()
    {
        var debt = await _service.CreateAsync(Small(original: 3_000m, installment: 1_000m));

        var table = await _service.GetAmortizationAsync(debt.Id);

        Assert.True(table.PaysOff);
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(0m, table.Rows[^1].Balance);
    }

    [Fact]
    public async Task Delete_RemovesDebtAndItsPayments()
    {
        var debt = await _service.CreateAsync(Small());
        await _service.RegisterPaymentAsync(debt.Id, Pay(100m, new DateOnly(2026, 11, 1)));

        await _service.DeleteAsync(debt.Id);

        Assert.Empty(await _service.GetSummaryAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetDetailAsync(debt.Id));
    }

    // ---------- Integration with the monthly ledger ----------

    [Fact]
    public async Task Ledger_InstallmentWithoutPaymentThisMonth_IsPending()
    {
        var debt = await _service.CreateAsync(Small());

        var items = await _ledger.GetMonthAsync(2026, 11);

        var item = Assert.Single(items);
        Assert.Equal(LedgerSource.DebtInstallment, item.Source);
        Assert.Equal(LedgerStatus.Pending, item.Status);
        Assert.Equal(1_000m, item.AmountInBase);
        Assert.Equal(new DateOnly(2026, 11, 10), item.Date);
        Assert.Equal(debt.Id, item.SourceId);
        Assert.Equal("Deudas", item.CategoryName);
        Assert.True(item.IsReadOnly);
    }

    [Fact]
    public async Task Ledger_RegisteringThePayment_MovesItFromPendingToSpent()
    {
        var debt = await _service.CreateAsync(Small());

        await _service.RegisterPaymentAsync(debt.Id, Pay(1_000m, new DateOnly(2026, 11, 12)));
        var items = await _ledger.GetMonthAsync(2026, 11);

        var item = Assert.Single(items);
        Assert.Equal(LedgerSource.DebtPayment, item.Source);
        Assert.Equal(LedgerStatus.Spent, item.Status);
        Assert.Equal(new DateOnly(2026, 11, 12), item.Date);
    }

    [Fact]
    public async Task Ledger_PartialPaymentCoversTheMonth_NoExtraPendingInstallment()
    {
        var debt = await _service.CreateAsync(Small());

        await _service.RegisterPaymentAsync(debt.Id, Pay(300m, new DateOnly(2026, 11, 3)));

        var items = await _ledger.GetMonthAsync(2026, 11);
        Assert.Single(items);
        Assert.Equal(LedgerStatus.Spent, items[0].Status);
    }

    [Fact]
    public async Task Ledger_PastMonthsWithoutPayments_ShowNoPendingInstallments()
    {
        await _service.CreateAsync(Small());

        Assert.Empty(await _ledger.GetMonthAsync(2026, 10));
    }

    [Fact]
    public async Task Ledger_PastPayments_AppearInTheirOwnMonth()
    {
        var debt = await _service.CreateAsync(Loan());
        await _service.RegisterPaymentAsync(debt.Id, Pay(30_000m, new DateOnly(2023, 10, 5)));

        var items = await _ledger.GetMonthAsync(2023, 10);

        Assert.Equal(30_000m, Assert.Single(items).AmountInBase);
    }

    [Fact]
    public async Task Ledger_FutureMonths_ProjectInstallmentsOnlyUntilPayoff()
    {
        await _service.CreateAsync(Small(original: 3_000m, installment: 1_000m)); // Nov, Dec, Jan

        Assert.Single(await _ledger.GetMonthAsync(2026, 12));
        Assert.Single(await _ledger.GetMonthAsync(2027, 1));
        Assert.Empty(await _ledger.GetMonthAsync(2027, 2));
    }

    [Fact]
    public async Task Ledger_ClosedDebt_HasNoPendingInstallments()
    {
        var debt = await _service.CreateAsync(Small(original: 1_000m));
        await _service.RegisterPaymentAsync(debt.Id, Pay(1_000m, new DateOnly(2026, 10, 1)));

        Assert.Empty(await _ledger.GetMonthAsync(2026, 11));
        Assert.Single(await _ledger.GetMonthAsync(2026, 10));
    }

    [Fact]
    public async Task Ledger_ForeignCurrencyDebt_IsConvertedToBase()
    {
        await _currency.SetRateAsync(new ExchangeRateInput { Currency = Currency.USD, RateToBase = 60m });
        var input = Small();
        input.Currency = Currency.USD;
        await _service.CreateAsync(input);

        var item = Assert.Single(await _ledger.GetMonthAsync(2026, 11));

        Assert.Equal(1_000m, item.Amount);
        Assert.Equal(60_000m, item.AmountInBase);
    }

    [Fact]
    public async Task Ledger_DebtStartingInTheFuture_ShowsFirstInstallmentFromItsStartMonth()
    {
        var input = Small();
        input.StartDate = new DateOnly(2027, 2, 1);
        await _service.CreateAsync(input);

        Assert.Empty(await _ledger.GetMonthAsync(2026, 12));
        Assert.Single(await _ledger.GetMonthAsync(2027, 2));
    }
}
