# Expense Tracker

Aplicación para llevar el historial de ingresos, gastos y deudas, comparar contra un plan mensual, seguir metas y estimar gastos futuros.

**Stack:** C# · .NET 10 · Blazor Web App (Interactive Server) · Onion Architecture · ASP.NET Core Identity · EF Core 10 · SQL Server

## Estructura

```
ExpenseTracker.slnx
└── src/
    ├── Core/            Domain, Application
    ├── Infrastructure/  Persistence, Identity
    └── Presentation/    Web
```

## Requisitos

- .NET 10 SDK
- SQL Server (LocalDB en desarrollo)

## Ejecutar

```bash
dotnet build
dotnet run --project src/Presentation/ExpenseTracker.Web
```

El plan de desarrollo está en [Agent/Plan.md](Agent/Plan.md).
