export interface DonutSegment {
  name: string;
  value: number;
  color: string;
}

export interface DonutProps {
  segments: readonly DonutSegment[];
  size?: number;
}

export function Donut({ segments, size = 140 }: DonutProps) {
  const cx = size / 2;
  const cy = size / 2;
  const r = size / 2 - 10;
  const sw = 18;
  const total = segments.reduce((s, x) => s + x.value, 0) || 1;
  const C = 2 * Math.PI * r;
  let acc = 0;
  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
      <circle cx={cx} cy={cy} r={r} fill="none" stroke="var(--ink-100)" strokeWidth={sw} />
      {segments.map((s, i) => {
        const dash = (s.value / total) * C;
        const off = -(acc / total) * C;
        acc += s.value;
        return (
          <circle
            key={`${s.name}-${i}`}
            cx={cx}
            cy={cy}
            r={r}
            fill="none"
            stroke={s.color}
            strokeWidth={sw}
            strokeDasharray={`${dash} ${C - dash}`}
            strokeDashoffset={off}
            transform={`rotate(-90 ${cx} ${cy})`}
            strokeLinecap="butt"
          />
        );
      })}
    </svg>
  );
}
