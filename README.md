# Expense Tracker

Aplicación para llevar el historial de ingresos, gastos y deudas, comparar el gasto contra un plan mensual, seguir metas financieras y estimar los gastos del próximo mes, del resto del año y del próximo año.

**Stack:** C# · .NET 10 · Blazor Web App (Interactive Server) · Onion Architecture · ASP.NET Core Identity · EF Core 10 · SQL Server

## Funcionalidades

| Pantalla | Qué hace |
|---|---|
| Inicio | Gráfico del mes (disponible / gastado / comprometido), próximos pagos, plan, metas y estimación del próximo mes. |
| Ingresos | Fuentes recurrentes e ingresos únicos. |
| Gastos | Gastos únicos y recurrentes por mes, con navegación por mes y año. |
| Deudas | Saldo, pagos divididos en interés y capital, y tabla de amortización. |
| Plan | Asignación por categoría (monto fijo o % del ingreso) y estimado a fin de mes. |
| Metas | Progreso, aporte mensual necesario y proyección. |
| Estimaciones | Próximo mes, resto del año y próximo año. |
| Configuración | Moneda base, tasas de cambio y categorías. |

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server. En desarrollo basta **LocalDB** (viene con Visual Studio) o un SQL Server local.
- Opcional: [Docker](https://www.docker.com/) para correr todo en contenedores.

## Cómo correr (desarrollo)

```bash
dotnet restore
dotnet build
dotnet run --project src/Presentation/ExpenseTracker.Web
```

La app usa LocalDB por defecto (`appsettings.Development.json`, clave `ConnectionStrings:ExpenseTracker`). Para usar otro servidor, cambia esa cadena o guárdala en *user-secrets*, que no entran al repositorio:

```bash
dotnet user-secrets set "ConnectionStrings:ExpenseTracker" "Server=...;Database=ExpenseTracker;..." --project src/Presentation/ExpenseTracker.Web
```

En desarrollo las migraciones se aplican solas al iniciar. Regístrate en `/Account/Register`: se crean tu configuración (moneda base DOP) y las categorías por defecto.

## Pruebas

```bash
dotnet test
```

Cubren el dominio (recurrencias, saldo de deuda), los servicios de Application con repositorios falsos en memoria y una fecha fija (ingresos, gastos, deudas, plan, metas, estimaciones, inicio), el aislamiento entre usuarios, el número de consultas por mes del libro de movimientos y los componentes y formularios de la interfaz (con bUnit).

### Pruebas con navegador (E2E)

`tests/ExpenseTracker.E2E` usa Playwright para .NET: arranca la app real en un puerto libre contra su propia base de datos (`ExpenseTrackerE2E`, que se borra al terminar) y abre el Chrome (o Edge) instalado, sin descargar navegadores. Prueba flujos reales (abrir formularios, validar, guardar, editar, eliminar) y guarda capturas en `tests/ExpenseTracker.E2E/artifacts/` (ignorada por git).

Solo corren con `E2E=1`; sin esa variable se omiten, así que `dotnet test` y el CI no las ejecutan (necesitan Chrome y SQL Server LocalDB).

```bash
dotnet build
E2E=1 dotnet test tests/ExpenseTracker.E2E                 # en PowerShell: $env:E2E=1; dotnet test tests/ExpenseTracker.E2E
E2E=1 E2E_HEADED=1 dotnet test tests/ExpenseTracker.E2E    # para ver el navegador
```

## Migraciones

Hay dos contextos, cada uno con su historial de migraciones. Se generan con el proyecto Web como proyecto de inicio:

```bash
# Datos de la aplicación (esquema dbo)
dotnet ef migrations add NombreMigracion -p src/Infrastructure/ExpenseTracker.Infrastructure.Persistence -s src/Presentation/ExpenseTracker.Web -c AppDbContext -o Migrations

# Identity (esquema identity)
dotnet ef migrations add NombreMigracion -p src/Infrastructure/ExpenseTracker.Infrastructure.Identity -s src/Presentation/ExpenseTracker.Web -c IdentityContext -o Migrations
```

`dotnet ef` está en `dotnet-tools.json`: ejecuta `dotnet tool restore` una vez.

Fuera de desarrollo las migraciones se aplican al iniciar solo si `ApplyMigrationsOnStartup` es `true`.

## Docker

```bash
cp .env.example .env        # y edita DB_PASSWORD
docker compose up --build
```

- La app queda en <http://localhost:8080> y la salud en <http://localhost:8080/health> (responde `Healthy` cuando la base de datos está disponible).
- `docker-compose.yml` levanta la app y SQL Server 2022, aplica las migraciones al iniciar y guarda los datos y las llaves de las cookies en volúmenes.
- `.env` está en `.gitignore`; nunca subas la contraseña.

## CI/CD (GitHub Actions)

- **CI** (`.github/workflows/ci.yml`): en cada `push` y `pull_request` a `main` restaura, compila y ejecuta las pruebas.
- **CD** (`.github/workflows/cd.yml`): cuando el CI pasa en `main`, o al crear un tag `v*`, construye la imagen y la publica en `ghcr.io/<usuario>/<repo>` con las etiquetas `latest` (solo desde `main`), el SHA del commit y la versión del tag.
- **Despliegue opcional:** define la variable de repositorio `DEPLOY_ENABLED=true` y los secretos `DEPLOY_HOST`, `DEPLOY_USER` y `DEPLOY_SSH_KEY` para que `cd.yml` ejecute `docker compose pull && docker compose up -d` por SSH.
- **Proteger `main`:** en *Settings → Branches → Add branch protection rule* para `main`, activa *Require status checks to pass before merging* y elige el check `build-test`. Así un PR con una prueba fallida no se puede fusionar.

## Estructura

```
ExpenseTracker.slnx
├── src/
│   ├── Core/
│   │   ├── ExpenseTracker.Core.Domain          Entidades, enums y recurrencias (sin dependencias)
│   │   └── ExpenseTracker.Core.Application     Servicios, DTOs e interfaces
│   ├── Infrastructure/
│   │   ├── ExpenseTracker.Infrastructure.Persistence   AppDbContext, repositorios, migraciones
│   │   └── ExpenseTracker.Infrastructure.Identity      Usuarios y autenticación
│   └── Presentation/
│       └── ExpenseTracker.Web                  Blazor, componentes y Program.cs
├── tests/ExpenseTracker.Tests                  xUnit
├── Dockerfile · docker-compose.yml
└── Agent/                                      Plan de desarrollo
```

Las dependencias apuntan siempre hacia el centro: Web → Application/Persistence/Identity → Domain.

## Seguridad

- Cada dato lleva el `UserId` de su dueño y un filtro global de EF lo aplica a todas las consultas: pedir por URL el `id` de otro usuario no devuelve nada.
- La protección antiforgery de Blazor está activa.
- Las cadenas de conexión de producción van en variables de entorno (`ConnectionStrings__ExpenseTracker`), nunca en el repositorio.

## Plan de desarrollo

El plan por fases está en [Agent/Plan.md](Agent/Plan.md) y [Agent/fase13_fase14.md](Agent/fase13_fase14.md).
