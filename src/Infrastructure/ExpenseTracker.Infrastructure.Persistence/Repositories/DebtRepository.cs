using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Persistence.Repositories;

public class DebtRepository : Repository<Debt>, IDebtRepository
{
    public DebtRepository(IDbContextFactory<AppDbContext> factory) : base(factory)
    {
    }

    public async Task<Debt?> GetWithPaymentsAsync(int id)
    {
        await using var context = await Factory.CreateDbContextAsync();
        return await context.Debts
            .AsNoTracking()
            .Include(d => d.Payments)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<List<Debt>> GetOpenWithPaymentsAsync()
    {
        await using var context = await Factory.CreateDbContextAsync();
        return await context.Debts
            .AsNoTracking()
            .Include(d => d.Payments)
            .Where(d => !d.IsClosed)
            .ToListAsync();
    }

    public async Task<List<Debt>> GetAllWithPaymentsAsync()
    {
        await using var context = await Factory.CreateDbContextAsync();
        return await context.Debts
            .AsNoTracking()
            .Include(d => d.Payments)
            .ToListAsync();
    }

    public async Task AddPaymentAsync(DebtPayment payment)
    {
        await using var context = await Factory.CreateDbContextAsync();

        // The debt must belong to the current user.
        if (!await context.Debts.AnyAsync(d => d.Id == payment.DebtId))
            throw new InvalidOperationException("La deuda no existe.");

        payment.Debt = null;
        context.DebtPayments.Add(payment);
        await context.SaveChangesAsync();
    }

    public async Task DeletePaymentAsync(int paymentId)
    {
        await using var context = await Factory.CreateDbContextAsync();

        var payment = await context.DebtPayments.FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new InvalidOperationException("El pago no existe.");

        context.DebtPayments.Remove(payment);
        await context.SaveChangesAsync();
    }
}
