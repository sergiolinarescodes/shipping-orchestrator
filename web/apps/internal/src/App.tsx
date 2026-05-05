import { Routes, Route, useLocation, Link } from "react-router-dom";
import { AppShell, Sidebar, Topbar } from "@ship/ui";
import { NAV } from "./nav";
import OpsConsole from "./pages/OpsConsole";
import IngestionFailuresPage from "./pages/IngestionFailuresPage";
import OnboardingListPage from "./pages/onboarding/ListPage";
import OnboardingNewPage from "./pages/onboarding/NewPage";
import OnboardingWizardPage from "./pages/onboarding/WizardPage";
import TenantsListPage from "./pages/tenants/ListPage";
import TenantDetailPage from "./pages/tenants/TenantDetailPage";
import BatchProgressPage from "./pages/tenants/BatchProgressPage";

const ROUTES: Array<{ path: string; element: React.ReactNode }> = [
  { path: "/",                                   element: <OpsConsole /> },
  { path: "/ingestion-failures",                 element: <IngestionFailuresPage /> },
  { path: "/onboarding",                         element: <OnboardingListPage /> },
  { path: "/onboarding/new",                     element: <OnboardingNewPage /> },
  { path: "/onboarding/:processId",              element: <OnboardingWizardPage /> },
  { path: "/tenants",                            element: <TenantsListPage /> },
  { path: "/tenants/:tenantId",                  element: <TenantDetailPage /> },
  { path: "/tenants/:tenantId/batches/:batchId", element: <BatchProgressPage /> },
];

function pickActiveId(pathname: string): string {
  let best: { id: string; len: number } | null = null;
  for (const sec of NAV) {
    for (const it of sec.items) {
      if (!it.href) continue;
      const matches =
        it.href === "/" ? pathname === "/" : pathname === it.href || pathname.startsWith(it.href + "/");
      if (matches && (!best || it.href.length > best.len)) {
        best = { id: it.id, len: it.href.length };
      }
    }
  }
  return best?.id ?? "ops";
}

export default function App() {
  const loc = useLocation();
  const activeId = pickActiveId(loc.pathname);

  return (
    <AppShell
      sidebar={
        <Sidebar
          variant="ops"
          items={NAV}
          activeId={activeId}
          brandLabel="Ship · Internal Ops"
          renderItem={(item, children, isActive) => (
            <Link
              to={item.href ?? "#"}
              className={
                "flex items-center gap-2.5 px-2.5 py-2 rounded text-[13px] font-medium leading-none cursor-pointer relative no-underline " +
                (isActive
                  ? "bg-white/[0.10] text-white shadow-[inset_3px_0_0_white]"
                  : "text-white/80 hover:bg-white/[0.06] hover:text-white")
              }
            >
              {children}
            </Link>
          )}
        />
      }
      topbar={
        <Topbar
          variant="ops"
          env="DEV · localhost"
          user={{ initials: "OP", name: "Operator", role: "Staff" }}
          search="Search shipments, tracking, tenants…"
        />
      }
    >
      <Routes>
        {ROUTES.map((r) => (
          <Route key={r.path} path={r.path} element={r.element} />
        ))}
      </Routes>
    </AppShell>
  );
}
