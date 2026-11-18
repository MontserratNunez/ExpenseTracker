using ExpenseTracker.Infrastructure.Identity.Context;
using ExpenseTracker.Infrastructure.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Infrastructure.Identity;

public static class DependencyInjection
{
    private const string ConnectionStringName = "ExpenseTracker";

    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' not found.");

        // Identity tables live in their own schema, with their own migrations history table.
        services.AddDbContext<IdentityContext>(options =>
            options.UseSqlServer(connectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityContext.Schema)));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false; // simple to start with
                options.Password.RequiredLength = 8;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddEntityFrameworkStores<IdentityContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        return services;
    }
}
