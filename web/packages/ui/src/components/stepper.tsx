import type { HTMLAttributes } from "react";
import { cn } from "../lib/cn";

export type StepperStatus = "pending" | "current" | "awaiting" | "completed" | "failed" | "skipped";

export interface StepperItem {
  code: string;
  title: string;
  status: StepperStatus;
}

export interface StepperProps extends HTMLAttributes<HTMLOListElement> {
  steps: StepperItem[];
  onStepClick?: (code: string) => void;
}

const DOT_STYLE: Record<StepperStatus, string> = {
  pending:   "bg-ink-200 text-ink-500",
  current:   "bg-ship-orange-500 text-white shadow-[0_0_0_4px_var(--ship-orange-50)]",
  awaiting:  "bg-amber-500 text-white",
  completed: "bg-ship-navy-500 text-white",
  failed:    "bg-red-500 text-white",
  skipped:   "bg-ink-100 text-ink-500",
};

const LABEL_STYLE: Record<StepperStatus, string> = {
  pending:   "text-ink-500",
  current:   "text-ink-800 font-semibold",
  awaiting:  "text-ink-800 font-semibold",
  completed: "text-ink-600",
  failed:    "text-red-600 font-semibold",
  skipped:   "text-ink-400 line-through",
};

export function Stepper({ steps, onStepClick, className, ...rest }: StepperProps) {
  return (
    <ol
      role="list"
      aria-label="Onboarding progress"
      className={cn("flex flex-col gap-3", className)}
      {...rest}
    >
      {steps.map((step, index) => {
        const clickable = !!onStepClick;
        return (
          <li
            key={step.code}
            aria-current={step.status === "current" ? "step" : undefined}
            className="flex items-start gap-3"
          >
            <button
              type="button"
              disabled={!clickable}
              onClick={() => onStepClick?.(step.code)}
              aria-label={`${step.title} (${step.status})`}
              className={cn(
                "size-6 shrink-0 rounded-full flex items-center justify-center text-[11px] font-semibold leading-none",
                "transition-shadow duration-100",
                DOT_STYLE[step.status],
                clickable ? "cursor-pointer" : "cursor-default",
              )}
            >
              {step.status === "completed" || step.status === "skipped" ? "✓" : index + 1}
            </button>
            <div className="flex flex-col">
              <span className={cn("text-[13px] leading-snug", LABEL_STYLE[step.status])}>
                {step.title}
              </span>
              <span className="text-[11px] uppercase tracking-wide text-ink-400">{step.status}</span>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
