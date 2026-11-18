# Plan de desarrollo — Expense Tracker

> **Stack:** C# · .NET 10 · Blazor Web App (Interactive Server) · Onion Architecture · ASP.NET Core Identity · EF Core 10 · SQL Server
> **Objetivo:** llevar un historial de ingresos, gastos y deudas, comparar el gasto contra un plan mensual, seguir metas financieras y estimar los gastos del próximo mes, del resto del año y del próximo año.

---

## Índice

1. [Reglas del proyecto](#1-reglas-del-proyecto)
2. [Arquitectura](#2-arquitectura)
3. [Decisiones y supuestos](#3-decisiones-y-supuestos)
4. [Modelo de dominio](#4-modelo-de-dominio)
5. [Reglas de cálculo](#5-reglas-de-cálculo)
6. [Rutas y pantallas](#6-rutas-y-pantallas)
7. [Fases de desarrollo](#7-fases-de-desarrollo)
   - [Fase 0 — Solución y estructura](#fase-0--solución-y-estructura)
   - [Fase 1 — Dominio](#fase-1--dominio-expensetrackercoredomain)
   - [Fase 2 — Persistencia](#fase-2--persistencia-expensetrackerinfrastructurepersistence)
   - [Fase 3 — Identity y autenticación](#fase-3--identity-y-autenticación)
   - [Fase 4 — Base de la capa Application](#fase-4--base-de-la-capa-application)
   - [Fase 5 — Categorías y monedas](#fase-5--categorías-y-monedas)
   - [Fase 6 — Ingresos](#fase-6--ingresos)
   - [Fase 7 — Gastos](#fase-7--gastos)
   - [Fase 8 — Deudas](#fase-8--deudas)
   - [Fase 9 — Plan (presupuesto)](#fase-9--plan-presupuesto)
   - [Fase 10 — Metas](#fase-10--metas)
   - [Fase 11 — Estimaciones](#fase-11--estimaciones)
   - [Fase 12 — Inicio (dashboard)](#fase-12--inicio-dashboard)
   - [Fase 13 — Pulido, pruebas y despliegue](#fase-13--pulido-pruebas-y-despliegue)
8. [Checklist general](#8-checklist-general)
9. [Mejoras futuras (fuera de alcance)](#9-mejoras-futuras-fuera-de-alcance)

---

## 1. Reglas del proyecto

| Regla | Cómo se aplica |
|---|---|
| Código simple | Sin MediatR, sin AutoMapper, sin CQRS. Servicios de aplicación directos y mapeo manual. |
| **No usar `record`** | Entidades, DTOs, view models y resultados son **clases**. |
| Capa de entidades | Solo **clases** y **enums**, más una clase estática auxiliar para recurrencias. |
| Onion | Las dependencias apuntan siempre hacia el centro (Domain). |
| Una responsabilidad por servicio | Un servicio por módulo: Ingresos, Gastos, Deudas, Plan, Metas, Estimaciones, Dashboard. |
| Dinero | Siempre `decimal` (`decimal(18,2)` en BD). Nunca `double`/`float`. |
| Fechas | `DateOnly` para fechas de movimientos; `DateTime` (UTC) solo para auditoría. |
| Idioma | Código en inglés y UI con opcion en inglés o español que el usuario puede guardar. |
| GitHub | No hacer commits, dame el comando con su mensaje y yo hago el commit manualmente |

---

## 2. Arquitectura

### 2.1 Estructura de la solución

```
ExpenseTracker.slnx
└── Source/
    ├── Core/
    │   ├── ExpenseTracker.Core.Domain
    │   └── ExpenseTracker.Core.Application
    ├── Infrastructure/
    │   ├── ExpenseTracker.Infrastructure.Persistence
    │   └── ExpenseTracker.Infrastructure.Identity
    └── Presentation/
        └── ExpenseTracker.Web
```

### 2.2 Dependencias entre proyectos

```
                  ┌───────────────────────┐
                  │  ExpenseTracker.Web   │  (composition root)
                  └──────────┬────────────┘
          ┌──────────────────┼──────────────────┐
          ▼                  ▼                  ▼
 Infrastructure.Persistence  Core.Application  Infrastructure.Identity
          │                  │                  │
          └────────►  Core.Application ◄────────┘
                             │
                             ▼
                       Core.Domain   (sin dependencias)
```

| Proyecto | Referencia a | Paquetes NuGet |
|---|---|---|
| `Core.Domain` | — | — |
| `Core.Application` | Domain | `Microsoft.Extensions.DependencyInjection.Abstractions` |
| `Infrastructure.Persistence` | Application, Domain | `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design` |
| `Infrastructure.Identity` | Application | `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer` |
| `Web` | Application, Persistence, Identity | `Microsoft.EntityFrameworkCore.Tools` |

### 2.3 Responsabilidad de cada capa

- **Domain:** entidades, enums y lógica pura sin I/O (cálculo de recurrencias, saldo de una deuda).
- **Application:** interfaces (repositorios, usuario actual), DTOs, servicios con las reglas de negocio y los cálculos (plan, estimaciones, dashboard).
- **Persistence:** `AppDbContext`, configuraciones EF, repositorios, migraciones, filtro global por usuario.
- **Identity:** `ApplicationUser`, `IdentityDbContext`, registro de Identity.
- **Web:** componentes Blazor, páginas de cuenta, `CurrentUserService`, `Program.cs`.

### 2.4 Estructura interna de carpetas

```
ExpenseTracker.Core.Domain/
├── Common/        BaseEntity.cs, UserOwnedEntity.cs
├── Entities/      IncomeSource.cs, OneTimeIncome.cs, Expense.cs, Category.cs,
│                  Debt.cs, DebtPayment.cs, BudgetAllocation.cs, Goal.cs,
│                  ExchangeRate.cs, UserSettings.cs
├── Enums/         Currency.cs, ExpenseType.cs, Frequency.cs, DebtType.cs,
│                  AllocationType.cs, GoalPeriod.cs
└── Helpers/       RecurrenceCalculator.cs (static)

ExpenseTracker.Core.Application/
├── Interfaces/    IRepository.cs, IExpenseRepository.cs, IDebtRepository.cs,
│                  ICurrentUserService.cs, IDateProvider.cs
├── DTOs/          Incomes/, Expenses/, Debts/, Budget/, Goals/, Forecast/, Dashboard/
├── Services/      IncomeService.cs, ExpenseService.cs, CategoryService.cs,
│                  CurrencyService.cs, DebtService.cs, BudgetService.cs,
│                  GoalService.cs, ForecastService.cs, DashboardService.cs,
│                  MonthlyLedgerService.cs
└── DependencyInjection.cs   (AddApplication)

ExpenseTracker.Infrastructure.Persistence/
├── Context/          AppDbContext.cs
├── Configurations/   *Configuration.cs (IEntityTypeConfiguration<T>)
├── Repositories/     Repository.cs, ExpenseRepository.cs, DebtRepository.cs
├── Migrations/
└── DependencyInjection.cs   (AddPersistence)

ExpenseTracker.Infrastructure.Identity/
├── Models/        ApplicationUser.cs
├── Context/       IdentityContext.cs
├── Migrations/
└── DependencyInjection.cs   (AddIdentityInfrastructure)

ExpenseTracker.Web/
├── Components/
│   ├── Layout/        MainLayout.razor, NavMenu.razor
│   ├── Account/       (páginas Identity movidas desde la plantilla)
│   ├── Shared/        PieChart.razor, MonthNavigator.razor, YearMonthPicker.razor,
│   │                  MoneyText.razor, ProgressBar.razor, ConfirmDialog.razor
│   └── Pages/
│       ├── Home.razor
│       ├── Incomes/   Incomes.razor, IncomeSourceForm.razor, OneTimeIncomeForm.razor
│       ├── Expenses/  Expenses.razor, ExpenseForm.razor
│       ├── Debts/     Debts.razor, DebtDetail.razor, DebtForm.razor, DebtPaymentForm.razor
│       ├── Budget/    Budget.razor, AllocationForm.razor
│       ├── Goals/     Goals.razor, GoalForm.razor
│       ├── Forecast/  Forecast.razor
│       └── Settings/  Settings.razor (moneda base, tasas, categorías)
├── Services/      CurrentUserService.cs
└── Program.cs
```

---

## 3. Decisiones y supuestos

Son decisiones tomadas para mantener la app simple. Se pueden cambiar antes de la fase correspondiente.

| # | Tema | Decisión |
|---|---|---|
| D1 | Modo de render | Blazor Web App con **Interactive Server** global. Es lo más simple porque no requiere API ni WASM. |
| D2 | Base de datos | SQL Server (LocalDB en desarrollo). Una sola BD con dos `DbContext`: `AppDbContext` (esquema `dbo`) e `IdentityContext` (esquema `identity`). |
| D3 | Aislamiento por usuario | Cada entidad tiene `UserId` (string, sin FK hacia Identity para no acoplar capas). Se usa un **filtro global de EF** por `UserId`. |
| D4 | DbContext y Blazor Server | Se usa `IDbContextFactory<AppDbContext>` en los repositorios (un contexto por operación) para evitar errores de concurrencia en el circuito. |
| D5 | Moneda | El usuario tiene una **moneda base** (DOP por defecto). Los gastos pueden registrarse en otra moneda (ej. 10 USD). Se guardan el monto original, la moneda y el **monto convertido a la moneda base** con la tasa vigente al registrarlo. Las tasas son manuales (tabla `ExchangeRate`). |
| D6 | Gastos recurrentes | Un gasto recurrente es una **plantilla** (`Expense` con `Type = Recurring`) con frecuencia, fecha de inicio y fecha de fin opcional. Sus ocurrencias se **calculan** y no se guardan. Una ocurrencia con fecha ≤ hoy cuenta como **gastada**; con fecha > hoy cuenta como **comprometida** (gris). |
| D7 | Recurrentes en moneda extranjera | Se convierten con la tasa vigente al momento del cálculo. |
| D8 | Ingresos recurrentes | `IncomeSource` tiene monto y frecuencia (mensual por defecto). Para el mes se suma su equivalente mensual (ver §5.1). |
| D9 | Deudas | La cuota mensual la ingresa el usuario (o se calcula si da el plazo en meses). El interés se calcula de forma simple sobre saldo: `saldo × tasa anual / 12`. Cada pago registrado se divide en interés y capital. |
| D10 | Deudas en el mes | Pagos de deuda registrados en el mes → **gastado**. Cuota del mes aún no pagada → **comprometido**. |
| D11 | Ahorro | El ahorro se registra como un gasto en una categoría con `IsSavings = true` (ej. "Ahorro"). En el dashboard se muestra dentro del rojo (dinero que ya salió del disponible) y las metas lo usan para medir progreso. |
| D12 | Inicio | El círculo representa **el mes actual**: total = ingresos del mes. No se arrastra saldo de meses anteriores (mejora futura). |
| D13 | Gráfico | Componente propio en **SVG** (`PieChart.razor`). No se agregan librerías de gráficos. UI con Bootstrap (incluido en la plantilla). |
| D14 | Validación | DataAnnotations en DTOs de entrada + validaciones de negocio en servicios. Sin FluentValidation. |
| D15 | Errores de servicio | Los servicios lanzan `InvalidOperationException`/`ArgumentException` con mensajes en español. La UI los captura y los muestra. Sin patrón `Result`. |
| D16 | Pruebas | Proyecto opcional `ExpenseTracker.Tests` (xUnit) en la carpeta `Tests`, enfocado en cálculos (recurrencias, deudas, plan, estimaciones). |

---

## 4. Modelo de dominio

### 4.1 Enums

```csharp
public enum Currency { DOP = 1, USD = 2, EUR = 3 }

public enum ExpenseType { OneTime = 1, Recurring = 2 }

public enum Frequency
{
    Weekly = 1,      // cada 7 días
    Biweekly = 2,    // cada 14 días
    Monthly = 3,
    Quarterly = 4,   // cada 3 meses
    SemiAnnual = 5,  // cada 6 meses
    Annual = 6
}

public enum DebtType { Loan = 1, Mortgage = 2, CreditCard = 3, CarLoan = 4, Other = 5 }

public enum AllocationType { FixedAmount = 1, PercentageOfIncome = 2 }

public enum GoalPeriod { Monthly = 1, Annual = 2, Custom = 3 }
```

### 4.2 Entidades

**Base**

```csharp
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public abstract class UserOwnedEntity : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
}
```

| Entidad | Propiedades | Notas |
|---|---|---|
| `UserSettings` | `BaseCurrency`, `CultureName` (`es-DO`), `MonthStartDay` (1) | Una fila por usuario; se crea al registrarse. |
| `ExchangeRate` | `Currency`, `RateToBase` (decimal 18,6), `UpdatedOn` (DateOnly) | Ej.: USD → 60.50 DOP. |
| `Category` | `Name`, `Color` (hex), `Icon`, `IsSavings`, `IsActive` | Semillas: Comida, Compras/Salidas, Servicios, Transporte, Salud, Entretenimiento, Suscripciones, Seguros, Ahorro (IsSavings), Otros. |
| `IncomeSource` | `Name`, `Amount`, `Currency`, `Frequency`, `StartDate`, `EndDate?`, `IsActive` | Ingreso recurrente. Ej.: "Sueldo trabajo" 50,000 DOP mensual. |
| `OneTimeIncome` | `Description`, `Amount`, `Currency`, `AmountInBase`, `Date` | Ingreso único. Ej.: "Bono" 5,000 DOP. |
| `Expense` | `Name`, `Amount`, `Currency`, `AmountInBase`, `Type`, `Date` (fecha del gasto único o primera ocurrencia del recurrente), `Frequency?`, `EndDate?`, `CategoryId`, `Notes?`, `IsActive` | Si `Type = Recurring`, `Frequency` es obligatoria. |
| `Debt` | `Name`, `DebtType`, `OriginalAmount`, `AnnualInterestRate` (%), `InitialPaidAmount`, `MonthlyPayment`, `PaymentDay` (1–28), `StartDate`, `TermMonths?`, `Currency`, `IsClosed` | `InitialPaidAmount` es lo pagado antes de usar la app (ej. 500,000). |
| `DebtPayment` | `DebtId`, `Amount`, `Date`, `InterestPortion`, `PrincipalPortion`, `Notes?` | Ej.: 30,000 el 5/10/2023. |
| `BudgetAllocation` | `CategoryId`, `AllocationType`, `Value`, `IncomeSourceId?` | `Value` es un monto o un porcentaje. Si es porcentaje e `IncomeSourceId` es null, se usa el total de ingresos recurrentes. Ej.: Ahorro = 10 % de "Sueldo trabajo". |
| `Goal` | `Name`, `TargetAmount`, `Period`, `StartDate`, `EndDate`, `CategoryId?`, `IsActive` | Ej.: "Ahorro de 200,000 anualmente" con `Period = Annual` y la categoría Ahorro. |

### 4.3 Lógica en el dominio (sin dependencias)

```csharp
public static class RecurrenceCalculator
{
    // Devuelve las fechas en que ocurre un recurrente dentro de [from, to].
    public static List<DateOnly> GetOccurrences(
        DateOnly start, Frequency frequency, DateOnly? end, DateOnly from, DateOnly to) { ... }

    // Equivalente mensual (para ingresos y provisiones).
    public static decimal ToMonthlyAmount(decimal amount, Frequency frequency) { ... }
}
```

- `Monthly/Quarterly/SemiAnnual/Annual`: se suman meses desde `start` respetando el día (si el mes es más corto, se usa el último día del mes; ej. 31 → 30 feb/28 feb).
- `Weekly/Biweekly`: se suman 7/14 días.

`Debt` expone métodos simples:

```csharp
public decimal GetRemainingBalance()  // OriginalAmount - InitialPaidAmount - Σ PrincipalPortion
public decimal GetMonthlyInterest(decimal balance) // balance * AnnualInterestRate / 100 / 12
```

---

## 5. Reglas de cálculo

> Todas las sumas se hacen en la **moneda base** del usuario.

### 5.1 Ingresos del mes

```
IngresosMes = Σ ocurrencias de IncomeSource activas en el mes (convertidas)
            + Σ OneTimeIncome con fecha en el mes
```
Un ingreso mensual cuenta 1 vez y uno quincenal cuenta 2 o 3 veces según el calendario.

### 5.2 Movimientos del mes (`MonthlyLedgerService`)

Es el servicio central que usan Gastos, Inicio, Plan y Estimaciones. Para un `(año, mes)` devuelve una lista de `LedgerItem` (clase):

| Origen | Fecha | Estado |
|---|---|---|
| Gasto único | `Expense.Date` | Gastado |
| Ocurrencia de recurrente | fecha calculada | Gastado si ≤ hoy, Pendiente si > hoy |
| Pago de deuda registrado | `DebtPayment.Date` | Gastado |
| Cuota de deuda sin pago en el mes | `PaymentDay` del mes | Pendiente |

Una cuota se considera cubierta si existe al menos un `DebtPayment` de esa deuda en el mes.

### 5.3 Inicio (gráfico circular)

```
Gastado     (rojo)  = Σ LedgerItems del mes con estado Gastado
Comprometido(gris)  = Σ LedgerItems del mes con estado Pendiente
Disponible  (verde) = IngresosMes − Gastado − Comprometido
```
- Si `Disponible < 0`, el verde vale 0 y se muestra la alerta **"Déficit de X DOP este mes"**.
- Si `IngresosMes = 0`, se muestra un estado vacío con un enlace a "Configurar ingresos".

### 5.4 Deudas

- **Saldo pendiente** = `OriginalAmount − InitialPaidAmount − Σ PrincipalPortion`.
- **Al registrar un pago:**
  ```
  interés  = min(Amount, saldo × tasa / 12)
  capital  = Amount − interés
  ```
- **Cuota calculada** (si el usuario da `TermMonths` y no `MonthlyPayment`):
  `cuota = P × r / (1 − (1 + r)^−n)`, con `r = tasa/12` y `n = meses restantes` (si `r = 0`: `P / n`).
- **Proyección:** se simulan cuotas mes a mes hasta que el saldo llegue a 0. La última cuota es igual al saldo más los intereses.
- **Ejemplo del enunciado:** préstamo de 1,000,000 al 5 % con 500,000 pagados → saldo 500,000. Un pago de 30,000 se divide en interés 500,000 × 0.05 / 12 = 2,083.33 y capital 27,916.67.

### 5.5 Plan (presupuesto)

- **Monto asignado** por categoría:
  - `FixedAmount` → `Value`.
  - `PercentageOfIncome` → `Value% × (ingreso de IncomeSourceId, o total recurrente si es null)`.
- **Gastado** = Σ LedgerItems Gastado de la categoría en el mes.
- **Disponible** = Asignado − Gastado − Pendiente de la categoría.
- **Estimación a fin de mes** por categoría:
  ```
  variable  = gastos únicos de la categoría en el mes
  ritmo     = variable / díasTranscurridos
  estimado  = Gastado + Pendiente + ritmo × díasRestantes
  ```
  Si quedan menos de 3 días transcurridos, se usa el promedio diario de los últimos 3 meses para evitar extrapolaciones locas al inicio del mes.
- **Estado:** 🟢 estimado ≤ 90 % · 🟡 90–100 % · 🔴 > 100 % del asignado.
- **Resumen:** total asignado vs. ingresos (avisar si se asigna más del 100 %), "sin asignar" = ingresos − total asignado.

### 5.6 Metas

- **Progreso** = Σ gastos de la categoría vinculada (ej. Ahorro) dentro de `[StartDate, EndDate]`.
- **Meses restantes** = meses completos desde hoy hasta `EndDate` (mínimo 1).
- **Aporte mensual necesario** = (Objetivo − Progreso) / meses restantes.
- **Proyección** = Progreso + promedio mensual de aportes × meses restantes.
- **Estado:** "En camino" si Proyección ≥ Objetivo, si no "Atrasada (faltan X)".
- `Period = Annual` → el usuario indica el año y se usa del 1 ene al 31 dic. `Monthly` → se reinicia cada mes.

### 5.7 Estimaciones de gasto

Para un rango `[desde, hasta]`:

```
Estimado = Recurrentes + Deudas + Variables
```
| Componente | Cálculo |
|---|---|
| Recurrentes | Σ ocurrencias de gastos recurrentes activos en el rango (`RecurrenceCalculator`). |
| Deudas | Cuotas simuladas (§5.4) en el rango, solo hasta saldar cada deuda. |
| Variables | Por categoría: promedio mensual de gastos únicos de los **últimos 3 meses completos** × meses del rango. Si no hay historial, se usa el monto del **Plan** de esa categoría (si existe). |

| Horizonte | Rango |
|---|---|
| Próximo mes | 1 al último día del mes siguiente |
| Resto del año | mañana → 31 dic (del mes actual solo se cuenta lo **pendiente** y el variable de los días restantes) |
| Próximo año | 1 ene → 31 dic del año siguiente |

Cada estimación también muestra **ingresos proyectados** del mismo rango y el **balance estimado** (ingresos − gastos).

---

## 6. Rutas y pantallas

| Ruta | Pantalla | Contenido |
|---|---|---|
| `/` | Inicio | Gráfico circular del mes, 3 tarjetas (Disponible, Gastado, Comprometido), próximos pagos (7 días), resumen del plan y metas. |
| `/ingresos` | Ingresos | Pestañas: **Fuentes recurrentes** / **Ingresos únicos**. CRUD y total mensual. |
| `/gastos` · `/gastos/{year:int}/{month:int}` | Gastos | Navegador de mes, botón 📅 con selector año/mes, lista del mes y totales. |
| `/deudas` | Deudas | Tarjetas con saldo, % pagado, cuota y fecha estimada de saldo. |
| `/deudas/{id:int}` | Detalle de deuda | Datos, historial de pagos, botón "Registrar pago" y tabla de amortización proyectada. |
| `/plan` | Plan | Asignaciones por categoría, barras de progreso, disponible por categoría y estimación de fin de mes. |
| `/metas` | Metas | Lista de metas con progreso, aporte mensual necesario y estado. |
| `/estimaciones` | Estimaciones | Tres tarjetas (próximo mes, resto del año, próximo año) con el desglose. |
| `/configuracion` | Configuración | Moneda base, tasas de cambio y categorías. |
| `/Account/*` | Identity | Registro, login, logout y perfil. |

Todas las rutas (excepto `/Account/*`) llevan `[Authorize]`.

---

## 7. Fases de desarrollo

Cada fase termina con la app **compilando y ejecutable**. Los criterios de aceptación indican cuándo se considera terminada.

---

### Fase 0 — Solución y estructura

**Objetivo:** crear la solución con la estructura de carpetas y referencias definida.

**Tareas**
1. Crear la solución y los proyectos:
   ```bash
   dotnet new sln -n ExpenseTracker            # en .NET 10 genera .slnx
   dotnet new classlib -n ExpenseTracker.Core.Domain                 -o src/Core/ExpenseTracker.Core.Domain
   dotnet new classlib -n ExpenseTracker.Core.Application            -o src/Core/ExpenseTracker.Core.Application
   dotnet new classlib -n ExpenseTracker.Infrastructure.Persistence  -o src/Infrastructure/ExpenseTracker.Infrastructure.Persistence
   dotnet new classlib -n ExpenseTracker.Infrastructure.Identity     -o src/Infrastructure/ExpenseTracker.Infrastructure.Identity
   dotnet new blazor   -n ExpenseTracker.Web --interactivity Server --auth Individual --all-interactive \
                       -o src/Presentation/ExpenseTracker.Web
   ```
2. Agregarlos a la solución dentro de sus carpetas de solución:
   ```bash
   dotnet sln add src/Core/ExpenseTracker.Core.Domain                --solution-folder Source/Core
   dotnet sln add src/Core/ExpenseTracker.Core.Application           --solution-folder Source/Core
   dotnet sln add src/Infrastructure/ExpenseTracker.Infrastructure.Persistence --solution-folder Source/Infrastructure
   dotnet sln add src/Infrastructure/ExpenseTracker.Infrastructure.Identity    --solution-folder Source/Infrastructure
   dotnet sln add src/Presentation/ExpenseTracker.Web                --solution-folder Source/Presentation
   ```
3. Agregar las referencias de proyecto según §2.2.
4. Crear `Directory.Build.props` en la raíz:
   ```xml
   <Project>
     <PropertyGroup>
       <TargetFramework>net10.0</TargetFramework>
       <Nullable>enable</Nullable>
       <ImplicitUsings>enable</ImplicitUsings>
       <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
     </PropertyGroup>
   </Project>
   ```
5. Borrar los `Class1.cs` y las páginas de ejemplo de la plantilla (`Counter`, `Weather`).
6. Agregar `.gitignore` (`dotnet new gitignore`), `README.md` y `.editorconfig`.
7. Inicializar git y hacer el primer commit.

**Criterios de aceptación**
- [ ] `dotnet build` compila sin errores.
- [ ] La solución muestra las carpetas `Source/Core`, `Source/Infrastructure` y `Source/Presentation`.
- [ ] Domain no referencia a ningún proyecto y Application solo a Domain.

---

### Fase 1 — Dominio (`ExpenseTracker.Core.Domain`)

**Objetivo:** modelar las entidades, los enums y la lógica pura.

**Tareas**
1. `Common/BaseEntity.cs` y `Common/UserOwnedEntity.cs`.
2. Enums de §4.1.
3. Entidades de §4.2 como **clases** con propiedades `{ get; set; }` y colecciones inicializadas:
   ```csharp
   public class Debt : UserOwnedEntity
   {
       public string Name { get; set; } = string.Empty;
       public DebtType DebtType { get; set; }
       public decimal OriginalAmount { get; set; }
       public decimal AnnualInterestRate { get; set; }
       public decimal InitialPaidAmount { get; set; }
       public decimal MonthlyPayment { get; set; }
       public int PaymentDay { get; set; }
       public DateOnly StartDate { get; set; }
       public int? TermMonths { get; set; }
       public Currency Currency { get; set; }
       public bool IsClosed { get; set; }
       public List<DebtPayment> Payments { get; set; } = new();

       public decimal GetRemainingBalance()
           => OriginalAmount - InitialPaidAmount - Payments.Sum(p => p.PrincipalPortion);
   }
   ```
4. `Helpers/RecurrenceCalculator.cs` (§4.3), con manejo de fin de mes.
5. (Opcional) Crear `ExpenseTracker.Tests` y probar `RecurrenceCalculator`:
   - Mensual que empieza el 31 → 28/29 feb, 31 mar.
   - Anual que empieza el 15/03/2025 → aparece en marzo 2026 y no en abril.
   - Quincenal en un mes con 3 ocurrencias.
   - `EndDate` corta las ocurrencias.

**Criterios de aceptación**
- [ ] No hay `record` en el proyecto.
- [ ] Domain no tiene paquetes NuGet.
- [ ] Las pruebas de recurrencia pasan (si se creó el proyecto de pruebas).

---

### Fase 2 — Persistencia (`ExpenseTracker.Infrastructure.Persistence`)

**Objetivo:** guardar las entidades en SQL Server, aisladas por usuario.

**Tareas**
1. Interfaces en **Application** (se crean ahora porque Persistence las implementa):
   ```csharp
   public interface ICurrentUserService
   {
       string UserId { get; }
   }

   public interface IRepository<T> where T : UserOwnedEntity
   {
       Task<T?> GetByIdAsync(int id);
       Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null);
       Task AddAsync(T entity);
       Task UpdateAsync(T entity);
       Task DeleteAsync(T entity);
   }

   public interface IExpenseRepository : IRepository<Expense>
   {
       Task<List<Expense>> GetOneTimeByRangeAsync(DateOnly from, DateOnly to);
       Task<List<Expense>> GetActiveRecurringAsync(DateOnly from, DateOnly to);
   }

   public interface IDebtRepository : IRepository<Debt>
   {
       Task<Debt?> GetWithPaymentsAsync(int id);
       Task<List<Debt>> GetOpenWithPaymentsAsync();
       Task AddPaymentAsync(DebtPayment payment);
       Task DeletePaymentAsync(int paymentId);
   }
   ```
2. `AppDbContext`:
   - Un `DbSet` por entidad.
   - Recibe `ICurrentUserService` y aplica el filtro global `e.UserId == _currentUser.UserId` a todas las `UserOwnedEntity`.
   - En `SaveChangesAsync`: asigna `UserId`, `CreatedAtUtc` y `UpdatedAtUtc` automáticamente.
3. Configuraciones (`IEntityTypeConfiguration<T>`):
   - Montos `HasPrecision(18, 2)`; `RateToBase` `HasPrecision(18, 6)`.
   - Enums `HasConversion<string>()` (legibles en BD).
   - Longitudes máximas: `Name` 100, `Description` 200 y `Notes` 500.
   - Índices: `(UserId, Date)` en `Expense`, `OneTimeIncome` y `DebtPayment`; `(UserId, Name)` único en `Category`.
   - `DebtPayment` → `Debt` en cascada. `Expense` → `Category` en restrict.
4. Repositorios con `IDbContextFactory<AppDbContext>` (D4).
5. `DependencyInjection.AddPersistence(IServiceCollection, IConfiguration)`.
6. Seed de categorías por defecto: un método `SeedDefaultsForUserAsync(userId)` que se llama al registrar un usuario.
7. Migración inicial:
   ```bash
   dotnet ef migrations add InitialCreate -p src/Infrastructure/ExpenseTracker.Infrastructure.Persistence \
       -s src/Presentation/ExpenseTracker.Web -c AppDbContext -o Migrations
   ```

**Criterios de aceptación**
- [ ] `dotnet ef database update` crea las tablas en `dbo`.
- [ ] Una consulta sin filtro manual solo devuelve datos del usuario actual (probar con dos usuarios).

---

### Fase 3 — Identity y autenticación

**Objetivo:** mover Identity de la plantilla a `Infrastructure.Identity` y proteger la app.

**Tareas**
1. Crear `ApplicationUser : IdentityUser` con `FullName` (opcional).
2. `IdentityContext : IdentityDbContext<ApplicationUser>` con `HasDefaultSchema("identity")`.
3. `AddIdentityInfrastructure(IServiceCollection, IConfiguration)`:
   ```csharp
   services.AddDbContext<IdentityContext>(o => o.UseSqlServer(cs));
   services.AddIdentityCore<ApplicationUser>(o =>
       {
           o.SignIn.RequireConfirmedAccount = false;   // simple para empezar
           o.Password.RequiredLength = 8;
       })
       .AddEntityFrameworkStores<IdentityContext>()
       .AddSignInManager()
       .AddDefaultTokenProviders();
   ```
4. En **Web**:
   - Borrar `Data/ApplicationDbContext.cs` y `Data/ApplicationUser.cs` de la plantilla y actualizar los `using` de `Components/Account/*` para usar `Infrastructure.Identity`.
   - Mantener `AddAuthentication().AddIdentityCookies()`, `AddCascadingAuthenticationState()` e `IdentityRevalidatingAuthenticationStateProvider`.
   - `CurrentUserService` que obtiene el `UserId` del `ClaimsPrincipal` (vía `AuthenticationStateProvider`/`IHttpContextAccessor`).
   - `@attribute [Authorize]` en `_Imports.razor` de `Pages` y `[AllowAnonymous]` en las páginas de cuenta.
   - Traducir los textos principales de Login y Registro al español.
5. Después del registro, crear `UserSettings` (DOP, `es-DO`) y las categorías por defecto.
6. Migración de Identity:
   ```bash
   dotnet ef migrations add InitialIdentity -p src/Infrastructure/ExpenseTracker.Infrastructure.Identity \
       -s src/Presentation/ExpenseTracker.Web -c IdentityContext -o Migrations
   ```
7. `Program.cs`: llamar `AddApplication()`, `AddPersistence()` y `AddIdentityInfrastructure()`, y aplicar migraciones al iniciar en desarrollo.

**Criterios de aceptación**
- [ ] Registro, login y logout funcionan.
- [ ] Sin sesión, cualquier página redirige a `/Account/Login`.
- [ ] Un usuario nuevo tiene sus categorías por defecto y su moneda base DOP.

---

### Fase 4 — Base de la capa Application

**Objetivo:** dejar listos los patrones comunes que usarán todos los módulos.

**Tareas**
1. `IDateProvider` con `DateOnly Today` (implementado en Web) para poder probar los cálculos con fechas fijas.
2. Convención de DTOs (clases):
   - `XxxDto` para lectura y `XxxInput` para crear o editar (con DataAnnotations y mensajes en español).
   - Mapeo manual con métodos `static` privados en cada servicio (`ToDto(entity)`).
3. `DependencyInjection.AddApplication()` registra todos los servicios como `Scoped`.
4. Componentes compartidos en Web:
   - `MoneyText.razor` formatea con `es-DO` y el símbolo de la moneda (`RD$ 4,885.40`, `US$ 10.00`).
   - `ConfirmDialog.razor` (modal Bootstrap).
   - `ErrorAlert.razor` muestra el mensaje de la excepción de negocio.
5. Configurar `CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("es-DO")`.
6. Menú lateral: Inicio, Ingresos, Gastos, Deudas, Plan, Metas, Estimaciones y Configuración.

**Criterios de aceptación**
- [ ] El menú navega a páginas vacías protegidas.
- [ ] `MoneyText` muestra correctamente montos en DOP y USD.

---

### Fase 5 — Categorías y monedas

**Objetivo:** configurar lo que necesitan los demás módulos.

**Tareas**
1. `CategoryService`: listar, crear, editar y desactivar. No permitir borrar una categoría con gastos; en ese caso se desactiva.
2. `CurrencyService`:
   - `GetBaseCurrencyAsync()`.
   - `ConvertToBaseAsync(decimal amount, Currency currency)`. Si `currency == base` devuelve el mismo monto; si no existe tasa lanza un error: *"Configure la tasa de USD en Configuración"*.
   - CRUD de `ExchangeRate`.
3. Página `/configuracion` con tres secciones: moneda base, tasas (tabla editable) y categorías (nombre, color e indicador de ahorro).

**Criterios de aceptación**
- [ ] Se puede definir USD = 60.50 DOP y la conversión de 10 USD devuelve 605.00.
- [ ] Las categorías se pueden crear, renombrar, colorear y desactivar.

---

### Fase 6 — Ingresos

**Objetivo:** registrar ingresos recurrentes y únicos.

**Tareas**
1. `IncomeService`:
   - CRUD de `IncomeSource` (nombre, monto, moneda, frecuencia, inicio, fin opcional).
   - CRUD de `OneTimeIncome` (descripción, monto, moneda, fecha). Calcula `AmountInBase` al guardar.
   - `GetMonthlyIncomeAsync(int year, int month)` devuelve el total según §5.1 con desglose.
   - `GetRecurringMonthlyTotalAsync()` para el Plan (porcentajes).
2. Página `/ingresos`:
   - **Fuentes recurrentes:** tabla Nombre | Monto | Frecuencia | Estado | acciones, y total mensual equivalente al pie.
   - **Ingresos únicos:** lista del mes con navegador de mes (reutilizar `MonthNavigator`) y numeración visible (`Ingreso #112`).
   - Formularios en modal.

**Ejemplo esperado:** Sueldo trabajo 50,000 + Sueldo universidad 10,000 + Bono 5,000 → ingresos del mes **65,000 DOP**.

**Criterios de aceptación**
- [ ] El total del mes coincide con el ejemplo.
- [ ] Un ingreso con `EndDate` pasado no cuenta en meses posteriores.

---

### Fase 7 — Gastos

**Objetivo:** registrar gastos únicos y recurrentes y navegar por meses y años.

**Tareas**
1. `ExpenseService`:
   - `CreateAsync(ExpenseInput)`: valida que un recurrente tenga frecuencia, convierte la moneda y guarda.
   - `UpdateAsync`, `DeleteAsync`. Para un recurrente, "eliminar" ofrece **finalizar** (pone `EndDate = hoy`) y conserva el historial, o **eliminar** por completo.
   - `GetMonthAsync(int year, int month)` usa `MonthlyLedgerService` y devuelve un `MonthExpensesDto` con items, total gastado, total pendiente y total por categoría.
2. `MonthlyLedgerService` (§5.2). En esta fase incluye gastos; las deudas se agregan en la Fase 8.
3. Componentes:
   - **`MonthNavigator.razor`:** `‹ Noviembre 2026 ›` más un botón 📅. Los parámetros `Year`, `Month` y `OnChange`. El botón "›" se deshabilita más allá del mes siguiente al actual (configurable).
   - **`YearMonthPicker.razor`:** modal con `‹ 2026 ›` y una cuadrícula de 12 meses (4×3). Marca el mes actual y el seleccionado; opcionalmente muestra el total gastado de cada mes debajo del nombre. Al hacer clic en un mes se cierra y navega.
4. Página `/gastos/{year}/{month}` (`/gastos` redirige al mes actual):
   - Encabezado: navegador, total gastado, total pendiente y botón **"+ Nuevo gasto"**.
   - Lista ordenada por fecha. Cada item muestra:
     ```
     Suscripción Netflix          [Mensual]        US$ 10.00  (RD$ 605.00)
     Suscripciones · 15 nov · Pagado
     ```
     El badge de frecuencia aparece solo en recurrentes, el estado *Pendiente* va en gris y se muestra el monto original con su conversión cuando la moneda no es la base.
   - Filtro por categoría y por tipo (Todos / Únicos / Recurrentes).
   - Clic en un item abre la edición.
5. `ExpenseForm.razor`: nombre, monto, moneda, categoría, fecha, el interruptor "Es recurrente" que despliega frecuencia y fecha fin, y notas.

**Datos de prueba (enunciado):** Supermercado 4,885.40 · Netflix 10 USD mensual · Supermercado 700 · Recibo luz 1,300 mensual · Ticket cine 381.20 · Combo palomitas 400 · Seguro 20,000 anual.

**Criterios de aceptación**
- [ ] Con los datos de prueba, el mes muestra 7 items y los totales correctos.
- [ ] El seguro anual aparece solo en su mes de aniversario.
- [ ] Netflix aparece todos los meses desde su inicio.
- [ ] Las flechas y el selector de calendario permiten ir a cualquier mes de años anteriores.
- [ ] La URL refleja el mes (se puede compartir o recargar).

---

### Fase 8 — Deudas

**Objetivo:** registrar compromisos, sus pagos y su impacto en el mes.

**Tareas**
1. `DebtService`:
   - CRUD de `Debt`. Si se da `TermMonths` sin `MonthlyPayment`, calcula la cuota (§5.4).
   - `RegisterPaymentAsync(debtId, amount, date)` divide el pago en interés y capital y valida que no supere saldo + interés. Si el saldo llega a 0, marca `IsClosed`.
   - `DeletePaymentAsync` (recalcula el saldo).
   - `GetSummaryAsync()` devuelve por deuda el saldo, el % pagado, la cuota, el próximo pago y la fecha estimada de saldo.
   - `GetAmortizationAsync(debtId)` devuelve la tabla proyectada mes a mes (fecha, cuota, interés, capital, saldo).
2. Integrar con `MonthlyLedgerService`: pagos registrados = Gastado; cuota del mes sin pago = Pendiente (categoría virtual "Deudas").
3. Páginas:
   - `/deudas`: tarjetas con nombre, tipo, barra de progreso (pagado / original), saldo, tasa, cuota y "Saldo estimado: mar 2029". Total de deuda arriba.
   - `/deudas/{id}`: datos editables, historial de pagos (fecha, monto, interés, capital) y tabla de amortización colapsable.
   - Modal **"Registrar pago"** con monto sugerido = cuota y fecha = hoy.
4. Los pagos de deuda aparecen en `/gastos` del mes correspondiente con un ícono distintivo y solo lectura (se editan desde la deuda).

**Ejemplo del enunciado:** Préstamo de 1,000,000 al 5 % con 500,000 pagados → saldo 500,000 y 50 % pagado. Un pago de 30,000 el 5/10/2023 → interés 2,083.33, capital 27,916.67 y saldo 472,083.33.

**Criterios de aceptación**
- [ ] Los cálculos del ejemplo coinciden.
- [ ] Si no hay pago este mes, la cuota aparece como Pendiente en Gastos (y luego en Inicio en gris).
- [ ] Al registrar el pago, pasa de Pendiente a Gastado.

---

### Fase 9 — Plan (presupuesto)

**Objetivo:** que el usuario defina cómo repartir su dinero y vea si lo está cumpliendo.

**Tareas**
1. `BudgetService`:
   - CRUD de `BudgetAllocation` (una por categoría como máximo).
   - `GetPlanStatusAsync(int year, int month)` devuelve un `PlanStatusDto` con:
     - Por categoría: asignado, gastado, pendiente, disponible, estimado a fin de mes, % y estado (§5.5).
     - Totales: ingresos, total asignado, sin asignar, gastado y estimado total.
     - Gastos en categorías **sin asignación**, agrupados como "Fuera del plan".
2. Página `/plan`:
   - Botón "Editar plan" abre una tabla editable: Categoría | Tipo (Monto fijo / % de ingreso) | Valor | Fuente de ingreso (si es %) | Equivale a.
   - Vista principal: una fila por categoría con una barra de progreso de 3 segmentos (gastado sólido, pendiente rayado, estimado como línea marcadora) y el texto *"Puedes gastar RD$ 3,214.60 más"* o *"Te pasarás por ~RD$ 1,200 al ritmo actual"*.
   - Resumen superior: "Asignado 83 % de tus ingresos" y "Estimado a fin de mes: RD$ 48,300".
   - Navegador de mes para ver el cumplimiento de meses pasados.

**Ejemplo del enunciado:** Comida 10,000 · Compras/Salidas 5,000 · Ahorro 10 % del salario (50,000) = 5,000.

**Criterios de aceptación**
- [ ] El porcentaje se resuelve contra la fuente indicada (Ahorro = 5,000).
- [ ] Con 5,585.40 gastados en Comida el día 10, la estimación de fin de mes ≈ 16,756 → estado 🔴.
- [ ] Aviso si el total asignado supera los ingresos.

---

### Fase 10 — Metas

**Objetivo:** configurar metas financieras y seguir su progreso.

**Tareas**
1. `GoalService`:
   - CRUD de `Goal`.
   - `GetGoalsStatusAsync()` devuelve por meta el progreso, el %, lo que falta, los meses restantes, el aporte mensual necesario, la proyección y el estado (§5.6).
2. Página `/metas`:
   - Tarjetas con barra de progreso, *"RD$ 45,000 de RD$ 200,000 (22.5 %)"*, *"Necesitas RD$ 15,500/mes"* y el badge En camino / Atrasada.
   - Formulario: nombre, monto objetivo, periodo (Mensual / Anual / Personalizado), año o fechas y categoría vinculada (por defecto la de ahorro).
3. Sugerencia: si la meta requiere más por mes que lo asignado a Ahorro en el Plan, mostrar el aviso *"Tu plan asigna RD$ 5,000/mes a Ahorro; la meta necesita RD$ 15,500/mes"*.

**Ejemplo del enunciado:** "Ahorro de 200,000 anualmente" → `Period = Annual`, objetivo 200,000, categoría Ahorro.

**Criterios de aceptación**
- [ ] El progreso refleja los gastos en la categoría Ahorro del año.
- [ ] El aporte mensual necesario se recalcula cada mes.

---

### Fase 11 — Estimaciones

**Objetivo:** proyectar gastos a mediano y largo plazo.

**Tareas**
1. `ForecastService`:
   - `EstimateAsync(DateOnly from, DateOnly to)` devuelve un `ForecastDto` con recurrentes, deudas, variables por categoría, total, ingresos proyectados y balance (§5.7).
   - Atajos `GetNextMonthAsync()`, `GetRestOfYearAsync()` y `GetNextYearAsync()`.
   - Simulación de deudas que reutiliza la lógica de amortización de `DebtService` (extraerla a un método compartido para no duplicarla).
2. Página `/estimaciones`:
   - Tres tarjetas: **Próximo mes**, **Resto de 2026** y **2027**. Cada una con gasto estimado, ingreso estimado y balance (verde o rojo).
   - Al expandir cada tarjeta: desglose Recurrentes / Deudas / Variables (por categoría) y, para el año, una tabla mes a mes.
   - Nota visible: *"Variables estimadas con el promedio de los últimos 3 meses"* (o *"con tu Plan"* si no hay historial).

**Criterios de aceptación**
- [ ] El seguro anual de 20,000 aparece solo en el mes que corresponde dentro de "Próximo año".
- [ ] Una deuda que se salda a mitad de año deja de sumar cuotas después.
- [ ] Sin historial, los variables se toman del Plan.
- [ ] "Resto del año" no cuenta gastos ya realizados del mes actual.

---

### Fase 12 — Inicio (dashboard)

**Objetivo:** pantalla de inicio con el gráfico circular y el resumen.

**Tareas**
1. `DashboardService.GetAsync()` devuelve un `DashboardDto` con:
   - `Income`, `Spent`, `Committed`, `Available`, `IsDeficit` (§5.3).
   - Próximos pagos de los siguientes 7 días (recurrentes y cuotas).
   - Top 3 categorías del plan más cercanas a exceder su asignación.
   - Resumen de metas (nombre y %).
   - Gasto estimado del próximo mes (de `ForecastService`).
2. `PieChart.razor` en SVG:
   - Parámetro `List<PieSlice>`, donde `PieSlice` es una clase con `Label`, `Value` y `Color`.
   - Dibuja arcos con `stroke-dasharray` sobre `<circle>` (estilo dona). Es simple y no requiere trigonometría de paths.
   - Colores: verde `#2E9E5B` (disponible), rojo `#D64545` (gastado) y gris `#9AA0A6` (comprometido).
   - Centro: "Disponible RD$ 21,300". Leyenda con monto y % de cada segmento.
   - `title` en cada segmento para el tooltip.
3. Página `/`:
   - Gráfico circular arriba, con 3 tarjetas al lado (o debajo en móvil).
   - Alerta de déficit si aplica.
   - Secciones "Próximos pagos", "Plan" y "Metas" con enlaces a sus páginas.
   - Estado vacío para usuarios nuevos con 3 pasos: *1. Configura tus ingresos → 2. Registra tus gastos → 3. Arma tu plan*.

**Criterios de aceptación**
- [ ] Con los datos de ejemplo, verde + rojo + gris = ingresos del mes.
- [ ] Registrar un gasto o un pago de deuda y volver a Inicio actualiza el gráfico.
- [ ] Se ve bien en móvil (≥ 360 px).

---

### Fase 13 — Pulido, pruebas y despliegue

**Objetivo:** dejar la app estable y lista para usar.

**Tareas**
1. **Validación y UX**
   - Mensajes de validación en español en todos los formularios.
   - Estados de carga (spinner) y estados vacíos en cada lista.
   - Confirmación antes de eliminar.
   - Toasts de éxito ("Gasto guardado").
2. **Rendimiento**
   - `AsNoTracking()` en las consultas de lectura.
   - Evitar N+1: cargar `DebtPayments` con `Include` solo donde se necesita.
   - Revisar que `MonthlyLedgerService` haga como máximo unas 4 consultas por mes.
3. **Pruebas (proyecto `ExpenseTracker.Tests`, opcional pero recomendado)**
   - Domain: `RecurrenceCalculator` y `Debt.GetRemainingBalance`.
   - Application (con repositorios falsos en memoria e `IDateProvider` fijo): `MonthlyLedgerService`, `BudgetService` (estimación y estados), `GoalService`, `ForecastService` (deuda que se salda y anual) y `DashboardService` (déficit).
   - Usar los ejemplos del enunciado como casos de prueba.
4. **Seguridad**
   - Verificar que no se pueda acceder a datos de otro usuario cambiando el `id` en la URL (el filtro global devuelve `null` → 404).
   - Antiforgery activo (por defecto en Blazor Web App).
   - Connection strings en `appsettings.Development.json` o *user-secrets*, no en el repositorio.
5. **Despliegue**
   - `appsettings.Production.json` y variables de entorno.
   - Aplicar migraciones con un *bundle* (`dotnet ef migrations bundle`) o al iniciar con una bandera.
   - Opciones: Azure App Service + Azure SQL, o un contenedor Docker (`dotnet publish /t:PublishContainer`).
6. **Documentación**
   - `README.md` con requisitos, cómo correr, migraciones y estructura.

**Criterios de aceptación**
- [ ] Todas las pruebas pasan.
- [ ] Un recorrido completo (registro → ingresos → gastos → deuda → plan → meta → estimación → inicio) funciona sin errores.
- [ ] Un segundo usuario no ve datos del primero.

---

## 8. Checklist general

| Fase | Entregable | Depende de |
|---|---|---|
| 0 | Solución y estructura | — |
| 1 | Entidades, enums y recurrencias | 0 |
| 2 | `AppDbContext`, repositorios y migración | 1 |
| 3 | Identity en Infrastructure y login | 2 |
| 4 | Convenciones Application y componentes base | 3 |
| 5 | Categorías y monedas | 4 |
| 6 | Ingresos | 5 |
| 7 | Gastos, navegador de mes y selector de calendario | 5 |
| 8 | Deudas y pagos | 7 |
| 9 | Plan | 6, 7, 8 |
| 10 | Metas | 7, 9 |
| 11 | Estimaciones | 6, 7, 8, 9 |
| 12 | Inicio | 6, 7, 8, 9, 10, 11 |
| 13 | Pulido, pruebas y despliegue | todas |

**Orden sugerido para un MVP rápido:** 0 → 1 → 2 → 3 → 4 → 5 → 6 → 7 → 12 (versión básica del gráfico) → 8 → 9 → 10 → 11 → 13.

---

## 9. Mejoras futuras (fuera de alcance)

- Arrastre de saldo entre meses (balance acumulado / cuenta de ahorro real).
- Tasas de cambio automáticas desde una API.
- Confirmación manual de recurrentes ("marcar como pagado") en lugar de usar la fecha.
- Importar gastos desde CSV o estados de cuenta bancarios.
- Notificaciones por correo de pagos próximos.
- Exportar reportes a Excel o PDF.
- Gráficos históricos (gasto por mes y por categoría).
- Interés compuesto diario y tarjetas de crédito con corte y pago mínimo.
- PWA / modo offline.
