export interface BarDatum {
  label: string;
  value: number;
  highlight?: boolean;
}

export interface BarChartProps {
  data: readonly BarDatum[];
  height?: number;
  color?: string;
}

export function BarChart({ data, height = 120, color = "var(--ship-orange-500)" }: BarChartProps) {
  const max = Math.max(1, ...data.map((d) => d.value));
  return (
    <div style={{ display: "flex", alignItems: "flex-end", gap: 8, height, padding: "0 4px" }}>
      {data.map((d, i) => (
        <div key={`${d.label}-${i}`} style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", gap: 6 }}>
          <div
            style={{
              width: "100%",
              height: `${(d.value / max) * (height - 24)}px`,
              background: d.highlight ? color : "var(--ink-150)",
              borderRadius: "3px 3px 0 0",
              transition: "200ms",
            }}
          />
          <span style={{ font: "500 10px/1 var(--ff-sans)", color: "var(--ink-400)" }}>{d.label}</span>
        </div>
      ))}
    </div>
  );
}
