using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Domain.Entities;

public class ExchangeRate : UserOwnedEntity
{
    public Currency Currency { get; set; }

    /// <summary>Units of base currency per one unit of <see cref="Currency"/> (e.g. USD to 60.50 DOP).</summary>
    public decimal RateToBase { get; set; }

    public DateOnly UpdatedOn { get; set; }
}
