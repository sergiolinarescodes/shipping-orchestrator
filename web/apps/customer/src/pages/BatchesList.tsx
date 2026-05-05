import { useState } from "react";
import { Link } from "react-router-dom";
import {
  Card, EmptyState, PageTitleRule, Spinner, StatusBadge,
  Table, TBody, TD, TH, THead, TR,
} from "@ship/ui";
import { useBatchesQuery } from "../api/queries";

const FILTERS: ReadonlyArray<{ id: string; label: string; value?: string }> = [
  { id: "all",      label: "All" },
  { id: "active",   label: "In progress", value: "Processing" },
  { id: "complete", label: "Completed",   value: "Completed" },
  { id: "failed",   label: "Failed",      value: "Failed" },
];

function formatDate(iso: string) {
  return new Date(iso).toLocaleString();
}

export default function BatchesListPage() {
  const [filter, setFilter] = useState<string>("all");
  const status = FILTERS.find((f) => f.id === filter)?.value;
  const batches = useBatchesQuery({ take: 100, status });
  const rows = batches.data ?? [];

  return (
    <div className="px-7 py-6">
      <div className="mb-5">
        <PageTitleRule />
        <div className="text-[11px] font-semibold uppercase tracking-[0.04em] mb-1.5 text-ship-navy-500">
          Shipping
        </div>
        <h1 className="font-display text-[28px] leading-[1.15] font-semibold tracking-[-0.02em] text-ship-navy-800">
          Shipment batches
        </h1>
        <div className="text-[13px] text-ink-500 mt-1">
          Bundles produced from pending orders. Each batch generates one shipping label per parcel.
        </div>
      </div>

      <div className="flex items-center gap-1.5 mb-3">
        {FILTERS.map((f) => {
          const active = f.id === filter;
          return (
            <button
              key={f.id}
              type="button"
              onClick={() => setFilter(f.id)}
              className={
                "rounded-full border px-3 py-1 text-[12px] transition-colors " +
                (active
                  ? "border-ship-orange-500 bg-ship-orange-50 text-ship-orange-700"
                  : "border-border bg-white text-ink-600 hover:bg-ink-25")
              }
            >
              {f.label}
            </button>
          );
        })}
      </div>

      <Card>
        {batches.isLoading && (
          <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
            <Spinner /> Loading batches…
          </div>
        )}
        {!batches.isLoading && rows.length === 0 && (
          <div className="p-6">
            <EmptyState
              title="No batches yet"
              description="Bundle a few pending orders into a batch from the Pending orders inbox."
            />
          </div>
        )}
        {rows.length > 0 && (
          <Table>
            <THead>
              <TR>
                <TH style={{ width: 200 }}>Batch</TH>
                <TH style={{ width: 140 }}>Status</TH>
                <TH style={{ textAlign: "right", width: 100 }}>Parcels</TH>
                <TH style={{ textAlign: "right", width: 110 }}>Successful</TH>
                <TH style={{ textAlign: "right", width: 100 }}>Failed</TH>
                <TH style={{ textAlign: "right", width: 180 }}>Created</TH>
              </TR>
            </THead>
            <TBody>
              {rows.map((b) => (
                <TR key={b.batchId}>
                  <TD>
                    <Link
                      to={`/batches/${b.batchId}`}
                      className="font-mono text-[12.5px] text-ship-orange-700 underline"
                    >
                      {b.batchId.slice(0, 8)}…
                    </Link>
                  </TD>
                  <TD><StatusBadge status={b.status} /></TD>
                  <TD style={{ textAlign: "right" }}>{b.parcelCount}</TD>
                  <TD style={{ textAlign: "right" }}>{b.successCount}</TD>
                  <TD style={{ textAlign: "right" }}>{b.failureCount}</TD>
                  <TD style={{ textAlign: "right" }} className="text-ink-500">
                    {formatDate(b.createdAt)}
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
