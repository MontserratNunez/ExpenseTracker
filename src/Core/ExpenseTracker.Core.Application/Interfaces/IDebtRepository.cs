using ExpenseTracker.Core.Domain.Entities;

namespace ExpenseTracker.Core.Application.Interfaces;

public interface IDebtRepository : IRepository<Debt>
{
    Task<Debt?> GetWithPaymentsAsync(int id);
    Task<List<Debt>> GetOpenWithPaymentsAsync();
    Task AddPaymentAsync(DebtPayment payment);
    Task DeletePaymentAsync(int paymentId);
}
