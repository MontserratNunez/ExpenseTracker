using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;

namespace ExpenseTracker.Tests.Fakes;

/// <summary>In-memory debt repository. Payments live inside their Debt, like the loaded entity graph.</summary>
public class FakeDebtRepository : FakeRepository<Debt>, IDebtRepository
{
    private int _nextPaymentId = 1;

    public Task<Debt?> GetWithPaymentsAsync(int id) => GetByIdAsync(id);

    public Task<List<Debt>> GetOpenWithPaymentsAsync() => Task.FromResult(Items.Where(d => !d.IsClosed).ToList());

    public Task<List<Debt>> GetAllWithPaymentsAsync() => Task.FromResult(Items.ToList());

    public Task AddPaymentAsync(DebtPayment payment)
    {
        var debt = Items.FirstOrDefault(d => d.Id == payment.DebtId)
            ?? throw new InvalidOperationException("La deuda no existe.");
        payment.Id = _nextPaymentId++;
        debt.Payments.Add(payment);
        return Task.CompletedTask;
    }

    public Task DeletePaymentAsync(int paymentId)
    {
        var debt = Items.FirstOrDefault(d => d.Payments.Any(p => p.Id == paymentId))
            ?? throw new InvalidOperationException("El pago no existe.");
        debt.Payments.RemoveAll(p => p.Id == paymentId);
        return Task.CompletedTask;
    }
}
