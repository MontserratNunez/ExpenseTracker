using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Domain.Entities;

public class ExchangeRate : UserOwnedEntity
{
    public Currency Currency { get; set; }

    /// <summary>Units of base currency per one unit of <see cref="Currency"/> (e.g. USD to 60.50 DOP).</summary>
    public decimal RateToBase { get; set; }

    /// <summary>Date from which this rate applies. Older rates are kept.</summary>
    public DateOnly EffectiveFrom { get; set; }
}
