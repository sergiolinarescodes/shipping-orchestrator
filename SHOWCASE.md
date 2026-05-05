# End-to-end Shopify showcase

Walk a real Shopify dev store all the way through onboarding, order ingestion, batch
generation, and the print stub of the customer dashboard. The whole thing runs locally
against the existing orchestrator stack (Postgres, LocalStack, Jaeger, Mailpit) plus a
single extra process: the Shopify Remix app in `shopify-app/`.

## Prerequisites (one-time)

| Tool | Version | Why |
| --- | --- | --- |
| .NET SDK | 10.0.201 (pinned in `global.json`) | Build and run the orchestrator. |
| Docker Desktop | running | Postgres + LocalStack + Jaeger + Mailpit. |
| Node | 20+ | Runs the SPAs and the Shopify Remix app. |
| Shopify CLI | 3.62+ | `npm install -g @shopify/cli @shopify/app`. |
| Shopify Partners account | free | <https://partners.shopify.com>. |
| Shopify dev store | free | Created from Partners -> Stores. |

## One-time setup

1. **Create the Shopify Partners app** (interactive).
   ```bash
   cd shopify-app
   shopify app init --template=remix --flavor=javascript
   ```
   The CLI prompts for store + app name. It generates `package.json`, `app/`,
   `shopify.app.toml`, and prints a `client_id` once the Partners app is created.

2. **Overlay the orchestrator-specific routes** that ship with this repo:
   ```bash
   cp -r app/routes-overlay/* app/routes/
   mkdir -p app/utils
   cp -r app/utils-overlay/* app/utils/
   ```
   Three webhook routes + one OAuth bridge + a Polaris card for the embedded admin UI.

3. **Patch `shopify.app.toml`** -- merge in the access scopes, redirect URL, and webhook
   subscriptions. See `shopify-app/README.md` for the exact blocks.

4. **Wire credentials**.
   - Copy `shopify-app/.env.example` to `shopify-app/.env`. Shopify CLI populates
     `SHOPIFY_API_KEY` / `SHOPIFY_API_SECRET` automatically when `shopify app dev` runs.
   - Paste the **same** ClientId into
     `src/ShippingOrchestrator.PublicApi/appsettings.Development.json` under
     `Connectors:Shopify:ClientId` (the ClientId is public — Shopify Partners shows it
     on the app config page and it is safe to commit).
   - **The ClientSecret is a real secret. Do NOT paste it into any committed JSON file.**
     Use .NET user-secrets instead (already wired in `PublicApi.csproj` via
     `<UserSecretsId>shipping-orchestrator-public-api</UserSecretsId>`):
     ```bash
     dotnet user-secrets set "Connectors:Shopify:ClientSecret" "shpss_..." \
       --project src/ShippingOrchestrator.PublicApi
     ```
     ASP.NET auto-loads user-secrets when the host runs in Development environment, so
     PublicApi reads the secret transparently. The store lives at
     `%APPDATA%\Microsoft\UserSecrets\shipping-orchestrator-public-api\secrets.json`
     (Windows) or `~/.microsoft/usersecrets/...` (Linux/Mac) — outside the repo.
   - Leave `Mode` as `InMemory` in `appsettings.Development.json`. The Showcase launch
     profile flips it to `Real` via env var when you run with `-ShowcaseShopify`.

## Boot the stack

```powershell
.\scripts\dev-up.ps1 -ShowcaseShopify
```

The script:
- starts Postgres, LocalStack, Jaeger, Mailpit
- pre-builds the solution (avoids CS2012 file-lock races)
- opens Windows Terminal tabs for: compose, public-api (in `Showcase` profile),
  private-api, worker, ui-customer, ui-internal, **shopify-app**

The `shopify-app` tab runs `shopify app dev`, which creates a Cloudflare quick-tunnel and
prints a URL like `https://abc-123.trycloudflare.com`. **Copy that URL** -- you need it for
the next step.

> The CLI rotates this URL every time you run `shopify app dev`. If you've used the
> showcase before, update `shopify.app.toml`'s `[auth].redirect_urls[0]` to the new URL,
> or run `shopify app deploy` to push the rewritten redirect to Partners.

For convenience, set it once for the internal SPA so the OAuth step pre-fills the right
redirect URI:

```bash
echo "VITE_SHOPIFY_TUNNEL_URL=https://abc-123.trycloudflare.com" > web/apps/internal/.env.local
```

(Then restart the `ui-internal` tab.)

## Run the showcase

| URL | What it is |
| --- | --- |
| <http://localhost:5174> | Internal (operator) SPA |
| <http://localhost:5173> | Customer SPA |
| <http://localhost:5101/scalar> | PublicApi OpenAPI explorer |
| <http://localhost:16686> | Jaeger traces |
| `https://*.trycloudflare.com` | Shopify-reachable tunnel into the Remix app |

Walk-through:

1. **Operator: start onboarding.** Internal SPA -> Operations -> Onboarding -> Start
   manual flow. Pick "Shopify + PostNL" template. Step `tenant.create` -> fill in display
   name + contact email -> Advance.
2. **Operator: connect Shopify.** Step `connection.shopify.start` -> shop domain is
   `<yourstore>.myshopify.com`, redirect URI is pre-filled from
   `VITE_SHOPIFY_TUNNEL_URL` -> Generate install URL -> open the URL. Authorize on the
   dev store. Shopify redirects to the Cloudflare tunnel, the Remix app 302s the browser
   to `localhost:5101/v1/onboarding/callback/shopify`, the orchestrator exchanges the code,
   and the wizard auto-advances. Step `carrier.assign.postnl` -> select PostNL -> Advance.
   Step `activate` -> Confirm.
3. **Place a test order.** In the dev-store storefront, add 2-3 products to cart and
   checkout (any test gateway works). Repeat 2 more times.
4. **Watch ingestion.** `worker` and `public-api` tabs log a `POST /v1/webhooks/shopify`
   per order. The Remix app's `webhooks.orders.create` route forwards each one with the
   HMAC header intact; the orchestrator validates HMAC (because Mode=Real) and persists
   each order as a `PendingEcommerceOrder`.
5. **Customer: bundle pending orders.** Customer SPA -> Pending orders. Three rows.
   Select all -> bulk-action toolbar -> "Bundle 3 orders". The orchestrator builds one
   shipment batch via `CreateShipmentBatchCommand`, the worker requests labels from the
   PostNL simulator, and the customer is navigated to the batch detail page.
6. **Print stub.** Customer SPA -> Batches -> [open the new batch] -> "Print labels".
   The modal lists the three tracking numbers with a banner explaining PDF generation is
   not enabled in this build, and the primary CTA is disabled.
7. **Operator confirmation.** Internal SPA -> Operations console -> the same batch
   appears under recent batches with `Completed` status.

## Useful checks

- `dotnet test` -- runs the full suite, including the E2E happy-path that drives the
  pending -> bundle -> labeled -> tracking sequence end-to-end.
- `corepack pnpm --filter @ship/dashboard-customer typecheck` -- verifies the customer
  SPA after edits.
- `docker compose logs -f postgres` -- inspect the `orchestrator.pending_ecommerce_orders`
  table grow as webhooks land.

## Troubleshooting

- **`401 Unauthorized` on `/v1/webhooks/shopify`** -- HMAC mismatch. Confirm the
  `ClientSecret` in `appsettings.Development.json` matches the Partners app secret.
- **`404 No tenant connection registered for shop`** -- the OAuth callback hasn't
  completed. The shop domain must match the value entered in step 2 of onboarding.
- **`shopify app dev` shows a different URL than `shopify.app.toml`** -- the CLI
  regenerates the tunnel each run. Update `redirect_urls` in the toml before placing the
  test order; webhook subscriptions auto-update on `shopify app deploy`.
- **`shopify` CLI not on PATH after `npm install -g`** -- restart the shell. On Windows,
  npm globals live in `%APPDATA%\npm` which must be on PATH.
- **No batches appear after bundling** -- check the `worker` tab. The PostNL simulator
  has a small randomized failure probability for realism; failed shipments are visible in
  the operator's "Exceptions" page.

## What is NOT in this showcase

- Real PDF labels. The carrier connector returns a placeholder `LabelUri`; the print
  button opens a modal explaining the stub.
- Real production auth. Both APIs run with the dev-only `TestTenantAuthHandler` /
  `TestStaffAuthHandler` schemes. Production must wire a JWT bearer.
- Multi-tenant isolation testing. The showcase uses a single tenant per onboarding flow
  end-to-end.
