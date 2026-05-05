---
name: dev-restart
description: Restart shipping-orchestrator dev services (PublicApi, PrivateApi, Worker, customer SPA, internal SPA, WordPress/WooCommerce stack). Trigger autonomously whenever a config change (launchSettings.json, appsettings*.json, env vars, Connectors:* options, EF migrations) needs to take effect, or whenever the user reports stale behaviour matching "still showing old", "didn't pick up", "wrong port/url", or after editing files known to require a restart.
---

# Dev restart

Use this whenever a code/config change requires a process bounce to take effect. Don't ask first — just restart, then continue. Background the long-running ones via `run_in_background: true` so the conversation isn't blocked.

## When to fire automatically

- Edited `launchSettings.json`, `appsettings*.json`, or `Directory.*.props`
- Edited any `Program.cs`, options class (`*Options.cs`), or DI registration
- Added/changed an EF migration
- Edited a connector module (`*ConnectorModule.cs`) or its config section
- Changed a Wolverine handler, message contract, or saga
- User says: "didn't take", "old config", "still 5111", "stale", "not picking up", "after restart"

If only changing read-side projection / SPA code and the relevant SPA dev server is hot-reloading, skip the SPA restart.

## Service map

| Service | Project / dir | Run command (background) | Health check |
|---|---|---|---|
| PublicApi (HTTP 5101 + HTTPS 5111 if Showcase) | `src/ShippingOrchestrator.PublicApi` | `dotnet run --project src/ShippingOrchestrator.PublicApi --launch-profile Showcase` | `curl -s http://localhost:5101/healthz` |
| PrivateApi (HTTP 5102) | `src/ShippingOrchestrator.PrivateApi` | `dotnet run --project src/ShippingOrchestrator.PrivateApi` | `curl -s http://localhost:5102/healthz` |
| Worker | `src/ShippingOrchestrator.Worker` | `dotnet run --project src/ShippingOrchestrator.Worker` | log line `Application started` |
| Customer SPA (5173) | `web/apps/customer` | `pnpm --filter @ship/customer dev` (cwd `web/`) | `curl -s http://localhost:5173` |
| Internal SPA (5174) | `web/apps/internal` | `pnpm --filter @ship/internal dev` (cwd `web/`) | `curl -s http://localhost:5174` |
| WordPress + WooCommerce | `woocommerce/docker-compose.yml` | `docker compose -f woocommerce/docker-compose.yml restart` | `curl -s http://localhost:8080/wp-json` |
| Local infra (Postgres, LocalStack, Jaeger, Mailpit) | `docker-compose.yml` | `docker compose restart` | `docker compose ps` |

The default `PublicApi` launch profile only listens on HTTP 5101. The `Showcase` profile listens on both 5101 and 5111 and sets `Connectors:WooCommerce:Mode=Real`. Pick `Showcase` whenever testing real WC/Shopify connectors; pick the default for plain dev.

## Procedure

### 1. Find the running process by listening port (Windows)

```bash
netstat -ano | grep ':5101\|:5102\|:5173\|:5174'
```

The last column is the PID.

### 2. Kill it

```bash
# Windows
powershell -NoProfile -Command "Stop-Process -Id <PID> -Force"

# or by image name (kills ALL matching — use cautiously)
powershell -NoProfile -Command "Get-Process -Name 'ShippingOrchestrator.PublicApi' -ErrorAction SilentlyContinue | Stop-Process -Force"
```

For SPAs it's a `node` process under `pnpm dev`. Identify by port (step 1).

### 3. Restart in background

Use Bash `run_in_background: true`. Example for PublicApi (Showcase profile, the common case):

```bash
dotnet run --project src/ShippingOrchestrator.PublicApi --launch-profile Showcase
```

For SPAs run from `web/`:

```bash
cd web && pnpm --filter @ship/customer dev
```

### 4. Verify boot

Wait until the health check passes. For PublicApi, also confirm the WC config log line:

```
WooCommerce config bound: OrchestratorWebhookUrl=... CallbackBaseUrl=...
```

That line is the canonical proof the new env was picked up. If it still shows the old value, env shadow exists in shell or Rider run config — not a launchSettings issue.

### 5. Tail logs on demand

```bash
# Last 100 lines of background dotnet run output via the BashOutput tool
# (use the bash_id returned when you started the process)
```

## Common gotchas

- **launchSettings.json env vars are only read at process start.** No hot reload, no IConfigurationProvider reload. A restart is mandatory.
- **`dotnet run` doesn't kill its child on Ctrl-C in some shells.** Always check the listening port is free before re-launching, otherwise you get `address already in use`.
- **Docker compose `restart` doesn't pick up image rebuilds.** Use `up -d --build` if you changed a Dockerfile.
- **WC mu-plugins** are mounted read-only into the WP container. Edits to `woocommerce/mu-plugins/*.php` apply on next request — no container restart needed.
- **EF migrations** run automatically on each host's startup via `Database.MigrateAsync()`. If a migration was added, just restart the host that owns that DbContext (PublicApi → Customer; PrivateApi → Operations; Worker → Orchestrator + read sides).

## What NOT to restart

- Postgres / LocalStack containers when only application code changed — wastes 30s of startup time.
- The whole `docker-compose.yml` stack when only WP needs a bounce — use the `woocommerce/` compose file alone.
- All SPAs when only one SPA's app code changed (Vite hot-reloads).
