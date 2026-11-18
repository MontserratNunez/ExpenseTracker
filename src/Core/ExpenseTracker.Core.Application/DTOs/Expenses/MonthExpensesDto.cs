namespace ExpenseTracker.Core.Application.DTOs.Expenses;

public class MonthExpensesDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<LedgerItem> Items { get; set; } = new();
    public decimal TotalSpent { get; set; }
    public decimal TotalPending { get; set; }
    public List<CategoryTotalDto> ByCategory { get; set; } = new();
}

public class CategoryTotalDto
{
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = string.Empty;
    public decimal Spent { get; set; }
    public decimal Pending { get; set; }
}
