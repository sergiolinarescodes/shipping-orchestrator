import logoUrl from "../assets/ship-logo.svg";
import clusterUrl from "../assets/ship-cluster.svg";
import type { CSSProperties } from "react";

export const SHIP_LOGO_URL = logoUrl;
export const SHIP_CLUSTER_URL = clusterUrl;

export function ShipLogo({ inverse = false, size = 32 }: { inverse?: boolean; size?: number }) {
  return (
    <span style={{ display: "inline-flex", alignItems: "center", height: size }}>
      <img
        src={logoUrl}
        alt="Ship"
        style={{
          height: size,
          width: "auto",
          display: "block",
          filter: inverse ? "brightness(0) invert(1)" : undefined,
        }}
      />
    </span>
  );
}

/**
 * Decorative confetti motif lifted from the Ship logo. Position absolutely
 * inside a relatively-positioned parent. `color="white"` filters to white for
 * dark surfaces; otherwise renders the original orange/red palette.
 */
export function ShipClusterBg({
  size = 280,
  opacity = 0.08,
  color,
  style,
}: {
  size?: number;
  opacity?: number;
  color?: "white";
  style?: CSSProperties;
}) {
  return (
    <img
      src={clusterUrl}
      alt=""
      aria-hidden="true"
      style={{
        position: "absolute",
        width: size,
        height: "auto",
        opacity,
        pointerEvents: "none",
        filter: color === "white" ? "brightness(0) invert(1)" : undefined,
        ...style,
      }}
    />
  );
}
