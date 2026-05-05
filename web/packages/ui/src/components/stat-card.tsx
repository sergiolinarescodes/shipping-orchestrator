import { Card } from "./card";
import { Badge } from "./badge";
import { cn } from "../lib/cn";

export interface StatCardProps {
  label: string;
  value: string;
  /** Positive renders ▲ green, negative ▼ red. */
  delta?: number;
  sub?: string;
  /** Pill rendered to the right of the label */
  accent?: string;
  variant?: "navy" | "accent";
  className?: string;
}

export function StatCard({
  label,
  value,
  delta,
  sub,
  accent,
  variant = "navy",
  className,
}: StatCardProps) {
  return (
    <Card pad="default" metric accent={variant === "accent"} className={cn("flex-1 min-w-0", className)}>
      <div className="flex items-center justify-between mb-2.5">
        <span className="text-[11px] font-semibold uppercase tracking-[0.06em] text-ink-400">
          {label}
        </span>
        {accent && <Badge variant="brand">{accent}</Badge>}
      </div>
      <div className="flex items-baseline gap-2">
        <span className="font-display text-[28px] leading-[1.1] font-semibold tracking-[-0.02em] text-ink-900 tnum">
          {value}
        </span>
        {delta != null && (
          <span
            className={cn(
              "text-[12px] leading-none font-semibold",
              delta >= 0 ? "text-green-500" : "text-red-500",
            )}
          >
            {delta >= 0 ? "▲" : "▼"} {Math.abs(delta)}%
          </span>
        )}
      </div>
      {sub && <div className="mt-1.5 text-[13px] leading-snug text-ink-400">{sub}</div>}
    </Card>
  );
}
