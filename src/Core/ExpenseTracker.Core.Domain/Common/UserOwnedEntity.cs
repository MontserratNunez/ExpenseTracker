namespace ExpenseTracker.Core.Domain.Common;

public abstract class UserOwnedEntity : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
}
