import { forwardRef, type ButtonHTMLAttributes } from "react";
import { cn } from "../lib/cn";

export type ButtonVariant =
  | "primary"
  | "accent"
  | "navy"
  | "secondary"
  | "ghost"
  | "danger";

export type ButtonSize = "sm" | "md" | "lg";

const VARIANT: Record<ButtonVariant, string> = {
  primary:
    "bg-ship-orange-500 text-white shadow-[0_1px_0_rgba(0,0,0,0.08),inset_0_-1px_0_rgba(0,0,0,0.18)] hover:bg-ship-orange-600",
  accent:
    "bg-ship-orange-500 text-white shadow-[0_1px_0_rgba(0,0,0,0.08),inset_0_-1px_0_rgba(0,0,0,0.18)] hover:bg-ship-orange-600",
  navy:
    "bg-ship-navy-500 text-white shadow-[0_1px_0_rgba(0,0,0,0.08),inset_0_-1px_0_rgba(0,0,0,0.18)] hover:bg-ship-navy-600",
  secondary: "bg-white text-ink-800 shadow-xs hover:bg-ink-50",
  ghost:     "bg-transparent text-ink-600 hover:bg-ink-100 hover:text-ink-800",
  danger:
    "bg-white text-red-500 shadow-[0_0_0_1px_rgba(205,53,0,0.25)] hover:bg-red-50",
};

const SIZE: Record<ButtonSize, string> = {
  sm: "h-[26px] px-[9px] text-[12px] rounded",
  md: "h-8 px-3 text-[13px] rounded",
  lg: "h-10 px-4 text-[14px] rounded-md",
};

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  { variant = "secondary", size = "md", className, children, ...rest },
  ref,
) {
  return (
    <button
      ref={ref}
      className={cn(
        "inline-flex items-center gap-1.5 whitespace-nowrap font-medium leading-none transition-colors duration-100 ease-out",
        "disabled:opacity-60 disabled:pointer-events-none",
        VARIANT[variant],
        SIZE[size],
        className,
      )}
      {...rest}
    >
      {children}
    </button>
  );
});
