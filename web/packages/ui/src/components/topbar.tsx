import type { ReactNode } from "react";
import { cn } from "../lib/cn";
import { IconSearch, IconBell } from "./icons";
import { Kbd } from "./kbd";

export type TopbarVariant = "ops" | "customer";

export interface TopbarUser {
  initials: string;
  name: string;
  role: string;
}

export interface TopbarProps {
  env: string;
  user: TopbarUser;
  search?: string;
  variant?: TopbarVariant;
  /** Optional slot rendered between the env badge and the user pill (e.g. "Switch tenant"). */
  actions?: ReactNode;
}

export function Topbar({ env, user, search = "Search shipments, tracking, tenants…", variant = "ops", actions }: TopbarProps) {
  return (
    <>
      <div
        className={cn(
          "h-1 flex-shrink-0",
          variant === "ops"
            ? "bg-gradient-to-r from-ship-navy-700 to-ship-navy-500"
            : "bg-gradient-to-r from-ship-orange-500 to-ship-orange-400",
        )}
      />
      <div className="h-14 bg-white border-b border-border px-6 flex items-center gap-4 flex-shrink-0">
        <div className="flex items-center gap-2 h-8 px-2.5 bg-ink-50 rounded w-[320px]">
          <IconSearch size={14} className="text-ink-400" />
          <input
            placeholder={search}
            className="bg-transparent border-0 outline-none flex-1 text-[13px] leading-none text-ink-800 placeholder:text-ink-400"
          />
          <Kbd>⌘K</Kbd>
        </div>
        <div className="flex-1" />
        <span
          className={cn(
            "inline-flex items-center gap-1.5 px-2.5 py-1 rounded-sm font-mono text-[11px] font-bold uppercase leading-none tracking-[0.06em] border",
            variant === "ops"
              ? "bg-ship-navy-50 text-ship-navy-700 border-ship-navy-100"
              : "bg-ship-orange-50 text-ship-orange-700 border-ship-orange-100",
          )}
        >
          <span aria-hidden className="size-1.5 rounded-full bg-current" />
          {env}
        </span>
        {actions}
        <button className="inline-flex items-center justify-center size-8 rounded text-ink-600 hover:bg-ink-100 hover:text-ink-800 transition-colors">
          <IconBell size={16} />
        </button>
        <div className="flex items-center gap-2 pl-2 border-l border-border">
          <div
            className={cn(
              "size-7 rounded-full flex items-center justify-center text-[12px] font-semibold leading-none",
              variant === "customer"
                ? "bg-ship-orange-50 text-ship-orange-700"
                : "bg-ship-navy-50 text-ship-navy-700",
            )}
          >
            {user.initials}
          </div>
          <div className="flex flex-col">
            <span className="text-[12px] leading-tight font-medium text-ink-800">{user.name}</span>
            <span className="text-[11px] leading-tight text-ink-400">{user.role}</span>
          </div>
        </div>
      </div>
    </>
  );
}
