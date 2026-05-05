/**
 * Shared body-and-header passthrough used by every webhook route. Reads the raw bytes
 * (HMAC validation requires byte-identical body), copies the X-Shopify-* headers, and
 * POSTs to the orchestrator's webhook receiver. Any non-2xx from the orchestrator
 * surfaces back to Shopify so its automatic retry kicks in.
 */
export async function forwardWebhook(request) {
  const orchestrator = process.env.ORCHESTRATOR_URL ?? "http://localhost:5101";
  const target = `${orchestrator}/v1/webhooks/shopify`;

  const raw = await request.arrayBuffer();
  const headers = new Headers();
  for (const [key, value] of request.headers) {
    if (key.toLowerCase().startsWith("x-shopify-") || key.toLowerCase() === "content-type") {
      headers.set(key, value);
    }
  }

  const upstream = await fetch(target, {
    method: "POST",
    headers,
    body: raw,
  });

  if (!upstream.ok) {
    return new Response(`Orchestrator forward failed: ${upstream.status}`, { status: 502 });
  }
  return new Response(null, { status: 200 });
}
