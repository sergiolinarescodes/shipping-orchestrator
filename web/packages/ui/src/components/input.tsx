import { forwardRef, type InputHTMLAttributes } from "react";
import { cn } from "../lib/cn";

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  function Input({ className, ...rest }, ref) {
    return (
      <input
        ref={ref}
        className={cn(
          "h-8 w-full rounded bg-white px-2.5 text-[13px] leading-none text-ink-800 outline-none",
          "shadow-xs transition-shadow duration-100",
          "focus:shadow-[0_0_0_1px_var(--ship-orange-500),0_0_0_4px_var(--ship-orange-50)]",
          "placeholder:text-ink-400",
          className,
        )}
        {...rest}
      />
    );
  },
);

export function Label({ children, className, ...rest }: React.LabelHTMLAttributes<HTMLLabelElement>) {
  return (
    <label className={cn("block text-[12px] font-medium leading-snug text-ink-600 mb-1.5", className)} {...rest}>
      {children}
    </label>
  );
}
