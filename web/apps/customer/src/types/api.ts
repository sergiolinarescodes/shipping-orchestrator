/**
 * Hand-rolled mirrors of read-side DTOs from
 * src/ShippingOrchestrator.ReadModels/Abstractions/Customer/CustomerReadModels.cs.
 *
 * `TenantId` round-trips as a plain Guid string (see TenantIdJsonConverter.cs),
 * so it surfaces here as `string`. JSON casing follows .NET defaults: PascalCase
 * record props become camelCase on the wire.
 */
export interface CurrentTenantView {
  tenantId: string;
  displayName: string;
  status: string;
  contactEmail: string | null;
}

export interface DevTenantSummary {
  tenantId: string;
  displayName: string;
  status: string;
  createdAt: string;
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

export interface PendingOrderView {
  id: string;
  platformCode: string;
  externalOrderId: string;
  ingestedAt: string;
  customerName: string | null;
  destinationCity: string | null;
  destinationCountry: string | null;
  itemCount: number;
  totalWeightGrams: number;
  declaredValue: number | null;
  currency: string | null;
}

export interface BundlePendingOrdersResponse {
  batchId: string;
  shipmentIds: string[];
  consumedPendingOrderIds: string[];
}

export interface EcommerceConnectionView {
  connectionId: string;
  platformCode: string;
  externalAccountId: string;
  status: "PendingVerification" | "Active" | "Rejected" | string;
  installedAt: string;
  verifiedAt: string | null;
  lastSyncAt: string | null;
}

export interface ConnectorCatalogEntry {
  connectorCode: string;
  displayName: string;
}

export interface ConnectionsListResponse {
  connections: EcommerceConnectionView[];
  availablePlatforms: ConnectorCatalogEntry[];
}

export interface InstallInputField {
  key: string;
  label: string;
  placeholder: string | null;
  required: boolean;
  helpText: string | null;
}

export interface InstallGuide {
  title: string;
  steps: string[];
  inputs: InstallInputField[];
  helpUrl: string | null;
}

export interface StartConnectionInstallResponse {
  authorizationUrl: string;
}

export interface CustomerIngestionFailureView {
  failureId: string;
  tenantId: string;
  connectorCode: string;
  externalOrderId: string | null;
  reasonCode: string;
  status: "Open" | "Resolved" | "Dismissed" | string;
  message: string;
  tenantHint: string;
  occurredAt: string;
  lastOccurredAt: string;
  occurrenceCount: number;
  resolvedAt: string | null;
  dismissedAt: string | null;
}

export interface OpenIngestionFailureCount {
  open: number;
}
