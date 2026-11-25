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

### 2. Configure the secrets

The connection string and the JWT signing key are secrets and are **not** stored in the repo. For local
development use [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) (stored outside the project folder):

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=TicketFlow;User Id=sa;Password=<YourStrong!Passw0rd>;TrustServerCertificate=True" \
  --project src/TicketFlow.Api

# Any random string of at least 32 characters, e.g. generated with: openssl rand -base64 48
dotnet user-secrets set "Jwt:SigningKey" "<random-string-of-32+-characters>" --project src/TicketFlow.Api
```

Alternatively, use the environment variables `ConnectionStrings__Default` and `Jwt__SigningKey`.
The API refuses to start if the signing key is missing or shorter than 32 characters.

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

### Authentication and Swagger

With the API running in Development, open **http://localhost:5000/swagger**.

1. Call `POST /api/auth/login` with a demo user (see below) and copy the `accessToken`.
2. Click **Authorize** and paste the token (without the `Bearer ` prefix).
3. Endpoints with a padlock now send the token, e.g. `GET /api/auth/me`.

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | Public | Creates an account (always with the Requester role) and returns a token |
| POST | `/api/auth/login` | Public | Returns a JWT (valid 60 min) and the user's data |
| GET | `/api/auth/me` | Bearer token | Current user |

Every endpoint requires a valid token unless it is marked as public. Errors use
[ProblemDetails](https://www.rfc-editor.org/rfc/rfc7807): 400 validation, 401 invalid credentials or token,
403 disabled account, 409 e-mail already registered.

### Tickets

| Method | Route | Who | Description |
|---|---|---|---|
| GET | `/api/tickets` | Any role | Paginated list with filters, search and sort (see below) |
| GET | `/api/tickets/{id}` | Any role | Details with comments, history and the allowed next statuses |
| POST | `/api/tickets` | Any role | Creates an `Open` ticket with the caller as requester |
| PUT | `/api/tickets/{id}` | Any role | Edits title, description, priority and category (not when `Closed`) |
| PATCH | `/api/tickets/{id}/status` | Staff; requester only to close | Changes the status and records it in the history |
| PATCH | `/api/tickets/{id}/assign` | Technician, Admin | Assigns an active technician/admin (`null` unassigns) |
| POST | `/api/tickets/{id}/comments` | Any role | Adds a comment (not when `Closed`) |
| DELETE | `/api/tickets/{id}` | Admin | Deletes the ticket with its comments and history |

**List parameters:** `page` (default 1), `pageSize` (default 20, max 100), `status`, `priority`, `branchId`,
`categoryId`, `assigneeId`, `search` (title, description or ticket number such as `#12`) and `sort`
(`createdAt`, `updatedAt`, `priority`, `status`, `title`; prefix with `-` for descending, default `-createdAt`).
Example: `GET /api/tickets?status=InProgress&priority=High&sort=-priority&page=1&pageSize=20`.

**Visibility by role**

- **Requester:** only their own tickets.
- **Technician:** tickets assigned to them plus unassigned tickets.
- **Admin:** every ticket.

A ticket outside the caller's visibility returns **404** (not 403), so the API does not reveal which ticket numbers exist.

**Status workflow**

```
Open → InProgress ⇄ OnHold
         ↓
      Resolved → Closed
```

Any other transition returns **400** with the allowed options, e.g.
`Invalid status transition from Open to Closed. Allowed: InProgress.`
Moving to `Resolved` sets `resolvedAt`. Staff can make any valid transition; a requester can only close
their own ticket once it is `Resolved` (confirming the fix). Every change of status, priority or assignee
is written to the ticket history with who made it and when.

### Catalogs, users and dashboard

| Method | Route | Who | Description |
|---|---|---|---|
| GET | `/api/branches` | Any role | Active branches by name (`?includeInactive=true` also lists inactive ones, admins only) |
| GET | `/api/branches/{id}` | Any role | One branch |
| POST / PUT / DELETE | `/api/branches[/{id}]` | Admin | Create, edit (including `isActive`) and delete |
| GET | `/api/categories[/{id}]` | Any role | Categories by name |
| POST / PUT / DELETE | `/api/categories[/{id}]` | Admin | Create, edit and delete |
| GET | `/api/users` | Admin | Paginated users; filters `role`, `isActive`, `search` (name or e-mail) |
| GET | `/api/users/{id}` | Admin | One user |
| POST | `/api/users` | Admin | Creates a user with any role |
| PUT | `/api/users/{id}` | Admin | Edits name, e-mail, role and status; `password` is optional (resets it) |
| GET | `/api/users/assignees` | Technician, Admin | Active technicians and admins, for assigning tickets |
| GET | `/api/dashboard/summary` | Any role | Totals by status, priority, branch and category; average resolution time |

- Branch and category names are unique, ignoring case (409 on duplicates).
- A branch or category with tickets cannot be deleted (409). Branches are **deactivated** instead: an inactive
  branch keeps its history but cannot receive new tickets.
- Users are never deleted, only deactivated. A deactivated user loses access immediately, even with a valid token.
  An admin cannot deactivate themselves or remove their own Admin role, so there is always an active admin.
- The dashboard uses the same visibility as the ticket list: admins get company-wide numbers, technicians and
  requesters get the numbers of the tickets they can see. Every number is computed by the database
  (`GROUP BY`, `COUNT`, `AVG(DATEDIFF(...))`), so no tickets are loaded into memory.

### Postman collection

[`docs/postman/TicketFlow.postman_collection.json`](docs/postman/TicketFlow.postman_collection.json) has a request
for every endpoint plus automated checks (roles, visibility, the full ticket lifecycle and error cases).
Import it into Postman and run it with the **Collection Runner**, in order: the *Auth* folder logs in one demo
user per role and stores their tokens for the rest. The run deletes the ticket it creates, so the demo data
stays unchanged. From the command line, with the API running:

```bash
npx newman run docs/postman/TicketFlow.postman_collection.json
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

### 2. Configurar los secretos

La cadena de conexión es un secreto y **no** se guarda en el repo. En desarrollo usa *user secrets*:

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=TicketFlow;User Id=sa;Password=<TuContraseña>;TrustServerCertificate=True" \
  --project src/TicketFlow.Api
```

También puedes usar la variable de entorno `ConnectionStrings__Default`.

También debes configurar la clave para firmar los JWT (cualquier texto aleatorio de 32 caracteres o más):

```bash
dotnet user-secrets set "Jwt:SigningKey" "<texto-aleatorio-de-32+-caracteres>" --project src/TicketFlow.Api
```

O usa las variables de entorno `ConnectionStrings__Default` y `Jwt__SigningKey`. Sin la clave, la API no arranca.

### 3. Aplicar migraciones y datos demo

- **Opción A:** `dotnet run --project src/TicketFlow.Api`. En `Development` aplica las migraciones e inserta los datos demo automáticamente (solo si la base está vacía).
- **Opción B:** `dotnet tool restore` y luego
  `dotnet ef database update --project src/TicketFlow.Infrastructure --startup-project src/TicketFlow.Api`.
  Solo crea el esquema; los datos demo se insertan al iniciar la API.

Para crear una migración nueva:
`dotnet ef migrations add <Nombre> --project src/TicketFlow.Infrastructure --startup-project src/TicketFlow.Api --output-dir Persistence/Migrations`

### Autenticación y Swagger

Con la API corriendo en Development abre **http://localhost:5000/swagger**, haz login en
`POST /api/auth/login` con un usuario demo, copia el `accessToken`, pulsa **Authorize** y pégalo.
Endpoints: `POST /api/auth/register` (público, siempre crea Requester), `POST /api/auth/login` (público)
y `GET /api/auth/me` (requiere token). Todos los demás endpoints requieren token salvo que sean públicos.

### Tickets

Los endpoints de `/api/tickets` están en la tabla de la sección en inglés. Resumen de las reglas:

- **Listado:** `page` (1 por defecto), `pageSize` (20 por defecto, máximo 100), filtros `status`, `priority`,
  `branchId`, `categoryId`, `assigneeId`, búsqueda `search` (título, descripción o número como `#12`)
  y orden `sort` (`createdAt`, `updatedAt`, `priority`, `status`, `title`; con `-` delante es descendente).
- **Visibilidad:** el solicitante ve solo sus tickets; el técnico ve los asignados a él y los sin asignar;
  el admin ve todos. Un ticket que no puedes ver responde **404**.
- **Estados:** `Open → InProgress ⇄ OnHold`, `InProgress → Resolved → Closed`. Cualquier otra transición
  responde **400** con las opciones válidas. Al pasar a `Resolved` se llena `resolvedAt`.
- **Permisos:** técnicos y admins cambian estados y asignan; el solicitante solo puede cerrar su ticket
  cuando ya está `Resolved`. Solo el admin elimina tickets. Los tickets `Closed` no se editan ni se comentan.
- **Historial:** todo cambio de estado, prioridad o asignado queda registrado con quién y cuándo.

### Catálogos, usuarios y dashboard

Las rutas están en la tabla de la sección en inglés. Reglas principales:

- **Sucursales y categorías:** cualquier usuario las consulta; solo el admin las crea, edita y elimina.
  Los nombres son únicos sin importar mayúsculas (409 si se repiten).
- Una sucursal o categoría con tickets no se puede eliminar (409). Las sucursales se **desactivan**: conservan su
  historial pero ya no reciben tickets nuevos.
- **Usuarios** (solo admin): listado paginado con filtros `role`, `isActive` y `search`. No se eliminan, se desactivan,
  y un usuario desactivado pierde el acceso al instante aunque tenga un token válido. El admin no puede desactivarse
  ni quitarse el rol Admin a sí mismo. `GET /api/users/assignees` lista a quién se puede asignar un ticket (técnicos y admins).
- **Dashboard:** `GET /api/dashboard/summary` da totales por estado, prioridad, sucursal y categoría, y el tiempo promedio
  de resolución. Respeta la visibilidad por rol: el admin ve todo; técnicos y solicitantes ven solo lo de sus tickets.
  Todo se calcula en la base de datos (`GROUP BY`, `COUNT`, `AVG(DATEDIFF(...))`), sin traer los tickets a memoria.

### Colección de Postman

`docs/postman/TicketFlow.postman_collection.json` trae una petición por endpoint con pruebas automáticas
(roles, visibilidad, ciclo completo del ticket y casos de error). Impórtala en Postman y ejecútala con el
**Collection Runner** en orden: la carpeta *Auth* inicia sesión con un usuario por rol y guarda los tokens.
Al final borra el ticket que crea. Desde la terminal, con la API corriendo:
`npx newman run docs/postman/TicketFlow.postman_collection.json`

### Datos demo

3 sucursales, 5 categorías, 30 tickets y los 6 usuarios de la tabla de arriba. Todos los datos son ficticios.
Contraseña de todas las cuentas: **`Demo123!`**

### Pruebas

`dotnet test` desde `backend/`. Las pruebas de base de datos usan SQLite en memoria, no necesitan SQL Server.
