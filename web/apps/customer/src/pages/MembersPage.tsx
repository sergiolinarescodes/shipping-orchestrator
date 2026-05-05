import { useState } from "react";
import { Badge, Button, Card, Spinner } from "@ship/ui";
import { useInviteMemberMutation, useMeQuery } from "../api/auth";

export default function MembersPage() {
  const me = useMeQuery();
  const invite = useInviteMemberMutation();
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<"Owner" | "Member">("Member");
  const [lastSent, setLastSent] = useState<{ email: string; role: string } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const currentTenant = me.data?.tenants.find((t) => t.tenantId === me.data?.currentTenantId);
  const isOwner = currentTenant?.role === "Owner";

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!email.trim()) return;
    try {
      await invite.mutateAsync({ email: email.trim(), role });
      setLastSent({ email: email.trim(), role });
      setEmail("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Invite failed.");
    }
  };

  if (me.isLoading) {
    return (
      <div className="p-6 flex items-center gap-2 text-[13px] text-ink-500">
        <Spinner /> Loading…
      </div>
    );
  }

  return (
    <div className="p-6 grid gap-5 max-w-2xl">
      <div>
        <h1 className="font-display text-[22px] font-semibold tracking-[-0.01em] text-ship-navy-800">
          Members
        </h1>
        <p className="text-[13px] text-ink-600 mt-1">
          Owners can invite additional emails to <strong>{currentTenant?.displayName}</strong>.
          Invitees receive a sign-in link; on first sign-in they're added to this tenant
          automatically.
        </p>
      </div>

      <Card pad="lg" className="grid gap-4">
        <div className="flex items-center justify-between">
          <h2 className="text-[14px] font-semibold text-ink-800">Invite a new member</h2>
          {isOwner ? (
            <Badge variant="success">You are an Owner</Badge>
          ) : (
            <Badge variant="neutral">View only — Owner role required to invite</Badge>
          )}
        </div>

        <form onSubmit={submit} className="grid gap-3">
          <label className="grid gap-1 text-[12px] text-ink-700">
            Email
            <input
              type="email"
              required
              disabled={!isOwner || invite.isPending}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="teammate@store.com"
              className="rounded-md border border-border bg-white px-3 py-2 text-[14px] text-ink-900 outline-none focus:border-ship-orange-500 disabled:bg-ink-50 disabled:text-ink-400"
            />
          </label>

          <label className="grid gap-1 text-[12px] text-ink-700">
            Role
            <select
              disabled={!isOwner || invite.isPending}
              value={role}
              onChange={(e) => setRole(e.target.value as "Owner" | "Member")}
              className="rounded-md border border-border bg-white px-3 py-2 text-[14px] text-ink-900 outline-none focus:border-ship-orange-500 disabled:bg-ink-50 disabled:text-ink-400"
            >
              <option value="Member">Member — can use the dashboard</option>
              <option value="Owner">Owner — can also invite and remove members</option>
            </select>
          </label>

          <div>
            <Button
              type="submit"
              variant="primary"
              size="md"
              disabled={!isOwner || !email.trim() || invite.isPending}
            >
              {invite.isPending ? "Sending…" : "Send invite"}
            </Button>
          </div>

          {error && <div className="text-[12px] text-red-600">{error}</div>}
          {lastSent && (
            <div className="text-[12px] text-emerald-700">
              Invite sent to <strong>{lastSent.email}</strong> as {lastSent.role}. They have
              15 minutes to use the link in their inbox (or Mailpit in dev).
            </div>
          )}
        </form>
      </Card>

      <Card pad="lg" className="grid gap-3">
        <h2 className="text-[14px] font-semibold text-ink-800">Notes</h2>
        <ul className="text-[12px] text-ink-600 list-disc pl-5 grid gap-1">
          <li>Invites are tied to the email address — if the invitee already has an account, they're added on their next sign-in.</li>
          <li>The first email matching the tenant's contact email becomes Owner automatically.</li>
          <li>To revoke access, contact your operator (UI for owner-level removal lands later).</li>
        </ul>
      </Card>
    </div>
  );
}
