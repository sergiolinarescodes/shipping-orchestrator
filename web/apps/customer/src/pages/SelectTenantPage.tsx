import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Badge, Button, Card, Spinner } from "@ship/ui";
import { useMeQuery, useSelectTenantMutation, useSignOutMutation } from "../api/auth";

export default function SelectTenantPage() {
  const navigate = useNavigate();
  const me = useMeQuery();
  const select = useSelectTenantMutation();
  const signOut = useSignOutMutation();
  const [chosen, setChosen] = useState<string | null>(null);

  if (me.isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center text-[13px] text-ink-500 gap-2">
        <Spinner /> Loading account…
      </div>
    );
  }

  if (me.isError || !me.data) {
    navigate("/login", { replace: true });
    return null;
  }

  const tenants = me.data.tenants;
  const active = chosen ?? tenants[0]?.tenantId ?? null;

  return (
    <div className="min-h-screen flex items-center justify-center px-6 bg-ink-25">
      <Card pad="lg" className="max-w-xl w-full grid gap-5">
        <div>
          <h1 className="font-display text-[22px] font-semibold tracking-[-0.01em] text-ship-navy-800">
            Pick a tenant
          </h1>
          <p className="text-[13px] text-ink-600 mt-1">
            Signed in as <strong>{me.data.account.email}</strong>. Choose which tenant to
            manage.
          </p>
        </div>

        {tenants.length === 0 && (
          <div className="text-[13px] text-ink-500">
            No tenants linked to this account. Ask an owner to invite you, or contact your
            operator.
          </div>
        )}

        {tenants.length > 0 && (
          <div className="grid gap-2">
            {tenants.map((t) => {
              const isActive = active === t.tenantId;
              return (
                <button
                  key={t.tenantId}
                  type="button"
                  onClick={() => setChosen(t.tenantId)}
                  className={
                    "rounded-md border bg-white p-3 text-left transition-shadow " +
                    (isActive
                      ? "border-ship-orange-500 shadow-[0_0_0_3px_var(--ship-orange-50)]"
                      : "border-border hover:border-ink-300")
                  }
                >
                  <div className="flex items-center justify-between">
                    <span className="text-[14px] font-semibold text-ink-800">{t.displayName}</span>
                    <div className="flex items-center gap-2">
                      <Badge variant={t.role === "Owner" ? "info" : "neutral"}>{t.role}</Badge>
                      <Badge variant={t.status === "Active" ? "success" : "neutral"}>{t.status}</Badge>
                    </div>
                  </div>
                  <span className="text-[11px] font-mono text-ink-500">{t.tenantId}</span>
                </button>
              );
            })}
          </div>
        )}

        <div className="flex justify-between gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={async () => {
              await signOut.mutateAsync();
              navigate("/login", { replace: true });
            }}
          >
            Sign out
          </Button>
          <Button
            variant="primary"
            size="md"
            disabled={!active || select.isPending}
            onClick={async () => {
              if (!active) return;
              await select.mutateAsync(active);
              navigate("/", { replace: true });
            }}
          >
            {select.isPending ? "Selecting…" : "Continue"}
          </Button>
        </div>
      </Card>
    </div>
  );
}
