using ExpenseTracker.Core.Domain.Enums;

namespace ExpenseTracker.Core.Application.DTOs.Expenses;

public class ExpenseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Currency Currency { get; set; }
    public decimal AmountInBase { get; set; }
    public bool IsRecurring { get; set; }
    public DateOnly Date { get; set; }
    public Frequency? Frequency { get; set; }
    public DateOnly? EndDate { get; set; }
    public int CategoryId { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}
