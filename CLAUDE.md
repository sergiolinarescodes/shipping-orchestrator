# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Modular monolith on **.NET 10** (SDK pinned in `global.json` to `10.0.201`, `latestFeature` roll-forward). Brokers between many ecommerce platforms (Shopify, WooCommerce, custom REST) and many carriers (PostNL, future) for multiple tenants.

Three deployable hosts share one Domain/Application/Infrastructure core:
- `PublicApi` — tenant-facing HTTP (Customer read side only)
- `PrivateApi` — internal/admin HTTP (Operations read side only)
- `Worker` — Wolverine handlers, sagas, **and** read-side projections (sees both read schemas)

## Common commands

```bash
# Local stack (Postgres, LocalStack, Jaeger, Mailpit)
docker compose up -d
./scripts/dev-up.ps1            # +opens Windows Terminal tabs for compose/api/worker
./scripts/dev-up.ps1 -Clean     # wipe volumes first
./scripts/dev-down.ps1 [-Clean]

# Build / restore (use the slnx, not a sln — it's the source of truth)
dotnet restore ShippingOrchestrator.slnx
dotnet build ShippingOrchestrator.slnx --configuration Release

# Tests
dotnet test                                           # entire solution
dotnet test tests/ShippingOrchestrator.Domain.UnitTests
dotnet test tests/ShippingOrchestrator.E2E.Tests --filter "FullyQualifiedName~HappyPath"

# EF Core (dotnet-ef is a local tool — restore first)
dotnet tool restore
dotnet ef migrations add <Name> \
  --project src/ShippingOrchestrator.Infrastructure \
  --startup-project src/ShippingOrchestrator.Worker
# (analogous for OperationsReadDbContext / CustomerReadDbContext, with -c <DbContext>)

# Run a host directly
dotnet run --project src/ShippingOrchestrator.PublicApi    # http://localhost:5101 (+/scalar)
dotnet run --project src/ShippingOrchestrator.PrivateApi   # http://localhost:5102
dotnet run --project src/ShippingOrchestrator.Worker
```

CI (`.github/workflows/ci.yml`) splits unit/architecture from integration/E2E — Testcontainers spins up Postgres + LocalStack on the runner.

## Architecture (the parts you need to know before editing)

### Layer rules — enforced by `ShippingOrchestrator.Architecture.Tests`
- **Domain** depends on nothing else in this repo.
- **Application** must not reference Infrastructure. Repositories and `IUnitOfWork` are interfaces here, implemented in Infrastructure.
- **ReadModels** (Operations, Customer, Projections) must not reference Application or Infrastructure — read side consumes domain events only.
- **Operations vs Customer** read namespaces must not reference each other. `Projections` is the only namespace allowed to touch both. The wall is enforced **per-namespace** because the read side now ships as a single assembly (`ShippingOrchestrator.ReadModels`) — not the multi-project layout that the older `README.md` still describes.
- **Connectors** (`*.CarrierConnectors.*`, `*.EcommerceConnectors.*`) may only depend on Domain + `Modules.Abstractions`.

If you break one of these, `DependencyRulesTests` will fail on CI before anything else. Treat it as load-bearing.

### Connector module pattern
New ecommerce or carrier integrations are isolated to a single project:
1. Implement `IConnectorModule` (`Modules.Abstractions/IConnectorModule.cs`) — declares `ConnectorCode`, `Kind`, DI registrations, runtime registry registration, optional `BootstrapAsync`.
2. Add one line per host: `services.AddConnectorModule<MyConnectorModule>(configuration)` in each `Program.cs`. Hosts iterate every registered module via `ConnectorRegistry`; **no edits to Application or Infrastructure** are required.

### Persistence schemas (Postgres, created by `scripts/db-init.sql`)
- `orchestrator` — write-side aggregates (the canonical state).
- `messaging` — Wolverine durable outbox/inbox.
- `ops_read` — Operations read projections.
- `customer_read` — Customer read projections.

Per-schema roles (`orchestrator_app`, `ops_read_role`, `customer_read_role`) are created locally so future role-scoped connections — the production guarantee that PublicApi cannot reach the orchestrator schema — can be tested. Each host calls `Database.MigrateAsync()` on its own DbContext at startup.

### Messaging — Wolverine
`WolverineConfigurationExtensions.ConfigureOrchestratorMessaging` is single-transport: SQS + the `messaging` Postgres schema as durable outbox/inbox, in every environment. `UseLocalStackIfDevelopment(port)` redirects the SQS client to LocalStack when `EnvironmentName == "Development"` (port from `Aws:LocalStackPort`, default 4566). Production hosts must NOT run as Development — they pick up real AWS via the SDK's standard credential chain.

E2E tests start their own LocalStack via `Testcontainers.LocalStack` and pass the dynamic port through `Aws:LocalStackPort`. They also set `Messaging:AutoPurgeOnStartup=true` and purge SQS queues between tests so per-test isolation matches Respawn's Postgres reset.

Conventional routing is on (`NamingSource.FromHandlerType`), so each handler type gets its own queue created on `AutoProvision`. `AutoApplyTransactions` is on, so command handlers participate in the EF transaction automatically. Discovery always includes the Application assembly; the Worker additionally passes the `ShippingOrchestrator.ReadModels` assembly so projection handlers are found.

### Hosts and authentication
- PublicApi wires `TestTenantAuthHandler` (policy `Tenant`) + `TenantContextMiddleware`.
- PrivateApi wires `TestStaffAuthHandler` (policy `Staff`).
- **Both throw at startup if `IsProduction()`** — these are dev-only schemes. A real JWT bearer (e.g. Cognito) must be wired before either host can boot in prod.

## Conventions and gotchas

- **Solution file**: `ShippingOrchestrator.slnx` (XML solution format) — not a `.sln`. Use it for `dotnet build`/`restore`.
- **Central package management**: all versions live in `Directory.Packages.props`; project files reference `<PackageReference Include="..." />` without versions. Update versions there.
- **Warnings are errors** (`Directory.Build.props`). The `NoWarn` list there documents which CA rules are intentionally suppressed; prefer fixing over expanding the list.
- **FluentAssertions pinned to 7.2.0**: v8 switched to a paid commercial license. Stay on 7.x.
- **Naming**: file-scoped namespaces (warning-level), `_camelCase` for private fields *including* `private static readonly` (overridden in `.editorconfig`).
- **InternalsVisibleTo** is wired in `Directory.Build.props` so `<Project>.UnitTests`, `<Project>.IntegrationTests`, `<Project>.Tests`, and `ShippingOrchestrator.E2E.Tests` can hit `internal` types — prefer `internal` over `public` for project-internal surfaces.
- **Idempotency**: batch creation honors `IdempotencyKey` per tenant; existing batches short-circuit without re-publishing the process command. Don't bypass `CreateShipmentBatchHandler` when adding new ingestion paths.
- **Encryption**: `IEnvelopeEncryptor` is an AES envelope abstraction; the `Encryption:Aes:Base64Key` in committed `appsettings.json` is a placeholder for local dev only.

## Frontend (`web/`)

pnpm monorepo holding two SPAs that consume the read endpoints of the two HTTP hosts.

```
web/
  apps/customer/     Vite SPA · http://localhost:5173 · talks to PublicApi  (/v1/dashboard/*)
  apps/internal/     Vite SPA · http://localhost:5174 · talks to PrivateApi (/ops/*)
  packages/ui/       @ship/ui — shared design system (CSS-var tokens + Tailwind preset + React primitives)
```

Stack: Vite + React 18 + TypeScript + Tailwind (Ship preset) + TanStack Query v5 + React Router v6.

```bash
cd web
pnpm install
pnpm dev                    # both dashboards in parallel
pnpm dev:customer           # one at a time
pnpm dev:internal
pnpm typecheck
pnpm build
```

Auth in dev is header-based — copy each app's `.env.example` to `.env.local`. Customer sends `X-Tenant-Id`/`X-Tenant-Role`; internal sends `X-Staff-Role`/`X-Staff-User`. Both APIs enable a named CORS policy (`DashboardDev`) only when `app.Environment.IsDevelopment()`; production must replace it with whatever real origin policy fronts the SPAs.

`TestTenantAuthHandler` validates the `X-Tenant-Id` against `orchestrator.tenants` — a header pointing at a non-existent tenant returns 401, just like a real JWT with an unknown subject would. The customer SPA's API client treats 401 as a stale-session signal: it wipes `localStorage["customer.tenant"]` and redirects to `/login`. Practical consequence: if you wipe the DB (`./scripts/dev-down.ps1 -Clean`) without clearing your browser, the next request auto-logs you out instead of silently creating orphan rows under a phantom tenant id.

Panels with no backing endpoint yet are mocked in `apps/*/src/mocks/` and marked `// TODO: replace with /v1/...` (or `/ops/...`). When a new endpoint lands, grep `TODO: replace` to find the mock to swap.
