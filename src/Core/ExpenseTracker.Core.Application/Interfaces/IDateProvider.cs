namespace ExpenseTracker.Core.Application.Interfaces;

/// <summary>Source of "today", so calculations can be tested with fixed dates.</summary>
public interface IDateProvider
{
    DateOnly Today { get; }
}
