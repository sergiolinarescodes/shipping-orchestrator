import type { HTMLAttributes } from "react";
import { cn } from "../lib/cn";

export type CardPad = "none" | "default" | "lg";

const PAD: Record<CardPad, string> = {
  none: "",
  default: "p-5",
  lg: "p-6",
};

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  pad?: CardPad;
  /** Adds the navy left rail used by stat-style cards */
  metric?: boolean;
  /** When `metric`, swap the rail to ship-orange */
  accent?: boolean;
}

export function Card({
  pad = "none",
  metric,
  accent,
  className,
  children,
  ...rest
}: CardProps) {
  return (
    <div
      className={cn(
        "bg-white rounded-md shadow-sm",
        PAD[pad],
        metric && "relative overflow-hidden",
        className,
      )}
      {...rest}
    >
      {metric && (
        <span
          aria-hidden
          className={cn(
            "absolute left-0 top-3 bottom-3 w-[3px] rounded-r",
            accent ? "bg-ship-orange-500" : "bg-ship-navy-500",
          )}
        />
      )}
      {children}
    </div>
  );
}

export function Divider({ className }: { className?: string }) {
  return <div className={cn("h-px bg-border", className)} />;
}
