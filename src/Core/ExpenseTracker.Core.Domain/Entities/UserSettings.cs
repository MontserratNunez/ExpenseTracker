using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Domain.Entities;

public class UserSettings : UserOwnedEntity
{
    public Currency BaseCurrency { get; set; } = Currency.DOP;
    public string CultureName { get; set; } = "es-DO";
    public int MonthStartDay { get; set; } = 1;
}
