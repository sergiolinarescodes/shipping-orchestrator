import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Badge, Button, Card, Input, Label, Spinner } from "@ship/ui";
import { useOnboardingFlowsQuery, useStartOnboardingMutation } from "../../api/queries";

export default function NewOnboardingPage() {
  const flows = useOnboardingFlowsQuery();
  const start = useStartOnboardingMutation();
  const navigate = useNavigate();
  const [flowCode, setFlowCode] = useState<string | null>(null);
  const [contactEmail, setContactEmail] = useState("");

  if (flows.isLoading) {
    return (
      <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
        <Spinner /> Loading flows…
      </div>
    );
  }
  if (!flows.data || flows.data.length === 0) {
    return <div className="p-6 text-[13px] text-ink-500">No flows registered.</div>;
  }

  const selectedFlow = flowCode ? flows.data.find((f) => f.code === flowCode) : flows.data[0];

  return (
    <div className="p-6 grid gap-6 max-w-3xl">
      <div>
        <h1 className="text-[18px] font-semibold text-ink-800">Start a new onboarding</h1>
        <p className="text-[12px] text-ink-500 mt-1">
          Pick a flow. Each defines the steps the operator (or a future self-serve user) walks through.
          Adding a new flow is a one-class addition in the application layer — no change to the wizard UI.
        </p>
      </div>
      <div className="grid grid-cols-2 gap-3">
        {flows.data.map((flow) => {
          const isSelected = (selectedFlow?.code ?? "") === flow.code;
          return (
            <button
              key={flow.code}
              type="button"
              onClick={() => setFlowCode(flow.code)}
              className={
                "rounded-md border bg-white p-4 text-left transition-shadow " +
                (isSelected
                  ? "border-ship-orange-500 shadow-[0_0_0_4px_var(--ship-orange-50)]"
                  : "border-border hover:border-ink-300")
              }
            >
              <div className="flex items-center justify-between">
                <span className="text-[14px] font-semibold text-ink-800">{flow.displayTitle}</span>
                <Badge variant="neutral">{flow.audience}</Badge>
              </div>
              <span className="text-[11px] text-ink-500">{flow.code}</span>
              <ul className="mt-2 list-disc pl-4 text-[12px] text-ink-500">
                {flow.steps.map((s) => (
                  <li key={s.code}>{s.displayTitle}</li>
                ))}
              </ul>
            </button>
          );
        })}
      </div>

      <Card pad="lg" className="grid gap-3 max-w-md">
        <div>
          <Label htmlFor="contactEmail">Contact email (optional)</Label>
          <Input
            id="contactEmail"
            type="email"
            value={contactEmail}
            onChange={(e) => setContactEmail(e.target.value)}
            placeholder="ops@acme.test"
          />
        </div>
        <Button
          variant="primary"
          size="md"
          disabled={start.isPending || !selectedFlow}
          onClick={() => {
            if (!selectedFlow) return;
            start.mutate(
              { flowCode: selectedFlow.code, contactEmail: contactEmail || null },
              { onSuccess: (resp) => navigate(`/onboarding/${resp.processId}`) },
            );
          }}
        >
          {start.isPending ? <Spinner /> : "Start onboarding"}
        </Button>
      </Card>
    </div>
  );
}
