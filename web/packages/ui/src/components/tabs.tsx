import { cn } from "../lib/cn";

export interface TabItem {
  id: string;
  label: string;
}

export interface TabsProps {
  items: readonly TabItem[];
  activeId: string;
  onChange?: (id: string) => void;
  className?: string;
}

export function Tabs({ items, activeId, onChange, className }: TabsProps) {
  return (
    <div className={cn("flex gap-0.5 border-b border-border", className)}>
      {items.map((it) => {
        const active = it.id === activeId;
        return (
          <button
            key={it.id}
            type="button"
            onClick={() => onChange?.(it.id)}
            className={cn(
              "px-3.5 py-2.5 text-[13px] leading-none font-medium cursor-pointer",
              "border-b-2 -mb-px transition-colors",
              active
                ? "text-ship-navy-700 border-ship-navy-500"
                : "text-ink-500 border-transparent hover:text-ink-800",
            )}
          >
            {it.label}
          </button>
        );
      })}
    </div>
  );
}
