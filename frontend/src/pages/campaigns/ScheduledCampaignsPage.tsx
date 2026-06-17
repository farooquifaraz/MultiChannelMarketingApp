import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Clock, X, Calendar, Mail, MessageSquare, Smartphone, ExternalLink, ChevronLeft, ChevronRight, RefreshCw, AlertCircle, User as UserIcon } from 'lucide-react';
import toast from 'react-hot-toast';
import { campaignApi } from '../../api/campaignApi';
import { useAuthStore } from '../../store/authStore';

const channelIcon = (c: string) => {
  const k = c.toLowerCase();
  if (k.includes('whatsapp')) return <MessageSquare className="w-4 h-4 text-green-600" />;
  if (k.includes('sms')) return <Smartphone className="w-4 h-4 text-purple-600" />;
  return <Mail className="w-4 h-4 text-blue-600" />;
};

function formatCountdown(dateStr: string): { text: string; isPast: boolean } {
  const target = new Date(dateStr).getTime();
  const now = Date.now();
  const diff = target - now;
  if (diff <= 0) return { text: 'Firing now…', isPast: true };
  const totalSeconds = Math.floor(diff / 1000);
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const mins = Math.floor((totalSeconds % 3600) / 60);
  const secs = totalSeconds % 60;
  if (days > 0) return { text: `in ${days}d ${hours}h`, isPast: false };
  if (hours > 0) return { text: `in ${hours}h ${mins}m`, isPast: false };
  if (mins > 0) return { text: `in ${mins}m ${secs}s`, isPast: false };
  return { text: `in ${secs}s`, isPast: false };
}

export default function ScheduledCampaignsPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const user = useAuthStore((s) => s.user);
  const isAdmin = user?.role?.toLowerCase() === 'admin';

  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [viewAll, setViewAll] = useState(false);
  const [tick, setTick] = useState(0); // re-render every second for countdown

  // Re-render every second so countdowns stay live
  useEffect(() => {
    const t = setInterval(() => setTick((x) => x + 1), 1000);
    return () => clearInterval(t);
  }, []);

  const { data: resp, isLoading, refetch } = useQuery({
    queryKey: ['scheduled-campaigns', page, pageSize, viewAll, tick === 0 ? 0 : undefined],
    queryFn: () => campaignApi.getAll({ pageNumber: page, pageSize, status: 'queued', viewAll }),
    refetchInterval: 30000, // auto refresh every 30s in case scheduled ones fire
  });

  const cancelMutation = useMutation({
    mutationFn: (id: string) => campaignApi.cancelSchedule(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['scheduled-campaigns'] });
      queryClient.invalidateQueries({ queryKey: ['campaigns'] });
      toast.success('Scheduled send cancelled. Campaign moved back to draft.');
    },
    onError: (err: any) => {
      toast.error(err?.response?.data?.message || 'Failed to cancel scheduled send');
    },
  });

  const data: any[] = (resp as any)?.data || [];
  // Only show campaigns that actually have a future scheduledAt
  const scheduled = data.filter((c) => c.scheduledAt && new Date(c.scheduledAt).getTime() > Date.now() - 60_000);
  const totalPages = (resp as any)?.totalPages || 1;

  const handleCancel = (c: any) => {
    if (confirm(`Cancel scheduled send for "${c.name}"?\n\nThe campaign will move back to draft. Recipients will NOT receive this email.`)) {
      cancelMutation.mutate(c.id);
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex items-center gap-3">
          <div className="p-2.5 bg-amber-100 rounded-xl">
            <Clock className="w-6 h-6 text-amber-600" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-gray-900">Scheduled Campaigns</h1>
            <p className="text-sm text-gray-500">Upcoming sends that haven't fired yet</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {isAdmin && (
            <label className="flex items-center gap-2 px-3 py-2 border border-gray-200 rounded-xl text-sm cursor-pointer hover:bg-gray-50">
              <input
                type="checkbox"
                checked={viewAll}
                onChange={(e) => { setViewAll(e.target.checked); setPage(1); }}
                className="w-4 h-4 text-primary-600 rounded border-gray-300"
              />
              <span className="text-gray-700">All users (admin)</span>
            </label>
          )}
          <button
            onClick={() => refetch()}
            className="flex items-center gap-2 px-4 py-2 border border-gray-200 rounded-xl text-sm font-medium hover:bg-gray-50"
          >
            <RefreshCw className="w-4 h-4" />
            Refresh
          </button>
        </div>
      </div>

      {/* Summary card */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-white rounded-xl border border-gray-100 p-4 shadow-sm">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-amber-100 rounded-lg flex items-center justify-center">
              <Clock className="w-5 h-5 text-amber-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">Pending</p>
              <p className="text-xl font-bold text-gray-900">{scheduled.length}</p>
            </div>
          </div>
        </div>
        <div className="bg-white rounded-xl border border-gray-100 p-4 shadow-sm">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
              <Calendar className="w-5 h-5 text-blue-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">Next send</p>
              <p className="text-sm font-bold text-gray-900">
                {scheduled.length > 0
                  ? formatCountdown([...scheduled].sort((a, b) => new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime())[0].scheduledAt).text
                  : '—'}
              </p>
            </div>
          </div>
        </div>
        <div className="bg-white rounded-xl border border-gray-100 p-4 shadow-sm">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
              <UserIcon className="w-5 h-5 text-green-600" />
            </div>
            <div>
              <p className="text-xs text-gray-500">Scope</p>
              <p className="text-sm font-bold text-gray-900">
                {viewAll ? 'All users' : 'My campaigns'}
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Table */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        {isLoading ? (
          <div className="py-16 flex items-center justify-center">
            <div className="w-8 h-8 border-4 border-primary-500 border-t-transparent rounded-full animate-spin" />
          </div>
        ) : scheduled.length === 0 ? (
          <div className="py-16 text-center">
            <Clock className="w-12 h-12 text-gray-200 mx-auto mb-3" />
            <p className="text-gray-500 text-sm mb-1">No scheduled campaigns</p>
            <p className="text-xs text-gray-400">Schedule one from the Send page to see it here.</p>
            <button
              onClick={() => navigate('/send')}
              className="mt-4 px-5 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 text-sm font-medium"
            >
              Go to Send
            </button>
          </div>
        ) : (
          <div className="overflow-x-auto"><table className="w-full text-sm min-w-[640px]">
            <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide">
              <tr>
                <th className="px-4 py-3 text-left">Campaign</th>
                {viewAll && <th className="px-4 py-3 text-left">Owner</th>}
                <th className="px-4 py-3 text-left">Channel</th>
                <th className="px-4 py-3 text-left">Scheduled For</th>
                <th className="px-4 py-3 text-left">Countdown</th>
                <th className="px-4 py-3 text-left">Recipients</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {scheduled.map((c) => {
                const countdown = formatCountdown(c.scheduledAt);
                return (
                  <tr key={c.id} className="hover:bg-gray-50/50">
                    <td className="px-4 py-3">
                      <div>
                        <p className="font-medium text-gray-900">{c.name}</p>
                        {c.templateName && (
                          <p className="text-xs text-gray-400 mt-0.5">Template: {c.templateName}</p>
                        )}
                      </div>
                    </td>
                    {viewAll && (
                      <td className="px-4 py-3">
                        <p className="text-sm text-gray-900">{c.ownerName || '—'}</p>
                        <p className="text-xs text-gray-400">{c.ownerEmail || ''}</p>
                      </td>
                    )}
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        {channelIcon(c.channel)}
                        <span className="capitalize text-gray-700">{c.channel}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1.5 text-gray-700">
                        <Calendar className="w-3.5 h-3.5 text-gray-400" />
                        {new Date(c.scheduledAt).toLocaleString()}
                      </div>
                    </td>
                    <td className="px-4 py-3">
                      <span className={`inline-flex items-center gap-1 px-2 py-1 rounded-lg text-xs font-medium ${
                        countdown.isPast ? 'bg-red-50 text-red-700' : 'bg-amber-50 text-amber-700'
                      }`}>
                        <Clock className="w-3 h-3" />
                        {countdown.text}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-gray-700">{c.totalContacts}</td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex items-center justify-end gap-1">
                        <button
                          onClick={() => navigate(`/campaigns/${c.id}`)}
                          className="p-2 text-gray-500 hover:text-primary-600 hover:bg-primary-50 rounded-lg"
                          title="Open campaign"
                        >
                          <ExternalLink className="w-4 h-4" />
                        </button>
                        <button
                          onClick={() => handleCancel(c)}
                          disabled={cancelMutation.isPending}
                          className="flex items-center gap-1 px-3 py-1.5 text-red-600 hover:bg-red-50 rounded-lg text-xs font-medium disabled:opacity-50"
                          title="Cancel scheduled send"
                        >
                          <X className="w-3.5 h-3.5" />
                          Cancel
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table></div>
        )}

        {totalPages > 1 && (
          <div className="px-4 py-3 border-t border-gray-100 flex items-center justify-end gap-2">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="flex items-center gap-1 px-3 py-1.5 border border-gray-200 rounded-lg text-sm hover:bg-gray-50 disabled:opacity-40"
            >
              <ChevronLeft className="w-4 h-4" />
              Previous
            </button>
            <span className="text-sm text-gray-600">{page} / {totalPages}</span>
            <button
              onClick={() => setPage((p) => p + 1)}
              disabled={page >= totalPages}
              className="flex items-center gap-1 px-3 py-1.5 border border-gray-200 rounded-lg text-sm hover:bg-gray-50 disabled:opacity-40"
            >
              Next
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        )}
      </div>

      {/* Help note */}
      <div className="bg-blue-50 border border-blue-100 rounded-xl p-4 flex items-start gap-3">
        <AlertCircle className="w-5 h-5 text-blue-600 flex-shrink-0 mt-0.5" />
        <div className="text-sm text-blue-800">
          <p className="font-medium">How scheduling works</p>
          <p className="text-xs text-blue-700 mt-1">
            Cancelling moves the campaign back to <strong>draft</strong> so you can edit and re-schedule it.
            Recipients will <strong>not</strong> receive the email — the background job becomes a no-op when it fires.
          </p>
        </div>
      </div>
    </div>
  );
}
