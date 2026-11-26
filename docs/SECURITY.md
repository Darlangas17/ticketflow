# Security and accessibility audit

Review of TicketFlow against **OWASP Top 10 (2021)**, the relevant requirements of
**OWASP ASVS 4.0 Level 2**, **NIST SP 800-63B** (passwords) and **WCAG 2.1 AA** (accessibility).
Every control below is backed by an automated test or a verification step that can be repeated.

*Resumen en español al final.*

## Scope and method

- **Code review** of the backend (.NET 8 API, EF Core) and the frontend (Angular 21).
- **Automated tests:** 212 backend tests (unit + integration with `WebApplicationFactory`) and 133 frontend tests.
- **End-to-end checks** in a real browser (Microsoft Edge via Playwright) against the `docker compose`
  stack: 98 checks covering every role and screen, on desktop and phone.
- **API contract checks:** Postman collection, 62 requests / 112 assertions (`docs/postman`).
- **Accessibility:** axe-core 4.10 with the `wcag2a`, `wcag2aa`, `wcag21a` and `wcag21aa` rule sets
  on 13 screens × 2 viewports × 2 themes.
- **Dependency scanning:** `dotnet list package --vulnerable --include-transitive` and `npm audit`,
  both also enforced in CI.

## OWASP Top 10 (2021)

| Risk | Controls | Evidence |
|---|---|---|
| **A01 Broken access control** | Fallback policy: every endpoint requires a valid token unless marked public. Role policies (`AdminOnly`, `StaffOnly`). Ticket visibility per role is an expression translated to SQL, so lists, details, actions and dashboard numbers are filtered by the database. Tickets outside the caller's visibility return **404** (not 403), so IDs cannot be probed. The user's role and status are re-read from the database on each request, so a deactivation or role change applies immediately. Admins cannot deactivate themselves or remove their own Admin role. Self-registration always creates a Requester. | `TicketAccessTests`, `TicketServiceTests` (visibility, 404), `TicketEndpointsTests`, `CatalogAndAdminEndpointsTests` (403 per role, deactivated token → 401), `AuthEndpointsTests` (`Register_cannot_choose_a_role`), browser checks per role |
| **A02 Cryptographic failures** | Passwords hashed with **BCrypt** (work factor 11, constant-time verification, dummy hash for unknown users). JWT signed with **HMAC-SHA256**, key ≥ 256 bits validated at startup, fixed algorithm list, issuer/audience/lifetime validated, 30 s clock skew. SQL Server connection with `Encrypt=True`. HTTPS redirection + HSTS available (`Https:Enabled`) for deployments where the API terminates TLS. | `JwtTokenServiceTests`, `JwtKeyConfigurationTests`, `AuthServiceTests` (hash ≠ password) |
| **A03 Injection** | All data access through EF Core LINQ (parameterized); no raw SQL anywhere (`FromSqlRaw`/`ExecuteSqlRaw` absent). Angular escapes all bindings; no `innerHTML`, `bypassSecurityTrust*` or `eval` in the code. Strict **Content-Security-Policy** (`script-src 'self'`, no inline scripts) as a second barrier. | Code search; browser check: the CSP blocks injected inline scripts |
| **A04 Insecure design** | **Rate limiting**: 10 sign-in/registration attempts per minute per client IP (429 + `Retry-After`), plus a global per-user limit. The real client IP is taken from `X-Forwarded-For` only when the proxy is in a trusted network. Explicit ticket workflow in the domain (invalid transitions → 400). Generic sign-in error for unknown e-mail and wrong password. | `SecurityTests.Repeated_sign_in_attempts_are_rate_limited`, `TicketStatusTransitionsTests` (all 25 pairs), verified through nginx in Docker |
| **A05 Security misconfiguration** | Security headers on every web response (CSP, `X-Frame-Options: DENY`, `frame-ancestors 'none'`, `nosniff`, `Referrer-Policy`, `Permissions-Policy`, COOP) and on every API response (CSP `default-src 'none'`, `nosniff`, `DENY`, `Cache-Control: no-store` for `/api`). No `Server` header from Kestrel, nginx version hidden. 1 MB request body limit, maximum lengths on every input. Errors are RFC 7807 ProblemDetails without stack traces; unexpected errors return a generic 500 and are logged. Containers run as **non-root** users. Swagger only in Development or when explicitly enabled. | `SecurityTests` (headers, body size), `GlobalExceptionHandler`, header checks against Docker |
| **A06 Vulnerable components** | Application packages: **0 known vulnerabilities** (NuGet and production npm). Outdated test packages that pulled vulnerable transitive dependencies were updated. CI fails on vulnerable NuGet packages and on high-severity production npm advisories. | CI jobs *Check for vulnerable NuGet packages* and *Audit production dependencies* |
| **A07 Identification and authentication failures** | Password policy per NIST SP 800-63B: minimum 8 characters, maximum 72 bytes (BCrypt limit, never silently truncated), letter + digit, and a **denylist of the most common passwords**. Same policy for self-registration and admin-created accounts. Disabled accounts cannot sign in (reported only after a correct password, so account state does not leak). Access tokens expire after 60 minutes; the frontend signs out automatically at expiry and on any 401. | `AuthServiceTests` (wrong password, unknown e-mail, disabled account, common passwords, length limits), frontend `auth.service.spec.ts` |
| **A08 Software and data integrity failures** | Lockfiles committed (`package-lock.json`, NuGet versions pinned); `npm ci` in CI and Docker. Multi-stage Docker builds from official images. DTOs separate from entities, so clients cannot set protected fields (mass assignment). | `Register_cannot_choose_a_role`, `Create_opens_a_ticket_on_behalf_of_the_caller` |
| **A09 Security logging and monitoring failures** | Sign-in failures, attempts on disabled accounts, registrations and successful sign-ins are logged with the user id or a **masked e-mail** (`m***@domain`); passwords are never logged. Unexpected errors are logged with a trace id that is also returned to the client. `GET /health` for monitoring. | `AuthServiceTests.Logged_emails_are_masked`, Docker logs |
| **A10 Server-side request forgery** | The API makes no outbound requests based on user input. | Code review |

## Selected OWASP ASVS 4.0 (Level 2) requirements

| Area | Requirement | Status |
|---|---|---|
| V2 Authentication | 2.1.1 min. 8 characters · 2.1.7 common passwords rejected · 2.2.1 anti-automation | ✅ |
| V3 Session | 3.3.1 logout invalidates the client session · 3.5.3 signed tokens, fixed algorithm | ✅ (stateless JWT, see limitations) |
| V4 Access control | 4.1.1 enforced server-side · 4.1.3 least privilege · 4.2.1 IDOR protection | ✅ |
| V5 Validation | 5.1.3 positive validation (FluentValidation, enums checked) · 5.3.3 output encoding | ✅ |
| V7 Errors and logging | 7.1.1 no secrets in logs · 7.4.1 generic error messages | ✅ |
| V8 Data protection | 8.2.1 no caching of sensitive responses (`no-store`) | ✅ |
| V12 Files | Request body size limits | ✅ (no file uploads) |
| V14 Configuration | 14.4 security headers · 14.3.2 no debug output in production | ✅ |

## Privacy

- **Data minimization:** the system stores only name, work e-mail, role and ticket content. No
  tracking, analytics, advertising or third-party cookies.
- **No third parties:** fonts and icons are bundled with the app (they used to load from Google Fonts,
  which shares the visitor's IP with a third party). The CSP only allows the app's own origin.
- **Credentials:** never stored in plain text; the JWT lives in `localStorage` for at most 60 minutes.
- **Demo data is fictional** (`.demo` domain, invented names). No secrets are stored in the repository:
  the connection string and JWT key come from user secrets or environment variables.

## Accessibility (WCAG 2.1 AA)

- **axe-core: 0 violations** on all 13 screens (sign-in, registration, dashboard, ticket list, detail,
  create/edit form, users, user dialog, branches, categories, not-found) on desktop and on a phone, in both the
  **light and dark themes** (52 audits).
- Keyboard: a **"Skip to main content"** link is the first focusable element (2.4.1); all actions are
  buttons or links reachable with Tab; dialogs trap focus and close with Escape (Angular CDK).
- Status and priority badges always include text (and an icon for status), never color alone (1.4.1).
- Charts are real `<table>` elements with captions, readable by screen readers, with the value written on
  every row.
- Form errors are linked to their fields and announced; loading and result counts use `aria-live`.
- Page titles per route (2.4.2), `lang` attribute (3.1.1).
- Animations (page transitions, cascades, count-ups, growing bars, background) are short and decorative, and all
  of them are disabled when the system asks for reduced motion (2.3.3); numbers then appear at their final value.
- Layout works from 320 px wide without horizontal scrolling (1.4.10 Reflow).

## Known limitations and production recommendations

1. **Run behind HTTPS.** The compose demo serves plain HTTP on localhost. In production terminate TLS at
   the proxy, uncomment HSTS in `nginx/security-headers.conf`, and set `Https:Enabled` if the API serves
   TLS itself.
2. **Secrets.** Set `JWT_SIGNING_KEY` and `MSSQL_SA_PASSWORD` (or use a secrets manager such as Azure Key
   Vault). Never enable `Jwt:GenerateKeyIfMissing` outside the local demo.
3. **Database account.** The demo connects as `sa` for simplicity. In production use a dedicated login
   with only the rights the application needs, and run migrations with a separate deployment account.
4. **Token revocation.** Tokens are stateless; a stolen token stays valid until it expires (≤ 60 min),
   except that deactivated accounts are rejected on every request. Refresh tokens with rotation, or
   cookie-based sessions with a server-side store, would allow immediate revocation.
5. **Token storage.** The JWT is in `localStorage`, readable by scripts in the page. The strict CSP and the
   absence of HTML injection points mitigate XSS; an `HttpOnly` cookie would remove this exposure.
6. **Development tooling advisories.** `npm audit` reports moderate advisories in build-time
   dependencies of the Angular CLI (`glob`/`minimatch`). They do not reach the browser bundle; the proposed
   fix downgrades Angular, so they are tracked until the CLI updates them.
7. **Swagger** is enabled in the compose demo for convenience; disable it in production
   (`Swagger:Enabled=false`, the default).
8. **CI hardening:** pin third-party GitHub Actions to commit SHAs and enable Dependabot alerts.

## Reporting a vulnerability

Please do not open a public issue. Contact the repository owner through GitHub with a description and steps
to reproduce.

---

## Resumen (español)

Se revisó TicketFlow contra **OWASP Top 10 (2021)**, requisitos de **OWASP ASVS 4.0 nivel 2**, **NIST
SP 800-63B** y **WCAG 2.1 AA**. Controles principales: control de acceso por rol aplicado en SQL (404 para
tickets ajenos), BCrypt y JWT HS256 con clave de 256+ bits, sin SQL crudo ni inyección de HTML, CSP estricta,
**límite de intentos de inicio de sesión** (429), cabeceras de seguridad en la web y la API, respuestas de la
API sin caché, límite de 1 MB por petición, contenedores sin root, política de contraseñas con lista de
contraseñas comunes, registro de eventos de autenticación con el correo enmascarado, fuentes alojadas en el
propio servidor (sin terceros) y **0 dependencias vulnerables** en la aplicación. Accesibilidad: **0
infracciones** de axe-core en 13 pantallas en escritorio y teléfono, enlace "saltar al contenido", gráficos como
tablas accesibles y diseño sin scroll horizontal desde 320 px. Las limitaciones y recomendaciones para
producción (HTTPS, gestor de secretos, usuario de base de datos con mínimos privilegios, revocación de tokens)
están en la sección anterior.
