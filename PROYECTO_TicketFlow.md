# TicketFlow: sistema de tickets de soporte TI

> Documento guía para construir el proyecto con **Claude Code**.
> Objetivo: tener un proyecto de portafolio en GitHub que muestre **C#, SQL, API REST y Angular**, el stack que pide la plaza de Programador de Grupo Farsiman (y similares).

---

## 1. Resumen del proyecto

**TicketFlow** es una aplicación web para registrar y dar seguimiento a tickets de soporte técnico (fallas de red, equipos, software, etc.) en una empresa con varias sucursales.

Por qué este tema: es un dominio que ya conoces desde tu trabajo en soporte, así que puedes explicarlo con seguridad en una entrevista.

**Roles:**
- **Solicitante**: crea tickets y ve los suyos.
- **Técnico**: ve tickets asignados, cambia estado, comenta.
- **Admin**: gestiona usuarios, sucursales y categorías, ve el dashboard completo.

---

## 2. Stack tecnológico

| Capa | Tecnología |
|---|---|
| Backend | .NET 8, ASP.NET Core Web API, C# |
| ORM | Entity Framework Core 8 (code-first, migraciones) |
| Base de datos | SQL Server 2022 (en Docker) |
| Auth | JWT (access token) + hash de contraseñas con BCrypt |
| Frontend | Angular 17+ (standalone components, signals, routing, reactive forms) |
| Tests | xUnit + FluentAssertions (backend), Jasmine/Karma o Jest (frontend) |
| Contenedores | Docker + docker-compose |
| CI | GitHub Actions (build + test) |
| Documentación API | Swagger / OpenAPI |

---

## 3. Alcance del MVP

### Incluye
1. Registro e inicio de sesión (JWT).
2. CRUD de tickets con filtros, búsqueda y paginación.
3. Asignación de tickets a técnicos.
4. Cambio de estado con historial.
5. Comentarios por ticket.
6. Catálogos: sucursales y categorías.
7. Dashboard con métricas básicas (tickets por estado, por sucursal, por prioridad).
8. Seed de datos de demostración (datos falsos).

### No incluye (futuro)
- Adjuntos de archivos, notificaciones por correo, SLA automáticos, tiempo real con SignalR.

---

## 4. Modelo de datos (SQL Server)

### Users
| Campo | Tipo | Notas |
|---|---|---|
| Id | int PK identity | |
| FullName | nvarchar(120) | requerido |
| Email | nvarchar(160) | único, requerido |
| PasswordHash | nvarchar(200) | BCrypt |
| Role | nvarchar(20) | `Requester`, `Technician`, `Admin` |
| IsActive | bit | default 1 |
| CreatedAt | datetime2 | UTC |

### Branches (sucursales)
| Id | Name (único) | City | IsActive |

### Categories
| Id | Name (único) | Description |

### Tickets
| Campo | Tipo | Notas |
|---|---|---|
| Id | int PK identity | |
| Title | nvarchar(150) | requerido |
| Description | nvarchar(max) | requerido |
| Status | nvarchar(20) | `Open`, `InProgress`, `OnHold`, `Resolved`, `Closed` |
| Priority | nvarchar(20) | `Low`, `Medium`, `High`, `Critical` |
| CategoryId | int FK | |
| BranchId | int FK | |
| RequesterId | int FK → Users | |
| AssigneeId | int? FK → Users | nullable |
| CreatedAt / UpdatedAt / ResolvedAt | datetime2 | UTC |

### TicketComments
| Id | TicketId FK | AuthorId FK | Body nvarchar(2000) | CreatedAt |

### TicketHistory
| Id | TicketId FK | ChangedById FK | Field | OldValue | NewValue | ChangedAt |

**Índices recomendados:** `Tickets(Status)`, `Tickets(AssigneeId)`, `Tickets(BranchId)`, `Tickets(CreatedAt)`, `Users(Email)` único.

---

## 5. API REST

Base: `/api`. Respuestas en JSON. Errores con `ProblemDetails` (RFC 7807).

### Auth
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/auth/register` | Registro (rol Requester por defecto) |
| POST | `/auth/login` | Devuelve JWT + datos del usuario |
| GET | `/auth/me` | Usuario actual |

### Tickets
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/tickets` | Lista paginada. Query: `page`, `pageSize`, `status`, `priority`, `branchId`, `categoryId`, `assigneeId`, `search`, `sort` |
| GET | `/tickets/{id}` | Detalle con comentarios e historial |
| POST | `/tickets` | Crear |
| PUT | `/tickets/{id}` | Editar (título, descripción, prioridad, categoría) |
| PATCH | `/tickets/{id}/status` | Cambiar estado (registra historial) |
| PATCH | `/tickets/{id}/assign` | Asignar técnico (Admin/Technician) |
| POST | `/tickets/{id}/comments` | Agregar comentario |
| DELETE | `/tickets/{id}` | Solo Admin |

### Catálogos
| Método | Ruta |
|---|---|
| GET/POST/PUT/DELETE | `/branches` |
| GET/POST/PUT/DELETE | `/categories` |
| GET/POST/PUT | `/users` (solo Admin) |

### Dashboard
| GET | `/dashboard/summary` | Totales por estado, prioridad, sucursal; tiempo promedio de resolución |

### Reglas de negocio
- Un Requester solo ve y edita **sus** tickets.
- Un Technician ve los asignados a él y los sin asignar.
- Un Admin ve todo.
- Transiciones válidas: `Open → InProgress → (OnHold ↔ InProgress) → Resolved → Closed`. Una transición inválida devuelve 400.
- Al pasar a `Resolved` se llena `ResolvedAt`.
- Todo cambio de estado, prioridad o asignado genera un registro en `TicketHistory`.

---

## 6. Frontend (Angular)

### Páginas
1. **Login / Registro**
2. **Dashboard**: tarjetas de totales + gráficos simples (puede usar `ng2-charts` o `ngx-charts`).
3. **Lista de tickets**: tabla con filtros, búsqueda, paginación y badges de estado/prioridad.
4. **Detalle de ticket**: info, comentarios, historial, acciones (cambiar estado, asignar).
5. **Crear/Editar ticket**: formulario reactivo con validaciones.
6. **Administración**: usuarios, sucursales, categorías (solo Admin).

### Requisitos técnicos
- Standalone components, lazy loading de rutas.
- `HttpInterceptor` para adjuntar el JWT y manejar 401.
- `AuthGuard` y `RoleGuard`.
- Servicios tipados con interfaces (sin `any`).
- Estado con **signals**.
- Diseño responsive (Angular Material o Tailwind, elegir uno y ser consistente).
- Manejo de loading y errores en todas las pantallas.

---

## 7. Estructura de carpetas

```
ticketflow/
├── CLAUDE.md
├── README.md
├── docker-compose.yml
├── .gitignore
├── .github/workflows/ci.yml
├── backend/
│   ├── TicketFlow.sln
│   ├── src/
│   │   ├── TicketFlow.Api/            # Controllers, Program.cs, middleware
│   │   ├── TicketFlow.Application/    # DTOs, servicios, validaciones, interfaces
│   │   ├── TicketFlow.Domain/         # Entidades, enums
│   │   └── TicketFlow.Infrastructure/ # DbContext, migraciones, repos, seed
│   └── tests/
│       └── TicketFlow.Tests/
└── frontend/
    └── ticketflow-web/
        └── src/app/
            ├── core/        # auth, interceptors, guards, models
            ├── shared/      # componentes reutilizables
            └── features/    # auth, dashboard, tickets, admin
```

---

## 8. Seguridad y calidad

- Contraseñas con BCrypt, nunca en texto plano.
- Secretos (cadena de conexión, clave JWT) por variables de entorno / `appsettings.Development.json` fuera del repo. Incluir `.env.example`.
- Validación de entrada con FluentValidation o DataAnnotations.
- CORS restringido al origen del frontend.
- Middleware global de excepciones.
- Paginación obligatoria en listados.
- Evitar consultas N+1 (usar `Include`/proyecciones).
- Logs con `ILogger`.

---

## 9. Docker

`docker-compose.yml` debe levantar con **un solo comando** (`docker compose up --build`):
- `db`: SQL Server 2022 con volumen persistente.
- `api`: backend en el puerto 5000, aplica migraciones y seed al iniciar.
- `web`: frontend servido con nginx en el puerto 4200.

---

## 10. Pruebas

**Backend (mínimo):**
- Servicio de tickets: transiciones de estado válidas/inválidas.
- Reglas de visibilidad por rol.
- Auth: login correcto, contraseña incorrecta, usuario inactivo.
- Al menos 1 prueba de integración con `WebApplicationFactory` (SQLite in-memory o Testcontainers).

**Frontend (mínimo):**
- Un test del `AuthService` y uno de un componente con formulario.

**Meta:** que `dotnet test` y `ng test` pasen en CI.

---

## 11. CI (GitHub Actions)

Workflow `ci.yml` en push y pull request:
1. Restaurar, compilar y ejecutar tests de .NET.
2. `npm ci`, lint, build y test del frontend.

Agregar el badge de CI al README.

---

## 12. Plan de trabajo por fases (prompts para Claude Code)

> Recomendación: trabaja **una fase a la vez**, revisa el resultado, haz commit y luego sigue. Antes de empezar, crea el archivo `CLAUDE.md` (ver sección 15).

### Fase 0: Preparación
**Prompt:**
> Lee este documento completo (PROYECTO_TicketFlow.md). Crea la estructura de carpetas de la sección 7, el `.gitignore` para .NET y Angular, un README mínimo y el `CLAUDE.md` de la sección 15. Inicializa git y haz el primer commit. No escribas código de la aplicación todavía.

### Fase 1: Dominio y base de datos
> Crea la solución .NET 8 con los 4 proyectos (Api, Application, Domain, Infrastructure) y el de tests. Implementa las entidades y enums del modelo de datos (sección 4), el `DbContext` con configuración Fluent API (longitudes, índices, relaciones), la primera migración y un seed con datos de demostración falsos: 3 sucursales, 5 categorías, 1 admin, 2 técnicos, 3 solicitantes y 30 tickets variados. Documenta en el README cómo correr las migraciones.

### Fase 2: Autenticación
> Implementa `/auth/register`, `/auth/login` y `/auth/me` con JWT y BCrypt. Configura autorización por roles y Swagger con soporte para el botón "Authorize". Agrega pruebas de login correcto, contraseña incorrecta y usuario inactivo.

### Fase 3: Tickets (núcleo)
> Implementa todos los endpoints de `/tickets` de la sección 5 con DTOs, validaciones, paginación, filtros y búsqueda. Aplica las reglas de visibilidad por rol y la máquina de estados con historial. Agrega pruebas unitarias para transiciones y visibilidad.

### Fase 4: Catálogos y dashboard
> Implementa CRUD de `/branches`, `/categories`, `/users` (solo Admin) y `/dashboard/summary` con consultas eficientes (agrupaciones en SQL, sin traer todo a memoria).

### Fase 5: Frontend base
> Crea la app Angular (standalone, routing, lazy loading). Implementa login, interceptor JWT, guards, layout con navegación según rol y el servicio de auth con signals.

### Fase 6: Frontend de tickets
> Implementa lista con filtros y paginación, detalle con comentarios e historial, y formulario de crear/editar con validaciones. Maneja estados de carga y error.

### Fase 7: Dashboard y administración
> Implementa el dashboard con gráficos y las pantallas de administración de usuarios, sucursales y categorías.

### Fase 8: Docker, CI y pulido
> Crea los Dockerfiles y el `docker-compose.yml` para levantar todo con un comando. Agrega el workflow de GitHub Actions. Revisa que el README tenga: descripción, capturas, stack, cómo correr, usuarios demo, decisiones técnicas y roadmap.

### Fase 9: Revisión final
> Haz una revisión de código completa: busca problemas de seguridad, consultas N+1, código duplicado, nombres poco claros y falta de manejo de errores. Corrige lo que encuentres y resume los cambios.

---

## 13. Criterios de aceptación (el proyecto está "listo" cuando…)

- [ ] `docker compose up --build` levanta DB, API y web sin pasos manuales.
- [ ] Puedo entrar con un usuario demo de cada rol y ver solo lo que corresponde.
- [ ] Crear, asignar, comentar y cerrar un ticket funciona de punta a punta.
- [ ] Una transición de estado inválida es rechazada con un error claro.
- [ ] Swagger documenta todos los endpoints.
- [ ] `dotnet test` y los tests del frontend pasan; CI en verde.
- [ ] No hay secretos ni datos reales en el repo.
- [ ] El README tiene capturas de pantalla y explica cómo correrlo.

---

## 14. Checklist del GitHub (para la postulación)

- [ ] Repositorio **público** con nombre claro (`ticketflow`).
- [ ] README en inglés (o bilingüe) con capturas o un GIF corto.
- [ ] Commits pequeños y con mensajes claros (no un solo commit gigante).
- [ ] Repo **fijado (pin)** en tu perfil de GitHub.
- [ ] Descripción y topics en el repo: `dotnet`, `csharp`, `angular`, `sql-server`, `rest-api`, `docker`.
- [ ] Enlace al repo en tu CV, LinkedIn y Upwork.

---

## 15. Archivo `CLAUDE.md` sugerido (copiar tal cual a la raíz del repo)

```markdown
# TicketFlow: instrucciones para Claude Code

## Proyecto
Sistema de tickets de soporte TI. Backend .NET 8 (Clean Architecture ligera) + SQL Server + Angular 17+.
La especificación completa está en PROYECTO_TicketFlow.md.

## Reglas
- Trabaja una fase a la vez y no adelantes trabajo de fases futuras.
- Antes de dar una fase por terminada: compila, corre los tests y arregla los errores.
- Nunca guardes secretos ni datos reales en el repo.
- Los datos de seed son siempre ficticios.
- Código y comentarios en inglés; README en inglés y español.
- Backend: nombres en PascalCase, async/await en todo acceso a datos, DTOs separados de entidades.
- Frontend: TypeScript estricto, sin `any`, standalone components, signals para estado.
- Commits pequeños con formato Conventional Commits (feat:, fix:, test:, docs:, chore:).

## Comandos
- Backend: `dotnet build`, `dotnet test`, `dotnet ef migrations add <Nombre>`
- Frontend: `npm ci`, `ng serve`, `ng test`, `ng build`
- Todo: `docker compose up --build`
```

---

## 16. Cómo evaluar a Claude Code (tu prueba)

Al terminar cada fase, anota:

| Fase | ¿Compiló a la primera? | ¿Pasaron los tests? | Correcciones que tuve que pedir | Notas |
|---|---|---|---|---|
| 1 | | | | |
| 2 | | | | |
| … | | | | |

**Preguntas para tu conclusión:**
1. ¿Respetó la arquitectura y el `CLAUDE.md`?
2. ¿Los tests que escribió prueban algo real o son relleno?
3. ¿Encontró y corrigió sus propios errores?
4. ¿Qué tuve que corregir yo? (esto es lo que más aprendes)

> **Importante para la entrevista:** lee y entiende el código que genere. Te van a preguntar por qué usaste JWT, cómo funciona una migración o cómo evitaste el N+1. Si no lo puedes explicar, el proyecto no te sirve. Pídele a Claude Code que te explique las partes que no entiendas.

---

## 17. Ideas de mejora (roadmap para el README)

- Adjuntos de imágenes en tickets.
- Notificaciones por correo.
- SLA con alertas por vencimiento.
- Tiempo real con SignalR.
- Exportar reportes a Excel/PDF.
- Despliegue en Azure o AWS (encaja con lo que piden en la plaza).
