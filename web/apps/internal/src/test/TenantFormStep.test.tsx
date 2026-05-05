import { describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ToastProvider } from "@ship/ui";
import { TenantFormStep } from "../pages/onboarding/steps/TenantFormStep";
import type { OnboardingStepView } from "../types/api";

const baseStep: OnboardingStepView = {
  code: "tenant.create",
  sequence: 1,
  displayTitle: "Create tenant",
  kind: "SyncInput",
  rendererCode: "tenant-form",
  status: "Pending",
  skippable: false,
  isCommitted: true,
  failureReason: null,
  externalCorrelationId: null,
  awaitingExpiresAt: null,
  startedAt: null,
  completedAt: null,
  collectedPayload: null,
  resultPayload: null,
  metadata: null,
};

describe("TenantFormStep", () => {
  it("validates required display name", async () => {
    const onAdvance = vi.fn();
    render(
      <ToastProvider>
        <TenantFormStep processId="p" step={baseStep} onAdvance={onAdvance} isPending={false} />
      </ToastProvider>,
    );
    await userEvent.click(screen.getByRole("button", { name: /create tenant/i }));
    await waitFor(() => expect(screen.getByText(/required/i)).toBeInTheDocument());
    expect(onAdvance).not.toHaveBeenCalled();
  });

  it("submits payload with the entered values", async () => {
    const onAdvance = vi.fn();
    render(
      <ToastProvider>
        <TenantFormStep processId="p" step={baseStep} onAdvance={onAdvance} isPending={false} />
      </ToastProvider>,
    );
    await userEvent.type(screen.getByLabelText(/Tenant display name/i), "Acme NL");
    await userEvent.type(screen.getByLabelText(/Contact email/i), "ops@acme.test");
    await userEvent.click(screen.getByRole("button", { name: /create tenant/i }));
    await waitFor(() =>
      expect(onAdvance).toHaveBeenCalledWith({
        displayName: "Acme NL",
        contactEmail: "ops@acme.test",
      }),
    );
  });
});
