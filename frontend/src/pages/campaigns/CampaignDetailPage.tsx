import { useParams, Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, Mail, MessageCircle, Smartphone, Users, CheckCircle, XCircle } from 'lucide-react';
import { campaignApi } from '../../api/campaignApi';
import { formatDate, formatNumber, getStatusColor, getChannelColor } from '../../utils/formatters';

export default function CampaignDetailPage() {
  const { id } = useParams<{ id: string }>();

  const { data: campaign, isLoading } = useQuery({
    queryKey: ['campaign', id],
    queryFn: () => campaignApi.getById(id!),
    enabled: !!id,
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
          <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
            <div className="text-center p-4 bg-gray-50 rounded-xl">
              <p className="text-2xl font-bold text-gray-900">{r.sendRate}%</p>
              <p className="text-sm text-gray-500 mt-1">Send Rate</p>
            </div>
            <div className="text-center p-4 bg-gray-50 rounded-xl">
              <p className="text-2xl font-bold text-red-600">{r.failRate}%</p>
              <p className="text-sm text-gray-500 mt-1">Fail Rate</p>
            </div>
            <div className="text-center p-4 bg-gray-50 rounded-xl">
              <p className="text-2xl font-bold text-green-600">{formatNumber(r.deliveredCount)}</p>
              <p className="text-sm text-gray-500 mt-1">Delivered</p>
            </div>
            <div className="text-center p-4 bg-gray-50 rounded-xl">
              <p className="text-2xl font-bold text-purple-600">{formatNumber(r.openedCount)}</p>
              <p className="text-sm text-gray-500 mt-1">Opened</p>
            </div>
            <div className="text-center p-4 bg-gray-50 rounded-xl">
              <p className="text-sm text-gray-500">{r.startedAt ? formatDate(r.startedAt) : '-'}</p>
              <p className="text-sm text-gray-500 mt-1">{r.completedAt ? formatDate(r.completedAt) : 'In progress'}</p>
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
                <td className="px-6 py-3 text-sm text-red-500">{m.errorMessage || '-'}</td>
              </tr>
            ))}
            {(!messages?.data || messages.data.length === 0) && (
              <tr><td colSpan={4} className="px-6 py-8 text-center text-gray-400">No messages yet</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}