using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Infrastructure.Persistence.Context;
using ExpenseTracker.Infrastructure.Persistence.Repositories;
using ExpenseTracker.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Infrastructure.Persistence;

public static class DependencyInjection
{
    public const string ConnectionStringName = "ExpenseTracker";

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' not found.");

        // Scoped factory: AppDbContext needs the scoped ICurrentUserService.
        services.AddDbContextFactory<AppDbContext>(
            options => options.UseSqlServer(connectionString),
            ServiceLifetime.Scoped);

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IDebtRepository, DebtRepository>();
        services.AddScoped<IBudgetPlanRepository, BudgetPlanRepository>();
        services.AddScoped<IUserDataSeeder, UserDataSeeder>();

        return services;
    }
}
