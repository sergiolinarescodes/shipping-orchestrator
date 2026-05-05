import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";

export interface SessionTenantDto {
  tenantId: string;
  displayName: string;
  status: string;
  role: "Owner" | "Member";
}

export interface SessionAccountDto {
  accountId: string;
  email: string;
  displayName: string | null;
}

export interface SessionMeResponse {
  account: SessionAccountDto;
  currentTenantId: string | null;
  tenants: SessionTenantDto[];
}

const BASE = (import.meta.env.VITE_API_BASE ?? "http://localhost:5101").replace(/\/$/, "");

export function buildVerifyUrl(): string {
  return `${BASE}/v1/auth/verify`;
}

export function useMeQuery() {
  return useQuery({
    queryKey: ["auth", "me"] as const,
    queryFn: () => api<SessionMeResponse>("/v1/auth/me", { silent401: true }),
    retry: false,
    staleTime: 30_000,
  });
}

export function useRequestMagicLinkMutation() {
  return useMutation({
    mutationFn: (email: string) =>
      api<void>("/v1/auth/request-link", {
        method: "POST",
        body: JSON.stringify({ email }),
      }),
  });
}

export function useSelectTenantMutation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (tenantId: string) =>
      api<void>("/v1/auth/select-tenant", {
        method: "POST",
        body: JSON.stringify({ tenantId }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["auth", "me"] }),
  });
}

export function useSignOutMutation() {
  return useMutation({
    mutationFn: () => api<void>("/v1/auth/sign-out", { method: "POST" }),
  });
}

export function useInviteMemberMutation() {
  return useMutation({
    mutationFn: (input: { email: string; role: "Owner" | "Member" }) =>
      api<{ invitationId: string }>("/v1/auth/invitations", {
        method: "POST",
        body: JSON.stringify(input),
      }),
  });
}
