import { forwardWebhook } from "../utils/forward-webhook";

export async function action({ request }) {
  return forwardWebhook(request);
}
