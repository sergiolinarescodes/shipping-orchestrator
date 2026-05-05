import { Link } from "react-router-dom";
import {
  Badge, Card, Spinner, Table, THead, TBody, TR, TH, TD,
  StatusBadge, PageTitleRule, IconChevronR,
} from "@ship/ui";
import { useShipmentsQuery } from "../api/queries";

function formatDate(iso: string) {
  return new Date(iso).toLocaleString();
}

export default function ShipmentsListPage() {
  const shipments = useShipmentsQuery({ take: 50 });

  return (
    <div className="px-7 py-6">
      <div className="mb-5">
        <PageTitleRule />
        <div className="text-[11px] font-semibold uppercase tracking-[0.04em] mb-1.5 text-ship-navy-500">
          Shipments
        </div>
        <h1 className="font-display text-[28px] leading-[1.15] font-semibold tracking-[-0.02em] text-ship-navy-800">
          All shipments
        </h1>
        <div className="text-[13px] text-ink-500 mt-1">
          {shipments.isLoading
            ? "Loading from /v1/dashboard/shipments…"
            : shipments.isError
              ? "Couldn't load shipments. Backend reachable?"
              : `${shipments.data?.length ?? 0} shipment${shipments.data?.length === 1 ? "" : "s"}`}
        </div>
      </div>

      <Card>
        {shipments.isLoading && (
          <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
            <Spinner /> Loading…
          </div>
        )}
        {!shipments.isLoading && shipments.data?.length === 0 && (
          <div className="p-6 text-[13px] text-ink-500">
            No shipments yet. Use the operations dashboard's "Send test parcel" to create one.
          </div>
        )}
        {shipments.data && shipments.data.length > 0 && (
          <Table>
            <THead>
              <TR>
                <TH style={{ width: 220 }}>Tracking</TH>
                <TH>Carrier</TH>
                <TH>Status</TH>
                <TH style={{ width: 220 }}>Label</TH>
                <TH style={{ textAlign: "right", width: 160 }}>Updated</TH>
                <TH style={{ width: 40 }} />
              </TR>
            </THead>
            <TBody>
              {shipments.data.map((s) => (
                <TR key={s.shipmentId}>
                  <TD>
                    <Link
                      to={`/shipments/${s.shipmentId}`}
                      className="font-mono text-[12.5px] text-ship-orange-700 underline"
                    >
                      {s.trackingNumber ?? `${s.shipmentId.slice(0, 8)}…`}
                    </Link>
                  </TD>
                  <TD>{s.carrierCode ?? <Badge variant="neutral">unassigned</Badge>}</TD>
                  <TD><StatusBadge status={s.status} /></TD>
                  <TD>
                    {s.labelUri
                      ? <a href={s.labelUri} target="_blank" rel="noopener noreferrer" className="text-ship-navy-700 underline">Open</a>
                      : <span className="text-ink-400">{s.failureReason ?? "—"}</span>}
                  </TD>
                  <TD style={{ textAlign: "right" }} className="text-ink-500">{formatDate(s.updatedAt)}</TD>
                  <TD><IconChevronR size={14} className="text-ink-300" /></TD>
                </TR>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
    </div>
  );
}
