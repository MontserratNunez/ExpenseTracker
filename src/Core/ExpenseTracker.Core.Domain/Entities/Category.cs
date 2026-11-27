using ExpenseTracker.Core.Domain.Common;

namespace ExpenseTracker.Core.Domain.Entities;

public class Category : UserOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6C757D";
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
}
