# ADR-0001: Clean Architecture, without MediatR or repositories

- **Status:** Accepted
- **Date:** 2026-10-01

## Context
The API has real business rules (a shipment lifecycle, customer data isolation, idempotent imports) and several delivery mechanisms: HTTP endpoints, a SignalR push, and a background simulator. The rules need to be testable without a web server, and each delivery mechanism needs to be replaceable on its own.

## Options considered
1. **Single project, controllers talk to EF Core.** This is the fastest to write, but rules spread across controllers and the simulator would have to duplicate them.
2. **Clean Architecture with MediatR, a repository per aggregate and AutoMapper.** This is the common template, but it is heavy for this size, MediatR became commercially licensed in 2025, and repositories over EF Core mostly re-wrap `DbSet`.
3. **Clean Architecture with plain use-case classes and `IAppDbContext`.** It keeps the layering but uses no extra libraries.

## Decision
**Option 3.** There are four projects: `Domain` → `Application` → `Infrastructure` / `Api`.
- **Domain:** rich entities. `Shipment.RecordEvent` is the only way to change a status, so the rules can't be bypassed.
- **Application:** use-case classes (`ShipmentCommands`, `ShipmentQueries`…) and the interfaces they need (`IAppDbContext`, `IShipmentNotifier`, `ITokenService`, `IReferenceGenerator`).
- **Application uses EF Core directly through `IAppDbContext`.** `DbSet` already is a repository and unit of work, and keeping `IQueryable` lets queries project straight to DTOs in SQL.
- **Mapping is explicit** (`ToDto()` extension methods). That is greppable, fast and easy to debug.

## Consequences
- ✅ Domain tests run in milliseconds with no database.
- ✅ The SignalR push, the token service and the database are adapters, and the simulator reuses the same use cases ([ADR-0005](0005-simulator-uses-real-commands.md)).
- ⚠️ Application references `Microsoft.EntityFrameworkCore` (only the abstractions, not SQL Server). This is an accepted leak: replacing EF Core is not a realistic goal for this project.
- 🔁 Revisit if cross-cutting behaviour (auditing, caching, transactions per command) grows. A small decorator or pipeline would then earn its place.
