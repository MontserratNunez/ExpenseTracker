using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Core.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Application services are registered here as each module is added (all Scoped).
        return services;
    }
}
