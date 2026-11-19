# TicketFlow

IT support ticket system for a company with multiple branches.
*Sistema de tickets de soporte TI para una empresa con varias sucursales.*

> 🚧 Work in progress / En construcción.

[English](#english) · [Español](#español)

## Stack

- **Backend:** .NET 8, ASP.NET Core Web API, Entity Framework Core 8
- **Database:** SQL Server 2022 (Docker)
- **Auth:** JWT + BCrypt
- **Frontend:** Angular 17+ (standalone components, signals)
- **Tests:** xUnit + FluentAssertions, Angular test runner
- **DevOps:** Docker Compose, GitHub Actions

## Structure / Estructura

```
backend/
  src/TicketFlow.Domain          Entities and enums
  src/TicketFlow.Application     DTOs, services, interfaces
  src/TicketFlow.Infrastructure  EF Core DbContext, migrations, demo seed
  src/TicketFlow.Api             ASP.NET Core host
  tests/TicketFlow.Tests         xUnit tests
frontend/
  ticketflow-web                 Angular app
```

---

## English

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer (projects target `net8.0`)
- [Docker](https://www.docker.com/) (for SQL Server)

### 1. Start SQL Server

```bash
docker run -d --name ticketflow-sql \
  -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=<YourStrong!Passw0rd>" \
  -p 1433:1433 -v ticketflow-sql-data:/var/opt/mssql \
  mcr.microsoft.com/mssql/server:2022-latest
```

### 2. Configure the connection string

The connection string is a secret and is **not** stored in the repo. For local development use
[user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) (stored outside the project folder):

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=TicketFlow;User Id=sa;Password=<YourStrong!Passw0rd>;TrustServerCertificate=True" \
  --project src/TicketFlow.Api
```

Alternatively, set the environment variable `ConnectionStrings__Default`.

### 3. Apply migrations and seed demo data

**Option A: run the API.** In the `Development` environment it applies pending migrations and inserts
the demo data automatically (only if the database is empty):

```bash
dotnet run --project src/TicketFlow.Api
```

**Option B: EF Core CLI.** `dotnet-ef` is pinned as a local tool in `dotnet-tools.json`:

```bash
dotnet tool restore
dotnet ef database update --project src/TicketFlow.Infrastructure --startup-project src/TicketFlow.Api
```

This creates the schema only; the demo data is inserted the next time the API starts.

### Working with migrations

```bash
# Create a new migration after changing entities or configurations
dotnet ef migrations add <Name> --project src/TicketFlow.Infrastructure \
  --startup-project src/TicketFlow.Api --output-dir Persistence/Migrations

# Remove the last migration (only if it has not been applied)
dotnet ef migrations remove --project src/TicketFlow.Infrastructure --startup-project src/TicketFlow.Api

# Start over with an empty database (deletes all data)
dotnet ef database drop --project src/TicketFlow.Infrastructure --startup-project src/TicketFlow.Api
```

### Demo data

The seed creates 3 branches, 5 categories, 30 tickets (with comments and history) and these users.
All data is fictional. Password for every account: **`Demo123!`**

| Role | E-mail |
|---|---|
| Admin | `admin@ticketflow.demo` |
| Technician | `carlos.rivera@ticketflow.demo` |
| Technician | `ana.torres@ticketflow.demo` |
| Requester | `jorge.castillo@ticketflow.demo` |
| Requester | `maria.lopez@ticketflow.demo` |
| Requester | `pedro.sanchez@ticketflow.demo` |

### Tests

```bash
cd backend
dotnet test
```

Database tests use in-memory SQLite, so they don't need SQL Server.

---

## Español

### Requisitos

- [.NET SDK](https://dotnet.microsoft.com/download) 8 o superior (los proyectos apuntan a `net8.0`)
- [Docker](https://www.docker.com/) (para SQL Server)

### 1. Levantar SQL Server

Usa el mismo comando `docker run` de la sección en inglés, cambiando `<YourStrong!Passw0rd>` por tu contraseña.

### 2. Configurar la cadena de conexión

La cadena de conexión es un secreto y **no** se guarda en el repo. En desarrollo usa *user secrets*:

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=TicketFlow;User Id=sa;Password=<TuContraseña>;TrustServerCertificate=True" \
  --project src/TicketFlow.Api
```

También puedes usar la variable de entorno `ConnectionStrings__Default`.

### 3. Aplicar migraciones y datos demo

- **Opción A:** `dotnet run --project src/TicketFlow.Api`. En `Development` aplica las migraciones e inserta los datos demo automáticamente (solo si la base está vacía).
- **Opción B:** `dotnet tool restore` y luego
  `dotnet ef database update --project src/TicketFlow.Infrastructure --startup-project src/TicketFlow.Api`.
  Solo crea el esquema; los datos demo se insertan al iniciar la API.

Para crear una migración nueva:
`dotnet ef migrations add <Nombre> --project src/TicketFlow.Infrastructure --startup-project src/TicketFlow.Api --output-dir Persistence/Migrations`

### Datos demo

3 sucursales, 5 categorías, 30 tickets y los 6 usuarios de la tabla de arriba. Todos los datos son ficticios.
Contraseña de todas las cuentas: **`Demo123!`**

### Pruebas

`dotnet test` desde `backend/`. Las pruebas de base de datos usan SQLite en memoria, no necesitan SQL Server.
