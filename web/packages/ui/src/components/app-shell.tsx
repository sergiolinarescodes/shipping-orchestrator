import type { ReactNode } from "react";
import { cn } from "../lib/cn";

export interface AppShellProps {
  sidebar: ReactNode;
  topbar: ReactNode;
  children: ReactNode;
  className?: string;
}

/**
 * Two-column layout: fixed sidebar + flexible main column with topbar above
 * a scrollable content region. Both dashboards use this shell.
 */
export function AppShell({ sidebar, topbar, children, className }: AppShellProps) {
  return (
    <div className={cn("flex h-screen bg-canvas", className)}>
      {sidebar}
      <div className="flex flex-col flex-1 min-w-0">
        {topbar}
        <div className="flex-1 overflow-auto">{children}</div>
      </div>
    </div>
  );
}

export function PageTitleRule({ accent }: { accent?: boolean }) {
  return (
    <span
      className={cn(
        "inline-block w-8 h-[3px] rounded-sm mb-3",
        accent ? "bg-ship-orange-500" : "bg-ship-navy-500",
      )}
    />
  );
}
