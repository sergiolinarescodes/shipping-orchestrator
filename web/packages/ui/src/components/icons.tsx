import type { SVGProps } from "react";

type IconProps = SVGProps<SVGSVGElement> & { size?: number; sw?: number };

function Icon({ size = 16, sw = 1.6, children, ...rest }: IconProps & { children: React.ReactNode }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={sw}
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ flexShrink: 0 }}
      {...rest}
    >
      {children}
    </svg>
  );
}

const D = (d: string) => <path d={d} />;

export const IconHome     = (p: IconProps) => <Icon {...p}>{D("M3 11l9-8 9 8M5 9v11h5v-6h4v6h5V9")}</Icon>;
export const IconBox      = (p: IconProps) => <Icon {...p}>{D("M3 7l9-4 9 4-9 4-9-4zm0 0v10l9 4 9-4V7M12 11v10")}</Icon>;
export const IconTruck    = (p: IconProps) => <Icon {...p}>{D("M3 7h11v10H3zM14 10h4l3 3v4h-7M6 20a2 2 0 100-4 2 2 0 000 4zm11 0a2 2 0 100-4 2 2 0 000 4z")}</Icon>;
export const IconChart    = (p: IconProps) => <Icon {...p}>{D("M3 3v18h18M7 14l4-4 3 3 5-6")}</Icon>;
export const IconPlug     = (p: IconProps) => <Icon {...p}>{D("M9 7V3m6 4V3M6 11h12v3a6 6 0 01-12 0v-3zm6 9v2")}</Icon>;
export const IconUsers    = (p: IconProps) => <Icon {...p}>{D("M9 11a4 4 0 100-8 4 4 0 000 8zm-7 9a7 7 0 0114 0M17 11a3 3 0 100-6m5 14a5 5 0 00-4-4.9")}</Icon>;
export const IconTag      = (p: IconProps) => <Icon {...p}>{D("M3 12V3h9l9 9-9 9-9-9zm5-5a1 1 0 100 2 1 1 0 000-2z")}</Icon>;
export const IconRoute    = (p: IconProps) => <Icon {...p}>{D("M6 19a3 3 0 100-6m12-2a3 3 0 100-6M6 13V8a3 3 0 013-3h6m3 6v5a3 3 0 01-3 3H9")}</Icon>;
export const IconAlert    = (p: IconProps) => <Icon {...p}>{D("M12 9v4m0 4h.01M10.3 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z")}</Icon>;
export const IconSettings = (p: IconProps) => <Icon {...p}>{D("M12 15a3 3 0 100-6 3 3 0 000 6zm7.4-3a7.4 7.4 0 00-.1-1.2l2-1.5-2-3.5-2.4.9a7.5 7.5 0 00-2-1.2L14.5 3h-5l-.4 2.5a7.5 7.5 0 00-2 1.2l-2.4-.9-2 3.5 2 1.5a7.4 7.4 0 000 2.4l-2 1.5 2 3.5 2.4-.9a7.5 7.5 0 002 1.2l.4 2.5h5l.4-2.5a7.5 7.5 0 002-1.2l2.4.9 2-3.5-2-1.5c.07-.4.1-.8.1-1.2z")}</Icon>;
export const IconSearch   = (p: IconProps) => <Icon {...p}>{D("M11 19a8 8 0 100-16 8 8 0 000 16zm10 2l-4.35-4.35")}</Icon>;
export const IconBell     = (p: IconProps) => <Icon {...p}>{D("M18 8a6 6 0 10-12 0c0 7-3 9-3 9h18s-3-2-3-9M13.7 21a2 2 0 01-3.4 0")}</Icon>;
export const IconArrowUp  = (p: IconProps) => <Icon {...p}>{D("M7 14l5-5 5 5")}</Icon>;
export const IconArrowDn  = (p: IconProps) => <Icon {...p}>{D("M7 10l5 5 5-5")}</Icon>;
export const IconCheck    = (p: IconProps) => <Icon {...p}>{D("M5 12l5 5L20 7")}</Icon>;
export const IconX        = (p: IconProps) => <Icon {...p}>{D("M6 6l12 12M18 6L6 18")}</Icon>;
export const IconPlus     = (p: IconProps) => <Icon {...p}>{D("M12 5v14M5 12h14")}</Icon>;
export const IconDownload = (p: IconProps) => <Icon {...p}>{D("M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4M7 10l5 5 5-5M12 15V3")}</Icon>;
export const IconPrinter  = (p: IconProps) => <Icon {...p}>{D("M6 9V3h12v6M6 18H4a2 2 0 01-2-2v-5a2 2 0 012-2h16a2 2 0 012 2v5a2 2 0 01-2 2h-2M6 14h12v8H6z")}</Icon>;
export const IconExternal = (p: IconProps) => <Icon {...p}>{D("M18 13v6a2 2 0 01-2 2H5a2 2 0 01-2-2V8a2 2 0 012-2h6M15 3h6v6M10 14L21 3")}</Icon>;
export const IconChevronR = (p: IconProps) => <Icon {...p}>{D("M9 6l6 6-6 6")}</Icon>;
export const IconCopy     = (p: IconProps) => <Icon {...p}>{D("M8 8h12v12H8zM4 16V4h12")}</Icon>;
export const IconFilter   = (p: IconProps) => <Icon {...p}>{D("M3 5h18l-7 9v6l-4-2v-4z")}</Icon>;
export const IconGlobe    = (p: IconProps) => <Icon {...p}>{D("M12 21a9 9 0 100-18 9 9 0 000 18zm0 0c2.5 0 4.5-4 4.5-9S14.5 3 12 3 7.5 7 7.5 12 9.5 21 12 21zM3 12h18")}</Icon>;
export const IconCO2      = (p: IconProps) => <Icon {...p}>{D("M12 21a9 9 0 110-18 9 9 0 010 18zm-3-7c0-1 .8-2 2-2s2 1 2 2-.8 2-2 2-2-1-2-2zm6 0c0-1 .8-2 2-2")}</Icon>;
export const IconStack    = (p: IconProps) => <Icon {...p}>{D("M12 3l9 5-9 5-9-5 9-5zM3 13l9 5 9-5M3 18l9 5 9-5")}</Icon>;
export const IconInbox    = (p: IconProps) => <Icon {...p}>{D("M22 12h-6l-2 3h-4l-2-3H2M5.45 5.11L2 12v6a2 2 0 002 2h16a2 2 0 002-2v-6l-3.45-6.89A2 2 0 0016.76 4H7.24a2 2 0 00-1.79 1.11z")}</Icon>;
