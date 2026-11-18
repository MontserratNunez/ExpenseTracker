using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;
using ExpenseTracker.Core.Domain.Enums;
using ExpenseTracker.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Persistence.Seed;

public class UserDataSeeder : IUserDataSeeder
{
    private static readonly (string Name, string Color, string Icon, bool IsSavings)[] DefaultCategories =
    {
        ("Comida", "#E67E22", "basket", false),
        ("Compras/Salidas", "#9B59B6", "bag", false),
        ("Servicios", "#3498DB", "lightning-charge", false),
        ("Transporte", "#1ABC9C", "bus-front", false),
        ("Salud", "#E74C3C", "heart-pulse", false),
        ("Entretenimiento", "#F1C40F", "film", false),
        ("Suscripciones", "#34495E", "collection-play", false),
        ("Seguros", "#16A085", "shield-check", false),
        ("Ahorro", "#2E9E5B", "piggy-bank", true),
        ("Otros", "#95A5A6", "tag", false)
    };

    private readonly IDbContextFactory<AppDbContext> _factory;

    public UserDataSeeder(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task SeedDefaultsForUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El usuario es obligatorio.", nameof(userId));

        await using var context = await _factory.CreateDbContextAsync();

        // The seeded user is usually not the one in the current context (just registered),
        // so look up by explicit UserId ignoring the global filter.
        if (!await context.UserSettings.IgnoreQueryFilters().AnyAsync(s => s.UserId == userId))
        {
            context.UserSettings.Add(new UserSettings
            {
                UserId = userId,
                BaseCurrency = Currency.DOP,
                CultureName = "es-DO",
                MonthStartDay = 1
            });
        }

        var existingNames = await context.Categories.IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .Select(c => c.Name)
            .ToListAsync();

        foreach (var (name, color, icon, isSavings) in DefaultCategories)
        {
            if (existingNames.Contains(name))
                continue;

            context.Categories.Add(new Category
            {
                UserId = userId,
                Name = name,
                Color = color,
                Icon = icon,
                IsSavings = isSavings
            });
        }

        await context.SaveChangesAsync();
    }
}
