import * as ToastPrimitive from "@radix-ui/react-toast";
import { createContext, useCallback, useContext, useState, type ReactNode } from "react";
import { cn } from "../lib/cn";

type ToastVariant = "info" | "success" | "danger";

export interface ToastMessage {
  id: number;
  title: string;
  description?: string;
  variant?: ToastVariant;
}

interface ToastContextValue {
  push: (msg: Omit<ToastMessage, "id">) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used inside <ToastProvider>");
  return ctx;
}

const VARIANT: Record<ToastVariant, string> = {
  info:    "border-l-4 border-ship-navy-500",
  success: "border-l-4 border-green-500",
  danger:  "border-l-4 border-red-500",
};

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const push = useCallback((msg: Omit<ToastMessage, "id">) => {
    setToasts((t) => [...t, { ...msg, id: Date.now() + Math.random() }]);
  }, []);
  return (
    <ToastContext.Provider value={{ push }}>
      <ToastPrimitive.Provider swipeDirection="right" duration={4000}>
        {children}
        {toasts.map((t) => (
          <ToastPrimitive.Root
            key={t.id}
            onOpenChange={(open) => {
              if (!open) setToasts((cur) => cur.filter((x) => x.id !== t.id));
            }}
            className={cn(
              "rounded bg-white p-3 shadow-md w-[320px]",
              VARIANT[t.variant ?? "info"],
            )}
          >
            <ToastPrimitive.Title className="text-[13px] font-semibold text-ink-800">{t.title}</ToastPrimitive.Title>
            {t.description && (
              <ToastPrimitive.Description className="mt-1 text-[12px] text-ink-500">
                {t.description}
              </ToastPrimitive.Description>
            )}
          </ToastPrimitive.Root>
        ))}
        <ToastPrimitive.Viewport className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 outline-none" />
      </ToastPrimitive.Provider>
    </ToastContext.Provider>
  );
}
