export interface MapHotspot {
  x: number;
  y: number;
  size?: number;
}

export interface DottedMapProps {
  height?: number;
  hotspots?: readonly MapHotspot[];
}

const COLS = 60;
const ROWS = 22;

function buildDots(): Array<readonly [number, number]> {
  const dots: Array<readonly [number, number]> = [];
  for (let r = 0; r < ROWS; r++) {
    for (let c = 0; c < COLS; c++) {
      const inEU = c > 28 && c < 38 && r > 6 && r < 12;
      const inNA = c > 8 && c < 22 && r > 5 && r < 13;
      const inSA = c > 16 && c < 24 && r > 12 && r < 19;
      const inAF = c > 30 && c < 40 && r > 10 && r < 18;
      const inAS = c > 38 && c < 54 && r > 5 && r < 14;
      const inAU = c > 48 && c < 56 && r > 14 && r < 18;
      if (inEU || inNA || inSA || inAF || inAS || inAU) dots.push([c, r] as const);
    }
  }
  return dots;
}

const DOTS = buildDots();

export function DottedMap({ height = 220, hotspots = [] }: DottedMapProps) {
  return (
    <div style={{ position: "relative", height, background: "var(--ink-25)", borderRadius: 8, overflow: "hidden" }}>
      <svg width="100%" height={height} viewBox={`0 0 ${COLS * 10} ${ROWS * 10}`} preserveAspectRatio="none">
        {DOTS.map(([c, r], i) => (
          <circle key={i} cx={c * 10 + 5} cy={r * 10 + 5} r="1.6" fill="var(--ink-200)" />
        ))}
        {hotspots.map((h, i) => (
          <g key={`h-${i}`}>
            <circle cx={h.x * COLS * 10} cy={h.y * ROWS * 10} r={h.size ?? 6} fill="var(--ship-orange-500)" opacity="0.85" />
            <circle
              cx={h.x * COLS * 10}
              cy={h.y * ROWS * 10}
              r={(h.size ?? 6) + 5}
              fill="none"
              stroke="var(--ship-orange-500)"
              strokeOpacity="0.35"
              strokeWidth="1"
            />
          </g>
        ))}
      </svg>
    </div>
  );
}
