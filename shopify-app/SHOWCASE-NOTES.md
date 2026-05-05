# Showcase Shopify app

A real Shopify Remix app that installs onto a real Shopify development store and connects it
to this orchestrator's onboarding flow. The app exists purely as the public-facing surface
that Shopify can reach: it forwards OAuth callbacks and webhook bodies straight through to
the orchestrator's PublicApi, which does the actual work.

> **Heads up**: this folder is **not** a finished scaffold. The Shopify CLI is interactive
> (it prompts for Partners login, app name, dev store, etc.) so we cannot generate the
> Remix project on your behalf. The route files in `app/routes-overlay/` and the env example
> here are designed to **drop into** a fresh CLI scaffold once you've created it.

## What you do once

1. Sign up at <https://partners.shopify.com> (free).
2. From the Partners dashboard, create a **development store** (Stores -> Add store ->
   Development store). Pick any name -- e.g. `acme-nl-dev`. Add 2-3 products with a non-zero
   shipping weight, and set a default checkout customer/address.
3. Install the Shopify CLI: `npm install -g @shopify/cli@latest @shopify/app@latest`.
4. From the repo root, scaffold the Remix app **into this folder**:
   ```bash
   cd shopify-app
   shopify app init --template=remix --flavor=javascript
   ```
   Pick the dev store and app name when prompted. The CLI populates
   `shopify.app.toml`, `package.json`, `app/`, etc.
5. Overlay the orchestrator-specific route files:
   ```bash
   cp -r app/routes-overlay/* app/routes/
   ```
6. Open `shopify.app.toml` and merge in these blocks (the CLI keeps `client_id` /
   `application_url` / `[build]` -- leave those alone):
   ```toml
   [access_scopes]
   scopes = "read_orders,write_fulfillments"

   [auth]
   redirect_urls = [
     "https://YOUR-CLI-TUNNEL/auth/orchestrator-forward"
   ]

   [[webhooks.subscriptions]]
   topics = ["orders/create"]
   uri = "/webhooks/orders/create"

   [[webhooks.subscriptions]]
   topics = ["orders/paid"]
   uri = "/webhooks/orders/paid"

   [[webhooks.subscriptions]]
   topics = ["fulfillments/create"]
   uri = "/webhooks/fulfillments/create"
   ```
   (Replace `YOUR-CLI-TUNNEL` with whatever URL `shopify app dev` printed; the CLI rewrites
   `application_url` for you on every run -- copy the same host into `redirect_urls`.)
7. Copy `.env.example` to `.env` and fill in the API key/secret from the Partners app
   credentials page.
8. Paste the **same** API key + secret into the orchestrator's
   `src/ShippingOrchestrator.PublicApi/appsettings.Development.json` under
   `Connectors:Shopify:ClientId` and `Connectors:Shopify:ClientSecret`.

## What you do every dev session

```bash
# From repo root, in one go:
./scripts/dev-up.ps1 -ShowcaseShopify
```

That brings up the orchestrator stack AND a `shopify-app` tab that runs `shopify app dev`.
The CLI prints a `https://*.trycloudflare.com` URL -- that's the Cloudflare quick-tunnel
Shopify uses to reach this Remix app. Webhooks land on the Remix routes, which forward to
the orchestrator at `http://localhost:5101`.

If the tunnel URL changes between runs, update `shopify.app.toml`'s `[auth].redirect_urls`
to match (the CLI updates `application_url` automatically).

## How the wiring works

```
Shopify dev store
   |
   |  orders/create webhook
   v
Cloudflare quick-tunnel (managed by Shopify CLI)
   |
   v
shopify-app (Remix, port 3000)
   |
   |  POST /v1/webhooks/shopify  (raw body + X-Shopify-Hmac-Sha256)
   v
orchestrator PublicApi (http://localhost:5101)
   - validates HMAC (Connectors:Shopify:Mode == Real)
   - translates JSON via ShopifyOrderTranslator
   - persists as PendingEcommerceOrder
```

OAuth follows the same shape:

```
operator clicks install URL on internal SPA
   v
Shopify /admin/oauth/authorize
   v
redirect to https://<tunnel>/auth/orchestrator-forward?code=...&state=<onboardingProcessId>
   v
Remix loader: 302 to http://localhost:5101/v1/onboarding/callback/shopify?code=...&state=...
   v
orchestrator exchanges code -> access token, advances onboarding, creates EcommerceConnection
```

## Why a Remix app at all if Remix doesn't do the OAuth?

Two reasons:

1. **Shopify Partners apps require a public `application_url`**. The CLI's tunnel makes
   our localhost reachable. The Remix app is the thing the tunnel terminates at.
2. **Embedded admin UI**. After install, Shopify shows the app in the merchant's admin --
   we use that surface for a "Connected to ShippingOrchestrator" card and a deep link
   into the operator dashboard. See `app/routes-overlay/app._index.jsx`.

Production deployments would replace the CLI tunnel with the company's own domain (ALB /
CDN / whatever fronts the orchestrator), keeping the same Remix app as the embedded admin
surface.
