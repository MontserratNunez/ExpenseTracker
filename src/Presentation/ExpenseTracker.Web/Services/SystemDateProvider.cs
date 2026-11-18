using ExpenseTracker.Core.Application.Interfaces;

namespace ExpenseTracker.Web.Services;

public class SystemDateProvider : IDateProvider
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
