import { useMemo, useState } from "react";
import {
  Badge, Button, Card, EmptyState, PageTitleRule, Spinner,
  Table, TBody, TD, TH, THead, TR,
} from "@ship/ui";
import { useNavigate } from "react-router-dom";
import {
  usePendingOrdersQuery,
  useBundlePendingOrdersMutation,
} from "../api/queries";

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

function formatMoney(value: number | null, currency: string | null) {
  if (value === null) return "—";
  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: currency ?? "EUR",
    maximumFractionDigits: 2,
  }).format(value);
}

export default function PendingOrdersPage() {
  const pending = usePendingOrdersQuery();
  const bundle = useBundlePendingOrdersMutation();
  const navigate = useNavigate();
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);

  const orders = pending.data ?? [];
  const allSelected = orders.length > 0 && selected.size === orders.length;

  const visibleSelected = useMemo(
    () => orders.filter((o) => selected.has(o.id)),
    [orders, selected],
  );

  function toggle(id: string) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function toggleAll() {
    setSelected((prev) => (prev.size === orders.length ? new Set() : new Set(orders.map((o) => o.id))));
  }

  async function handleBundle() {
    if (selected.size === 0) return;
    setError(null);
    try {
      const result = await bundle.mutateAsync({ orderIds: Array.from(selected) });
      setSelected(new Set());
      navigate(`/batches/${result.batchId}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Bundle failed.");
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
            Pending orders
          </h1>
          <div className="text-[13px] text-ink-500">
            {pending.isLoading
              ? "Loading…"
              : pending.isError
                ? "Couldn't load pending orders."
                : `${orders.length} order${orders.length === 1 ? "" : "s"} waiting to be bundled`}
          </div>
        </div>
        <div className="text-[13px] text-ink-500 mt-1">
          New Shopify orders land here automatically. Select several and bundle them into a single shipment batch.
        </div>
      </div>

      {selected.size > 0 && (
        <div className="sticky top-2 z-10 mb-3 flex items-center justify-between rounded-md border border-ship-orange-200 bg-ship-orange-50 px-4 py-2.5 shadow-sm">
          <div className="flex items-center gap-3">
            <Badge variant="brand" dot>
              {selected.size} selected
            </Badge>
            <span className="text-[12px] text-ink-600">
              Bundle these orders into one shipment batch and request labels.
            </span>
          </div>
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setSelected(new Set())}
              disabled={bundle.isPending}
            >
              Clear selection
            </Button>
            <Button
              variant="primary"
              size="sm"
              onClick={handleBundle}
              disabled={bundle.isPending}
            >
              {bundle.isPending ? <Spinner /> : `Bundle ${selected.size} order${selected.size === 1 ? "" : "s"}`}
            </Button>
          </div>
        </div>
      )}

      {error && (
        <div className="mb-3 rounded border border-red-200 bg-red-50 px-4 py-2 text-[12px] text-red-700">
          {error}
        </div>
      )}

      <Card>
        {pending.isLoading && (
          <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
            <Spinner /> Loading pending orders…
          </div>
        )}
        {!pending.isLoading && orders.length === 0 && (
          <div className="p-6">
            <EmptyState
              title="No pending orders"
              description="Place a test order in your dev store and it will appear here within a few seconds."
            />
          </div>
        )}
        {orders.length > 0 && (
          <Table>
            <THead>
              <TR>
                <TH style={{ width: 36 }}>
                  <input
                    type="checkbox"
                    aria-label={allSelected ? "Deselect all" : "Select all"}
                    checked={allSelected}
                    onChange={toggleAll}
                  />
                </TH>
                <TH style={{ width: 200 }}>Order</TH>
                <TH>Customer</TH>
                <TH>Destination</TH>
                <TH style={{ textAlign: "right", width: 80 }}>Items</TH>
                <TH style={{ textAlign: "right", width: 100 }}>Weight</TH>
                <TH style={{ textAlign: "right", width: 110 }}>Value</TH>
                <TH style={{ width: 100 }}>Source</TH>
                <TH style={{ width: 110, textAlign: "right" }}>Ingested</TH>
              </TR>
            </THead>
            <TBody>
              {orders.map((o) => {
                const isChecked = selected.has(o.id);
                return (
                  <TR
                    key={o.id}
                    className={isChecked ? "bg-ship-orange-50" : undefined}
                  >
                    <TD>
                      <input
                        type="checkbox"
                        aria-label={`Select order ${o.externalOrderId}`}
                        checked={isChecked}
                        onChange={() => toggle(o.id)}
                      />
                    </TD>
                    <TD>
                      <button
                        type="button"
                        onClick={() => toggle(o.id)}
                        className="font-mono text-[12.5px] text-ship-orange-700 hover:underline"
                      >
                        #{o.externalOrderId}
                      </button>
                    </TD>
                    <TD>{o.customerName ?? <span className="text-ink-400">—</span>}</TD>
                    <TD>
                      {o.destinationCity ? `${o.destinationCity}, ${o.destinationCountry}` : <span className="text-ink-400">—</span>}
                    </TD>
                    <TD style={{ textAlign: "right" }}>{o.itemCount}</TD>
                    <TD style={{ textAlign: "right" }}>
                      {o.totalWeightGrams > 0 ? `${(o.totalWeightGrams / 1000).toFixed(2)} kg` : "—"}
                    </TD>
                    <TD style={{ textAlign: "right" }}>
                      {formatMoney(o.declaredValue, o.currency)}
                    </TD>
                    <TD>
                      <Badge variant="neutral">{o.platformCode}</Badge>
                    </TD>
                    <TD style={{ textAlign: "right" }} className="text-ink-500">
                      {formatRelative(o.ingestedAt)}
                    </TD>
                  </TR>
                );
              })}
            </TBody>
          </Table>
        )}
      </Card>

      {orders.length > 0 && selected.size === 0 && (
        <div className="mt-3 text-[12px] text-ink-500">
          Tip: select a few orders to enable the bundle action.
        </div>
      )}

      {visibleSelected.length > 0 && (
        <div className="sr-only" aria-live="polite">
          {visibleSelected.length} order{visibleSelected.length === 1 ? "" : "s"} selected
        </div>
      )}
    </div>
  );
}
