using ExpenseTracker.Core.Domain.Entities;

namespace ExpenseTracker.Tests.Domain;

public class DebtTests
{
    private static Debt CreateLoan() => new()
    {
        OriginalAmount = 1_000_000m,
        AnnualInterestRate = 5m,
        InitialPaidAmount = 500_000m
    };

    [Fact]
    public void RemainingBalance_WithoutPayments_IsOriginalMinusInitialPaid()
    {
        Assert.Equal(500_000m, CreateLoan().GetRemainingBalance());
    }

    [Fact]
    public void RemainingBalance_SubtractsOnlyPrincipalPortions()
    {
        var debt = CreateLoan();
        debt.Payments.Add(new DebtPayment { Amount = 30_000m, InterestPortion = 2_083.33m, PrincipalPortion = 27_916.67m });

        Assert.Equal(472_083.33m, debt.GetRemainingBalance());
    }

    [Fact]
    public void MonthlyInterest_MatchesEnunciadoExample()
    {
        var interest = Math.Round(CreateLoan().GetMonthlyInterest(500_000m), 2);

        Assert.Equal(2_083.33m, interest);
    }
}
