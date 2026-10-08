# ADR-0002: SignalR over polling, with role-based groups

- **Status:** Accepted
- **Date:** 2026-10-01

## Context
Dispatchers and customers need to see status changes and positions as they happen. Several pages show the same shipment at once: the dashboard, the list, the detail page and the map.

## Options considered
1. **Polling every N seconds.** Simple, but updates arrive late, and most requests return nothing new. The cost grows with users × pages × frequency.
2. **Server-Sent Events.** One-way and simple, but no built-in groups or reconnection logic in .NET, and auth needs extra work.
3. **SignalR.** WebSockets with fallback, automatic reconnect on the client, groups, and JWT auth through the standard pipeline.

## Decision
**SignalR**, with a strongly typed hub (`Hub<ITrackingClient>`) so client method names are checked at compile time.

**Group strategy:** on connect, the **server** puts each connection in exactly one group, based on JWT claims:
- `staff` for Admin and Dispatcher;
- `customer-{id}` for customer users.

Updates go to `staff` + `customer-{owner}`. Clients never ask to join a group, so a customer can't subscribe to another customer's shipments. Each connection is in a single group, so nobody receives duplicates. Pages filter by `shipmentId` on the client.

The application layer only knows `IShipmentNotifier`; the SignalR implementation lives in the Api project.

## Consequences
- ✅ Updates appear in well under a second, and there is no load when nothing changes.
- ✅ Authorization for realtime data is in one place and is covered by an integration test ("other customers do not receive the update").
- ⚠️ Staff receive every update, which is fine at this scale. With thousands of shipments per minute, staff would need per-region or per-shipment groups.
- ⚠️ The browser sends the JWT as `access_token` in the query string for WebSockets (a browser limit). Tokens are short-lived and the proxy logs don't record query strings.
- 🔁 More than one API instance needs a backplane (Redis or Azure SignalR Service). That's on the roadmap.
