import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Button, Card } from "@ship/ui";
import { useRequestMagicLinkMutation } from "../api/auth";

export default function LoginPage() {
  const [params] = useSearchParams();
  const [email, setEmail] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const request = useRequestMagicLinkMutation();
  const error = params.get("error");

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim()) return;
    await request.mutateAsync(email.trim());
    setSubmitted(true);
  };

  return (
    <div className="min-h-screen flex items-center justify-center px-6 bg-ink-25">
      <Card pad="lg" className="max-w-md w-full grid gap-5">
        <div>
          <h1 className="font-display text-[22px] font-semibold tracking-[-0.01em] text-ship-navy-800">
            Sign in
          </h1>
          <p className="text-[13px] text-ink-600 mt-1">
            Enter your email and we'll mail you a one-time sign-in link.
          </p>
        </div>

        {submitted ? (
          <div className="text-[13px] text-ink-700">
            Check <strong>{email}</strong> for a sign-in link. The link expires in 15 minutes
            and works once.
          </div>
        ) : (
          <form onSubmit={submit} className="grid gap-3">
            {error && (
              <div className="text-[12px] text-red-600">
                Sign-in failed: {error}. Request a new link.
              </div>
            )}
            <label className="grid gap-1 text-[12px] text-ink-700">
              Email
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@store.com"
                className="rounded-md border border-border bg-white px-3 py-2 text-[14px] text-ink-900 outline-none focus:border-ship-orange-500"
              />
            </label>
            <Button
              type="submit"
              variant="primary"
              size="md"
              disabled={!email.trim() || request.isPending}
            >
              {request.isPending ? "Sending…" : "Email me a link"}
            </Button>
          </form>
        )}
      </Card>
    </div>
  );
}
