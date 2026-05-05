import type { ReactNode } from "react";
import { cn } from "../lib/cn";

export interface EmptyStateProps {
  title: string;
  description?: string;
  action?: ReactNode;
  className?: string;
}

export function EmptyState({ title, description, action, className }: EmptyStateProps) {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center gap-2 rounded-md border border-dashed border-border bg-white px-6 py-10 text-center",
        className,
      )}
    >
      <span className="text-[14px] font-semibold text-ink-700">{title}</span>
      {description && <span className="text-[12px] text-ink-500 max-w-md">{description}</span>}
      {action && <div className="mt-3">{action}</div>}
    </div>
  );
}
