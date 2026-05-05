/**
 * SignalR push channel for the customer dashboard. PublicApi-resident handlers (today: webhook
 * intake) push "dashboard:invalidate" events to the tenant's connection group; this hook
 * subscribes a single connection per session and invalidates the matching TanStack Query keys
 * so the SPA reacts in real time instead of polling.
 *
 * Design notes:
 * - One shared connection per app session (singleton). React StrictMode mounts twice in dev;
 *   the connection is reference-counted so the second mount/unmount reuses the same socket.
 * - Auth: the hub is anonymous over the wire (cookies/JWT can't reach a WebSocket from the
 *   browser without query-string ferrying). Once connected, the client calls SubscribeTenant
 *   with the tenant id from `useMeQuery` and the server validates against the tenants table.
 * - Backplane: production multi-pod deploys must add Redis (Microsoft.AspNetCore.SignalR.
 *   StackExchangeRedis) so an event published on pod A reaches a client connected to pod B.
 */
import { useEffect } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";

const HUB_BASE = (import.meta.env.VITE_API_BASE ?? "http://localhost:5101").replace(/\/$/, "");
const HUB_URL = `${HUB_BASE}/v1/realtime`;

interface InvalidatePayload {
  area?: string;
}

let connection: HubConnection | null = null;
let refCount = 0;
let currentTenantId: string | null = null;
const handlers = new Set<(eventName: string, payload: InvalidatePayload) => void>();

async function ensureConnection(): Promise<HubConnection> {
  if (connection) return connection;
  connection = new HubConnectionBuilder()
    .withUrl(HUB_URL, { withCredentials: true })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
  connection.on("dashboard:invalidate", (payload: InvalidatePayload) => {
    handlers.forEach((h) => h("dashboard:invalidate", payload ?? {}));
  });
  await connection.start();
  return connection;
}

async function subscribeTenant(tenantId: string): Promise<void> {
  if (currentTenantId === tenantId) return;
  const c = await ensureConnection();
  if (currentTenantId && currentTenantId !== tenantId) {
    try { await c.invoke("UnsubscribeTenant", currentTenantId); } catch { /* ignore */ }
  }
  await c.invoke("SubscribeTenant", tenantId);
  currentTenantId = tenantId;
}

async function teardown(): Promise<void> {
  if (!connection) return;
  if (currentTenantId) {
    try { await connection.invoke("UnsubscribeTenant", currentTenantId); } catch { /* ignore */ }
  }
  if (connection.state !== HubConnectionState.Disconnected) {
    try { await connection.stop(); } catch { /* ignore */ }
  }
  connection = null;
  currentTenantId = null;
}

/**
 * Subscribe the current session to the tenant's push channel. Pass `null` (e.g. when the
 * user is signed out) to skip subscription. The hook is safe to call from multiple
 * components; only one underlying SignalR connection is created.
 */
export function useRealtimeInvalidations(tenantId: string | null | undefined): void {
  const qc = useQueryClient();
  useEffect(() => {
    if (!tenantId) return;
    const handler = (eventName: string, payload: InvalidatePayload) => {
      if (eventName !== "dashboard:invalidate") return;
      // Best-effort: invalidate everything dashboard-shaped. Area-scoped invalidation
      // can be wired later by inspecting payload.area against query key prefixes.
      void qc.invalidateQueries();
      void payload;
    };
    handlers.add(handler);
    refCount += 1;
    let cancelled = false;
    void subscribeTenant(tenantId).catch((err) => {
      if (!cancelled) console.warn("[realtime] subscribe failed", err);
    });
    return () => {
      cancelled = true;
      handlers.delete(handler);
      refCount -= 1;
      if (refCount <= 0) {
        refCount = 0;
        void teardown();
      }
    };
  }, [tenantId, qc]);
}
