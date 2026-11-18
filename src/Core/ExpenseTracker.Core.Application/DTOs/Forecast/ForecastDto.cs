namespace ExpenseTracker.Core.Application.DTOs.Forecast;

/// <summary>Where a variable-spending estimate comes from.</summary>
public enum VariableBasis
{
    /// <summary>Average of the one-time spending of the last 3 full months.</summary>
    History = 1,

    /// <summary>No history: the amount assigned in the Plan.</summary>
    Plan = 2
}

public class ForecastDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    // ----- Expenses (base currency) -----

    public decimal RecurringTotal { get; set; }
    public decimal DebtsTotal { get; set; }
    public decimal VariablesTotal { get; set; }

    /// <summary>Recurring + debts + variables.</summary>
    public decimal TotalExpenses { get; set; }

    // ----- Income and balance -----

    public decimal ProjectedIncome { get; set; }

    /// <summary>Projected income minus estimated expenses.</summary>
    public decimal Balance { get; set; }

    // ----- Breakdown -----

    public List<ForecastItemDto> RecurringItems { get; set; } = new();
    public List<ForecastItemDto> DebtItems { get; set; } = new();
    public List<ForecastVariableDto> VariableItems { get; set; } = new();

    /// <summary>One row per calendar month the range touches.</summary>
    public List<ForecastMonthDto> Months { get; set; } = new();

    public bool UsesHistory => VariableItems.Any(v => v.Basis == VariableBasis.History);
    public bool UsesPlan => VariableItems.Any(v => v.Basis == VariableBasis.Plan);
}

public class ForecastItemDto
{
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public decimal Amount { get; set; }
}

public class ForecastVariableDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public VariableBasis Basis { get; set; }
}

public class ForecastMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>True when the range covers only part of the month.</summary>
    public bool IsPartial { get; set; }

    public decimal Recurring { get; set; }
    public decimal Debts { get; set; }
    public decimal Variables { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal Income { get; set; }
    public decimal Balance { get; set; }
}
