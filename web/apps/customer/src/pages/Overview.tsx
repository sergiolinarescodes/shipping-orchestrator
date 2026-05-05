import { Link } from "react-router-dom";
import {
  Badge, Card, Table, THead, TBody, TR, TH, TD,
  StatusBadge, PageTitleRule, ShipClusterBg,
  IconChevronR,
} from "@ship/ui";
import { useShipmentsQuery, useCurrentTenantQuery } from "../api/queries";

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { month: "short", day: "numeric" });
}

export default function Overview() {
  const tenant = useCurrentTenantQuery();
  const shipments = useShipmentsQuery({ take: 10 });

  return (
    <div className="px-7 py-6">
      <div className="flex items-center justify-between mb-2 relative">
        <ShipClusterBg size={160} opacity={0.06} style={{ right: -10, top: -20, transform: "rotate(-8deg)" }} />
        <div className="relative">
          <PageTitleRule />
          <div className="text-[11px] font-semibold uppercase tracking-[0.04em] mb-1.5 text-ship-navy-500">
            Dashboard
          </div>
          <h1 className="font-display text-[28px] leading-[1.15] font-semibold tracking-[-0.02em] text-ship-navy-800">
            {tenant.data?.displayName ?? "Tenant dashboard"}
          </h1>
          <div className="text-[13px] text-ink-500 mt-1 flex items-center gap-2">
            {tenant.data && (
              <Badge variant={tenant.data.status === "Active" ? "success" : "neutral"}>
                {tenant.data.status}
              </Badge>
            )}
            {tenant.data?.contactEmail && <span>· {tenant.data.contactEmail}</span>}
          </div>
        </div>
      </div>

      <Card className="mt-5">
        <div className="flex items-center justify-between px-5 py-4 border-b border-border">
          <div>
            <h2 className="text-[18px] leading-snug font-semibold tracking-[-0.01em] text-ink-900">Recent shipments</h2>
            <div className="text-[13px] text-ink-600">
              {shipments.isLoading
                ? "Loading…"
                : shipments.isError
                  ? "Couldn't load shipments. Backend reachable?"
                  : `Latest ${shipments.data?.length ?? 0} from /v1/dashboard/shipments`}
            </div>
          </div>
          <Link to="/shipments" className="text-[13px] text-ship-orange-700 underline">
            View all
          </Link>
        </div>
        <Table>
          <THead>
            <TR>
              <TH style={{ width: 200 }}>Tracking</TH>
              <TH>Carrier</TH>
              <TH>Status</TH>
              <TH style={{ width: 220 }}>Label</TH>
              <TH style={{ width: 110, textAlign: "right" }}>Updated</TH>
              <TH style={{ width: 40 }} />
            </TR>
          </THead>
          <TBody>
            {shipments.data?.length === 0 && (
              <TR>
                <TD colSpan={6} className="text-center text-ink-500 py-8">
                  No shipments yet.
                </TD>
              </TR>
            )}
            {shipments.data?.map((s) => (
              <TR key={s.shipmentId}>
                <TD>
                  <Link
                    to={`/shipments/${s.shipmentId}`}
                    className="font-mono text-[12.5px] text-ship-orange-700 underline"
                  >
                    {s.trackingNumber ?? `${s.shipmentId.slice(0, 8)}…`}
                  </Link>
                </TD>
                <TD>{s.carrierCode ?? "—"}</TD>
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
      </Card>
    </div>
  );
}
