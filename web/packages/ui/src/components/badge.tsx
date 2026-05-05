import type { HTMLAttributes } from "react";
import { cn } from "../lib/cn";

export type BadgeVariant =
  | "neutral"
  | "success"
  | "info"
  | "warn"
  | "danger"
  | "brand"
  | "purple";

const VARIANT: Record<BadgeVariant, string> = {
  neutral: "bg-ink-100 text-ink-600",
  success: "bg-green-50 text-green-700",
  info:    "bg-blue-50 text-blue-700",
  warn:    "bg-amber-50 text-amber-500",
  danger:  "bg-red-50 text-red-500",
  brand:   "bg-ship-orange-50 text-ship-orange-700",
  purple:  "bg-purple-50 text-purple-500",
};

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  variant?: BadgeVariant;
  /** Render a small leading dot in `currentColor` */
  dot?: boolean;
}

export function Badge({ variant = "neutral", dot, className, children, ...rest }: BadgeProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full px-2 py-[2px] font-semibold text-[11px] leading-[1.6] tracking-[0.01em]",
        VARIANT[variant],
        className,
      )}
      {...rest}
    >
      {dot && (
        <span
          aria-hidden
          className="size-[6px] rounded-full bg-current opacity-90"
        />
      )}
      {children}
    </span>
  );
}
