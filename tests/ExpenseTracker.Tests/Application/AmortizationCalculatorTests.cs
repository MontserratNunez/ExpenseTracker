using ExpenseTracker.Core.Application.Helpers;

namespace ExpenseTracker.Tests.Application;

public class AmortizationCalculatorTests
{
    [Fact]
    public void SplitPayment_MatchesEnunciadoExample()
    {
        var (interest, principal) = AmortizationCalculator.SplitPayment(30_000m, 500_000m, 5m);

        Assert.Equal(2_083.33m, interest);
        Assert.Equal(27_916.67m, principal);
    }

    [Fact]
    public void SplitPayment_SmallerThanInterest_IsAllInterest()
    {
        var (interest, principal) = AmortizationCalculator.SplitPayment(1_000m, 1_000_000m, 12m);

        Assert.Equal(1_000m, interest);
        Assert.Equal(0m, principal);
    }

    [Theory]
    [InlineData(100_000, 12, 12, 8884.88)]
    [InlineData(1_200, 0, 12, 100)]
    [InlineData(0, 10, 12, 0)]
    public void CalculateInstallment_UsesAnnuityFormula(double principal, double rate, int months, double expected)
    {
        Assert.Equal((decimal)expected, AmortizationCalculator.CalculateInstallment((decimal)principal, (decimal)rate, months));
    }

    [Fact]
    public void Simulate_PaysOffExactly_WithLastInstallmentEqualToBalancePlusInterest()
    {
        var result = AmortizationCalculator.Simulate(100_000m, 12m, 8_884.88m, 2026, 12, 15);

        Assert.True(result.PaysOff);
        Assert.Equal(12, result.Rows.Count);
        var last = result.Rows[^1];
        Assert.Equal(0m, last.Balance);
        Assert.Equal(last.Interest + last.Principal, last.Payment);
        Assert.Equal(new DateOnly(2027, 11, 15), result.PayoffDate);
        Assert.Equal(100_000m, result.Rows.Sum(r => r.Principal));
    }

    [Fact]
    public void Simulate_ZeroInterest_DividesBalanceEvenly_AndShortensLastInstallment()
    {
        var result = AmortizationCalculator.Simulate(3_500m, 0m, 1_000m, 2026, 11, 10);

        Assert.Equal(new[] { 1_000m, 1_000m, 1_000m, 500m }, result.Rows.Select(r => r.Payment));
        Assert.Equal(new DateOnly(2027, 2, 10), result.PayoffDate);
    }

    [Fact]
    public void Simulate_InstallmentBelowInterest_NeverPaysOff_AndStopsAtLimit()
    {
        var result = AmortizationCalculator.Simulate(1_000_000m, 12m, 5_000m, 2026, 1, 1, maxMonths: 24);

        Assert.False(result.PaysOff);
        Assert.Equal(24, result.Rows.Count);
        Assert.Null(result.PayoffDate);
        Assert.Equal(1_000_000m, result.Rows[^1].Balance);
    }

    [Fact]
    public void Simulate_NoPaymentOrBalance_ReturnsEmpty()
    {
        Assert.Empty(AmortizationCalculator.Simulate(1_000m, 5m, 0m, 2026, 1, 1).Rows);
        Assert.Empty(AmortizationCalculator.Simulate(0m, 5m, 100m, 2026, 1, 1).Rows);
    }
}
