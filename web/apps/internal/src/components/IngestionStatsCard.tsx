import { useMemo } from "react";
import { Card, Badge } from "@ship/ui";
import { useOpsIngestionFailureStatsQuery } from "../api/queries";

const REASONS = [
  "MissingShippingAddress",
  "UnknownCountry",
  "ZeroWeight",
  "InvalidPostalCode",
  "UnsupportedCurrency",
  "ParseError",
  "Unknown",
] as const;

const REASON_LABELS: Record<string, string> = {
  MissingShippingAddress: "Address",
  UnknownCountry: "Country",
  ZeroWeight: "Weight",
  InvalidPostalCode: "Postal",
  UnsupportedCurrency: "Currency",
  ParseError: "Parse",
  Unknown: "Other",
};

interface PivotRow {
  tenantId: string;
  tenantDisplayName: string;
  total: number;
  byReason: Record<string, number>;
}

interface IngestionStatsCardProps {
  window?: "1h" | "24h" | "7d";
  onCellClick?: (tenantId: string, reasonCode: string) => void;
}

export default function IngestionStatsCard({ window = "24h", onCellClick }: IngestionStatsCardProps) {
  const stats = useOpsIngestionFailureStatsQuery(window);

  const rows = useMemo<PivotRow[]>(() => {
    const data = stats.data ?? [];
    const map = new Map<string, PivotRow>();
    for (const g of data) {
      let row = map.get(g.tenantId);
      if (!row) {
        row = {
          tenantId: g.tenantId,
          tenantDisplayName: g.tenantDisplayName,
          total: 0,
          byReason: {},
        };
        map.set(g.tenantId, row);
      }
      row.byReason[g.reasonCode] = (row.byReason[g.reasonCode] ?? 0) + g.openCount;
      row.total += g.openCount;
    }
    return [...map.values()].sort((a, b) => b.total - a.total).slice(0, 10);
  }, [stats.data]);

  return (
    <Card pad="lg">
      <div className="flex items-center justify-between mb-4">
        <h2 className="text-[18px] leading-snug font-semibold tracking-[-0.01em] text-ink-900">
          Ingestion failures · {window}
        </h2>
        <span className="text-[12px] text-ink-500">open · by tenant × reason</span>
      </div>

      {stats.isLoading && <div className="text-[12px] text-ink-400">Loading…</div>}
      {!stats.isLoading && rows.length === 0 && (
        <div className="text-[12px] text-ink-400">No open ingestion failures in this window.</div>
      )}
      {rows.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-[12px]">
            <thead>
              <tr className="text-ink-500">
                <th className="text-left font-medium pb-2">Tenant</th>
                {REASONS.map((r) => (
                  <th key={r} className="text-right font-medium pb-2 pl-2">
                    {REASON_LABELS[r]}
                  </th>
                ))}
                <th className="text-right font-medium pb-2 pl-2">Total</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.tenantId} className="border-t border-border">
                  <td className="py-1.5 pr-2 text-ink-800 truncate max-w-[180px]" title={r.tenantDisplayName}>
                    {r.tenantDisplayName}
                  </td>
                  {REASONS.map((reason) => {
                    const v = r.byReason[reason] ?? 0;
                    return (
                      <td key={reason} className="text-right py-1.5 pl-2">
                        {v === 0 ? (
                          <span className="text-ink-300">·</span>
                        ) : (
                          <button
                            type="button"
                            className="font-mono text-ink-800 hover:text-ship-orange-700 underline-offset-2 hover:underline"
                            onClick={() => onCellClick?.(r.tenantId, reason)}
                          >
                            {v}
                          </button>
                        )}
                      </td>
                    );
                  })}
                  <td className="text-right py-1.5 pl-2">
                    <Badge variant={r.total >= 10 ? "danger" : r.total >= 3 ? "warn" : "neutral"}>{r.total}</Badge>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  );
}
