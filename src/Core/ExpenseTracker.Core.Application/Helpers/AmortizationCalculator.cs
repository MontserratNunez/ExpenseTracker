using ExpenseTracker.Core.Domain.Entities;

namespace ExpenseTracker.Core.Application.Helpers;

public class AmortizationRow
{
    public DateOnly Date { get; set; }

    /// <summary>Total paid in this installment (interest + principal).</summary>
    public decimal Payment { get; set; }

    public decimal Interest { get; set; }
    public decimal Principal { get; set; }

    /// <summary>Balance left after this installment.</summary>
    public decimal Balance { get; set; }
}

public class AmortizationResult
{
    public List<AmortizationRow> Rows { get; set; } = new();

    /// <summary>False when the installment does not cover the interest, or the row limit was reached first.</summary>
    public bool PaysOff { get; set; }

    public DateOnly? PayoffDate => PaysOff && Rows.Count > 0 ? Rows[^1].Date : null;
}

/// <summary>
/// Pure debt calculations (rule 5.4). Shared by the debts module, the monthly ledger and the forecast.
/// Interest is simple on balance: balance x annual rate / 12.
/// </summary>
public static class AmortizationCalculator
{
    public const int DefaultMaxMonths = 600;

    /// <summary>Interest due for one month on a balance, rounded to cents.</summary>
    public static decimal MonthlyInterest(decimal balance, decimal annualRatePercent)
        => Round(balance * annualRatePercent / 100m / 12m);

    /// <summary>Splits a payment into interest and principal: interest = min(payment, balance x rate / 12).</summary>
    public static (decimal Interest, decimal Principal) SplitPayment(decimal payment, decimal balance, decimal annualRatePercent)
    {
        var interest = Math.Min(payment, MonthlyInterest(balance, annualRatePercent));
        return (interest, Round(payment - interest));
    }

    /// <summary>Fixed installment: P x r / (1 - (1 + r)^-n), with r = rate / 12. If r = 0 it is P / n.</summary>
    public static decimal CalculateInstallment(decimal principal, decimal annualRatePercent, int months)
    {
        if (principal <= 0 || months <= 0)
            return 0m;

        var r = (double)(annualRatePercent / 100m / 12m);
        if (r == 0d)
            return Round(principal / months);

        var installment = (double)principal * r / (1d - Math.Pow(1d + r, -months));
        return Round((decimal)installment);
    }

    /// <summary>
    /// Simulates installments month by month from the given month until the balance reaches 0.
    /// The last installment equals the remaining balance plus its interest.
    /// </summary>
    public static AmortizationResult Simulate(
        decimal balance, decimal annualRatePercent, decimal monthlyPayment,
        int startYear, int startMonth, int paymentDay, int maxMonths = DefaultMaxMonths)
    {
        var result = new AmortizationResult();
        if (balance <= 0 || monthlyPayment <= 0)
            return result;

        var first = new DateOnly(startYear, startMonth, 1);

        for (var n = 0; balance > 0 && n < maxMonths; n++)
        {
            var month = first.AddMonths(n);
            var date = new DateOnly(month.Year, month.Month, Math.Min(paymentDay, DateTime.DaysInMonth(month.Year, month.Month)));
            var interestDue = MonthlyInterest(balance, annualRatePercent);

            decimal payment, interest, principal;
            if (balance + interestDue <= monthlyPayment)
            {
                payment = balance + interestDue;
                interest = interestDue;
                principal = balance;
            }
            else
            {
                payment = monthlyPayment;
                (interest, principal) = SplitPayment(monthlyPayment, balance, annualRatePercent);
            }

            balance = Round(balance - principal);
            result.Rows.Add(new AmortizationRow
            {
                Date = date,
                Payment = payment,
                Interest = interest,
                Principal = principal,
                Balance = balance
            });
        }

        result.PaysOff = balance <= 0;
        return result;
    }

    /// <summary>
    /// Pending installments of a debt: starts this month if no payment has been registered
    /// this month, otherwise next month (and never before the debt starts).
    /// The debt must be loaded with its payments.
    /// </summary>
    public static AmortizationResult ForDebt(Debt debt, DateOnly today, int maxMonths = DefaultMaxMonths)
    {
        var balance = debt.GetRemainingBalance();
        if (debt.IsClosed || balance <= 0)
            return new AmortizationResult { PaysOff = true };

        var paidThisMonth = debt.Payments.Any(p => p.Date.Year == today.Year && p.Date.Month == today.Month);
        var first = new DateOnly(today.Year, today.Month, 1);
        if (paidThisMonth)
            first = first.AddMonths(1);

        var start = new DateOnly(debt.StartDate.Year, debt.StartDate.Month, 1);
        if (start > first)
            first = start;

        return Simulate(balance, debt.AnnualInterestRate, debt.MonthlyPayment, first.Year, first.Month, debt.PaymentDay, maxMonths);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
