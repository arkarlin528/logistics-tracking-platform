# ADR-0005: The demo simulator goes through the real use cases

- **Status:** Accepted
- **Date:** 2026-10-01

## Context
A tracking demo with nothing moving doesn't show realtime. Real carrier and GPS feeds aren't available, so something has to generate updates.

## Options considered
1. **Fake events in the browser.** This proves nothing about the backend.
2. **A script that writes rows straight to the database.** It skips validation and the SignalR push, and the data can contradict the rules.
3. **A `BackgroundService` that calls `ShipmentCommands`,** the same use cases the HTTP endpoints use.

## Decision
**Option 3.** Every 5 seconds, `ShipmentSimulator` picks an active shipment and does one of three things:
- moves it one step along the lifecycle;
- sends a position ping along the route (taking the shorter way round the globe, via `GeoMath`);
- occasionally revises its ETA.

Every 12 ticks it books a new shipment, so the demo never runs out. The **seeder** uses the same domain methods (`Shipment.Create`, `RecordEvent`) to build realistic history relative to "now", from a fixed random seed.

## Consequences
- ✅ What you see in the demo is exactly what production code does: validation, the `rowversion` check, persistence and the SignalR push.
- ✅ The simulator is a stand-in for a real carrier integration, which would be another caller of the same command.
- ⚠️ Positions are straight lines between ports (over land sometimes). That's fine for a demo; a real feed would send real coordinates.
- 🔧 It's configurable (`Simulation:Enabled`, `IntervalSeconds`) and **off in tests**, so the tests stay deterministic.
