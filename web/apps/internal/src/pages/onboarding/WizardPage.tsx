import { useMemo } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  Badge,
  Button,
  Card,
  Spinner,
  Stepper,
  type StepperItem,
  type StepperStatus,
  useToast,
} from "@ship/ui";
import {
  useAdvanceOnboardingStepMutation,
  useCancelOnboardingMutation,
  useOnboardingProcessQuery,
} from "../../api/queries";
import type { OnboardingStepStatus, OnboardingStepView } from "../../types/api";
import { RENDERERS, fallbackRenderer } from "./RendererRegistry";

const STATUS_TO_STEPPER: Record<OnboardingStepStatus, StepperStatus> = {
  Pending: "pending",
  Awaiting: "awaiting",
  Completed: "completed",
  Skipped: "skipped",
  Failed: "failed",
};

export default function WizardPage() {
  const { processId = "" } = useParams<{ processId: string }>();
  const navigate = useNavigate();
  const { push } = useToast();
  const processQuery = useOnboardingProcessQuery(processId);
  const advance = useAdvanceOnboardingStepMutation(processId);
  const cancel = useCancelOnboardingMutation(processId);

  const process = processQuery.data;

  const currentStep = useMemo<OnboardingStepView | null>(() => {
    if (!process) return null;
    return (
      process.steps.find(
        (s) => s.status === "Pending" || s.status === "Failed",
      )
      ?? process.steps.find((s) => s.status === "Awaiting")
      ?? null
    );
  }, [process]);

  if (processQuery.isLoading) {
    return (
      <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
        <Spinner /> Loading onboarding process…
      </div>
    );
  }
  if (!process) {
    return <div className="p-6 text-[13px] text-red-500">Onboarding process not found.</div>;
  }

  const stepperItems: StepperItem[] = process.steps
    .slice()
    .sort((a, b) => a.sequence - b.sequence)
    .map((s) => ({
      code: s.code,
      title: s.displayTitle,
      status:
        s.code === currentStep?.code && s.status !== "Completed"
          ? "current"
          : STATUS_TO_STEPPER[s.status],
    }));

  const Renderer = currentStep ? (RENDERERS[currentStep.rendererCode] ?? fallbackRenderer()) : null;

  return (
    <div className="grid grid-cols-[280px_1fr] gap-6 p-6">
      <Card pad="lg" className="self-start">
        <div className="flex flex-col gap-4">
          <div>
            <span className="text-[11px] uppercase tracking-wide text-ink-400">Flow</span>
            <h2 className="text-[15px] font-semibold text-ink-800">{process.flowTitle}</h2>
            <span className="text-[12px] text-ink-500">{process.flowCode}</span>
          </div>
          <Stepper steps={stepperItems} />
        </div>
      </Card>

      <Card pad="lg" className="flex flex-col gap-5">
        <div className="flex items-center justify-between gap-3">
          <div className="flex flex-col">
            <h1 className="text-[16px] font-semibold text-ink-800">
              {currentStep?.displayTitle ?? "Process"}
            </h1>
            <span className="text-[12px] text-ink-500">
              Status: <Badge variant={mapBadge(process.status)}>{process.status}</Badge>
            </span>
          </div>
          <div className="flex items-center gap-2">
            <Link
              to="/onboarding"
              className="text-[12px] text-ink-500 hover:text-ink-800 underline"
            >
              ← all processes
            </Link>
            {process.status === "InProgress" && (
              <Button
                size="sm"
                variant="ghost"
                onClick={() => {
                  cancel.mutate("operator-cancel");
                  push({ title: "Onboarding cancelled", variant: "info" });
                }}
              >
                Cancel
              </Button>
            )}
          </div>
        </div>

        {process.status === "Completed" && process.tenantId && (
          <div className="rounded border border-green-100 bg-green-50 p-4 flex flex-col gap-3">
            <div className="flex items-center justify-between gap-3">
              <div className="flex flex-col gap-1">
                <span className="text-[13px] font-medium text-ink-800">Tenant created.</span>
                <span className="text-[12px] text-ink-600">
                  Tenant <code className="text-[11px]">{process.tenantId}</code> is Active. The customer connects their stores themselves from the dashboard.
                </span>
              </div>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => navigate(`/tenants/${process.tenantId}`)}
              >
                Tenant detail
              </Button>
            </div>
            {process.dashboardUrl && (
              <div className="flex items-center gap-2 rounded border border-green-200 bg-white px-3 py-2">
                <span className="text-[11px] uppercase tracking-wide text-ink-400 shrink-0">Dashboard URL</span>
                <code className="flex-1 text-[12px] text-ink-700 truncate">{process.dashboardUrl}</code>
                <Button
                  variant="primary"
                  size="sm"
                  onClick={async () => {
                    if (!process.dashboardUrl) return;
                    try {
                      await navigator.clipboard.writeText(process.dashboardUrl);
                      push({ title: "Dashboard URL copied", variant: "success" });
                    } catch (e) {
                      push({
                        title: "Could not copy",
                        description: e instanceof Error ? e.message : String(e),
                        variant: "danger",
                      });
                    }
                  }}
                >
                  Copy
                </Button>
              </div>
            )}
          </div>
        )}

        {currentStep && Renderer && (
          <div className="border-t border-border pt-5">
            <Renderer
              processId={process.processId}
              step={currentStep}
              isPending={advance.isPending}
              onAdvance={(payload) => {
                advance.mutate(
                  { stepCode: currentStep.code, payload },
                  {
                    onError: (err) => {
                      push({
                        title: "Step failed",
                        description: err instanceof Error ? err.message : String(err),
                        variant: "danger",
                      });
                    },
                  },
                );
              }}
            />
          </div>
        )}
      </Card>
    </div>
  );
}

function mapBadge(status: string): "info" | "success" | "warn" | "danger" | "neutral" {
  switch (status) {
    case "InProgress": return "info";
    case "Completed": return "success";
    case "Cancelled": return "neutral";
    case "TimedOut": return "warn";
    default: return "neutral";
  }
}
