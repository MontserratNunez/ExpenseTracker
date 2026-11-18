using ExpenseTracker.Core.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Core.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Application services are registered here as each module is added (all Scoped).
        services.AddScoped<CategoryService>();
        services.AddScoped<CurrencyService>();
        services.AddScoped<IncomeService>();
        services.AddScoped<DebtService>();
        services.AddScoped<MonthlyLedgerService>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<BudgetService>();

        return services;
    }
}
