import { redirect } from "@remix-run/node";

/**
 * OAuth-callback bridge.
 *
 * Shopify redirects the merchant's browser here after they authorize the install. The
 * orchestrator wants the `code` + `state` itself so its existing CompleteEcommerceOAuth
 * handler can do the token exchange. We just 302 the browser to the orchestrator's
 * anonymous onboarding callback -- it lives on localhost, which the merchant's browser
 * can reach because the merchant is sitting at this developer's machine.
 *
 * `state` is the OnboardingProcessId set on the install URL by the internal SPA.
 */
export async function loader({ request }) {
  const url = new URL(request.url);
  const code = url.searchParams.get("code");
  const state = url.searchParams.get("state");
  const shop = url.searchParams.get("shop");

  if (!code || !state) {
    return new Response("Missing code or state from Shopify callback.", { status: 400 });
  }

  const orchestrator = process.env.ORCHESTRATOR_URL ?? "http://localhost:5101";
  const target = new URL(`${orchestrator}/v1/onboarding/callback/shopify`);
  target.searchParams.set("code", code);
  target.searchParams.set("state", state);
  if (shop) target.searchParams.set("shop", shop);

  return redirect(target.toString());
}
