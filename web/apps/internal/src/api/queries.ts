import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api, ApiError } from "./client";
import type {
  OpsBatchRow,
  OpsShipmentRow,
  OpsCarrierKpi,
  OpsTenantRow,
  OnboardingFlowSummary,
  OnboardingProcessView,
  TenantDetailView,
  SimulateOrderResponse,
  CustomerShipmentView,
  CustomerBatchView,
  OpsIngestionFailureRow,
  OpsIngestionFailureStatGroup,
} from "../types/api";

export function useExceptionsQuery({ take = 20, skip = 0 } = {}) {
  return useQuery({
    queryKey: ["ops", "exceptions", { take, skip }] as const,
    queryFn: () => api<OpsShipmentRow[]>(`/ops/exceptions?take=${take}&skip=${skip}`),
    staleTime: 15_000,
  });
}

export function useBatchesQuery({ status, take = 20, skip = 0 }: { status?: string; take?: number; skip?: number } = {}) {
  return useQuery({
    queryKey: ["ops", "queues", { status, take, skip }] as const,
    queryFn: () => {
      const params = new URLSearchParams({ take: String(take), skip: String(skip) });
      if (status) params.set("status", status);
      return api<OpsBatchRow[]>(`/ops/queues?${params.toString()}`);
    },
    staleTime: 15_000,
  });
}

export interface CarrierKpiRange {
  /** "yyyy-MM-dd" — defaults to last 7 days when omitted */
  from?: string;
  to?: string;
}

function isoDay(offsetDays: number): string {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + offsetDays);
  return d.toISOString().slice(0, 10);
}

export function useCarrierKpisQuery({ from, to }: CarrierKpiRange = {}) {
  const fromDate = from ?? isoDay(-7);
  const toDate = to ?? isoDay(0);
  return useQuery({
    queryKey: ["ops", "carrier-kpis", { from: fromDate, to: toDate }] as const,
    queryFn: () => api<OpsCarrierKpi[]>(`/ops/kpis/carrier-success-rate?from=${fromDate}&to=${toDate}`),
    staleTime: 60_000,
  });
}

// --- Ingestion failures (ops) ------------------------------------------------

export interface OpsIngestionFailureFilter {
  tenantId?: string;
  connectorCode?: string;
  reasonCode?: string;
  status?: string;
  from?: string;
  to?: string;
  take?: number;
  skip?: number;
}

export function useOpsIngestionFailuresQuery(filter: OpsIngestionFailureFilter = {}) {
  const params = new URLSearchParams();
  if (filter.tenantId) params.set("tenantId", filter.tenantId);
  if (filter.connectorCode) params.set("connectorCode", filter.connectorCode);
  if (filter.reasonCode) params.set("reasonCode", filter.reasonCode);
  if (filter.status) params.set("status", filter.status);
  if (filter.from) params.set("from", filter.from);
  if (filter.to) params.set("to", filter.to);
  params.set("take", String(filter.take ?? 100));
  params.set("skip", String(filter.skip ?? 0));
  return useQuery({
    queryKey: ["ops", "ingestion-failures", filter] as const,
    queryFn: () => api<OpsIngestionFailureRow[]>(`/ops/ingestion-failures?${params.toString()}`),
    staleTime: 15_000,
    refetchInterval: 15_000,
  });
}

export function useOpsIngestionFailureStatsQuery(window: string = "24h") {
  return useQuery({
    queryKey: ["ops", "ingestion-failures", "stats", window] as const,
    queryFn: () => api<OpsIngestionFailureStatGroup[]>(`/ops/ingestion-failures/stats?window=${window}`),
    staleTime: 30_000,
    refetchInterval: 30_000,
  });
}

export function useOpsDismissIngestionFailureMutation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (failureId: string) =>
      api<void>(`/ops/ingestion-failures/${failureId}/dismiss`, { method: "POST" }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["ops", "ingestion-failures"] });
    },
  });
}

export interface OpsRecheckIngestionFailureResponse {
  outcome: "resolved" | "still_failing";
  pendingOrderId?: string;
  detail?: string;
}

export function useOpsRecheckIngestionFailureMutation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (failureId: string) =>
      api<OpsRecheckIngestionFailureResponse>(
        `/ops/ingestion-failures/${failureId}/recheck`,
        { method: "POST" },
      ),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["ops", "ingestion-failures"] });
    },
  });
}

// --- Onboarding ---------------------------------------------------------------

export function useOnboardingFlowsQuery() {
  return useQuery({
    queryKey: ["onboarding", "flows"] as const,
    queryFn: () => api<OnboardingFlowSummary[]>("/admin/onboarding/flows"),
    staleTime: 5 * 60_000,
  });
}

export function useOnboardingProcessesQuery() {
  return useQuery({
    queryKey: ["onboarding", "processes"] as const,
    queryFn: () => api<OnboardingProcessView[]>("/admin/onboarding"),
    staleTime: 5_000,
  });
}

export function useOnboardingProcessQuery(processId: string | undefined) {
  return useQuery({
    queryKey: ["onboarding", "process", processId] as const,
    queryFn: () => api<OnboardingProcessView>(`/admin/onboarding/${processId}`),
    enabled: !!processId,
    refetchInterval: (query) => {
      // Keep polling while a step is awaiting external resolution (OAuth callback) or the
      // process auto-progresses through an automatic step. Stops on terminal states.
      const data = query.state.data as OnboardingProcessView | undefined;
      if (!data) return 1500;
      if (data.status !== "InProgress") return false;
      const hasMoving = data.steps.some(
        (s) => s.status === "Awaiting" || s.status === "Pending",
      );
      return hasMoving ? 1500 : 5_000;
    },
  });
}

export function useStartOnboardingMutation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { flowCode: string; contactEmail?: string | null }) =>
      api<{ processId: string }>("/admin/onboarding/", { method: "POST", body: JSON.stringify(body) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["onboarding", "processes"] }),
  });
}

export function useAdvanceOnboardingStepMutation(processId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (vars: { stepCode: string; payload: unknown }) =>
      api<{ stepCode: string; status: string; failureReason?: string }>(
        `/admin/onboarding/${processId}/steps/${vars.stepCode}/advance`,
        { method: "POST", body: JSON.stringify(vars.payload ?? null) },
      ),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["onboarding", "process", processId] }),
  });
}

export function useSimulateShopifyCallbackMutation(processId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () =>
      api<void>(`/admin/onboarding/${processId}/simulate-shopify-callback`, { method: "POST" }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["onboarding", "process", processId] }),
  });
}

export function useCancelOnboardingMutation(processId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (reason?: string) =>
      api<void>(`/admin/onboarding/${processId}/cancel`, {
        method: "POST",
        body: JSON.stringify({ reason: reason ?? null }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["onboarding", "process", processId] }),
  });
}

// --- Tenants ------------------------------------------------------------------

export function useTenantsListQuery({ take = 50, skip = 0 } = {}) {
  return useQuery({
    queryKey: ["admin", "tenants", { take, skip }] as const,
    queryFn: () => api<OpsTenantRow[]>(`/admin/tenants?take=${take}&skip=${skip}`),
    staleTime: 15_000,
  });
}

export function useTenantDetailQuery(tenantId: string | undefined) {
  return useQuery({
    queryKey: ["tenant", tenantId] as const,
    queryFn: () => api<TenantDetailView>(`/admin/tenants/${tenantId}`),
    enabled: !!tenantId,
    staleTime: 10_000,
  });
}

export interface SimulateOrderInput {
  originCountry?: string;
  destinationCountry?: string;
  weightGrams?: number;
  description?: string;
}

export function useSimulateOrderMutation(tenantId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: SimulateOrderInput) =>
      api<SimulateOrderResponse>(`/admin/tenants/${tenantId}/simulate-order`, {
        method: "POST",
        body: JSON.stringify(input),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tenant", tenantId] }),
  });
}

export function useSuspendTenantMutation(tenantId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (reason: string) =>
      api<void>(`/admin/tenants/${tenantId}/suspend`, {
        method: "POST",
        body: JSON.stringify({ reason }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tenant", tenantId] }),
  });
}

export function useReverifyConnectionMutation(tenantId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (connectionId: string) =>
      api<{ status: string; rejectReason: string | null }>(
        `/admin/tenants/${tenantId}/connections/${connectionId}/re-verify`,
        { method: "POST" },
      ),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tenant", tenantId] }),
  });
}

// --- Shipment / batch (PrivateApi has no read shape today; reach across to PublicApi) ---

const PUBLIC_API_BASE = (import.meta.env.VITE_PUBLIC_API_BASE ?? "http://localhost:5101").replace(/\/$/, "");

async function publicTenantFetch<T>(tenantId: string, path: string): Promise<T> {
  const url = `${PUBLIC_API_BASE}${path.startsWith("/") ? path : `/${path}`}`;
  const res = await fetch(url, {
    headers: {
      Accept: "application/json",
      "X-Tenant-Id": tenantId,
      "X-Tenant-Role": "tenant",
    },
  });
  if (!res.ok) {
    const body = await res.text().catch(() => "");
    throw new ApiError(res.status, url, body || `${res.status} ${res.statusText}`);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export function useTenantBatchQuery(tenantId: string | undefined, batchId: string | undefined) {
  return useQuery({
    queryKey: ["tenant-batch", tenantId, batchId] as const,
    queryFn: () => publicTenantFetch<CustomerBatchView>(tenantId!, `/v1/shipments/batches/${batchId}`),
    enabled: !!tenantId && !!batchId,
    refetchInterval: (query) => {
      const data = query.state.data as CustomerBatchView | undefined;
      if (!data) return 1000;
      if (data.status === "Completed" || data.status === "Failed" || data.status === "PartiallyFailed") {
        // Keep polling briefly while shipment-level updates settle (carrier select → label →
        // tracking) — they trail the batch terminal state by a few hundred ms.
        const allSettled = data.shipments.every((s) =>
          ["Labeled", "InTransit", "Delivered", "Failed", "Cancelled"].includes(s.status));
        return allSettled ? false : 1000;
      }
      return 1000;
    },
  });
}

export function useTenantShipmentQuery(tenantId: string | undefined, shipmentId: string | undefined) {
  return useQuery({
    queryKey: ["tenant-shipment", tenantId, shipmentId] as const,
    queryFn: () => publicTenantFetch<CustomerShipmentView>(tenantId!, `/v1/shipments/${shipmentId}`),
    enabled: !!tenantId && !!shipmentId,
    refetchInterval: 2000,
  });
}
