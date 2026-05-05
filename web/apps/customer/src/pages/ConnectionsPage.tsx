import { useMemo, useState } from "react";
import {
  Badge, Button, Card, EmptyState, PageTitleRule, Spinner,
  Table, TBody, TD, TH, THead, TR,
} from "@ship/ui";
import {
  useDisconnectConnectionMutation,
  useEcommerceConnectionsQuery,
} from "../api/queries";
import { ConnectStoreModal } from "../components/ConnectStoreModal";
import type { EcommerceConnectionView } from "../types/api";

const PLATFORM_LABELS: Record<string, { name: string; tagline: string; }> = {
  shopify: {
    name: "Shopify",
    tagline: "Install the Ship Shipping app from your Shopify admin.",
  },
  woocommerce: {
    name: "WooCommerce",
    tagline: "Approve a WooCommerce REST API key from your wp-admin.",
  },
};

function statusVariant(status: string): "success" | "neutral" | "warn" | "danger" {
  switch (status) {
    case "Active": return "success";
    case "PendingVerification": return "warn";
    case "Rejected": return "danger";
    default: return "neutral";
  }
}

function formatRelative(iso: string | null) {
  if (!iso) return "—";
  const then = new Date(iso).getTime();
  const diff = Date.now() - then;
  if (diff < 60_000) return "just now";
  const minutes = Math.floor(diff / 60_000);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

export default function ConnectionsPage() {
  const list = useEcommerceConnectionsQuery();
  const disconnect = useDisconnectConnectionMutation();
  const [modal, setModal] = useState<{ platform: string } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const connections = list.data?.connections ?? [];
  const platforms = list.data?.availablePlatforms ?? [];

  const orderedPlatforms = useMemo(() => {
    const order = ["shopify", "woocommerce"];
    const seen = new Set<string>();
    const head = order.flatMap((code) => {
      const found = platforms.find((p) => p.connectorCode === code);
      if (!found) return [];
      seen.add(code);
      return [found];
    });
    const tail = platforms.filter((p) => !seen.has(p.connectorCode));
    return [...head, ...tail];
  }, [platforms]);

  async function handleDisconnect(c: EcommerceConnectionView) {
    setError(null);
    if (!window.confirm(`Disconnect ${c.externalAccountId}? This removes the connection completely. To reconnect, install the app again.`)) {
      return;
    }
    try {
      await disconnect.mutateAsync({ connectionId: c.connectionId, reason: "tenant requested via dashboard" });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Disconnect failed.");
    }
  }

  return (
    <div className="px-7 py-6">
      <div className="mb-5">
        <PageTitleRule />
        <div className="text-[11px] font-semibold uppercase tracking-[0.04em] mb-1.5 text-ship-navy-500">
          Settings
        </div>
        <h1 className="font-display text-[28px] leading-[1.15] font-semibold tracking-[-0.02em] text-ship-navy-800">
          Store connections
        </h1>
        <div className="text-[13px] text-ink-500 mt-1">
          Connect your ecommerce stores once and orders flow into the same pending inbox.
          Disconnecting removes the link entirely — to reconnect, just install the platform's
          app again.
        </div>
      </div>

      {error && (
        <div className="mb-3 rounded border border-red-200 bg-red-50 px-4 py-2 text-[12px] text-red-700">
          {error}
        </div>
      )}

      {/* Add a store ------------------------------------------------------ */}
      <div className="mb-6 grid gap-3 grid-cols-1 md:grid-cols-2">
        {orderedPlatforms.map((p) => {
          const meta = PLATFORM_LABELS[p.connectorCode] ?? { name: p.displayName, tagline: "" };
          const existing = connections.filter((c) => c.platformCode === p.connectorCode);
          return (
            <Card key={p.connectorCode} pad="default">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="text-[14px] font-semibold text-ink-800">{meta.name}</div>
                  <div className="text-[12px] text-ink-500 mt-0.5">{meta.tagline}</div>
                  {existing.length > 0 && (
                    <div className="text-[11px] text-ink-400 mt-1.5">
                      {existing.length} store{existing.length === 1 ? "" : "s"} connected
                    </div>
                  )}
                </div>
                <Button
                  variant="primary"
                  size="sm"
                  onClick={() => setModal({ platform: p.connectorCode })}
                >
                  Connect
                </Button>
              </div>
            </Card>
          );
        })}
      </div>

      {/* Connected stores ------------------------------------------------- */}
      <Card>
        {list.isLoading && (
          <div className="flex items-center gap-2 p-6 text-[13px] text-ink-500">
            <Spinner /> Loading connections…
          </div>
        )}
        {!list.isLoading && connections.length === 0 && (
          <div className="p-6">
            <EmptyState
              title="No stores connected yet"
              description="Pick a platform above to connect your first store. Orders will start landing on the Pending orders page automatically."
            />
          </div>
        )}
        {connections.length > 0 && (
          <Table>
            <THead>
              <TR>
                <TH>Platform</TH>
                <TH>Store</TH>
                <TH>Status</TH>
                <TH>Installed</TH>
                <TH>Last sync</TH>
                <TH style={{ width: 220, textAlign: "right" }}>Actions</TH>
              </TR>
            </THead>
            <TBody>
              {connections.map((c) => {
                const meta = PLATFORM_LABELS[c.platformCode] ?? { name: c.platformCode, tagline: "" };
                return (
                  <TR key={c.connectionId}>
                    <TD>
                      <span className="text-[13px] font-medium text-ink-800">{meta.name}</span>
                    </TD>
                    <TD>
                      <span className="font-mono text-[12px] text-ink-700">{c.externalAccountId}</span>
                    </TD>
                    <TD>
                      <Badge variant={statusVariant(c.status)} dot>{c.status}</Badge>
                    </TD>
                    <TD className="text-ink-500 text-[12px]">{formatRelative(c.installedAt)}</TD>
                    <TD className="text-ink-500 text-[12px]">{formatRelative(c.lastSyncAt)}</TD>
                    <TD style={{ textAlign: "right" }}>
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => handleDisconnect(c)}
                        disabled={disconnect.isPending}
                        title="Removes the connection completely. Reconnect by installing the platform app again."
                      >
                        {disconnect.isPending ? <Spinner /> : "Disconnect"}
                      </Button>
                    </TD>
                  </TR>
                );
              })}
            </TBody>
          </Table>
        )}
      </Card>

      {modal && (
        <ConnectStoreModal
          open={true}
          platform={modal.platform}
          onClose={() => setModal(null)}
        />
      )}
    </div>
  );
}
