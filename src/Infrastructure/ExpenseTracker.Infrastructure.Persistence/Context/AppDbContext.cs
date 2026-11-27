using System.Linq.Expressions;
using System.Reflection;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Common;
using ExpenseTracker.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Persistence.Context;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<IncomeSource> IncomeSources => Set<IncomeSource>();
    public DbSet<OneTimeIncome> OneTimeIncomes => Set<OneTimeIncome>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<DebtPayment> DebtPayments => Set<DebtPayment>();
    public DbSet<BudgetPlan> BudgetPlans => Set<BudgetPlan>();
    public DbSet<BudgetAllocation> BudgetAllocations => Set<BudgetAllocation>();
    public DbSet<Goal> Goals => Set<Goal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(UserOwnedEntity).IsAssignableFrom(t.ClrType)))
        {
            var method = typeof(AppDbContext)
                .GetMethod(nameof(ApplyUserFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);
            method.Invoke(this, new object[] { modelBuilder });
        }
    }

    // The filter reads _currentUser.UserId through the context instance, so EF
    // evaluates it per context (a parameter), not once for the whole model.
    private void ApplyUserFilter<T>(ModelBuilder modelBuilder) where T : UserOwnedEntity
    {
        Expression<Func<T, bool>> filter = e => e.UserId == _currentUser.UserId;
        modelBuilder.Entity<T>().HasQueryFilter(filter);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    private void ApplyAuditInfo()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;

                if (entry.Entity is UserOwnedEntity owned && string.IsNullOrEmpty(owned.UserId))
                {
                    if (string.IsNullOrEmpty(_currentUser.UserId))
                        throw new InvalidOperationException("No hay un usuario autenticado.");

                    owned.UserId = _currentUser.UserId;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Property(e => e.CreatedAtUtc).IsModified = false;

                if (entry.Entity is UserOwnedEntity)
                    entry.Property(nameof(UserOwnedEntity.UserId)).IsModified = false;
            }
        }
    }
}
