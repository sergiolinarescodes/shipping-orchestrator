import { forwardRef, type TextareaHTMLAttributes } from "react";
import { cn } from "../lib/cn";

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(
  function Textarea({ className, ...rest }, ref) {
    return (
      <textarea
        ref={ref}
        className={cn(
          "min-h-[72px] w-full rounded bg-white px-2.5 py-2 text-[13px] leading-snug text-ink-800 outline-none resize-y",
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
