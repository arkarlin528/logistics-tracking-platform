# ADR-0003: SQL Server, with `rowversion` concurrency and `SEQUENCE` numbering

- **Status:** Accepted
- **Date:** 2026-10-01

## Context
This project targets enterprise logistics stacks, where SQL Server is common. (The auction project uses PostgreSQL on purpose, to show both.) Two concurrency problems need solving:
1. **Two people update the same shipment at once,** for example a dispatcher and the carrier feed. Without protection, the last write silently wins and history can contradict the status.
2. **Shipment numbers** (`SH-100042`) must be unique and readable. `SELECT MAX(...) + 1` hands out duplicates under load.

## Options considered
For updates:
- **Pessimistic locks** (`UPDLOCK`): correct, but holds locks across the request.
- **Optimistic `rowversion`**: no locks; conflicts are detected on save.

For numbers:
- **`MAX + 1`**: has race conditions.
- **Identity column formatted on read**: ties the public number to the internal key.
- **A `SEQUENCE`**: atomic and independent of the key.

## Decision
- **`Shipment.RowVersion` is a SQL Server `rowversion`,** configured as EF Core's concurrency token. A losing writer gets `DbUpdateConcurrencyException`, which the API maps to **409 Conflict** ("reload and try again").
- **A `SEQUENCE ShipmentNumbers` starting at 100001** is read through `IReferenceGenerator`. The query is executed with `ToListAsync`, because SQL Server rejects `NEXT VALUE FOR` inside the `TOP(2)` subquery that `SingleAsync` generates. A comment in the code explains this.
- **Enums are stored as strings.** That costs a few bytes, but rows are readable in SSMS and reordering enum values can't corrupt data.

## Consequences
- ✅ The integration test `Two_simultaneous_updates_never_both_win` fires 5 parallel identical updates. Exactly one succeeds; the others get 409 (stale version) or 422 (the rule no longer allows it).
- ✅ Numbers are gap-tolerant but never duplicated.
- ⚠️ A sequence can leave gaps (for example after a rolled-back booking). That's acceptable for references; gapless numbering is only needed for things like invoices.
- ⚠️ SQL Server Express in Docker needs ~2 GB RAM and x86-64, which shapes the EC2 choice (t3.medium). See [deploy/README.md](../../deploy/README.md).
