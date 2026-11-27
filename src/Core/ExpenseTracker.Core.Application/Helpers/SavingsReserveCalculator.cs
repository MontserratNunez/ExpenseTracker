using ExpenseTracker.Core.Application.DTOs.Budget;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.Helpers;

/// <summary>Savings are what is not spent; the reserve is the minimum of them the user wants to keep.</summary>
public static class SavingsReserveCalculator
{
    /// <param name="expectedSpending">Spending expected by month end (null: only used + pending).</param>
    public static SavingsStatusDto Calculate(
        decimal income, decimal reserve, decimal used, decimal pending,
        decimal alertThresholdPercent, decimal? expectedSpending = null)
    {
        var spendable = income - reserve;
        var margin = spendable - used - pending;
        var realSavings = income - used - pending;

        var status = new SavingsStatusDto
        {
            Income = income,
            Reserve = reserve,
            Spendable = spendable,
            Used = used,
            Pending = pending,
            Margin = margin,
            RealSavings = realSavings,
            Extra = Math.Max(0m, realSavings - reserve),
            UsedSavings = Math.Clamp(-margin, 0m, Math.Max(0m, reserve)),
            Projection = income - (expectedSpending ?? used + pending)
        };

        if (income == 0m && used + pending == 0m)
            status.State = SavingsState.NoData;
        else if (realSavings <= 0m)
            status.State = SavingsState.Deficit;
        else if (margin <= 0m)
            status.State = SavingsState.UsingSavings;
        else if (margin < spendable * alertThresholdPercent / 100m)
            status.State = SavingsState.NearSavings;
        else
            status.State = SavingsState.OnPlan;

        if (margin >= 0m && reserve > 0m && status.Projection < reserve)
            status.ProjectedSavingsUse = Math.Min(reserve, reserve - status.Projection);

        return status;
    }

    /// <summary>The sentence that explains the state.</summary>
    public static string Message(SavingsStatusDto status, Currency currency)
    {
        string Money(decimal amount) => MoneyFormatter.Format(amount, currency);

        return status.State switch
        {
            SavingsState.OnPlan when status.Reserve > 0 => $"Puedes gastar {Money(status.Margin)} sin tocar tu ahorro.",
            SavingsState.OnPlan => $"Puedes gastar {Money(status.Margin)} sin pasar de tus ingresos.",
            SavingsState.NearSavings when status.Reserve > 0 => $"Te quedan {Money(status.Margin)} antes de usar tu ahorro.",
            SavingsState.NearSavings => $"Te quedan {Money(status.Margin)} de tus ingresos.",
            SavingsState.UsingSavings => $"Has usado {Money(status.UsedSavings)} de tus {Money(status.Reserve)} de ahorro.",
            SavingsState.Deficit when status.RealSavings < 0 => $"Gastaste {Money(-status.RealSavings)} más de lo que ganaste este mes.",
            SavingsState.Deficit => "Gastaste todo lo que ganaste este mes.",
            _ => "Aún no hay ingresos ni gastos este mes."
        };
    }
}
