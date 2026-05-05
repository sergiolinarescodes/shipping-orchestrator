import {
  Card, Badge, Button, Table, THead, TBody, TR, TH, TD,
  StatusBadge, PageTitleRule, ShipClusterBg,
  IconChevronR, IconFilter,
} from "@ship/ui";
import { Link } from "react-router-dom";
import { useExceptionsQuery, useCarrierKpisQuery, useBatchesQuery } from "../api/queries";
import type { OpsCarrierKpi } from "../types/api";

interface CarrierHealthRow {
  name: string;
  ontimePct: number;
  vol: number;
  state: "healthy" | "warn" | "down";
}

function aggregateCarrierHealth(rows: readonly OpsCarrierKpi[] | undefined): CarrierHealthRow[] {
  if (!rows || rows.length === 0) return [];
  const byCarrier = new Map<string, { success: number; failure: number }>();
  for (const r of rows) {
    const cur = byCarrier.get(r.carrierCode) ?? { success: 0, failure: 0 };
    cur.success += r.successCount;
    cur.failure += r.failureCount;
    byCarrier.set(r.carrierCode, cur);
  }
  return [...byCarrier.entries()]
    .map(([name, { success, failure }]) => {
      const total = success + failure;
      const pct = total === 0 ? 0 : (success / total) * 100;
      const state: CarrierHealthRow["state"] = pct >= 92 ? "healthy" : pct >= 85 ? "warn" : "down";
      return { name, ontimePct: pct, vol: total, state };
    })
    .sort((a, b) => b.vol - a.vol);
}

const STATE_VARIANT = { healthy: "success", warn: "warn", down: "danger" } as const;

function ageMinutesToText(iso: string): string {
  const mins = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60_000));
  if (mins < 60) return `${mins}m`;
  const hrs = Math.round(mins / 60);
  if (hrs < 24) return `${hrs}h`;
  return `${Math.round(hrs / 24)}d`;
}

function severityFromStatus(status: string): "high" | "med" | "low" {
  if (status === "Failed" || status === "Exception") return "high";
  if (status === "Pending" || status === "LabelRequested") return "med";
  return "low";
}

const SEV_VARIANT = { high: "danger", med: "warn", low: "neutral" } as const;

function batchStatusVariant(status: string): "info" | "success" | "warn" | "danger" | "neutral" {
  switch (status) {
    case "Completed": return "success";
    case "Failed": return "danger";
    case "PartiallyFailed": return "warn";
    case "Processing": return "info";
    default: return "neutral";
  }
}

export default function OpsConsole() {
  const exceptions = useExceptionsQuery({ take: 8 });
  const kpis = useCarrierKpisQuery();
  const batches = useBatchesQuery({ take: 10 });

  const carrierHealth = aggregateCarrierHealth(kpis.data);

  return (
    <div className="px-7 py-6">
      <div className="flex items-center justify-between mb-2 relative">
        <ShipClusterBg size={160} opacity={0.07} style={{ right: -10, top: -20, transform: "rotate(15deg)" }} />
        <div className="relative">
          <PageTitleRule accent />
          <div className="text-[11px] font-semibold uppercase tracking-[0.04em] mb-1.5 text-ship-orange-600">
            Operations console
          </div>
          <h1 className="font-display text-[32px] leading-[1.15] font-semibold tracking-[-0.02em] text-ship-navy-800">
            Network health
          </h1>
        </div>
      </div>

      <div className="grid grid-cols-[1.5fr_1fr] gap-4 mb-5 mt-5">
        <Card>
          <div className="flex items-center justify-between px-5 py-4 border-b border-border">
            <div>
              <h2 className="text-[18px] leading-snug font-semibold tracking-[-0.01em] text-ink-900">Exceptions queue</h2>
              <div className="text-[13px] text-ink-600">
                {exceptions.isLoading
                  ? "Loading from /ops/exceptions…"
                  : exceptions.isError
                    ? "Couldn't load exceptions. Backend reachable?"
                    : `${exceptions.data?.length ?? 0} shown · ordered by recency`}
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Button variant="secondary" size="sm"><IconFilter size={12} /> Severity</Button>
            </div>
          </div>
          <Table>
            <THead>
              <TR>
                <TH style={{ width: 130 }}>Tracking</TH>
                <TH>Carrier</TH>
                <TH>Route</TH>
                <TH>Status</TH>
                <TH>Severity</TH>
                <TH style={{ width: 60 }}>Age</TH>
              </TR>
            </THead>
            <TBody>
              {exceptions.data?.length === 0 && (
                <TR>
                  <TD colSpan={6} className="text-center text-ink-500 py-8">
                    No open exceptions.
                  </TD>
                </TR>
              )}
              {exceptions.data?.map((e) => {
                const sev = severityFromStatus(e.status);
                return (
                  <TR key={e.shipmentId}>
                    <TD>
                      <span className="font-mono text-[12.5px] text-ink-800">
                        {e.trackingNumber ?? e.shipmentId.slice(0, 8)}
                      </span>
                    </TD>
                    <TD>{e.carrierCode ?? "—"}</TD>
                    <TD className="font-mono text-[12px] text-ink-500">{e.countryFrom} → {e.countryTo}</TD>
                    <TD><StatusBadge status={e.status} /></TD>
                    <TD><Badge variant={SEV_VARIANT[sev]} dot>{sev}</Badge></TD>
                    <TD className="font-mono text-ink-500">{ageMinutesToText(e.updatedAt)}</TD>
                  </TR>
                );
              })}
            </TBody>
          </Table>
        </Card>

        <Card pad="lg">
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-[18px] leading-snug font-semibold tracking-[-0.01em] text-ink-900">Carrier health</h2>
          </div>
          <div className="flex flex-col gap-3">
            {kpis.isLoading && <div className="text-[12px] text-ink-400">Loading carrier KPIs…</div>}
            {!kpis.isLoading && carrierHealth.length === 0 && (
              <div className="text-[12px] text-ink-400">
                No carrier KPI rows yet. Run a successful shipment batch to seed /ops/kpis/carrier-success-rate.
              </div>
            )}
            {carrierHealth.map((c) => (
              <div key={c.name} className="flex flex-col gap-1.5 pb-2.5 border-b border-border last:border-b-0 last:pb-0">
                <div className="flex items-center justify-between">
                  <Badge variant={STATE_VARIANT[c.state]} dot>{c.name}</Badge>
                  <span className="font-mono text-[12px] text-ink-500">{c.vol.toLocaleString()} · 24h</span>
                </div>
                <div className="flex items-center gap-2">
                  <div className="flex-1 h-1.5 bg-ink-100 rounded-sm overflow-hidden">
                    <div
                      className="h-full"
                      style={{
                        width: `${Math.max(0, Math.min(100, c.ontimePct))}%`,
                        background:
                          c.state === "down"
                            ? "var(--red-500)"
                            : c.state === "warn"
                              ? "var(--amber-500)"
                              : "var(--green-500)",
                      }}
                    />
                  </div>
                  <span className="font-mono text-[12px] font-semibold text-ink-700 w-12 text-right">
                    {c.ontimePct.toFixed(1)}%
                  </span>
                </div>
              </div>
            ))}
          </div>
        </Card>
      </div>

      <Card>
        <div className="flex items-center justify-between px-5 py-4 border-b border-border">
          <div>
            <h2 className="text-[18px] leading-snug font-semibold tracking-[-0.01em] text-ink-900">Recent batches</h2>
            <div className="text-[13px] text-ink-600">
              {batches.isLoading
                ? "Loading from /ops/queues…"
                : batches.isError
                  ? "Couldn't load batches. Backend reachable?"
                  : `${batches.data?.length ?? 0} shown · ordered by recency`}
            </div>
          </div>
        </div>
        <Table>
          <THead>
            <TR>
              <TH style={{ width: 220 }}>Batch</TH>
              <TH>Tenant</TH>
              <TH>Status</TH>
              <TH style={{ textAlign: "right" }}>Parcels</TH>
              <TH style={{ textAlign: "right" }}>Success</TH>
              <TH style={{ textAlign: "right" }}>Failed</TH>
              <TH style={{ width: 60 }}>Age</TH>
              <TH style={{ width: 40 }} />
            </TR>
          </THead>
          <TBody>
            {batches.data?.length === 0 && (
              <TR>
                <TD colSpan={8} className="text-center text-ink-500 py-8">
                  No batches yet.
                </TD>
              </TR>
            )}
            {batches.data?.map((b) => (
              <TR key={b.batchId}>
                <TD>
                  <Link
                    to={`/tenants/${b.tenantId}/batches/${b.batchId}`}
                    className="font-mono text-[12.5px] text-ship-orange-700 underline"
                  >
                    {b.batchId.slice(0, 8)}…
                  </Link>
                </TD>
                <TD>
                  <Link
                    to={`/tenants/${b.tenantId}`}
                    className="text-[13px] text-ink-800 underline"
                  >
                    {b.tenantDisplayName}
                  </Link>
                </TD>
                <TD><Badge variant={batchStatusVariant(b.status)}>{b.status}</Badge></TD>
                <TD className="tnum text-right">{b.parcelCount}</TD>
                <TD className="tnum text-right text-green-600">{b.successCount}</TD>
                <TD className="tnum text-right text-red-600">{b.failureCount}</TD>
                <TD className="font-mono text-ink-500">{ageMinutesToText(b.createdAt)}</TD>
                <TD><IconChevronR size={14} className="text-ink-300" /></TD>
              </TR>
            ))}
          </TBody>
        </Table>
      </Card>
    </div>
  );
}
