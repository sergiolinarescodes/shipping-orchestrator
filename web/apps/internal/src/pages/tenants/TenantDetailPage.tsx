import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  Badge,
  Button,
  Card,
  EmptyState,
  Spinner,
  Table,
  TBody,
  TD,
  TH,
  THead,
  TR,
} from "@ship/ui";
import {
  useTenantDetailQuery,
  useSuspendTenantMutation,
  useReverifyConnectionMutation,
} from "../../api/queries";
import { SendTestParcelDialog } from "./SendTestParcelDialog";

export default function TenantDetailPage() {
  const { tenantId = "" } = useParams<{ tenantId: string }>();
  const tenant = useTenantDetailQuery(tenantId);
  const suspend = useSuspendTenantMutation(tenantId);
  const reverify = useReverifyConnectionMutation(tenantId);
  const [dialogOpen, setDialogOpen] = useState(false);

  if (tenant.isLoading) {
    return (
      <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
        <Spinner /> Loading…
      </div>
    );
  }
  if (!tenant.data) {
    return <div className="p-6 text-[13px] text-ink-500">Tenant not found.</div>;
  }
  const t = tenant.data;

  return (
    <div className="p-6 grid gap-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-[18px] font-semibold text-ink-800">{t.displayName}</h1>
          <div className="mt-1 flex items-center gap-2 text-[12px] text-ink-500">
            <Badge variant={t.status === "Active" ? "success" : t.status === "Suspended" ? "danger" : "warn"}>
              {t.status}
            </Badge>
            {t.carrierMode && <Badge variant="neutral">{t.carrierMode}</Badge>}
            {t.contactEmail && (
              <>
                <span>·</span>
                <span>{t.contactEmail}</span>
              </>
            )}
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="primary" size="md" onClick={() => setDialogOpen(true)}>
            Send test parcel
          </Button>
          <Button
            variant="secondary"
            size="md"
            disabled={t.status === "Suspended" || suspend.isPending}
            onClick={() => {
              const reason = window.prompt("Suspend reason?");
              if (reason && reason.trim()) suspend.mutate(reason.trim());
            }}
          >
            {suspend.isPending ? "Suspending…" : "Suspend"}
          </Button>
        </div>
      </div>

      <SendTestParcelDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        tenantId={t.tenantId}
      />

      <div className="grid grid-cols-2 gap-4">
        <Card pad="lg">
          <h2 className="text-[14px] font-semibold text-ink-800 mb-2">Ecommerce connections</h2>
          {t.ecommerceConnections.length === 0 ? (
            <EmptyState title="No ecommerce connections" />
          ) : (
            <Table>
              <THead>
                <TR><TH>Platform</TH><TH>Account</TH><TH>Status</TH><TH>Mode</TH><TH>Installed</TH><TH></TH></TR>
              </THead>
              <TBody>
                {t.ecommerceConnections.map((c) => (
                  <TR key={c.connectionId}>
                    <TD className="font-medium">{c.platformCode}</TD>
                    <TD className="text-ink-500">{c.externalAccountId}</TD>
                    <TD>
                      <Badge
                        variant={
                          c.status === "Active"
                            ? "success"
                            : c.status === "Rejected"
                              ? "danger"
                              : "warn"
                        }
                      >
                        {c.status}
                      </Badge>
                    </TD>
                    <TD><Badge variant="neutral">{c.mode ?? "real"}</Badge></TD>
                    <TD className="text-ink-500">{new Date(c.installedAt).toLocaleString()}</TD>
                    <TD>
                      <button
                        type="button"
                        className="text-ship-orange-700 text-[12px] underline disabled:opacity-50"
                        disabled={reverify.isPending}
                        onClick={() => reverify.mutate(c.connectionId)}
                      >
                        re-verify
                      </button>
                    </TD>
                  </TR>
                ))}
              </TBody>
            </Table>
          )}
        </Card>

        <Card pad="lg">
          <h2 className="text-[14px] font-semibold text-ink-800 mb-2">Carrier assignments</h2>
          {t.carrierAssignments.length === 0 ? (
            <EmptyState title="No carriers assigned" />
          ) : (
            <Table>
              <THead>
                <TR><TH>Carrier</TH><TH>Priority</TH><TH>From → To</TH><TH>Mode</TH></TR>
              </THead>
              <TBody>
                {t.carrierAssignments.map((a) => (
                  <TR key={a.assignmentId}>
                    <TD className="font-medium">{a.carrierCode}</TD>
                    <TD>{a.priority}</TD>
                    <TD className="text-ink-500">
                      {a.originCountries.join(",")} → {a.destinationCountries.join(",")}
                    </TD>
                    <TD><Badge variant="neutral">{a.mode ?? "real"}</Badge></TD>
                  </TR>
                ))}
              </TBody>
            </Table>
          )}
        </Card>
      </div>

      <Card pad="lg">
        <h2 className="text-[14px] font-semibold text-ink-800 mb-2">Recent batches</h2>
        {t.recentBatches.length === 0 ? (
          <EmptyState title="No batches yet" description="Send a test parcel to see one here." />
        ) : (
          <Table>
            <THead>
              <TR>
                <TH>Batch</TH><TH>Status</TH><TH>Parcels</TH><TH>Success</TH><TH>Failed</TH><TH>Created</TH><TH></TH>
              </TR>
            </THead>
            <TBody>
              {t.recentBatches.map((b) => (
                <TR key={b.batchId}>
                  <TD className="font-mono text-[11px] text-ink-500">{b.batchId.slice(0, 8)}…</TD>
                  <TD><Badge variant={b.status === "Completed" ? "success" : b.status === "Failed" ? "danger" : "info"}>{b.status}</Badge></TD>
                  <TD>{b.parcelCount}</TD>
                  <TD>{b.successCount}</TD>
                  <TD>{b.failureCount}</TD>
                  <TD className="text-ink-500">{new Date(b.createdAt).toLocaleString()}</TD>
                  <TD>
                    <Link to={`/tenants/${t.tenantId}/batches/${b.batchId}`} className="text-ship-orange-700 text-[12px] underline">
                      track
                    </Link>
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
