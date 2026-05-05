import {
  IconHome, IconUsers, IconPlus, IconAlert,
  type SidebarSection,
} from "@ship/ui";

export const NAV: readonly SidebarSection[] = [
  {
    section: "Operations",
    items: [
      { id: "ops",                href: "/",                    icon: <IconHome />,  label: "Ops Console" },
      { id: "ingestion-failures", href: "/ingestion-failures",  icon: <IconAlert />, label: "Ingestion failures" },
      { id: "onboarding",         href: "/onboarding",          icon: <IconPlus />,  label: "Onboarding" },
    ],
  },
  {
    section: "Platform",
    items: [
      { id: "tenants", href: "/tenants", icon: <IconUsers />, label: "Tenants" },
    ],
  },
];
