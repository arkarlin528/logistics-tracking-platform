# ADR-0004: JWT with role policies; customer scoping in one place

- **Status:** Accepted
- **Date:** 2026-10-01

## Context
There are three roles. Admin and Dispatcher (together, "staff") see and change everything. A Customer may only read their own shipments. The same identity must work for REST and for the SignalR WebSocket. The data pipeline is a machine client with no user.

## Decision
- **Stateless JWT bearer tokens** (HS256, 2 h). Claims: `sub`, `email`, `name`, `role`, and `customer_id` for customer users.
- **Authorization works in two layers:**
  1. **Endpoint policies** (`Staff`) decide *what you may do*: book, update, dashboard, customer list.
  2. **Data scoping** decides *what you may see*. It happens once, in `ShipmentQueries.VisibleShipments()`. Every query starts from it, so a new endpoint can't forget the filter.
- **Another customer's shipment returns 404, not 403,** so customers can't probe which references exist.
- **Machine-to-machine:** `POST /api/rates/import` uses an `X-Api-Key` header, compared in constant time and kept separate from user auth.
- **Passwords** are hashed with ASP.NET Core Identity's `PasswordHasher` (PBKDF2, salted, versioned), without pulling in the whole Identity stack.
- **Login is rate-limited** to 10 per minute per IP. Behind Caddy and nginx, the client IP comes from `X-Forwarded-For` (`ForwardLimit = 2`); otherwise every user would share the proxy's bucket.
- **Options are bound lazily and validated on start.** A missing or short signing key stops the app at startup, not on the first request.

## Trade-offs
- ⚠️ **The token is stored in `localStorage`,** so it is readable by JavaScript if there's an XSS bug. Angular escapes templates by default and the app renders no raw HTML. The safer design is a short-lived access token in memory plus a refresh token in an `httpOnly` cookie, and that's on the roadmap.
- ⚠️ **There's no token revocation before expiry.** That's acceptable with 2 h tokens for a demo; production would add refresh-token rotation and a deny list.

## Consequences
- ✅ Integration tests cover 401 (no token), 403 (customer on a staff endpoint), 404 (another customer's data), and hub rejection of anonymous connections.
