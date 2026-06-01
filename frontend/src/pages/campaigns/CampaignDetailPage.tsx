import { useParams, Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, Mail, MessageCircle, Smartphone, Users, CheckCircle, XCircle, RotateCw, Eye } from 'lucide-react';
import toast from 'react-hot-toast';
import { campaignApi } from '../../api/campaignApi';
import { formatDate, formatNumber, getStatusColor, getChannelColor } from '../../utils/formatters';
import ActiveSenderBanner from '../../components/messages/ActiveSenderBanner';

export default function CampaignDetailPage() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const { data: campaign, isLoading } = useQuery({
    queryKey: ['campaign', id],
    queryFn: () => campaignApi.getById(id!),
    enabled: !!id,
  });

  const retryMutation = useMutation({
    mutationFn: () => campaignApi.retryFailed(id!),
    onSuccess: (res: any) => {
      const n = res?.data?.retriedCount ?? 0;
      queryClient.invalidateQueries({ queryKey: ['campaign', id] });
      queryClient.invalidateQueries({ queryKey: ['campaign-report', id] });
      queryClient.invalidateQueries({ queryKey: ['campaign-messages', id] });
      toast.success(`Retrying ${n} failed message${n === 1 ? '' : 's'}…`);
    },
    onError: (err: any) => {
      toast.error(err?.response?.data?.message || 'Failed to retry');
    },
  });

  const { data: report } = useQuery({
    queryKey: ['campaign-report', id],
    queryFn: () => campaignApi.getReport(id!),
    enabled: !!id,
  });

  const { data: messages } = useQuery({
    queryKey: ['campaign-messages', id],
    queryFn: () => campaignApi.getMessages(id!, { pageNumber: 1, pageSize: 50 }),
    enabled: !!id,
  });

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="w-8 h-8 border-4 border-primary-500 border-t-transparent rounded-full animate-spin" />
      </div>
    );
  }

  const c = campaign?.data;
  const r = report?.data;

  if (!c) return <p className="text-center text-gray-500 mt-12">Campaign not found</p>;

  const ChannelIcon = c.channel === 'email' ? Mail : c.channel === 'whatsapp' ? MessageCircle : Smartphone;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div className="flex items-center gap-4">
          <Link to="/campaigns" className="p-2 hover:bg-gray-100 rounded-xl"><ArrowLeft className="w-5 h-5" /></Link>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold text-gray-900">{c.name}</h1>
              <span className={`px-2.5 py-1 rounded-lg text-xs font-medium ${getStatusColor(c.status)}`}>{c.status}</span>
            </div>
            <p className="text-gray-500 mt-1">Created {formatDate(c.createdAt)}</p>
          </div>
        </div>
        {/* M2 — when a retry is possible, the user is about to trigger a send; show provider info. */}
        {(c.status === 'completed' || c.status === 'failed') && c.failedCount > 0 && c.channel === 'email' && (
          <div className="w-full">
            <ActiveSenderBanner channel="email" compact />
          </div>
        )}
        {/* Retry only makes sense when the campaign has finished and has some failures */}
        {(c.status === 'completed' || c.status === 'failed') && c.failedCount > 0 && (
          <button
            onClick={() => {
              if (confirm(`Retry ${c.failedCount} failed message${c.failedCount === 1 ? '' : 's'}?\n\nThe campaign will re-queue and only the failed messages will be re-sent.`)) {
                retryMutation.mutate();
              }
            }}
            disabled={retryMutation.isPending}
            className="flex items-center gap-2 px-4 py-2 bg-amber-600 text-white rounded-xl text-sm font-medium hover:bg-amber-700 disabled:opacity-50"
          >
            <RotateCw className={`w-4 h-4 ${retryMutation.isPending ? 'animate-spin' : ''}`} />
            {retryMutation.isPending ? 'Retrying…' : `Retry ${c.failedCount} failed`}
          </button>
        )}
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        {[
          { label: 'Channel', value: c.channel, icon: ChannelIcon, color: getChannelColor(c.channel) },
          { label: 'Total Contacts', value: formatNumber(c.totalContacts), icon: Users, color: 'bg-blue-50 text-blue-600' },
          { label: 'Sent', value: formatNumber(c.sentCount), icon: CheckCircle, color: 'bg-green-50 text-green-600' },
          { label: 'Failed', value: formatNumber(c.failedCount), icon: XCircle, color: 'bg-red-50 text-red-600' },
        ].map((stat) => (
          <div key={stat.label} className="bg-white rounded-2xl p-5 shadow-sm border border-gray-100">
            <div className="flex items-center gap-3">
              <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${stat.color}`}>
                <stat.icon className="w-5 h-5" />
              </div>
              <div>
                <p className="text-sm text-gray-500">{stat.label}</p>
                <p className="text-xl font-bold text-gray-900">{stat.value}</p>
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Report */}
      {r && (
        <div className="bg-white rounded-2xl p-6 shadow-sm border border-gray-100">
          <h3 className="text-lg font-semibold text-gray-900 mb-4">Delivery Report</h3>
          <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-7 gap-4">
            <div className="text-center p-4 bg-gray-50 rounded-xl">
              <p className="text-2xl font-bold text-gray-900">{r.sendRate}%</p>
              <p className="text-xs text-gray-500 mt-1">Send Rate</p>
            </div>
            <div className="text-center p-4 bg-gray-50 rounded-xl">
              <p className="text-2xl font-bold text-red-600">{r.failRate}%</p>
              <p className="text-xs text-gray-500 mt-1">Fail Rate</p>
            </div>
            <div className="text-center p-4 bg-gray-50 rounded-xl">
              <p className="text-2xl font-bold text-orange-600">{formatNumber((r as any).bouncedCount || 0)}</p>
              <p className="text-xs text-gray-500 mt-1">Bounced</p>
            </div>
            <div className="text-center p-4 bg-gray-50 rounded-xl">
              <p className="text-2xl font-bold text-green-600">{formatNumber(r.deliveredCount)}</p>
              <p className="text-xs text-gray-500 mt-1">Delivered</p>
            </div>
            <div className="text-center p-4 bg-gray-50 rounded-xl" title={(r as any).openRate != null ? `${(r as any).openRate}% of sent` : undefined}>
              <p className="text-2xl font-bold text-purple-600">{formatNumber(r.openedCount)}</p>
              <p className="text-xs text-gray-500 mt-1">Opened {(r as any).openRate != null && r.openedCount > 0 ? `(${(r as any).openRate}%)` : ''}</p>
            </div>
            <div className="text-center p-4 bg-gray-50 rounded-xl" title={(r as any).totalClicks ? `${(r as any).totalClicks} total clicks` : undefined}>
              <p className="text-2xl font-bold text-pink-600">{formatNumber((r as any).clickedCount || 0)}</p>
              <p className="text-xs text-gray-500 mt-1">Clicked {(r as any).clickRate != null && ((r as any).clickedCount || 0) > 0 ? `(${(r as any).clickRate}%)` : ''}</p>
            </div>
            <div className="text-center p-4 bg-gray-50 rounded-xl">
              <p className="text-[11px] text-gray-500 leading-tight">{r.startedAt ? formatDate(r.startedAt) : '-'}</p>
              <p className="text-[11px] text-gray-500 mt-1">{r.completedAt ? formatDate(r.completedAt) : 'In progress'}</p>
            </div>
          </div>
        </div>
      )}

      {/* Messages */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        <div className="p-6 border-b border-gray-100">
          <h3 className="text-lg font-semibold text-gray-900">Messages ({messages?.totalCount || 0})</h3>
        </div>
        <table className="w-full">
          <thead>
            <tr className="bg-gray-50 border-b border-gray-100">
              <th className="text-left px-6 py-3 text-xs font-semibold text-gray-500 uppercase">Contact</th>
              <th className="text-left px-6 py-3 text-xs font-semibold text-gray-500 uppercase">Status</th>
              <th className="text-left px-6 py-3 text-xs font-semibold text-gray-500 uppercase">Sent At</th>
              <th className="text-left px-6 py-3 text-xs font-semibold text-gray-500 uppercase">Opened</th>
              <th className="text-left px-6 py-3 text-xs font-semibold text-gray-500 uppercase">Clicks</th>
              <th className="text-left px-6 py-3 text-xs font-semibold text-gray-500 uppercase">Error</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {messages?.data?.map((m) => (
              <tr key={m.id} className="hover:bg-gray-50">
                <td className="px-6 py-3">
                  <p className="font-medium text-gray-900 text-sm">{m.contactName}</p>
                  <p className="text-xs text-gray-500">{m.contactEmail || m.contactPhone}</p>
                </td>
                <td className="px-6 py-3">
                  <span className={`px-2.5 py-1 rounded-lg text-xs font-medium ${getStatusColor(m.status)}`}>{m.status}</span>
                </td>
                <td className="px-6 py-3 text-sm text-gray-500">{formatDate(m.sentAt)}</td>
                <td className="px-6 py-3 text-sm">
                  {(m as any).openedAt ? (
                    <span className="inline-flex items-center gap-1 text-purple-600">
                      <Eye className="w-3.5 h-3.5" />
                      {formatDate((m as any).openedAt)}
                    </span>
                  ) : (
                    <span className="text-gray-300">—</span>
                  )}
                </td>
                <td className="px-6 py-3 text-sm">
                  {((m as any).clickCount || 0) > 0 ? (
                    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-pink-50 text-pink-700 text-xs font-medium">
                      {(m as any).clickCount}× click{(m as any).clickCount > 1 ? 's' : ''}
                    </span>
                  ) : (
                    <span className="text-gray-300">—</span>
                  )}
                </td>
                <td className="px-6 py-3 text-sm text-red-500">{m.errorMessage || '-'}</td>
              </tr>
            ))}
            {(!messages?.data || messages.data.length === 0) && (
              <tr><td colSpan={6} className="px-6 py-8 text-center text-gray-400">No messages yet</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}