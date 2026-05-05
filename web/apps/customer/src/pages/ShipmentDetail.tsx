import { Link, useParams } from "react-router-dom";
import {
  Badge,
  Card,
  Spinner,
  StatusBadge,
  Timeline,
  type TimelineItem,
} from "@ship/ui";
import { useShipmentQuery } from "../api/queries";

export default function ShipmentDetail() {
  const { shipmentId = "" } = useParams<{ shipmentId: string }>();
  const shipment = useShipmentQuery(shipmentId);

  if (shipment.isLoading) {
    return (
      <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
        <Spinner /> Loading…
      </div>
    );
  }
  if (!shipment.data) {
    return <div className="p-6 text-[13px] text-red-500">Shipment not found.</div>;
  }
  const s = shipment.data;

  const events: TimelineItem[] = (s.events ?? []).map((e) => ({
    id: e.sequence,
    title: e.eventCode,
    description: e.description ?? undefined,
    location: e.location,
    occurredAt: e.occurredAt,
    status: e.eventCode === "Delivered" ? "success" : e.eventCode === "Failed" ? "danger" : "info",
  }));

  return (
    <div className="p-6 grid gap-6 max-w-3xl">
      <div className="flex items-center justify-between">
        <div>
          <span className="text-[12px] text-ink-500 font-mono">{s.shipmentId}</span>
          <h1 className="text-[18px] font-semibold text-ink-800">Shipment</h1>
        </div>
        <Link to="/" className="text-[12px] text-ink-500 underline">
          ← back
        </Link>
      </div>

      <Card pad="lg">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <StatusBadge status={s.status} />
            {s.carrierCode && <Badge variant="neutral">{s.carrierCode}</Badge>}
          </div>
          <div className="flex flex-col items-end gap-1">
            {s.trackingNumber && (
              <span className="text-[12px] font-mono text-ink-500">{s.trackingNumber}</span>
            )}
            {s.labelUri && (
              <a
                href={s.labelUri}
                target="_blank"
                rel="noreferrer"
                className="text-[12px] text-ship-orange-700 underline"
              >
                Open label PDF →
              </a>
            )}
          </div>
        </div>
        {s.failureReason && (
          <div className="mt-3 rounded border border-red-100 bg-red-50 px-3 py-2 text-[12px] text-red-600">
            {s.failureReason}
          </div>
        )}
      </Card>

      <Card pad="lg">
        <h2 className="text-[14px] font-semibold text-ink-800 mb-3">Tracking timeline</h2>
        <Timeline items={events} />
      </Card>
    </div>
  );
}
