/**
 * Thin fetch wrapper for PublicApi (`/v1/...`).
 *
 * Auth is cookie-based: the magic-link verify endpoint sets an HTTP-only `so.session`
 * cookie. `credentials: 'include'` ships it on every request. The server picks the
 * current tenant off the session row, so no tenant header is sent from the SPA.
 *
 * 401 means the session is missing or revoked — bounce to /login.
 */
const BASE = (import.meta.env.VITE_API_BASE ?? "http://localhost:5101").replace(/\/$/, "");

export class ApiError extends Error {
  constructor(public readonly status: number, public readonly url: string, message: string) {
    super(message);
    this.name = "ApiError";
  }
}

export interface ApiOptions extends RequestInit {
  /** Set true to bypass the 401 → /login redirect (e.g. probing /v1/auth/me at boot). */
  silent401?: boolean;
}

export async function api<T>(path: string, init?: ApiOptions): Promise<T> {
  const url = `${BASE}${path.startsWith("/") ? path : `/${path}`}`;
  const headers = new Headers(init?.headers);
  headers.set("Accept", "application/json");
  if (init?.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");

  const res = await fetch(url, { ...init, credentials: "include", headers });
  if (!res.ok) {
    const body = await res.text().catch(() => "");
    if (res.status === 401 && !init?.silent401) {
      if (typeof window !== "undefined" && window.location.pathname !== "/login") {
        window.location.assign("/login");
      }
    }
    throw new ApiError(res.status, url, body || `${res.status} ${res.statusText}`);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}
