import { cn } from "../lib/cn";

export interface TimelineItem {
  id: string | number;
  title: string;
  description?: string;
  location?: string | null;
  occurredAt: string | Date;
  status?: "info" | "success" | "warn" | "danger";
}

export function Timeline({ items, className }: { items: TimelineItem[]; className?: string }) {
  if (items.length === 0) {
    return (
      <div className={cn("text-[13px] text-ink-400", className)}>No events yet.</div>
    );
  }
  return (
    <ol className={cn("relative ml-3 flex flex-col gap-4 border-l border-border pl-5", className)}>
      {items.map((item) => (
        <li key={item.id} className="relative">
          <span
            aria-hidden
            className={cn(
              "absolute -left-[27px] top-0.5 size-3 rounded-full border-2 border-white",
              item.status === "success" && "bg-green-500",
              item.status === "warn" && "bg-amber-500",
              item.status === "danger" && "bg-red-500",
              (!item.status || item.status === "info") && "bg-ship-navy-500",
            )}
          />
          <div className="flex flex-col gap-0.5">
            <span className="text-[13px] font-medium text-ink-800">{item.title}</span>
            {item.description && (
              <span className="text-[12px] text-ink-500">{item.description}</span>
            )}
            <div className="flex gap-2 text-[11px] uppercase tracking-wide text-ink-400">
              <span>{formatTime(item.occurredAt)}</span>
              {item.location && <span>· {item.location}</span>}
            </div>
          </div>
        </li>
      ))}
    </ol>
  );
}

function formatTime(value: string | Date): string {
  const d = typeof value === "string" ? new Date(value) : value;
  return d.toLocaleString();
}
