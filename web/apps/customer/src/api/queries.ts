import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import type {
  CustomerShipmentView,
  CustomerBatchView,
  CurrentTenantView,
  PendingOrderView,
  BundlePendingOrdersResponse,
  ConnectionsListResponse,
  InstallGuide,
  StartConnectionInstallResponse,
  CustomerIngestionFailureView,
  OpenIngestionFailureCount,
} from "../types/api";

export interface ListShipmentsParams {
  take?: number;
  skip?: number;
}

export function useShipmentsQuery({ take = 20, skip = 0 }: ListShipmentsParams = {}) {
  return useQuery({
    queryKey: ["customer", "shipments", { take, skip }] as const,
    queryFn: () =>
      api<CustomerShipmentView[]>(`/v1/dashboard/shipments?take=${take}&skip=${skip}`),
    staleTime: 30_000,
  });
}

export function useBatchQuery(batchId: string | undefined) {
  return useQuery({
    queryKey: ["customer", "batch", batchId] as const,
    queryFn: () => api<CustomerBatchView>(`/v1/shipments/batches/${batchId}`),
    enabled: !!batchId,
  });
}

export function useShipmentQuery(shipmentId: string | undefined) {
  return useQuery({
    queryKey: ["customer", "shipment", shipmentId] as const,
    queryFn: () => api<CustomerShipmentView>(`/v1/shipments/${shipmentId}`),
    enabled: !!shipmentId,
    refetchInterval: (query) => {
      const data = query.state.data as CustomerShipmentView | undefined;
      if (!data) return 2000;
      return data.status === "Delivered" || data.status === "Failed" || data.status === "Cancelled" ? false : 2000;
    },
  });
}

export function useCurrentTenantQuery() {
  return useQuery({
    queryKey: ["customer", "current-tenant"] as const,
    queryFn: () => api<CurrentTenantView>("/v1/dashboard/tenant"),
    staleTime: 60_000,
  });
}

export function usePendingOrdersQuery() {
  return useQuery({
    queryKey: ["customer", "pending-orders"] as const,
    queryFn: () => api<PendingOrderView[]>("/v1/dashboard/orders/pending?take=100"),
    staleTime: 5_000,
    refetchInterval: 5_000,
  });
}

export interface BundlePendingOrdersInput {
  orderIds: string[];
  idempotencyKey?: string;
}

export function useBundlePendingOrdersMutation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: BundlePendingOrdersInput) =>
      api<BundlePendingOrdersResponse>("/v1/dashboard/orders/bundle", {
        method: "POST",
        body: JSON.stringify({
          orderIds: input.orderIds,
          idempotencyKey: input.idempotencyKey ?? null,
        }),
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["customer", "pending-orders"] });
      qc.invalidateQueries({ queryKey: ["customer", "batches"] });
      qc.invalidateQueries({ queryKey: ["customer", "shipments"] });
    },
  });
}

export interface ListBatchesParams {
  take?: number;
  skip?: number;
  status?: string;
}

export function useBatchesQuery({ take = 50, skip = 0, status }: ListBatchesParams = {}) {
  const params = new URLSearchParams({ take: String(take), skip: String(skip) });
  if (status) params.set("status", status);
  return useQuery({
    queryKey: ["customer", "batches", { take, skip, status }] as const,
    queryFn: () => api<CustomerBatchView[]>(`/v1/dashboard/batches?${params.toString()}`),
    staleTime: 5_000,
  });
}

export function useEcommerceConnectionsQuery() {
  return useQuery({
    queryKey: ["customer", "connections"] as const,
    queryFn: () => api<ConnectionsListResponse>("/v1/dashboard/connections"),
    staleTime: 5_000,
    refetchInterval: 10_000,
  });
}

export function useInstallGuideQuery(platformCode: string | undefined) {
  return useQuery({
    queryKey: ["customer", "install-guide", platformCode] as const,
    queryFn: () =>
      api<InstallGuide>(
        `/v1/dashboard/connections/${encodeURIComponent(platformCode!)}/install-guide`,
      ),
    enabled: !!platformCode,
    staleTime: 60_000,
  });
}

export interface StartConnectionInstallInput {
  platformCode: string;
  externalAccountId: string;
}

export function useStartConnectionInstallMutation() {
  return useMutation({
    mutationFn: (input: StartConnectionInstallInput) =>
      api<StartConnectionInstallResponse>(
        `/v1/dashboard/connections/${encodeURIComponent(input.platformCode)}/start`,
        {
          method: "POST",
          body: JSON.stringify({ externalAccountId: input.externalAccountId, redirectUri: null }),
        },
      ),
  });
}

export function useDisconnectConnectionMutation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: { connectionId: string; reason?: string }) =>
      api<void>(`/v1/dashboard/connections/${input.connectionId}/disconnect`, {
        method: "POST",
        body: JSON.stringify({ reason: input.reason ?? "tenant requested" }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["customer", "connections"] }),
  });
}

export interface ListIngestionFailuresParams {
  status?: string;
  take?: number;
  skip?: number;
}

export function useIngestionFailuresQuery({ status = "Open", take = 100, skip = 0 }: ListIngestionFailuresParams = {}) {
  const params = new URLSearchParams({ take: String(take), skip: String(skip) });
  if (status) params.set("status", status);
  return useQuery({
    queryKey: ["customer", "ingestion-failures", { status, take, skip }] as const,
    queryFn: () =>
      api<CustomerIngestionFailureView[]>(`/v1/dashboard/orders/needs-attention?${params.toString()}`),
    staleTime: 15_000,
    refetchInterval: 15_000,
  });
}

export function useOpenIngestionFailureCountQuery() {
  return useQuery({
    queryKey: ["customer", "ingestion-failures", "open-count"] as const,
    queryFn: () => api<OpenIngestionFailureCount>("/v1/dashboard/orders/needs-attention/count"),
    staleTime: 15_000,
    refetchInterval: 15_000,
  });
}

export function useDismissIngestionFailureMutation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (failureId: string) =>
      api<void>(`/v1/dashboard/orders/needs-attention/${failureId}/dismiss`, {
        method: "POST",
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["customer", "ingestion-failures"] });
    },
  });
}

export interface RecheckIngestionFailureResponse {
  outcome: "resolved" | "still_failing";
  pendingOrderId?: string;
  detail?: string;
}

export function useRecheckIngestionFailureMutation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (failureId: string) =>
      api<RecheckIngestionFailureResponse>(
        `/v1/dashboard/orders/needs-attention/${failureId}/recheck`,
        { method: "POST" },
      ),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["customer", "ingestion-failures"] });
      qc.invalidateQueries({ queryKey: ["customer", "pending-orders"] });
    },
  });
}

