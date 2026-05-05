import { forwardRef, type SelectHTMLAttributes } from "react";
import { cn } from "../lib/cn";

export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(
  function Select({ className, children, ...rest }, ref) {
    return (
      <select
        ref={ref}
        className={cn(
          "h-8 w-full rounded bg-white px-2 text-[13px] leading-none text-ink-800 outline-none",
          "shadow-xs transition-shadow duration-100",
          "focus:shadow-[0_0_0_1px_var(--ship-orange-500),0_0_0_4px_var(--ship-orange-50)]",
          className,
        )}
        {...rest}
      >
        {children}
      </select>
    );
  },
);
