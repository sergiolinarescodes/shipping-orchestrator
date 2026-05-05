import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  Badge, Button, Card, PageTitleRule,
  Table, TBody, TD, TH, THead, TR,
} from "@ship/ui";
import {
  useOpsIngestionFailuresQuery,
  useOpsDismissIngestionFailureMutation,
  useOpsRecheckIngestionFailureMutation,
  type OpsIngestionFailureFilter,
} from "../api/queries";
import IngestionStatsCard from "../components/IngestionStatsCard";
import type { OpsIngestionFailureRow } from "../types/api";

const REASON_OPTIONS = [
  "MissingShippingAddress",
  "UnknownCountry",
  "ZeroWeight",
  "InvalidPostalCode",
  "UnsupportedCurrency",
  "ParseError",
  "Unknown",
];

function ageMinutesToText(iso: string): string {
  const mins = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60_000));
  if (mins < 60) return `${mins}m`;
  const hrs = Math.round(mins / 60);
  if (hrs < 24) return `${hrs}h`;
  return `${Math.round(hrs / 24)}d`;
}

function statusVariant(status: string): "info" | "success" | "warn" | "danger" | "neutral" {
  switch (status) {
    case "Open": return "warn";
    case "Resolved": return "success";
    case "Dismissed": return "neutral";
    default: return "info";
  }
}

export default function IngestionFailuresPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const filter: OpsIngestionFailureFilter = {
    tenantId: searchParams.get("tenantId") ?? undefined,
    reasonCode: searchParams.get("reasonCode") ?? undefined,
    status: searchParams.get("status") ?? "Open",
    take: 100,
  };

  const failures = useOpsIngestionFailuresQuery(filter);
  const dismiss = useOpsDismissIngestionFailureMutation();
  const recheck = useOpsRecheckIngestionFailureMutation();
  const [banner, setBanner] = useState<{ kind: "ok" | "err"; text: string } | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  function setFilter(key: string, value: string | null) {
    const next = new URLSearchParams(searchParams);
    if (value === null || value === "") next.delete(key);
    else next.set(key, value);
    setSearchParams(next, { replace: true });
  }

  function clearFilters() {
    setSearchParams({ status: "Open" }, { replace: true });
  }

  async function handleDismiss(row: OpsIngestionFailureRow) {
    setBanner(null);
    setBusyId(row.failureId);
    try {
      await dismiss.mutateAsync(row.failureId);
    } catch (e) {
      setBanner({ kind: "err", text: e instanceof Error ? e.message : "Dismiss failed." });
    } finally {
      setBusyId(null);
    }
  }

  async function handleRecheck(row: OpsIngestionFailureRow) {
    setBanner(null);
    setBusyId(row.failureId);
    try {
      const result = await recheck.mutateAsync(row.failureId);
      if (result.outcome === "resolved") {
        setBanner({ kind: "ok", text: `Order ${row.externalOrderId ?? row.failureId.slice(0, 8)} re-pulled — pending order created.` });
      } else {
        setBanner({ kind: "err", text: result.detail ?? "Order still fails translation." });
      }
    } catch (e) {
      setBanner({ kind: "err", text: e instanceof Error ? e.message : "Recheck failed." });
    } finally {
      setBusyId(null);
    }
  }

  const rows = failures.data ?? [];
  const activeFilters = [filter.tenantId, filter.reasonCode].filter(Boolean).length;

  return (
    <div className="px-7 py-6">
      <div className="mb-5">
        <PageTitleRule accent />
        <div className="text-[11px] font-semibold uppercase tracking-[0.04em] mb-1.5 text-ship-orange-600">
          Ingestion failures
        </div>
        <h1 className="font-display text-[32px] leading-[1.15] font-semibold tracking-[-0.02em] text-ship-navy-800">
          Webhook translation failures
        </h1>
        <div className="text-[13px] text-ink-600 mt-1">
          Inbound orders that couldn't be translated. Tenants see the same row on their "Needs attention" tab.
          Click a cell in the stats card to drill into a tenant × reason slice.
        </div>
      </div>

      <div className="mb-5">
        <IngestionStatsCard
          window="24h"
          onCellClick={(tenantId, reasonCode) => {
            setSearchParams({ tenantId, reasonCode, status: "Open" }, { replace: true });
          }}
        />
      </div>

      <Card>
        <div className="flex items-center justify-between gap-3 px-5 py-4 border-b border-border">
          <div>
            <h2 className="text-[18px] leading-snug font-semibold tracking-[-0.01em] text-ink-900">
              Failure rows
            </h2>
            <div className="text-[13px] text-ink-600">
              {failures.isLoading
                ? "Loading…"
                : failures.isError
                  ? "Couldn't load failures."
                  : `${rows.length} shown · ${filter.status?.toLowerCase()} · ordered by recency`}
            </div>
          </div>
          <div className="flex items-center gap-2 flex-wrap">
            {(["Open", "Resolved", "Dismissed"] as const).map((s) => (
              <Button
                key={s}
                variant={filter.status === s ? "primary" : "secondary"}
                size="sm"
                onClick={() => setFilter("status", s)}
              >
                {s}
              </Button>
            ))}
            <select
              className="text-[12px] border border-border rounded px-2 py-1"
              value={filter.reasonCode ?? ""}
              onChange={(e) => setFilter("reasonCode", e.target.value || null)}
            >
              <option value="">All reasons</option>
              {REASON_OPTIONS.map((r) => <option key={r} value={r}>{r}</option>)}
            </select>
            {activeFilters > 0 && (
              <Button variant="ghost" size="sm" onClick={clearFilters}>
                Clear filters
              </Button>
            )}
          </div>
        </div>

        {banner && (
          <div className={
            "mx-5 my-2 rounded border px-3 py-1.5 text-[12px] " +
            (banner.kind === "ok"
              ? "border-green-200 bg-green-50 text-green-700"
              : "border-red-200 bg-red-50 text-red-700")
          }>
            {banner.text}
          </div>
        )}

        <Table>
          <THead>
            <TR>
              <TH style={{ width: 200 }}>Tenant</TH>
              <TH style={{ width: 130 }}>Order</TH>
              <TH>Reason</TH>
              <TH>Hint</TH>
              <TH style={{ width: 90 }}>Source</TH>
              <TH style={{ width: 70, textAlign: "right" }}>Hits</TH>
              <TH style={{ width: 100 }}>Status</TH>
              <TH style={{ width: 70 }}>Sev</TH>
              <TH style={{ width: 60 }}>Age</TH>
              <TH style={{ width: 170, textAlign: "right" }}>Actions</TH>
            </TR>
          </THead>
          <TBody>
            {rows.length === 0 && !failures.isLoading && (
              <TR>
                <TD colSpan={10} className="text-center text-ink-500 py-8">
                  No rows match the current filter.
                </TD>
              </TR>
            )}
            {rows.map((r) => (
              <TR key={r.failureId}>
                <TD className="text-ink-800 truncate max-w-[180px]" title={r.tenantDisplayName}>
                  {r.tenantDisplayName}
                </TD>
                <TD>
                  {r.externalOrderId
                    ? <span className="font-mono text-[12.5px] text-ship-orange-700">#{r.externalOrderId}</span>
                    : <span className="text-ink-400 italic">unparseable</span>}
                </TD>
                <TD>{r.reasonCode}</TD>
                <TD className="text-ink-700">{r.tenantHint || r.message}</TD>
                <TD><Badge variant="neutral">{r.connectorCode}</Badge></TD>
                <TD style={{ textAlign: "right" }}>{r.occurrenceCount}</TD>
                <TD><Badge variant={statusVariant(r.status)} dot>{r.status}</Badge></TD>
                <TD>
                  <Badge variant={r.severity === "Critical" ? "danger" : r.severity === "Warning" ? "warn" : "info"} dot>
                    {r.severity}
                  </Badge>
                </TD>
                <TD className="font-mono text-ink-500">{ageMinutesToText(r.lastOccurredAt)}</TD>
                <TD style={{ textAlign: "right" }}>
                  {r.status === "Open" && (
                    <div className="inline-flex items-center gap-1">
                      {r.externalOrderId && (
                        <Button
                          variant="primary"
                          size="sm"
                          onClick={() => handleRecheck(r)}
                          disabled={busyId === r.failureId}
                        >
                          {busyId === r.failureId && recheck.isPending ? "Checking…" : "Recheck"}
                        </Button>
                      )}
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleDismiss(r)}
                        disabled={busyId === r.failureId}
                      >
                        Dismiss
                      </Button>
                    </div>
                  )}
                </TD>
              </TR>
            ))}
          </TBody>
        </Table>
      </Card>
    </div>
  );
}
