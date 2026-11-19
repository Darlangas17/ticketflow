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
