import { useMemo } from "react";
import { Routes, Route, useLocation, Link, Navigate, useNavigate } from "react-router-dom";
import { AppShell, Sidebar, Topbar, Button, Spinner } from "@ship/ui";
import { buildNav } from "./nav";
import { useOpenIngestionFailureCountQuery } from "./api/queries";
import { useMeQuery, useSignOutMutation } from "./api/auth";
import { useRealtimeInvalidations } from "./api/realtime";
import Overview from "./pages/Overview";
import ShipmentsList from "./pages/ShipmentsList";
import ShipmentDetail from "./pages/ShipmentDetail";
import PendingOrdersPage from "./pages/PendingOrdersPage";
import NeedsAttentionPage from "./pages/NeedsAttentionPage";
import BatchesListPage from "./pages/BatchesList";
import BatchDetailPage from "./pages/BatchDetail";
import ConnectionsPage from "./pages/ConnectionsPage";
import MembersPage from "./pages/MembersPage";
import LoginPage from "./pages/Login";
import SelectTenantPage from "./pages/SelectTenantPage";

const ROUTES: Array<{ path: string; element: React.ReactNode }> = [
  { path: "/",                      element: <Overview /> },
  { path: "/pending",               element: <PendingOrdersPage /> },
  { path: "/needs-attention",       element: <NeedsAttentionPage /> },
  { path: "/batches",               element: <BatchesListPage /> },
  { path: "/batches/:batchId",      element: <BatchDetailPage /> },
  { path: "/shipments",             element: <ShipmentsList /> },
  { path: "/shipments/:shipmentId", element: <ShipmentDetail /> },
  { path: "/connections",           element: <ConnectionsPage /> },
  { path: "/members",               element: <MembersPage /> },
];

function pickActiveId(pathname: string, nav: ReturnType<typeof buildNav>): string {
  let best: { id: string; len: number } | null = null;
  for (const sec of nav) {
    for (const it of sec.items) {
      if (!it.href) continue;
      const matches =
        it.href === "/" ? pathname === "/" : pathname === it.href || pathname.startsWith(it.href + "/");
      if (matches && (!best || it.href.length > best.len)) {
        best = { id: it.id, len: it.href.length };
      }
    }
  }
  return best?.id ?? "overview";
}

export default function App() {
  const loc = useLocation();
  const navigate = useNavigate();
  const me = useMeQuery();
  const signOut = useSignOutMutation();
  useRealtimeInvalidations(me.data?.currentTenantId ?? null);
  const failureCount = useOpenIngestionFailureCountQuery();
  const nav = useMemo(
    () => buildNav({ needsAttentionCount: failureCount.data?.open ?? 0 }),
    [failureCount.data?.open],
  );
  const activeId = pickActiveId(loc.pathname, nav);

  if (loc.pathname === "/login") {
    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
      </Routes>
    );
  }

  if (loc.pathname === "/select-tenant") {
    return (
      <Routes>
        <Route path="/select-tenant" element={<SelectTenantPage />} />
      </Routes>
    );
  }

  if (me.isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center text-[13px] text-ink-500 gap-2">
        <Spinner /> Loading…
      </div>
    );
  }

  if (!me.data) {
    return <Navigate to="/login" replace />;
  }

  if (!me.data.currentTenantId) {
    return <Navigate to="/select-tenant" replace />;
  }

  const currentTenant = me.data.tenants.find((t) => t.tenantId === me.data!.currentTenantId);
  const displayName = currentTenant?.displayName ?? me.data.account.email;

  const initials = displayName
    .split(/\s+/)
    .map((p) => p[0])
    .filter(Boolean)
    .slice(0, 2)
    .join("")
    .toUpperCase() || "T";

  return (
    <AppShell
      sidebar={
        <Sidebar
          variant="customer"
          items={nav}
          activeId={activeId}
          brandLabel={`${displayName} · Customer`}
          renderItem={(item, children, isActive) => (
            <Link
              to={item.href ?? "#"}
              className={
                "flex items-center gap-2.5 px-2.5 py-2 rounded text-[13px] font-medium leading-none cursor-pointer relative no-underline " +
                (isActive
                  ? "bg-ship-orange-50 text-ship-orange-700 shadow-[inset_3px_0_0_var(--ship-orange-500)]"
                  : "text-ink-700 hover:bg-ship-orange-50 hover:text-ship-orange-700")
              }
            >
              {children}
            </Link>
          )}
        />
      }
      topbar={
        <Topbar
          variant="customer"
          env="DEV · localhost"
          user={{ initials, name: displayName, role: currentTenant?.role ?? "Member" }}
          search="Search shipments, tracking…"
          actions={
            <div className="flex items-center gap-2">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => navigate("/select-tenant")}
              >
                Switch tenant
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={async () => {
                  await signOut.mutateAsync();
                  navigate("/login", { replace: true });
                }}
              >
                Sign out
              </Button>
            </div>
          }
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
