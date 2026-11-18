using ExpenseTracker.Core.Domain.Entities;

namespace ExpenseTracker.Core.Application.Interfaces;

public interface IDebtRepository : IRepository<Debt>
{
    Task<Debt?> GetWithPaymentsAsync(int id);
    Task<List<Debt>> GetOpenWithPaymentsAsync();

    /// <summary>All debts (open and closed) with their payments.</summary>
    Task<List<Debt>> GetAllWithPaymentsAsync();
    Task AddPaymentAsync(DebtPayment payment);
    Task DeletePaymentAsync(int paymentId);
}
