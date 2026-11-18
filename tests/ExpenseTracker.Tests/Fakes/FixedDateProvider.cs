using ExpenseTracker.Core.Application.Interfaces;

namespace ExpenseTracker.Tests.Fakes;

public class FixedDateProvider : IDateProvider
{
    public FixedDateProvider(DateOnly today)
    {
        Today = today;
    }

    public DateOnly Today { get; set; }
}
