import { useState } from "react";
import {
  Badge, Button, Card, EmptyState, PageTitleRule, Spinner,
  Table, TBody, TD, TH, THead, TR,
} from "@ship/ui";
import {
  useIngestionFailuresQuery,
  useDismissIngestionFailureMutation,
  useRecheckIngestionFailureMutation,
} from "../api/queries";
import type { CustomerIngestionFailureView } from "../types/api";

const REASON_LABELS: Record<string, string> = {
  MissingShippingAddress: "Missing shipping address",
  UnknownCountry: "Unknown country",
  ZeroWeight: "Zero weight",
  InvalidPostalCode: "Invalid postal code",
  UnsupportedCurrency: "Unsupported currency",
  ParseError: "Parse error",
  Unknown: "Unknown",
};

function humanizeReason(code: string) {
  return REASON_LABELS[code] ?? code;
}

function formatRelative(iso: string) {
  const then = new Date(iso).getTime();
  const diff = Date.now() - then;
  if (diff < 60_000) return "just now";
  const minutes = Math.floor(diff / 60_000);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function statusVariant(status: string): "neutral" | "brand" | "success" | "warn" {
  switch (status) {
    case "Open": return "warn";
    case "Resolved": return "success";
    case "Dismissed": return "neutral";
    default: return "neutral";
  }
}

export default function NeedsAttentionPage() {
  const [statusFilter, setStatusFilter] = useState<string>("Open");
  const failures = useIngestionFailuresQuery({ status: statusFilter });
  const dismiss = useDismissIngestionFailureMutation();
  const recheck = useRecheckIngestionFailureMutation();
  const [banner, setBanner] = useState<{ kind: "ok" | "err"; text: string } | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const rows = failures.data ?? [];

  async function handleDismiss(row: CustomerIngestionFailureView) {
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

  async function handleRecheck(row: CustomerIngestionFailureView) {
    setBanner(null);
    setBusyId(row.failureId);
    try {
      const result = await recheck.mutateAsync(row.failureId);
      if (result.outcome === "resolved") {
        setBanner({ kind: "ok", text: `Order #${row.externalOrderId} re-pulled successfully — moved to Pending orders.` });
      } else {
        setBanner({ kind: "err", text: result.detail ?? "Order still fails translation. Check the hint and try again." });
      }
    } catch (e) {
      setBanner({ kind: "err", text: e instanceof Error ? e.message : "Recheck failed." });
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="px-7 py-6">
      <div className="mb-5">
        <PageTitleRule />
        <div className="text-[11px] font-semibold uppercase tracking-[0.04em] mb-1.5 text-ship-navy-500">
          Inbox
        </div>
        <div className="flex items-center justify-between gap-4">
          <h1 className="font-display text-[28px] leading-[1.15] font-semibold tracking-[-0.02em] text-ship-navy-800">
            Needs attention
          </h1>
          <div className="text-[13px] text-ink-500">
            {failures.isLoading
              ? "Loading…"
              : failures.isError
                ? "Couldn't load failures."
                : `${rows.length} ${statusFilter.toLowerCase()} failure${rows.length === 1 ? "" : "s"}`}
          </div>
        </div>
        <div className="text-[13px] text-ink-500 mt-1">
          Orders that couldn't be processed automatically. Fix the issue in your store and the row clears itself when the next webhook arrives.
        </div>
      </div>

      <div className="mb-3 flex items-center gap-2">
        {(["Open", "Resolved", "Dismissed"] as const).map((s) => (
          <Button
            key={s}
            variant={statusFilter === s ? "primary" : "ghost"}
            size="sm"
            onClick={() => setStatusFilter(s)}
          >
            {s}
          </Button>
        ))}
      </div>

      {banner && (
        <div className={
          "mb-3 rounded border px-4 py-2 text-[12px] " +
          (banner.kind === "ok"
            ? "border-green-200 bg-green-50 text-green-700"
            : "border-red-200 bg-red-50 text-red-700")
        }>
          {banner.text}
        </div>
      )}

      <Card>
        {failures.isLoading && (
          <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
            <Spinner /> Loading failures…
          </div>
        )}
        {!failures.isLoading && rows.length === 0 && (
          <div className="p-6">
            <EmptyState
              title={statusFilter === "Open" ? "All clear" : `No ${statusFilter.toLowerCase()} failures`}
              description={
                statusFilter === "Open"
                  ? "Every order that arrived recently was translated successfully."
                  : "Switch to Open to see failures that still need attention."
              }
            />
          </div>
        )}
        {rows.length > 0 && (
          <Table>
            <THead>
              <TR>
                <TH style={{ width: 180 }}>Order</TH>
                <TH style={{ width: 220 }}>Reason</TH>
                <TH>What to do</TH>
                <TH style={{ width: 100 }}>Source</TH>
                <TH style={{ width: 70, textAlign: "right" }}>Hits</TH>
                <TH style={{ width: 110, textAlign: "right" }}>Last seen</TH>
                <TH style={{ width: 100 }}>Status</TH>
                <TH style={{ width: 180, textAlign: "right" }}>Actions</TH>
              </TR>
            </THead>
            <TBody>
              {rows.map((r) => (
                <TR key={r.failureId}>
                  <TD>
                    {r.externalOrderId
                      ? <span className="font-mono text-[12.5px] text-ship-orange-700">#{r.externalOrderId}</span>
                      : <span className="text-ink-400">unparseable</span>}
                  </TD>
                  <TD>{humanizeReason(r.reasonCode)}</TD>
                  <TD className="text-ink-700">{r.tenantHint || r.message}</TD>
                  <TD><Badge variant="neutral">{r.connectorCode}</Badge></TD>
                  <TD style={{ textAlign: "right" }}>{r.occurrenceCount}</TD>
                  <TD style={{ textAlign: "right" }} className="text-ink-500">
                    {formatRelative(r.lastOccurredAt)}
                  </TD>
                  <TD>
                    <Badge variant={statusVariant(r.status)} dot>{r.status}</Badge>
                  </TD>
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
                          I'll handle it
                        </Button>
                      </div>
                    )}
                  </TD>
                </TR>
              ))}
            </TBody>
          </Table>
        )}
      </Card>
    </div>
  );
}
