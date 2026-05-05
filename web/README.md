# Ship GDS dashboards

pnpm monorepo. Two SPAs share one design system.

```
web/
  apps/
    customer/         Dashboard.Customer  → PublicApi  (http://localhost:5101)
    internal/         Dashboard.Internal  → PrivateApi (http://localhost:5102)
  packages/
    ui/               @ship/ui          shared design system (tokens + primitives + charts)
```

## Stack

Vite · React 18 · TypeScript · Tailwind CSS · CSS-variable tokens · TanStack Query v5 · React Router v6.

Visual vocabulary translated from the `claude.ai/design` handoff (Ship GDS × Stripe). Source of truth for the look: `packages/ui/src/tokens.css` and the components under `packages/ui/src/components/`.

## Dev

```bash
# one-time
pnpm install

# both dashboards in parallel
pnpm dev
# → customer http://localhost:5173
# → internal http://localhost:5174

# one at a time
pnpm dev:customer
pnpm dev:internal

pnpm typecheck
pnpm build
```

The .NET backend must be running for live data to load — see the root `CLAUDE.md` (`docker compose up -d` then `dotnet run --project src/ShippingOrchestrator.PublicApi` and `…PrivateApi` in separate terminals). Without it both dashboards still render shell + mocks; tables and KPI panels backed by the API just show their loading/empty state.

## Env vars

Each app ships an `.env.example`. Copy to `.env.local` (gitignored).

| App      | Var                  | Default                                      | Sent as          |
|----------|----------------------|----------------------------------------------|------------------|
| customer | `VITE_API_BASE`      | `http://localhost:5101`                      | request base URL |
| customer | `VITE_TENANT_ID`     | `00000000-0000-0000-0000-000000000001`       | `X-Tenant-Id`    |
| internal | `VITE_API_BASE`      | `http://localhost:5102`                      | request base URL |
| internal | `VITE_STAFF_ROLE`    | `admin`                                      | `X-Staff-Role`   |
| internal | `VITE_STAFF_USER`    | `dev@ship.local`                           | `X-Staff-User`   |

Both APIs throw at startup in production — the dev auth handlers are dev-only.

## Endpoints wired today

| Dashboard | UI panel                | Endpoint                                              |
|-----------|-------------------------|-------------------------------------------------------|
| customer  | Recent shipments table  | `GET /v1/dashboard/shipments?take&skip`               |
| internal  | Exceptions queue        | `GET /ops/exceptions?take&skip`                       |
| internal  | Carrier health bars     | `GET /ops/kpis/carrier-success-rate?from&to`          |
| internal  | (used by KPI strip)     | `GET /ops/queues?take&skip`                           |

Everything else (carrier-mix donut, throughput history, integrations grid, tenants table, sagas/rate-cache/connectors cards, weekly bars, world map) is **mock data with `// TODO: replace with …` markers**. Grep for `TODO: replace` to find them when you add the matching backend endpoints.
