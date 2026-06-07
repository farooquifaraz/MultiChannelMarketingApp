import { useEffect, useState, useRef } from 'react';
import { CheckCircle2, XCircle, Clock, Send, X, PartyPopper, Mail, Loader2, TrendingUp, AlertTriangle } from 'lucide-react';
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
  failRate: number;
  startedAt?: string;
  completedAt?: string;
}

interface Props {
  campaignId: string;
  channel: string;
  onClose: () => void;
}

export default function SendProgressModal({ campaignId, channel, onClose }: Props) {
  const [report, setReport] = useState<CampaignReport | null>(null);
  const [messages, setMessages] = useState<MessageItem[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const pollRef = useRef<number | null>(null);
  const elapsedRef = useRef<number>(Date.now());
  const [elapsedSec, setElapsedSec] = useState(0);

  // Poll the campaign report + messages until status === completed/failed
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
      setLoadError(null);

      // Stop polling once campaign is in terminal state
      const status = reportRes?.data?.status?.toLowerCase();
      if (status === 'completed' || status === 'failed') {
        if (pollRef.current) { window.clearInterval(pollRef.current); pollRef.current = null; }
      }
    } catch (err: any) {
      setLoadError(err?.response?.data?.message || 'Failed to load progress');
    }
  };

  useEffect(() => {
    // Initial fetch immediately, then poll every 1.5s
    fetchProgress();
    pollRef.current = window.setInterval(fetchProgress, 1500);
    const tickRef = window.setInterval(() => {
      setElapsedSec(Math.floor((Date.now() - elapsedRef.current) / 1000));
    }, 1000);
    return () => {
      if (pollRef.current) window.clearInterval(pollRef.current);
      window.clearInterval(tickRef);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [campaignId]);

  // Compute live counts from the messages list (report endpoint only updates at end of campaign)
  const liveSent = messages.filter(m => ['sent', 'delivered', 'opened'].includes(m.status?.toLowerCase() ?? '')).length;
  const liveFailed = messages.filter(m => m.status?.toLowerCase() === 'failed').length;
  const livePending = messages.filter(m => !['sent', 'delivered', 'opened', 'failed'].includes(m.status?.toLowerCase() ?? '')).length;

  // Total comes from report (or messages length as fallback during initial load)
  const total = report?.totalContacts || messages.length || 0;
  // Prefer report counts when campaign is completed (authoritative), else use live counts
  const isComplete = report?.status?.toLowerCase() === 'completed' || report?.status?.toLowerCase() === 'failed';
  const sent = isComplete ? (report?.sentCount ?? liveSent) : liveSent;
  const failed = isComplete ? (report?.failedCount ?? liveFailed) : liveFailed;
  const pending = isComplete ? Math.max(0, total - sent - failed) : (livePending || Math.max(0, total - sent - failed));
  const processed = sent + failed;
  const progressPct = total > 0 ? Math.round((processed / total) * 100) : 0;
  const successRate = total > 0 ? Math.round((sent / total) * 100) : 0;

  const formatElapsed = (s: number) => {
    if (s < 60) return `${s}s`;
    return `${Math.floor(s / 60)}m ${s % 60}s`;
  };

  const statusBadge = (status: string) => {
    const s = status?.toLowerCase();
    if (s === 'sent' || s === 'delivered') return (
      <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200">
        <CheckCircle2 className="w-3 h-3" /> Sent
      </span>
    );
    if (s === 'failed') return (
      <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-red-50 text-red-700 border border-red-200">
        <XCircle className="w-3 h-3" /> Failed
      </span>
    );
    return (
      <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full bg-amber-50 text-amber-700 border border-amber-200">
        <Clock className="w-3 h-3 animate-pulse" /> Pending
      </span>
    );
  };

  // Sort messages: failed first, then pending, then sent — so user sees actionable items at top
  const sortedMessages = [...messages].sort((a, b) => {
    const order = { failed: 0, pending: 1, sent: 2, delivered: 2 } as Record<string, number>;
    const ao = order[a.status?.toLowerCase()] ?? 1;
    const bo = order[b.status?.toLowerCase()] ?? 1;
    return ao - bo;
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
      <div className="bg-white rounded-2xl w-full max-w-3xl max-h-[92vh] flex flex-col shadow-2xl overflow-hidden">
        {/* === Header === */}
        <div className={`px-6 py-5 ${isComplete ? 'bg-gradient-to-r from-emerald-500 via-green-500 to-teal-500' : 'bg-gradient-to-r from-blue-600 via-primary-600 to-purple-600'} text-white relative overflow-hidden`}>
          {/* Decorative blobs */}
          <div className="absolute -top-12 -right-12 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
          <div className="absolute -bottom-12 -left-12 w-40 h-40 bg-white/10 rounded-full blur-2xl" />

          <div className="relative flex items-start justify-between">
            <div className="flex items-center gap-3">
              <div className="w-12 h-12 bg-white/20 backdrop-blur-sm rounded-xl flex items-center justify-center">
                {isComplete ? <PartyPopper className="w-6 h-6" /> : <Send className="w-6 h-6 animate-pulse" />}
              </div>
              <div>
                <h2 className="text-xl font-bold">
                  {isComplete ? 'Campaign Complete!' : 'Sending Campaign...'}
                </h2>
                <p className="text-sm text-white/80 mt-0.5">
                  {channel.toUpperCase()} &middot; {report?.campaignName || 'Loading...'} &middot; {formatElapsed(elapsedSec)} elapsed
                </p>
              </div>
            </div>
            <button onClick={onClose} className="p-2 hover:bg-white/20 rounded-lg transition-colors">
              <X className="w-5 h-5" />
            </button>
          </div>

          {/* Progress bar */}
          <div className="relative mt-5">
            <div className="flex items-end justify-between mb-1.5">
              <span className="text-sm font-medium">
                {processed} of {total} processed
              </span>
              <span className="text-2xl font-bold tabular-nums">{progressPct}%</span>
            </div>
            <div className="w-full h-3 bg-white/20 rounded-full overflow-hidden">
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

        {/* === Stats Grid === */}
        <div className="grid grid-cols-4 gap-3 px-6 py-5 bg-gray-50 border-b border-gray-100">
          <StatCard
            icon={<Mail className="w-5 h-5" />}
            label="Total"
            value={total}
            color="blue"
          />
          <StatCard
            icon={<CheckCircle2 className="w-5 h-5" />}
            label="Sent"
            value={sent}
            color="emerald"
            highlight={sent > 0}
          />
          <StatCard
            icon={<XCircle className="w-5 h-5" />}
            label="Failed"
            value={failed}
            color="red"
            highlight={failed > 0}
          />
          <StatCard
            icon={<Clock className="w-5 h-5" />}
            label="Pending"
            value={pending}
            color="amber"
            animate={!isComplete && pending > 0}
          />
        </div>

        {/* Success rate banner — visible only when complete */}
        {isComplete && total > 0 && (
          <div className={`px-6 py-3 flex items-center gap-3 border-b ${
            failed === 0 ? 'bg-emerald-50 border-emerald-100 text-emerald-800' :
            failed === total ? 'bg-red-50 border-red-100 text-red-800' :
            'bg-amber-50 border-amber-100 text-amber-800'
          }`}>
            <TrendingUp className="w-5 h-5 flex-shrink-0" />
            <p className="text-sm font-medium">
              {failed === 0 && `🎉 Perfect! All ${total} ${channel === 'email' ? 'emails' : 'messages'} delivered. ${successRate}% success rate.`}
              {failed === total && `All ${total} messages failed. Check your provider settings.`}
              {failed > 0 && failed < total && `${sent}/${total} sent (${successRate}% success rate). ${failed} failed — see details below.`}
            </p>
          </div>
        )}

        {/* === Recipients list === */}
        <div className="flex-1 overflow-y-auto px-6 py-4">
          {loadError && (
            <div className="mb-3 p-3 bg-red-50 border border-red-100 rounded-lg text-sm text-red-700 flex items-start gap-2">
              <AlertTriangle className="w-4 h-4 mt-0.5 flex-shrink-0" />
              <span>{loadError}</span>
            </div>
          )}

          <h3 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2">
            Recipients ({messages.length})
            {!isComplete && <Loader2 className="w-3.5 h-3.5 animate-spin text-blue-500" />}
          </h3>

          {messages.length === 0 ? (
            <div className="text-center py-8">
              <Loader2 className="w-8 h-8 animate-spin text-blue-500 mx-auto mb-2" />
              <p className="text-sm text-gray-500">Initializing campaign...</p>
            </div>
          ) : (
            <div className="space-y-1.5">
              {sortedMessages.map((m) => (
                <div
                  key={m.id}
                  className={`flex items-center justify-between p-3 rounded-lg border transition-colors ${
                    m.status?.toLowerCase() === 'sent' || m.status?.toLowerCase() === 'delivered'
                      ? 'bg-emerald-50/30 border-emerald-100'
                      : m.status?.toLowerCase() === 'failed'
                      ? 'bg-red-50/30 border-red-100'
                      : 'bg-gray-50/50 border-gray-100'
                  }`}
                >
                  <div className="flex items-center gap-3 min-w-0 flex-1">
                    <div className={`w-9 h-9 rounded-full flex items-center justify-center text-sm font-semibold text-white flex-shrink-0 ${
                      m.status?.toLowerCase() === 'sent' ? 'bg-gradient-to-br from-emerald-500 to-teal-500' :
                      m.status?.toLowerCase() === 'failed' ? 'bg-gradient-to-br from-red-500 to-pink-500' :
                      'bg-gradient-to-br from-blue-500 to-primary-500'
                    }`}>
                      {(m.contactName || '?').charAt(0).toUpperCase()}
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="font-medium text-sm text-gray-900 truncate">{m.contactName}</p>
                      <p className="text-xs text-gray-500 truncate">
                        {m.contactEmail || m.contactPhone || '—'}
                      </p>
                      {m.errorMessage && (
                        <p className="text-xs text-red-600 mt-0.5 truncate" title={m.errorMessage}>
                          {m.errorMessage}
                        </p>
                      )}
                    </div>
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    {statusBadge(m.status)}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* === Footer === */}
        <div className="px-6 py-4 bg-gray-50 border-t border-gray-100 flex items-center justify-between">
          <p className="text-xs text-gray-500">
            {isComplete ? (
              <>Campaign finished in <span className="font-medium text-gray-700">{formatElapsed(elapsedSec)}</span></>
            ) : (
              <>Updating every 1.5s &middot; You can safely close this window — campaign continues in background</>
            )}
          </p>
          <button
            onClick={onClose}
            className={`px-5 py-2 rounded-lg text-sm font-medium transition-colors ${
              isComplete
                ? 'bg-emerald-600 text-white hover:bg-emerald-700'
                : 'bg-white text-gray-700 border border-gray-200 hover:bg-gray-100'
            }`}
          >
            {isComplete ? 'Done' : 'Close'}
          </button>
        </div>
      </div>
    </div>
  );
}

function StatCard({ icon, label, value, color, highlight, animate }: {
  icon: React.ReactNode;
  label: string;
  value: number;
  color: 'blue' | 'emerald' | 'red' | 'amber';
  highlight?: boolean;
  animate?: boolean;
}) {
  const palette = {
    blue: { bg: 'bg-blue-50', border: 'border-blue-100', text: 'text-blue-700', icon: 'text-blue-600' },
    emerald: { bg: 'bg-emerald-50', border: 'border-emerald-100', text: 'text-emerald-700', icon: 'text-emerald-600' },
    red: { bg: 'bg-red-50', border: 'border-red-100', text: 'text-red-700', icon: 'text-red-600' },
    amber: { bg: 'bg-amber-50', border: 'border-amber-100', text: 'text-amber-700', icon: 'text-amber-600' },
  }[color];
  return (
    <div className={`${palette.bg} ${palette.border} border rounded-xl p-3 transition-transform ${highlight ? 'scale-[1.02]' : ''}`}>
      <div className="flex items-center justify-between mb-1">
        <span className={`${palette.icon} ${animate ? 'animate-pulse' : ''}`}>{icon}</span>
        <span className="text-[10px] uppercase tracking-wide text-gray-500 font-medium">{label}</span>
      </div>
      <p className={`${palette.text} text-2xl font-bold tabular-nums`}>{value}</p>
    </div>
  );
}
