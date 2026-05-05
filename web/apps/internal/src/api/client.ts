/**
 * Thin fetch wrapper for PrivateApi (`/admin/...`, `/ops/...`).
 * Sends the dev `X-Staff-Role` / `X-Staff-User` headers from Vite env vars
 * so the existing TestStaffAuthHandler accepts requests in local dev.
 */
const BASE = (import.meta.env.VITE_API_BASE ?? "http://localhost:5102").replace(/\/$/, "");
const STAFF_ROLE = import.meta.env.VITE_STAFF_ROLE ?? "";
const STAFF_USER = import.meta.env.VITE_STAFF_USER ?? "staff";

export class ApiError extends Error {
  constructor(public readonly status: number, public readonly url: string, message: string) {
    super(message);
    this.name = "ApiError";
  }
}

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const url = `${BASE}${path.startsWith("/") ? path : `/${path}`}`;
  const headers = new Headers(init?.headers);
  headers.set("Accept", "application/json");
  if (init?.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  if (STAFF_ROLE) headers.set("X-Staff-Role", STAFF_ROLE);
  if (STAFF_USER) headers.set("X-Staff-User", STAFF_USER);

  const res = await fetch(url, { ...init, headers });
  if (!res.ok) {
    const body = await res.text().catch(() => "");
    throw new ApiError(res.status, url, body || `${res.status} ${res.statusText}`);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}
