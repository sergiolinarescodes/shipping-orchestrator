import { Link, useParams } from "react-router-dom";
import {
  Badge,
  Card,
  Spinner,
  Timeline,
  type TimelineItem,
} from "@ship/ui";
import { useTenantBatchQuery } from "../../api/queries";

export default function BatchProgressPage() {
  const { tenantId = "", batchId = "" } = useParams<{ tenantId: string; batchId: string }>();
  const batch = useTenantBatchQuery(tenantId, batchId);

  if (batch.isLoading) {
    return (
      <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
        <Spinner /> Loading…
      </div>
    );
  }
  if (!batch.data) {
    return <div className="p-6 text-[13px] text-red-500">Batch not found.</div>;
  }
  const b = batch.data;

  return (
    <div className="p-6 grid gap-6 max-w-4xl">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-[18px] font-semibold text-ink-800">Batch progress</h1>
          <span className="text-[12px] text-ink-500 font-mono">{b.batchId}</span>
        </div>
        <Link to={`/tenants/${tenantId}`} className="text-[12px] text-ink-500 underline">
          ← tenant
        </Link>
      </div>

      <Card pad="lg">
        <div className="flex items-center gap-3">
          <Badge variant={badgeFor(b.status)}>{b.status}</Badge>
          <span className="text-[12px] text-ink-500">
            {b.successCount}/{b.parcelCount} succeeded · {b.failureCount} failed
          </span>
        </div>
      </Card>

      <div className="grid gap-4">
        {b.shipments.map((s) => {
          const events: TimelineItem[] = [
            ...(s.events ?? []).map((e) => ({
              id: `${s.shipmentId}-${e.sequence}`,
              title: e.eventCode,
              description: e.description ?? undefined,
              location: e.location,
              occurredAt: e.occurredAt,
              status: timelineStatus(e.eventCode),
            })),
          ];
          return (
            <Card key={s.shipmentId} pad="lg">
              <div className="flex items-center justify-between">
                <div>
                  <span className="text-[13px] font-mono text-ink-500">{s.shipmentId.slice(0, 8)}…</span>
                  <h3 className="text-[14px] font-semibold text-ink-800">{s.carrierCode ?? "—"}</h3>
                </div>
                <div className="flex flex-col items-end gap-1">
                  <Badge variant={shipmentBadge(s.status)}>{s.status}</Badge>
                  {s.trackingNumber && (
                    <span className="text-[11px] font-mono text-ink-500">{s.trackingNumber}</span>
                  )}
                  {s.labelUri && (
                    <a
                      href={s.labelUri}
                      target="_blank"
                      rel="noreferrer"
                      className="text-[11px] text-ship-orange-700 underline"
                    >
                      label PDF →
                    </a>
                  )}
                </div>
              </div>
              <div className="mt-4 border-t border-border pt-4">
                <Timeline items={events} />
              </div>
            </Card>
          );
        })}
      </div>
    </div>
  );
}

function badgeFor(status: string): "info" | "success" | "warn" | "danger" | "neutral" {
  switch (status) {
    case "Completed": return "success";
    case "Failed": return "danger";
    case "PartiallyFailed": return "warn";
    case "Processing": return "info";
    default: return "neutral";
  }
}

function shipmentBadge(status: string): "info" | "success" | "warn" | "danger" | "neutral" {
  switch (status) {
    case "Delivered": return "success";
    case "InTransit": return "info";
    case "Labeled": return "neutral";
    case "Failed": return "danger";
    case "Cancelled": return "neutral";
    default: return "neutral";
  }
}

function timelineStatus(code: string): "info" | "success" | "warn" | "danger" {
  if (code === "Delivered") return "success";
  if (code === "Failed") return "danger";
  return "info";
}
