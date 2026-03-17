import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, Send, Trash2, Eye, Mail, MessageCircle, Smartphone } from 'lucide-react';
import { campaignApi } from '../../api/campaignApi';
import { contactApi } from '../../api/contactApi';
import { templateApi } from '../../api/templateApi';
import { formatDate, formatNumber, getStatusColor, getChannelColor } from '../../utils/formatters';
import toast from 'react-hot-toast';

export default function CampaignsPage() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState('');
  const [channelFilter, setChannelFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);

  // Form state
  const [formName, setFormName] = useState('');
  const [formChannel, setFormChannel] = useState('email');
  const [formTemplateId, setFormTemplateId] = useState('');
  const [formGroupId, setFormGroupId] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['campaigns', page, statusFilter, channelFilter],
    queryFn: () => campaignApi.getAll({ pageNumber: page, pageSize: 20, status: statusFilter || undefined, channel: channelFilter || undefined }),
  });

  const { data: templates } = useQuery({
    queryKey: ['templates'],
    queryFn: () => templateApi.getAll(),
    enabled: showCreate,
  });

  const { data: groups } = useQuery({
    queryKey: ['contact-groups'],
    queryFn: () => contactApi.getGroups(),
    enabled: showCreate,
  });

  const createMutation = useMutation({
    mutationFn: (d: any) => campaignApi.create(d),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['campaigns'] });
      setShowCreate(false);
      setFormName('');
      toast.success('Campaign created!');
    },
  });

  const sendMutation = useMutation({
    mutationFn: (id: string) => campaignApi.send(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['campaigns'] });
      toast.success('Campaign queued for sending!');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => campaignApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['campaigns'] });
      toast.success('Campaign deleted');
    },
  });

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    createMutation.mutate({ name: formName, channel: formChannel, templateId: formTemplateId, groupId: formGroupId });
  };

  const campaigns = data?.data || [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Campaigns</h1>
          <p className="text-gray-500 mt-1">Manage your marketing campaigns</p>
        </div>
        <button
          onClick={() => setShowCreate(!showCreate)}
          className="flex items-center gap-2 px-4 py-2.5 bg-gradient-to-r from-primary-600 to-primary-700 text-white rounded-xl font-medium hover:from-primary-700 hover:to-primary-800 shadow-lg shadow-primary-200 transition-all"
        >
          <Plus className="w-5 h-5" />
          New Campaign
        </button>
      </div>

      {/* Create Form */}
      {showCreate && (
        <div className="bg-white rounded-2xl p-6 shadow-sm border border-gray-100">
          <h3 className="text-lg font-semibold mb-4">Create Campaign</h3>
          <form onSubmit={handleCreate} className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Name</label>
              <input
                type="text"
                value={formName}
                onChange={(e) => setFormName(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-transparent outline-none"
                placeholder="Campaign name"
                required
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Channel</label>
              <select
                value={formChannel}
                onChange={(e) => setFormChannel(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-transparent outline-none"
              >
                <option value="email">Email</option>
                <option value="whatsapp">WhatsApp</option>
                <option value="sms">SMS</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Template</label>
              <select
                value={formTemplateId}
                onChange={(e) => setFormTemplateId(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-transparent outline-none"
                required
              >
                <option value="">Select template</option>
                {templates?.data?.map((t) => (
                  <option key={t.id} value={t.id}>{t.name} ({t.channel})</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Contact Group</label>
              <select
                value={formGroupId}
                onChange={(e) => setFormGroupId(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 focus:border-transparent outline-none"
                required
              >
                <option value="">Select group</option>
                {groups?.data?.map((g) => (
                  <option key={g.id} value={g.id}>{g.name} ({g.contactCount} contacts)</option>
                ))}
              </select>
            </div>
            <div className="md:col-span-2 flex justify-end gap-3">
              <button type="button" onClick={() => setShowCreate(false)} className="px-4 py-2 text-gray-600 hover:bg-gray-100 rounded-xl">Cancel</button>
              <button type="submit" disabled={createMutation.isPending} className="px-6 py-2 bg-primary-600 text-white rounded-xl font-medium hover:bg-primary-700 disabled:opacity-50">
                {createMutation.isPending ? 'Creating...' : 'Create'}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Filters */}
      <div className="flex gap-3">
        <select value={statusFilter} onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }} className="px-4 py-2 border border-gray-200 rounded-xl text-sm focus:ring-2 focus:ring-primary-500 outline-none">
          <option value="">All Statuses</option>
          <option value="draft">Draft</option>
          <option value="queued">Queued</option>
          <option value="running">Running</option>
          <option value="completed">Completed</option>
          <option value="failed">Failed</option>
        </select>
        <select value={channelFilter} onChange={(e) => { setChannelFilter(e.target.value); setPage(1); }} className="px-4 py-2 border border-gray-200 rounded-xl text-sm focus:ring-2 focus:ring-primary-500 outline-none">
          <option value="">All Channels</option>
          <option value="email">Email</option>
          <option value="whatsapp">WhatsApp</option>
          <option value="sms">SMS</option>
        </select>
      </div>

      {/* Campaign List */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
        {isLoading ? (
          <div className="p-12 text-center text-gray-400">
            <div className="w-8 h-8 border-4 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto mb-3" />
            Loading campaigns...
          </div>
        ) : campaigns.length === 0 ? (
          <div className="p-12 text-center text-gray-400">
            <Send className="w-10 h-10 mx-auto mb-3 opacity-50" />
            <p className="text-lg font-medium">No campaigns found</p>
            <p className="text-sm mt-1">Create your first campaign to get started</p>
          </div>
        ) : (
          <table className="w-full">
            <thead>
              <tr className="bg-gray-50 border-b border-gray-100">
                <th className="text-left px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider">Campaign</th>
                <th className="text-left px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider">Channel</th>
                <th className="text-left px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider">Status</th>
                <th className="text-left px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider">Progress</th>
                <th className="text-left px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider">Date</th>
                <th className="text-right px-6 py-3 text-xs font-semibold text-gray-500 uppercase tracking-wider">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {campaigns.map((c) => (
                <tr key={c.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-6 py-4">
                    <Link to={`/campaigns/${c.id}`} className="font-medium text-gray-900 hover:text-primary-600">{c.name}</Link>
                    {c.groupName && <p className="text-xs text-gray-500 mt-0.5">{c.groupName}</p>}
                  </td>
                  <td className="px-6 py-4">
                    <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-medium ${getChannelColor(c.channel)}`}>
                      {c.channel === 'email' ? <Mail className="w-3.5 h-3.5" /> : c.channel === 'whatsapp' ? <MessageCircle className="w-3.5 h-3.5" /> : <Smartphone className="w-3.5 h-3.5" />}
                      {c.channel}
                    </span>
                  </td>
                  <td className="px-6 py-4">
                    <span className={`px-2.5 py-1 rounded-lg text-xs font-medium ${getStatusColor(c.status)}`}>{c.status}</span>
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex items-center gap-2">
                      <div className="w-24 h-2 bg-gray-100 rounded-full overflow-hidden">
                        <div
                          className="h-full bg-green-500 rounded-full"
                          style={{ width: `${c.totalContacts > 0 ? (c.sentCount / c.totalContacts) * 100 : 0}%` }}
                        />
                      </div>
                      <span className="text-xs text-gray-500">{formatNumber(c.sentCount)}/{formatNumber(c.totalContacts)}</span>
                    </div>
                  </td>
                  <td className="px-6 py-4 text-sm text-gray-500">{formatDate(c.createdAt)}</td>
                  <td className="px-6 py-4 text-right">
                    <div className="flex items-center justify-end gap-1">
                      <Link to={`/campaigns/${c.id}`} className="p-2 text-gray-400 hover:text-primary-600 hover:bg-primary-50 rounded-lg"><Eye className="w-4 h-4" /></Link>
                      {c.status === 'draft' && (
                        <button onClick={() => sendMutation.mutate(c.id)} className="p-2 text-gray-400 hover:text-green-600 hover:bg-green-50 rounded-lg"><Send className="w-4 h-4" /></button>
                      )}
                      {c.status !== 'running' && (
                        <button onClick={() => deleteMutation.mutate(c.id)} className="p-2 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg"><Trash2 className="w-4 h-4" /></button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {/* Pagination */}
        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-between px-6 py-4 border-t border-gray-100">
            <p className="text-sm text-gray-500">
              Showing {((page - 1) * 20) + 1}-{Math.min(page * 20, data.totalCount)} of {data.totalCount}
            </p>
            <div className="flex gap-2">
              <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={!data.hasPrevious} className="px-3 py-1.5 border rounded-lg text-sm disabled:opacity-50 hover:bg-gray-50">Previous</button>
              <button onClick={() => setPage(p => p + 1)} disabled={!data.hasNext} className="px-3 py-1.5 border rounded-lg text-sm disabled:opacity-50 hover:bg-gray-50">Next</button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}