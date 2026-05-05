# Bundled WordPress + WooCommerce showcase

Runs WordPress + WooCommerce on `http://localhost:8080` so the orchestrator showcase can
demo a real WC merchant install end-to-end. WP talks to the orchestrator at
`http://host.docker.internal:5101` (no tunnel needed — they share the host).

## Quickstart

```bash
cd woocommerce
docker compose up -d
# wait ~30s for the wp-cli container to seed the store, then:
open http://localhost:8080/wp-admin     # macOS
# or just visit the URL
```

Login: `admin` / `admin`.

WooCommerce is preinstalled and the store is preconfigured for NL/EUR so PostNL labels make
sense without further setup. Add a couple of products via *Products → Add new* in the WP admin
to have something to order.

## Connecting to the orchestrator

1. Boot the orchestrator stack with `pwsh ./scripts/dev-up.ps1 -ShowcaseWooCommerce` (this
   adds a `woocommerce` tab that runs `docker compose up` here).
2. Open the customer dashboard at `http://localhost:5173/connections`.
3. Click **Connect** under **WooCommerce**.
4. Enter `http://localhost:8080` as the store URL → *Continue*.
5. Your browser is redirected to `http://localhost:8080/wc-auth/v1/authorize?...` — the
   built-in WC Authentication Endpoint. You're already logged into wp-admin, so it shows the
   approve screen immediately.
6. Click **Approve** → WP POSTs the consumer key + secret back to the orchestrator at
   `http://localhost:5101/v1/connections/dashboard-callback/woocommerce`.
7. The orchestrator persists an `EcommerceConnection` for your tenant and auto-registers
   `order.created` / `order.updated` webhooks pointing at
   `http://host.docker.internal:5101/v1/webhooks/woocommerce`.

## Cross-test with Shopify

If you also ran `dev-up.ps1 -ShowcaseShopify -ShowcaseWooCommerce` and connected a Shopify
store under the same tenant, every order placed in *either* store lands in the same Pending
orders inbox at `http://localhost:5173/pending`. Select a Shopify row + a WooCommerce row,
hit **Bundle**, and watch a single batch carry both shipments through the carrier connector.

## Disconnect / reconnect

`Connections` page → **Disconnect** flips the connection to `Disconnected` (orchestrator
stops accepting webhooks for it). **Reconnect** flips it back to `Active` without rerunning
the auth flow — for full re-install (rotated keys), use the *Connect* button to start over.

## Tear down

```bash
docker compose down            # keep volumes
docker compose down -v         # also wipe WP data + DB
```
