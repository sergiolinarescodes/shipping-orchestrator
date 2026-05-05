import { useEffect, useState } from "react";
import { Button, Dialog, Input, Label, Spinner } from "@ship/ui";
import { useInstallGuideQuery, useStartConnectionInstallMutation } from "../api/queries";
import type { InstallInputField } from "../types/api";

export interface ConnectStoreModalProps {
  open: boolean;
  platform: string;
  onClose: () => void;
}

/**
 * Two-step install dialog. Step 1 fetches the per-connector install guide from PublicApi
 * (each connector module owns its own copy + required-input metadata) and renders short
 * bullets + the collected form fields. Step 2 reuses the existing
 * <c>POST /v1/dashboard/connections/{platform}/start</c> mutation — submitting the first
 * required field as the platform's <c>externalAccountId</c> and redirecting the browser to
 * the OAuth authorize URL the connector built. Same code path locally and in production.
 */
export function ConnectStoreModal({ open, platform, onClose }: ConnectStoreModalProps) {
  const guideQuery = useInstallGuideQuery(open ? platform : undefined);
  const start = useStartConnectionInstallMutation();
  const [values, setValues] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);

  // Reset local state every time the dialog reopens or the platform switches so a half-typed
  // Shopify domain doesn't leak into a subsequent WooCommerce attempt.
  useEffect(() => {
    if (open) {
      setValues({});
      setError(null);
    }
  }, [open, platform]);

  const guide = guideQuery.data;

  function setField(key: string, v: string) {
    setValues((prev) => ({ ...prev, [key]: v }));
  }

  function validate(inputs: InstallInputField[]): string | null {
    for (const f of inputs) {
      if (f.required && !(values[f.key]?.trim())) {
        return `${f.label} is required.`;
      }
    }
    return null;
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!guide) return;
    const validation = validate(guide.inputs);
    if (validation) {
      setError(validation);
      return;
    }
    setError(null);
    // The dashboard/start contract takes a single externalAccountId today. The first input
    // in the guide is the canonical "store identifier" (shop domain for Shopify, site URL
    // for WooCommerce). Future multi-field connectors will extend the start contract.
    const primary = guide.inputs[0];
    if (!primary) {
      setError("Install guide does not declare any inputs.");
      return;
    }
    try {
      const result = await start.mutateAsync({
        platformCode: platform,
        externalAccountId: (values[primary.key] ?? "").trim(),
      });
      window.location.href = result.authorizationUrl;
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to start install.");
    }
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(v) => {
        if (!v) onClose();
      }}
      title={guide?.title ?? "Connect a store"}
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onClose} disabled={start.isPending}>
            Cancel
          </Button>
          <Button
            variant="primary"
            size="sm"
            type="submit"
            form="connect-store-form"
            disabled={!guide || start.isPending}
          >
            {start.isPending ? <Spinner /> : "Continue"}
          </Button>
        </>
      }
    >
      {guideQuery.isLoading || !guide ? (
        <div className="flex items-center gap-2 py-3 text-[13px] text-ink-500">
          <Spinner /> Loading instructions…
        </div>
      ) : (
        <form id="connect-store-form" onSubmit={submit} className="grid gap-4">
          {guide.steps.length > 0 && (
            <ol className="list-decimal pl-5 text-[13px] text-ink-700 grid gap-1">
              {guide.steps.map((s, i) => (
                <li key={i}>{s}</li>
              ))}
            </ol>
          )}

          <div className="grid gap-3">
            {guide.inputs.map((field) => (
              <div key={field.key}>
                <Label htmlFor={`install-${field.key}`}>{field.label}</Label>
                <Input
                  id={`install-${field.key}`}
                  type="text"
                  placeholder={field.placeholder ?? undefined}
                  value={values[field.key] ?? ""}
                  onChange={(e) => setField(field.key, e.target.value)}
                  required={field.required}
                />
                {field.helpText && (
                  <p className="text-[11px] text-ink-500 mt-1">{field.helpText}</p>
                )}
              </div>
            ))}
          </div>

          {guide.helpUrl && (
            <a
              className="text-[12px] text-ship-orange-700 underline"
              href={guide.helpUrl}
              target="_blank"
              rel="noreferrer"
            >
              Platform documentation ↗
            </a>
          )}

          {error && (
            <div className="rounded border border-red-200 bg-red-50 px-3 py-2 text-[12px] text-red-700">
              {error}
            </div>
          )}
        </form>
      )}
    </Dialog>
  );
}
