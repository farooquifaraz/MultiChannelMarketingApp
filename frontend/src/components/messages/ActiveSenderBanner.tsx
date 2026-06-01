import { useEffect, useState } from 'react';
import { Send, AlertTriangle, CheckCircle2, Server, RefreshCw } from 'lucide-react';
import { meApi, type ActiveSender } from '../../api/meApi';

/**
 * M2 — pre-send transparency banner.
 *
 * Asked of any page that lets a user trigger an outbound email send. Sits at the top
 * of the compose flow and answers, in one glance:
 *   - Which SmtpGroup is wired up to send my mail right now?
 *   - Which provider does that group use (Brevo / Zoho SMTP / SendGrid / Mailgun)?
 *   - From which address will the recipient see this?
 *   - Are the credentials present, or is this group misconfigured?
 *
 * Three visual states, in priority order:
 *   1. ERROR (red)   — no default group, or credentials missing for the resolved provider
 *   2. WARN  (amber) — falling back to platform default (user not explicitly assigned)
 *   3. OK    (green) — explicitly assigned group, credentials present
 *
 * This component is deliberately read-only. Changing the active group is an admin action
 * (Admin → SMTP Groups → Make Default / Assign Users). We deep-link to that page rather
 * than duplicating the controls here.
 */
interface Props {
  /** Only show the banner for email channel; WhatsApp/SMS use different routing. */
  channel?: 'email' | 'whatsapp' | 'sms';
  /** Compact pill style for inline contexts (e.g. campaign list). Default false = full banner. */
  compact?: boolean;
}

const PROVIDER_LABEL: Record<string, string> = {
  smtp: 'SMTP (custom server)',
  brevo: 'Brevo',
  sendgrid: 'SendGrid',
  mailgun: 'Mailgun',
};

export default function ActiveSenderBanner({ channel = 'email', compact = false }: Props) {
  const [sender, setSender] = useState<ActiveSender | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const load = async (showSpinner = false) => {
    if (showSpinner) setRefreshing(true);
    try {
      const res: any = await meApi.getActiveSender();
      setSender(res?.data ?? null);
    } catch {
      setSender(null);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => { load(); }, []);

  // Banner is email-only. WhatsApp/SMS routing is separate; quietly skip for now.
  if (channel !== 'email') return null;

  if (loading) {
    return (
      <div className="mb-4 px-4 py-3 rounded-lg bg-gray-50 border border-gray-200 text-sm text-gray-500 flex items-center gap-2">
        <RefreshCw className="w-4 h-4 animate-spin" />
        Resolving active sender…
      </div>
    );
  }

  // Hard error — no group resolved at all. Without one, sends fall back to the mock service.
  if (!sender || !sender.groupId) {
    return (
      <div className="mb-4 px-4 py-3 rounded-lg bg-red-50 border border-red-300 text-sm">
        <div className="flex items-start gap-2">
          <AlertTriangle className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" />
          <div className="flex-1">
            <p className="font-semibold text-red-800">No active sending group configured.</p>
            <p className="text-red-700 mt-0.5">
              Sends will fail or hit the mock service. An admin must set a default SMTP group
              under <a href="/admin/smtp-groups" className="underline font-medium">Admin → SMTP Groups</a>.
            </p>
          </div>
        </div>
      </div>
    );
  }

  const provider = sender.provider ?? 'smtp';
  const providerLabel = PROVIDER_LABEL[provider] ?? provider.toUpperCase();
  const credentialMissing = provider === 'smtp' ? !sender.hasSmtpPassword : !sender.hasApiKey;

  // Tone selection — credential issue is the most actionable, so it leads.
  let tone: 'error' | 'warn' | 'ok' = 'ok';
  let message = 'Assigned to you.';
  if (credentialMissing) {
    tone = 'error';
    message = provider === 'smtp'
      ? 'SMTP password is not set — sends through this group will fail.'
      : `${providerLabel} API key is not set — sends through this group will fail.`;
  } else if (!sender.isAssignedToUser) {
    tone = 'warn';
    message = sender.isDefault
      ? 'Falling back to the platform default (you are not explicitly assigned).'
      : 'Falling back to the platform default.';
  }

  const styles = {
    error: 'bg-red-50 border-red-300 text-red-900',
    warn: 'bg-amber-50 border-amber-300 text-amber-900',
    ok: 'bg-emerald-50 border-emerald-300 text-emerald-900',
  }[tone];

  const Icon = tone === 'error' ? AlertTriangle : tone === 'warn' ? AlertTriangle : CheckCircle2;
  const iconColor = tone === 'error' ? 'text-red-600' : tone === 'warn' ? 'text-amber-600' : 'text-emerald-600';

  if (compact) {
    return (
      <div className={`inline-flex items-center gap-2 px-3 py-1.5 rounded-md border text-xs font-medium ${styles}`}>
        <Send className="w-3.5 h-3.5" />
        <span>Sending via <strong>{providerLabel}</strong></span>
        <span className="opacity-60">·</span>
        <span>{sender.groupName}</span>
        {credentialMissing && <AlertTriangle className="w-3.5 h-3.5 text-red-600" />}
      </div>
    );
  }

  return (
    <div className={`mb-4 px-4 py-3 rounded-lg border text-sm ${styles}`}>
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-start gap-3 flex-1 min-w-0">
          <Icon className={`w-5 h-5 flex-shrink-0 mt-0.5 ${iconColor}`} />
          <div className="flex-1 min-w-0">
            <p className="font-semibold flex items-center gap-2 flex-wrap">
              <Send className="w-4 h-4 inline" />
              Will send via: <span className="font-bold">{providerLabel}</span>
              <span className="opacity-60 font-normal">·</span>
              <span>{sender.groupName}</span>
              {sender.isDefault && (
                <span className="text-xs uppercase tracking-wide px-1.5 py-0.5 rounded bg-white/60 border border-current/30">default</span>
              )}
            </p>
            {(sender.fromEmail || sender.fromName) && (
              <p className="mt-1 text-xs opacity-80 truncate">
                <Server className="w-3 h-3 inline mr-1" />
                From: <span className="font-medium">{sender.fromName ?? '—'}</span>{' '}
                &lt;<code className="font-mono">{sender.fromEmail ?? '(no from-address set)'}</code>&gt;
              </p>
            )}
            <p className="mt-1 text-xs">{message}</p>
          </div>
        </div>
        <button
          type="button"
          onClick={() => load(true)}
          disabled={refreshing}
          className="p-1.5 rounded hover:bg-white/50 disabled:opacity-50 flex-shrink-0"
          title="Refresh sender info"
        >
          <RefreshCw className={`w-4 h-4 ${refreshing ? 'animate-spin' : ''}`} />
        </button>
      </div>
    </div>
  );
}
