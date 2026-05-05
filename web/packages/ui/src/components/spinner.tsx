import { cn } from "../lib/cn";

export function Spinner({ className, label = "Loading…" }: { className?: string; label?: string }) {
  return (
    <span
      role="status"
      aria-label={label}
      className={cn(
        "inline-block size-4 rounded-full border-2 border-ink-200 border-t-ship-orange-500",
        "animate-spin",
        className,
      )}
    />
  );
}
