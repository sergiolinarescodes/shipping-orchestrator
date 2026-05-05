import { Link } from "react-router-dom";
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
import { useOnboardingProcessesQuery } from "../../api/queries";

export default function OnboardingListPage() {
  const query = useOnboardingProcessesQuery();

  return (
    <div className="p-6 grid gap-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-[18px] font-semibold text-ink-800">Onboarding</h1>
          <p className="text-[12px] text-ink-500">In-flight and completed onboarding processes.</p>
        </div>
        <Link to="/onboarding/new">
          <Button variant="primary" size="md">
            Start new onboarding
          </Button>
        </Link>
      </div>

      <Card pad="none">
        {query.isLoading ? (
          <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
            <Spinner /> Loading…
          </div>
        ) : !query.data || query.data.length === 0 ? (
          <EmptyState
            title="No onboardings yet"
            description="Click 'Start new onboarding' to bring the first client onto the platform."
            action={
              <Link to="/onboarding/new">
                <Button variant="primary" size="md">
                  Start new onboarding
                </Button>
              </Link>
            }
          />
        ) : (
          <Table>
            <THead>
              <TR>
                <TH>Flow</TH>
                <TH>Status</TH>
                <TH>Current step</TH>
                <TH>Tenant</TH>
                <TH>Started</TH>
                <TH></TH>
              </TR>
            </THead>
            <TBody>
              {query.data.map((p) => (
                <TR key={p.processId}>
                  <TD className="font-medium">{p.flowTitle}</TD>
                  <TD>
                    <Badge variant={badgeFor(p.status)}>{p.status}</Badge>
                  </TD>
                  <TD className="text-ink-500">{p.currentStepCode ?? "—"}</TD>
                  <TD>
                    {p.tenantId ? (
                      <Link className="text-ship-orange-700 underline" to={`/tenants/${p.tenantId}`}>
                        {p.tenantId.slice(0, 8)}…
                      </Link>
                    ) : (
                      "—"
                    )}
                  </TD>
                  <TD className="text-ink-500">{new Date(p.createdAt).toLocaleString()}</TD>
                  <TD>
                    <Link to={`/onboarding/${p.processId}`} className="text-ship-orange-700 text-[12px] underline">
                      open
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

function badgeFor(status: string): "info" | "success" | "warn" | "danger" | "neutral" {
  switch (status) {
    case "InProgress": return "info";
    case "Completed": return "success";
    case "TimedOut": return "warn";
    case "Cancelled": return "neutral";
    default: return "neutral";
  }
}
