import {
  IconHome, IconBox, IconStack, IconInbox, IconSettings, IconAlert, IconUsers,
  type SidebarSection,
} from "@ship/ui";

export interface NavBuildOptions {
  needsAttentionCount?: number;
}

export function buildNav({ needsAttentionCount }: NavBuildOptions = {}): readonly SidebarSection[] {
  return [
    {
      section: "Overview",
      items: [
        { id: "overview",  href: "/",          icon: <IconHome />, label: "Home" },
      ],
    },
    {
      section: "Shipping",
      items: [
        { id: "pending",          href: "/pending",          icon: <IconInbox />, label: "Pending orders" },
        {
          id: "needs-attention",
          href: "/needs-attention",
          icon: <IconAlert />,
          label: "Needs attention",
          count: needsAttentionCount && needsAttentionCount > 0 ? needsAttentionCount : undefined,
        },
        { id: "batches",          href: "/batches",          icon: <IconStack />, label: "Batches" },
        { id: "shipments",        href: "/shipments",        icon: <IconBox />,   label: "Shipments" },
      ],
    },
    {
      section: "Settings",
      items: [
        { id: "connections", href: "/connections", icon: <IconSettings />, label: "Connections" },
        { id: "members",     href: "/members",     icon: <IconUsers />,   label: "Members" },
      ],
    },
  ];
}

/** Static fallback for places that don't need a live badge (e.g. tests). */
export const NAV: readonly SidebarSection[] = buildNav();
