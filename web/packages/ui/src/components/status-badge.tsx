import { Badge, type BadgeVariant } from "./badge";

/**
 * Maps shipment status strings (raw from PublicApi `CustomerShipmentView.status`
 * and the design copy) to the badge variants from the design system.
 *
 * Pass any string — unknown values fall back to neutral so the table never breaks.
 */
const MAP: Record<string, { variant: BadgeVariant; label: string }> = {
  // Customer-facing copy from the design
  Delivered:     { variant: "success", label: "Delivered" },
  "In transit":  { variant: "info",    label: "In transit" },
  "Label ready": { variant: "neutral", label: "Label ready" },
  Customs:       { variant: "purple",  label: "In customs" },
  Exception:     { variant: "danger",  label: "Exception" },
  Pending:       { variant: "warn",    label: "Pending" },
  Returned:      { variant: "neutral", label: "Returned" },

  // Backend `ShipmentStatus` enum names (mirrors of the C# enum)
  Created:       { variant: "neutral", label: "Created" },
  Routed:        { variant: "info",    label: "Routed" },
  LabelRequested:{ variant: "info",    label: "Label requested" },
  LabelIssued:   { variant: "neutral", label: "Label ready" },
  InTransit:     { variant: "info",    label: "In transit" },
  Failed:        { variant: "danger",  label: "Failed" },
  Cancelled:     { variant: "neutral", label: "Cancelled" },
};

export function StatusBadge({ status }: { status: string | null | undefined }) {
  const key = status ?? "";
  const entry = MAP[key] ?? { variant: "neutral" as BadgeVariant, label: key || "Unknown" };
  return <Badge variant={entry.variant} dot>{entry.label}</Badge>;
}
