# TicketFlow Web

Angular 21 front end of TicketFlow (standalone components, signals, Angular Material).
See the [main README](../../README.md) for the whole project.

```bash
npm ci          # requires npm 11 (see the main README)
npm start       # http://localhost:4200, /api is proxied to the API on http://localhost:5000
npm test        # unit tests (Vitest)
npm run lint    # ESLint (angular-eslint)
npm run build   # production build in dist/
```

## Structure

```
src/app/
  core/       auth (service, interceptor, guards), API error handling, models, layout
  shared/     reusable components and form validators
  features/   pages, one folder per area (auth, ...)
src/testing/  helpers shared by the unit tests
```
