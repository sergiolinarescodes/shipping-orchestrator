import { forwardWebhook } from "../utils/forward-webhook";

/**
 * Shopify -> Remix forwarder for orders/create. Delegates HMAC validation and the
 * normalize-and-persist pipeline to the orchestrator at /v1/webhooks/shopify.
 */
export async function action({ request }) {
  return forwardWebhook(request);
}
