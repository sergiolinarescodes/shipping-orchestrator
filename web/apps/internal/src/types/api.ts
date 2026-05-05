/**
 * Hand-rolled mirrors of read-side DTOs from
 * src/ShippingOrchestrator.ReadModels/Abstractions/Operations/OperationsReadModels.cs.
 *
 * `TenantId` round-trips as a plain Guid string; `DateOnly` round-trips as
 * "yyyy-MM-dd" by System.Text.Json defaults in .NET 10.
 */
export interface OpsTenantRow {
  tenantId: string;
  displayName: string;
  status: string;
  createdAt: string;
}

export interface OpsBatchRow {
  batchId: string;
  tenantId: string;
  tenantDisplayName: string;
  status: string;
  parcelCount: number;
  successCount: number;
  failureCount: number;
  createdAt: string;
  completedAt: string | null;
  ageingMinutes: number;
}

export interface OpsShipmentRow {
  shipmentId: string;
  tenantId: string;
  batchId: string | null;
  status: string;
  carrierCode: string | null;
  trackingNumber: string | null;
  failureReason: string | null;
  countryFrom: string;
  countryTo: string;
  createdAt: string;
  updatedAt: string;
}

export interface OpsCarrierKpi {
  carrierCode: string;
  date: string;
  successCount: number;
  failureCount: number;
  successRate: number;
}

// --- Onboarding ---------------------------------------------------------------
export type OnboardingStepKind = "SyncInput" | "AwaitExternal" | "StaffApproval" | "Automatic";
export type OnboardingStepStatus = "Pending" | "Awaiting" | "Completed" | "Skipped" | "Failed";
export type OnboardingProcessStatus = "InProgress" | "Completed" | "Cancelled" | "TimedOut";

export interface OnboardingFlowStepSummary {
  code: string;
  sequence: number;
  displayTitle: string;
  kind: OnboardingStepKind;
  rendererCode: string;
}

export interface OnboardingFlowSummary {
  code: string;
  displayTitle: string;
  audience: "Staff" | "Tenant" | "Public";
  steps: OnboardingFlowStepSummary[];
}

export interface OnboardingStepView {
  code: string;
  sequence: number;
  displayTitle: string;
  kind: OnboardingStepKind;
  rendererCode: string;
  status: OnboardingStepStatus;
  skippable: boolean;
  isCommitted: boolean;
  failureReason: string | null;
  externalCorrelationId: string | null;
  awaitingExpiresAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  collectedPayload: unknown;
  resultPayload: unknown;
  metadata: Record<string, string> | null;
}

export interface OnboardingProcessView {
  processId: string;
  flowCode: string;
  flowTitle: string;
  status: OnboardingProcessStatus;
  tenantId: string | null;
  startedByStaffUserId: string | null;
  contactEmail: string | null;
  currentStepCode: string | null;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
  steps: OnboardingStepView[];
  dashboardUrl: string | null;
}

// --- Tenant detail ------------------------------------------------------------
export interface ToSAcceptance {
  signerName: string;
  signerEmail: string;
  ipAddress: string;
  toSVersion: string;
  acceptedAt: string;
}

export interface TenantEcommerceConnectionView {
  connectionId: string;
  platformCode: string;
  externalAccountId: string;
  installedAt: string;
  lastSyncAt: string | null;
  mode: string | null;
  status: string;
  verifiedAt: string | null;
  rejectedAt: string | null;
  rejectionReason: string | null;
}

export interface TenantCarrierAssignmentView {
  assignmentId: string;
  carrierCode: string;
  priority: number;
  isActive: boolean;
  originCountries: string[];
  destinationCountries: string[];
  mode: string | null;
}

export interface TenantDetailView {
  tenantId: string;
  displayName: string;
  status: string;
  contactEmail: string | null;
  createdAt: string;
  updatedAt: string;
  carrierMode: string | null;
  toSAcceptance: ToSAcceptance | null;
  ecommerceConnections: TenantEcommerceConnectionView[];
  carrierAssignments: TenantCarrierAssignmentView[];
  recentBatches: OpsBatchRow[];
}

// --- Simulator + shipment detail ---------------------------------------------
export interface SimulateOrderResponse {
  batchId: string;
  shipmentIds: string[];
  externalOrderId: string;
}

export interface CustomerShipmentTrackingEventView {
  sequence: number;
  eventCode: string;
  description: string | null;
  location: string | null;
  occurredAt: string;
}

export interface CustomerShipmentView {
  shipmentId: string;
  tenantId: string;
  batchId: string | null;
  status: string;
  carrierCode: string | null;
  trackingNumber: string | null;
  labelUri: string | null;
  failureReason: string | null;
  createdAt: string;
  updatedAt: string;
  events: CustomerShipmentTrackingEventView[] | null;
}

export interface CustomerBatchView {
  batchId: string;
  tenantId: string;
  status: string;
  parcelCount: number;
  successCount: number;
  failureCount: number;
  createdAt: string;
  completedAt: string | null;
  shipments: CustomerShipmentView[];
}

// --- Ingestion failures (ops side) -------------------------------------------
export interface OpsIngestionFailureRow {
  failureId: string;
  tenantId: string;
  tenantDisplayName: string;
  connectorCode: string;
  externalOrderId: string | null;
  reasonCode: string;
  status: string;
  severity: string;
  message: string;
  tenantHint: string;
  occurredAt: string;
  lastOccurredAt: string;
  occurrenceCount: number;
  resolvedAt: string | null;
  resolvedReason: string | null;
  dismissedAt: string | null;
  dismissedBy: string | null;
}

export interface OpsIngestionFailureStatGroup {
  tenantId: string;
  tenantDisplayName: string;
  reasonCode: string;
  openCount: number;
  resolvedCount: number;
  dismissedCount: number;
  lastSeen: string | null;
}
