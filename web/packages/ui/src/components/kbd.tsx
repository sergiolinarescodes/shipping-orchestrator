import type { HTMLAttributes } from "react";
import { cn } from "../lib/cn";

export function Kbd({ className, ...rest }: HTMLAttributes<HTMLSpanElement>) {
  return (
    <span
      className={cn(
        "font-mono text-[11px] leading-none px-[5px] py-[2px] rounded-sm",
        "bg-ink-100 text-ink-600 border border-border",
        className,
      )}
      {...rest}
    />
  );
}
