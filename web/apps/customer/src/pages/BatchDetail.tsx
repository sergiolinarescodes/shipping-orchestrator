import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  Badge, Button, Card, PageTitleRule, Spinner, StatusBadge,
  Table, TBody, TD, TH, THead, TR,
} from "@ship/ui";
import { useBatchQuery } from "../api/queries";
import { PrintLabelsModal } from "../components/PrintLabelsModal";

function formatDate(iso: string | null) {
  if (!iso) return "—";
  return new Date(iso).toLocaleString();
}

export default function BatchDetailPage() {
  const { batchId } = useParams<{ batchId: string }>();
  const batch = useBatchQuery(batchId);
  const [printOpen, setPrintOpen] = useState(false);

  const data = batch.data;

  return (
    <div className="px-7 py-6">
      <div className="mb-5 flex items-start justify-between gap-4">
        <div>
          <PageTitleRule />
          <div className="text-[11px] font-semibold uppercase tracking-[0.04em] mb-1.5 text-ship-navy-500">
            Batch
          </div>
          <h1 className="font-display text-[28px] leading-[1.15] font-semibold tracking-[-0.02em] text-ship-navy-800 font-mono">
            {batchId}
          </h1>
          <div className="text-[13px] text-ink-500 mt-1 flex items-center gap-2">
            {data ? (
              <>
                <StatusBadge status={data.status} />
                <span>·</span>
                <span>Created {formatDate(data.createdAt)}</span>
                {data.completedAt && (
                  <>
                    <span>·</span>
                    <span>Completed {formatDate(data.completedAt)}</span>
                  </>
                )}
              </>
            ) : (
              <Spinner />
            )}
          </div>
        </div>
        <Button
          variant="primary"
          size="md"
          disabled={!data || data.shipments.length === 0}
          onClick={() => setPrintOpen(true)}
        >
          Print labels
        </Button>
      </div>

      {data && (
        <div className="grid grid-cols-3 gap-3 mb-4">
          <Card>
            <div className="px-4 py-3">
              <div className="text-[11px] uppercase tracking-[0.04em] text-ink-400">Parcels</div>
              <div className="text-[20px] font-semibold text-ship-navy-800">{data.parcelCount}</div>
            </div>
          </Card>
          <Card>
            <div className="px-4 py-3">
              <div className="text-[11px] uppercase tracking-[0.04em] text-ink-400">Labeled</div>
              <div className="text-[20px] font-semibold text-green-700">{data.successCount}</div>
            </div>
          </Card>
          <Card>
            <div className="px-4 py-3">
              <div className="text-[11px] uppercase tracking-[0.04em] text-ink-400">Failed</div>
              <div className="text-[20px] font-semibold text-red-700">{data.failureCount}</div>
            </div>
          </Card>
        </div>
      )}

      <Card>
        {batch.isLoading && (
          <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
            <Spinner /> Loading batch…
          </div>
        )}
        {batch.isError && (
          <div className="p-6 text-[13px] text-red-700">
            Couldn't load this batch.
          </div>
        )}
        {data && (
          <Table>
            <THead>
              <TR>
                <TH style={{ width: 220 }}>Tracking</TH>
                <TH>Carrier</TH>
                <TH>Status</TH>
                <TH style={{ width: 220 }}>Label</TH>
                <TH style={{ textAlign: "right", width: 160 }}>Updated</TH>
              </TR>
            </THead>
            <TBody>
              {data.shipments.map((s) => (
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
                    {s.trackingNumber ? (
                      <Button
                        variant="ghost"
                        size="sm"
                        disabled
                        title="PDF generation not enabled in this showcase build"
                      >
                        Print
                      </Button>
                    ) : (
                      <span className="text-ink-400">{s.failureReason ?? "—"}</span>
                    )}
                  </TD>
                  <TD style={{ textAlign: "right" }} className="text-ink-500">
                    {formatDate(s.updatedAt)}
                  </TD>
                </TR>
              ))}
            </TBody>
          </Table>
        )}
      </Card>

      {data && (
        <PrintLabelsModal
          open={printOpen}
          shipments={data.shipments}
          onClose={() => setPrintOpen(false)}
        />
      )}
    </div>
  );
}
