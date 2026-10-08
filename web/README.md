# ShipTrack web (Angular)

Frontend for the logistics tracking platform. See the [main README](../README.md).

```bash
npm install
npm start        # http://localhost:4200, proxies /api and /hubs to the API on :5080
npm run build    # production build in dist/web/browser
```

Structure: `core/` (auth, API client, SignalR tracking service, models), `shared/` (map, status badges),
`pages/` (one lazy-loaded standalone component per route), `layout/` (app shell).
