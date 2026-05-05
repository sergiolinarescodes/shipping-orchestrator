import { useId } from "react";

export interface AreaChartProps {
  data: readonly number[];
  color?: string;
  height?: number;
}

export function AreaChart({ data, color = "var(--ship-orange-500)", height = 80 }: AreaChartProps) {
  const id = useId().replace(/[^a-zA-Z0-9_-]/g, "");
  const w = 600;
  const pad = 4;
  if (data.length === 0) {
    return <svg viewBox={`0 0 ${w} ${height}`} preserveAspectRatio="none" style={{ width: "100%", height }} />;
  }
  const max = Math.max(...data);
  const min = Math.min(...data);
  const range = max - min || 1;
  const denom = data.length - 1 || 1;
  const pts = data.map((v, i) => {
    const x = pad + (i / denom) * (w - pad * 2);
    const y = pad + (1 - (v - min) / range) * (height - pad * 2);
    return [x, y] as const;
  });
  const first = pts[0]!;
  const last = pts[pts.length - 1]!;
  const line = pts.map((p, i) => (i ? "L" : "M") + p[0].toFixed(1) + " " + p[1].toFixed(1)).join(" ");
  const area = `${line} L${last[0].toFixed(1)} ${height} L${first[0].toFixed(1)} ${height} Z`;
  const grad = `area-grad-${id}`;
  return (
    <svg viewBox={`0 0 ${w} ${height}`} preserveAspectRatio="none" style={{ width: "100%", height, display: "block" }}>
      <defs>
        <linearGradient id={grad} x1="0" x2="0" y1="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity="0.2" />
          <stop offset="100%" stopColor={color} stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d={area} fill={`url(#${grad})`} />
      <path d={line} fill="none" stroke={color} strokeWidth="1.8" />
    </svg>
  );
}
