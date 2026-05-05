import { Link } from "react-router-dom";
import {
  Badge, Card, Spinner, Table, THead, TBody, TR, TH, TD,
  PageTitleRule, IconChevronR,
} from "@ship/ui";
import { useTenantsListQuery } from "../../api/queries";

function statusVariant(status: string): "info" | "success" | "warn" | "danger" | "neutral" {
  switch (status) {
    case "Active": return "success";
    case "Onboarding": return "info";
    case "Suspended": return "warn";
    default: return "neutral";
  }
}

export default function TenantsListPage() {
  const tenants = useTenantsListQuery();

  return (
    <div className="px-7 py-6">
      <div className="mb-5">
        <PageTitleRule accent />
        <div className="text-[11px] font-semibold uppercase tracking-[0.04em] mb-1.5 text-ship-orange-600">
          Platform
        </div>
        <h1 className="font-display text-[28px] leading-[1.15] font-semibold tracking-[-0.02em] text-ship-navy-800">
          Tenants
        </h1>
        <div className="text-[13px] text-ink-500 mt-1">
          {tenants.isLoading
            ? "Loading from /admin/tenants…"
            : tenants.isError
              ? "Couldn't load tenants. Backend reachable?"
              : `${tenants.data?.length ?? 0} tenant${tenants.data?.length === 1 ? "" : "s"}`}
        </div>
      </div>

      <Card>
        {tenants.isLoading && (
          <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
            <Spinner /> Loading…
          </div>
        )}
        {!tenants.isLoading && tenants.data?.length === 0 && (
          <div className="p-6 text-[13px] text-ink-500">
            No tenants yet. Create one through{" "}
            <Link to="/onboarding/new" className="text-ship-orange-700 underline">onboarding</Link>.
          </div>
        )}
        {tenants.data && tenants.data.length > 0 && (
          <Table>
            <THead>
              <TR>
                <TH>Tenant</TH>
                <TH>ID</TH>
                <TH>Status</TH>
                <TH>Created</TH>
                <TH style={{ width: 40 }} />
              </TR>
            </THead>
            <TBody>
              {tenants.data.map((t) => (
                <TR key={t.tenantId}>
                  <TD>
                    <Link
                      to={`/tenants/${t.tenantId}`}
                      className="text-[13px] font-medium text-ink-800 underline"
                    >
                      {t.displayName}
                    </Link>
                  </TD>
                  <TD>
                    <span className="font-mono text-[12px] text-ink-500">{t.tenantId}</span>
                  </TD>
                  <TD><Badge variant={statusVariant(t.status)}>{t.status}</Badge></TD>
                  <TD className="font-mono text-[12px] text-ink-500">
                    {new Date(t.createdAt).toLocaleString()}
                  </TD>
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
