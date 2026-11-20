using ExpenseTracker.Infrastructure.Identity.Context;
using ExpenseTracker.Infrastructure.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Web.Services;

public static class DatabaseInitializer
{
    private const int Attempts = 4;

    /// <summary>
    /// Applies pending migrations for both contexts. SQL Server LocalDB can take a while to start the first time
    /// it is used, so a failed connection is retried a few times before giving up.
    /// </summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await MigrateOnceAsync(services);
                return;
            }
            catch (SqlException) when (attempt < Attempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
            }
        }
    }

    private static async Task MigrateOnceAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        var identity = scope.ServiceProvider.GetRequiredService<IdentityContext>();
        await identity.Database.MigrateAsync();

        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var app = await factory.CreateDbContextAsync();
        await app.Database.MigrateAsync();
    }
}
