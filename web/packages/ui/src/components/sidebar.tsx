import type { ReactNode } from "react";
import { Fragment } from "react";
import { cn } from "../lib/cn";
import { IconSettings } from "./icons";
import { SHIP_CLUSTER_URL } from "./ship-logo";

export type SidebarVariant = "ops" | "customer";

export interface SidebarItem {
  id: string;
  icon: ReactNode;
  label: string;
  count?: number | string;
  /** Optional href; when set, the item renders as `<a href>` so React Router's
   *  outer link wrapping handles navigation. The component itself stays
   *  router-agnostic. */
  href?: string;
}

export interface SidebarSection {
  section: string;
  items: SidebarItem[];
}

export interface SidebarProps {
  items: readonly SidebarSection[];
  activeId: string;
  brandLabel: string;
  variant?: SidebarVariant;
  onPick?: (id: string) => void;
  /** Renders a custom element (e.g. React Router <Link>) for each item.
   *  Receives the SidebarItem and the rendered children, must return JSX. */
  renderItem?: (item: SidebarItem, children: ReactNode, isActive: boolean) => ReactNode;
}

const ROOT_VARIANT: Record<SidebarVariant, string> = {
  ops: "bg-ship-navy-900 text-white/[0.78]",
  customer: "bg-[#fffaf5] text-ink-700 border-r border-ship-orange-100",
};

const SECTION_VARIANT: Record<SidebarVariant, string> = {
  ops: "text-white/[0.42]",
  customer: "text-ink-400",
};

const ITEM_BASE = "flex items-center gap-2.5 px-2.5 py-2 rounded text-[13px] font-medium leading-none cursor-pointer relative";
const ITEM_VARIANT: Record<SidebarVariant, { base: string; hover: string; active: string }> = {
  ops: {
    base:   "text-white/80",
    hover:  "hover:bg-white/[0.06] hover:text-white",
    active: "bg-white/[0.10] text-white shadow-[inset_3px_0_0_white]",
  },
  customer: {
    base:   "text-ink-700",
    hover:  "hover:bg-ship-orange-50 hover:text-ship-orange-700",
    active: "bg-ship-orange-50 text-ship-orange-700 shadow-[inset_3px_0_0_var(--ship-orange-500)]",
  },
};

export function Sidebar({
  items,
  activeId,
  brandLabel,
  variant = "ops",
  onPick,
  renderItem,
}: SidebarProps) {
  const isOps = variant === "ops";

  return (
    <aside
      className={cn(
        "flex flex-col gap-0.5 w-[248px] flex-shrink-0 px-3.5 py-5 relative overflow-hidden",
        ROOT_VARIANT[variant],
      )}
    >
      <div className="relative flex items-center h-16 pl-1 mb-3">
        <img
          src={SHIP_CLUSTER_URL}
          alt=""
          aria-hidden
          className="absolute -left-[150px] -top-[150px] w-[200px] h-auto pointer-events-none z-0"
          style={{
            opacity: isOps ? 0.18 : 0.22,
            filter: isOps ? "brightness(0) invert(1)" : undefined,
          }}
        />
        <span
          className={cn(
            "relative font-display text-[26px] leading-none font-bold tracking-[-0.025em]",
            isOps ? "text-white" : "text-ship-orange-600",
          )}
        >
          Ship
        </span>
        <span
          className={cn(
            "relative font-display text-[26px] leading-none font-medium tracking-[-0.02em] ml-1.5",
            isOps ? "text-ship-orange-300" : "text-ship-orange-500",
          )}
        >
          Hub
        </span>
      </div>

      <div className="px-1 pb-1 relative">
        <span
          className={cn(
            "inline-block rounded-full px-2.5 py-1 text-[11px] font-semibold leading-none tracking-[0.04em] uppercase",
            isOps
              ? "bg-white/[0.06] text-white/80 border border-white/10"
              : "bg-white text-ship-orange-700 border border-ship-orange-100",
          )}
        >
          {brandLabel}
        </span>
      </div>

      {items.map((sec) => (
        <Fragment key={sec.section}>
          <div className={cn("text-[10px] font-semibold uppercase tracking-[0.1em] pt-4 px-2.5 pb-2", SECTION_VARIANT[variant])}>
            {sec.section}
          </div>
          {sec.items.map((it) => {
            const active = activeId === it.id;
            const inner = (
              <>
                <span className="size-4 opacity-85">{it.icon}</span>
                <span className="flex-1">{it.label}</span>
                {it.count != null && (
                  <span
                    className={cn(
                      "font-mono text-[11px] leading-none",
                      isOps ? "text-white/50" : "text-ink-400",
                    )}
                  >
                    {it.count}
                  </span>
                )}
              </>
            );
            const className = cn(
              ITEM_BASE,
              ITEM_VARIANT[variant].base,
              ITEM_VARIANT[variant].hover,
              active && ITEM_VARIANT[variant].active,
            );
            if (renderItem) return <Fragment key={it.id}>{renderItem(it, inner, active)}</Fragment>;
            return (
              <div
                key={it.id}
                className={className}
                onClick={() => onPick?.(it.id)}
                role="button"
                tabIndex={0}
              >
                {inner}
              </div>
            );
          })}
        </Fragment>
      ))}

      <div className="flex-1" />
      <div className={cn(ITEM_BASE, ITEM_VARIANT[variant].base, ITEM_VARIANT[variant].hover, "mt-2")}>
        <span className="size-4 opacity-85"><IconSettings /></span>
        <span>Settings</span>
      </div>
    </aside>
  );
}
