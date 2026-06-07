import { useEffect, useState, useRef } from 'react';
import { CheckCircle2, XCircle, Clock, Send, X, PartyPopper, Mail, Loader2, ChevronDown, ChevronUp, ExternalLink } from 'lucide-react';
import { Link } from 'react-router-dom';
import axiosInstance from '../../api/axiosInstance';

interface MessageItem {
  id: string;
  contactName: string;
  contactEmail?: string;
  contactPhone?: string;
  status: string;
  errorMessage?: string;
  sentAt?: string;
}

interface CampaignReport {
  campaignId: string;
  campaignName: string;
  channel: string;
  status: string;
  totalContacts: number;
  sentCount: number;
  failedCount: number;
  deliveredCount: number;
  sendRate: number;
  startedAt?: string;
  completedAt?: string;
}

interface Props {
  campaignId: string;
  channel: string;
  onDismiss: () => void;
}

/**
 * Inline (non-blocking) progress card placed in the right sidebar of the Send Message page.
 * Polls the campaign status every 1.5s. User can freely navigate elsewhere in the app —
 * the campaign continues server-side; closing this card just stops the UI polling.
 */
export default function SendProgressCard({ campaignId, channel, onDismiss }: Props) {
  const [report, setReport] = useState<CampaignReport | null>(null);
  const [messages, setMessages] = useState<MessageItem[]>([]);
  const [expanded, setExpanded] = useState(true);
  const [recipientsExpanded, setRecipientsExpanded] = useState(false);
  const pollRef = useRef<number | null>(null);
  const startedAtRef = useRef<number>(Date.now());
  const [elapsedSec, setElapsedSec] = useState(0);

  const fetchProgress = async () => {
    try {
      const [reportRes, messagesRes]: any[] = await Promise.all([
        axiosInstance.get(`/campaigns/${campaignId}/report`, { _silent: true } as any),
        axiosInstance.get(`/campaigns/${campaignId}/messages?pageSize=200`, { _silent: true } as any),
      ]);
      if (reportRes?.data) setReport(reportRes.data);
      const msgData = messagesRes?.data;
      const list = Array.isArray(msgData) ? msgData : (msgData?.items || []);
      setMessages(list);

      const status = reportRes?.data?.status?.toLowerCase();
      if (status === 'completed' || status === 'failed') {
        if (pollRef.current) { window.clearInterval(pollRef.current); pollRef.current = null; }
        // One last fetch after a short delay — the campaign's sentCount/failedCount and
        // the individual message rows may settle a moment after status becomes "completed",
        // so we re-poll once to guarantee the UI reflects the final authoritative state.
        window.setTimeout(async () => {
          try {
            const [r2, m2]: any[] = await Promise.all([
              axiosInstance.get(`/campaigns/${campaignId}/report`, { _silent: true } as any),
              axiosInstance.get(`/campaigns/${campaignId}/messages?pageSize=200`, { _silent: true } as any),
            ]);
            if (r2?.data) setReport(r2.data);
            const m2Data = m2?.data;
            const m2List = Array.isArray(m2Data) ? m2Data : (m2Data?.items || []);
            setMessages(m2List);
          } catch { /* silent */ }
        }, 1500);
      }
    } catch { /* silent — keep retrying */ }
  };

  useEffect(() => {
    fetchProgress();
    pollRef.current = window.setInterval(fetchProgress, 1500);
    const tickRef = window.setInterval(() => {
      setElapsedSec(Math.floor((Date.now() - startedAtRef.current) / 1000));
    }, 1000);
    return () => {
      if (pollRef.current) window.clearInterval(pollRef.current);
      window.clearInterval(tickRef);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [campaignId]);

  // Live counts from messages (the report endpoint only updates at end of campaign)
  const liveSent = messages.filter(m => ['sent', 'delivered', 'opened'].includes(m.status?.toLowerCase() ?? '')).length;
  const liveFailed = messages.filter(m => m.status?.toLowerCase() === 'failed').length;
  const livePending = messages.filter(m => !['sent', 'delivered', 'opened', 'failed'].includes(m.status?.toLowerCase() ?? '')).length;

  const total = report?.totalContacts || messages.length || 0;
  const isComplete = report?.status?.toLowerCase() === 'completed' || report?.status?.toLowerCase() === 'failed';
  const sent = isComplete ? (report?.sentCount ?? liveSent) : liveSent;
  const failed = isComplete ? (report?.failedCount ?? liveFailed) : liveFailed;
  const pending = isComplete ? Math.max(0, total - sent - failed) : (livePending || Math.max(0, total - sent - failed));
  const processed = sent + failed;
  const progressPct = total > 0 ? Math.round((processed / total) * 100) : 0;
  const successRate = total > 0 ? Math.round((sent / total) * 100) : 0;

  const formatElapsed = (s: number) => s < 60 ? `${s}s` : `${Math.floor(s / 60)}m ${s % 60}s`;

  const sortedMessages = [...messages].sort((a, b) => {
    const order = { failed: 0, pending: 1, sent: 2, delivered: 2 } as Record<string, number>;
    return (order[a.status?.toLowerCase()] ?? 1) - (order[b.status?.toLowerCase()] ?? 1);
  });

  const headerGradient = isComplete
    ? (failed === 0 ? 'from-emerald-500 via-green-500 to-teal-500'
       : failed === total ? 'from-red-500 via-rose-500 to-pink-500'
       : 'from-amber-500 via-orange-500 to-yellow-500')
    : 'from-blue-600 via-primary-600 to-purple-600';

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden">
      {/* === Header === */}
      <div className={`px-4 py-3 bg-gradient-to-r ${headerGradient} text-white relative`}>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2.5 min-w-0">
            <div className="w-8 h-8 bg-white/20 backdrop-blur-sm rounded-lg flex items-center justify-center flex-shrink-0">
              {isComplete ? <PartyPopper className="w-4 h-4" /> : <Send className="w-4 h-4 animate-pulse" />}
            </div>
            <div className="min-w-0">
              <h3 className="font-semibold text-sm truncate">
                {isComplete ? 'Campaign Complete' : 'Sending Campaign'}
              </h3>
              <p className="text-[11px] text-white/80 mt-0.5">
                {channel.toUpperCase()} &middot; {formatElapsed(elapsedSec)}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-1 flex-shrink-0">
            <button
              onClick={() => setExpanded(!expanded)}
              className="p-1.5 hover:bg-white/20 rounded transition-colors"
              title={expanded ? 'Minimize' : 'Expand'}
            >
              {expanded ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
            </button>
            <button
              onClick={onDismiss}
              className="p-1.5 hover:bg-white/20 rounded transition-colors"
              title="Dismiss (campaign continues in background)"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Progress bar — always visible in header */}
        <div className="mt-3">
          <div className="flex items-end justify-between mb-1">
            <span className="text-[11px] font-medium">
              {processed}/{total}
            </span>
            <span className="text-lg font-bold tabular-nums leading-none">{progressPct}%</span>
          </div>
          <div className="w-full h-2 bg-white/20 rounded-full overflow-hidden">
            <div
              className="h-full bg-white rounded-full transition-all duration-500 ease-out relative"
              style={{ width: `${progressPct}%` }}
            >
              {!isComplete && progressPct > 0 && (
                <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/40 to-transparent animate-pulse" />
              )}
            </div>
          </div>
        </div>
      </div>

      {/* === Body (collapsible) === */}
      {expanded && (
        <>
          {/* Stat grid */}
          <div className="grid grid-cols-3 gap-2 p-3 bg-gray-50 border-b border-gray-100">
            <StatChip icon={<CheckCircle2 className="w-3.5 h-3.5" />} label="Sent" value={sent} color="emerald" highlight={sent > 0} />
            <StatChip icon={<XCircle className="w-3.5 h-3.5" />} label="Failed" value={failed} color="red" highlight={failed > 0} />
            <StatChip icon={<Clock className="w-3.5 h-3.5" />} label="Pending" value={pending} color="amber" animate={!isComplete && pending > 0} />
          </div>

          {/* Status banner when complete */}
          {isComplete && total > 0 && (
            <div className={`px-3 py-2 text-[11px] font-medium ${
              failed === 0 ? 'bg-emerald-50 text-emerald-800' :
              failed === total ? 'bg-red-50 text-red-800' :
              'bg-amber-50 text-amber-800'
            } border-b ${
              failed === 0 ? 'border-emerald-100' : failed === total ? 'border-red-100' : 'border-amber-100'
            }`}>
              {failed === 0 && `🎉 All ${total} ${channel === 'email' ? 'emails' : 'messages'} sent successfully (${successRate}%)`}
              {failed === total && `All ${total} failed. Check your provider settings.`}
              {failed > 0 && failed < total && `${sent}/${total} sent (${successRate}%). ${failed} failed.`}
            </div>
          )}

          {/* Recipients toggle */}
          <button
            onClick={() => setRecipientsExpanded(!recipientsExpanded)}
            className="w-full px-3 py-2 flex items-center justify-between hover:bg-gray-50 transition-colors text-xs font-medium text-gray-700 border-b border-gray-100"
          >
            <span className="flex items-center gap-1.5">
              <Mail className="w-3.5 h-3.5" />
              Recipients ({messages.length})
              {!isComplete && <Loader2 className="w-3 h-3 animate-spin text-blue-500" />}
            </span>
            {recipientsExpanded ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
          </button>

          {recipientsExpanded && (
            <div className="max-h-64 overflow-y-auto p-2 space-y-1">
              {messages.length === 0 ? (
                <div className="text-center py-4 text-xs text-gray-400 flex items-center justify-center gap-2">
                  <Loader2 className="w-3.5 h-3.5 animate-spin" /> Initializing...
                </div>
              ) : (
                sortedMessages.map(m => {
                  const s = m.status?.toLowerCase();
                  const isSent = s === 'sent' || s === 'delivered';
                  const isFailed = s === 'failed';
                  return (
                    <div
                      key={m.id}
                      className={`flex items-center gap-2 p-2 rounded text-xs ${
                        isSent ? 'bg-emerald-50/40' : isFailed ? 'bg-red-50/40' : 'bg-gray-50/50'
                      }`}
                      title={m.errorMessage || ''}
                    >
                      <div className={`w-5 h-5 rounded-full flex items-center justify-center flex-shrink-0 ${
                        isSent ? 'text-emerald-600 bg-emerald-100' :
                        isFailed ? 'text-red-600 bg-red-100' :
                        'text-amber-600 bg-amber-100'
                      }`}>
                        {isSent ? <CheckCircle2 className="w-3 h-3" /> :
                         isFailed ? <XCircle className="w-3 h-3" /> :
                         <Clock className="w-3 h-3 animate-pulse" />}
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="font-medium text-gray-900 truncate">{m.contactName}</p>
                        <p className="text-[10px] text-gray-500 truncate">
                          {m.contactEmail || m.contactPhone || '—'}
                        </p>
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          )}

          {/* Footer note */}
          <div className="px-3 py-2 bg-gray-50 border-t border-gray-100 text-[10px] text-gray-500 flex items-center justify-between gap-2">
            <span>
              {isComplete ? (
                <>Finished in <span className="font-medium text-gray-700">{formatElapsed(elapsedSec)}</span></>
              ) : (
                <span className="flex items-center gap-1">
                  <span className="relative flex w-1.5 h-1.5">
                    <span className="absolute inline-flex h-full w-full rounded-full bg-blue-400 opacity-75 animate-ping" />
                    <span className="relative inline-flex rounded-full h-1.5 w-1.5 bg-blue-500" />
                  </span>
                  Live &middot; updating every 1.5s
                </span>
              )}
            </span>
            <Link
              to={`/campaigns/${campaignId}`}
              className="flex items-center gap-1 text-blue-600 hover:text-blue-800 font-medium"
            >
              View details <ExternalLink className="w-3 h-3" />
            </Link>
          </div>
        </>
      )}
    </div>
  );
}

function StatChip({ icon, label, value, color, highlight, animate }: {
  icon: React.ReactNode;
  label: string;
  value: number;
  color: 'emerald' | 'red' | 'amber';
  highlight?: boolean;
  animate?: boolean;
}) {
  const palette = {
    emerald: { bg: 'bg-emerald-50', border: 'border-emerald-200', text: 'text-emerald-700', icon: 'text-emerald-600' },
    red: { bg: 'bg-red-50', border: 'border-red-200', text: 'text-red-700', icon: 'text-red-600' },
    amber: { bg: 'bg-amber-50', border: 'border-amber-200', text: 'text-amber-700', icon: 'text-amber-600' },
  }[color];
  return (
    <div className={`${palette.bg} ${palette.border} border rounded-lg p-2 transition-transform ${highlight ? 'scale-[1.02]' : ''}`}>
      <div className="flex items-center justify-between mb-0.5">
        <span className={`${palette.icon} ${animate ? 'animate-pulse' : ''}`}>{icon}</span>
        <span className="text-[9px] uppercase tracking-wide text-gray-500 font-medium">{label}</span>
      </div>
      <p className={`${palette.text} text-lg font-bold tabular-nums leading-tight`}>{value}</p>
    </div>
  );
}
