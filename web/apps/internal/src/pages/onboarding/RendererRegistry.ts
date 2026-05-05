import type { ComponentType } from "react";
import type { OnboardingStepView } from "../../types/api";
import { TenantFormStep } from "./steps/TenantFormStep";

export interface StepRendererProps {
  processId: string;
  step: OnboardingStepView;
  onAdvance: (payload: unknown) => void;
  isPending: boolean;
}

/**
 * Map from server-side `rendererCode` to the React component that draws that step. The only
 * shipped flow today is single-step (tenant create); future flows that introduce new step
 * kinds register their renderer here.
 */
export const RENDERERS: Record<string, ComponentType<StepRendererProps>> = {
  "tenant-form": TenantFormStep,
};

export function fallbackRenderer(): ComponentType<StepRendererProps> {
  return TenantFormStep;
}
