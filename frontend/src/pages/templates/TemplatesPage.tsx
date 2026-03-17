import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2, Edit2, Eye, Mail, MessageCircle, Smartphone, FileText, X } from 'lucide-react';
import { templateApi } from '../../api/templateApi';
import { formatDate, getChannelColor } from '../../utils/formatters';
import toast from 'react-hot-toast';

export default function TemplatesPage() {
  const queryClient = useQueryClient();
  const [channelFilter, setChannelFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const [preview, setPreview] = useState<string | null>(null);

  // Form
  const [formName, setFormName] = useState('');
  const [formChannel, setFormChannel] = useState('email');
  const [formSubject, setFormSubject] = useState('');
  const [formBody, setFormBody] = useState('');

  const { data: templates, isLoading } = useQuery({
    queryKey: ['templates', channelFilter],
    queryFn: () => templateApi.getAll(channelFilter || undefined),
  });

  const createMutation = useMutation({
    mutationFn: (d: any) => editId ? templateApi.update(editId, d) : templateApi.create(d),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['templates'] });
      resetForm();
      toast.success(editId ? 'Template updated' : 'Template created');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => templateApi.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['templates'] });
      toast.success('Template deleted');
    },
  });

  const resetForm = () => {
    setShowCreate(false);
    setEditId(null);
    setFormName(''); setFormChannel('email'); setFormSubject(''); setFormBody('');
  };

  const startEdit = (t: any) => {
    setEditId(t.id);
    setFormName(t.name);
    setFormChannel(t.channel);
    setFormSubject(t.subject || '');
    setFormBody(t.body);
    setShowCreate(true);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    createMutation.mutate({
      name: formName,
      channel: formChannel,
      subject: formChannel === 'email' ? formSubject : undefined,
      body: formBody,
      ...(editId ? { isActive: true } : {}),
    });
  };

  const handlePreview = async (id: string) => {
    try {
      const res = await templateApi.preview(id, { name: 'John Doe', email: 'john@example.com', phone: '+1234567890', offer: '30%' });
      setPreview(res.data);
    } catch { /* handled */ }
  };

  const list = templates?.data || [];
  const ChannelIcons: Record<string, any> = { email: Mail, whatsapp: MessageCircle, sms: Smartphone };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Templates</h1>
          <p className="text-gray-500 mt-1">Create and manage message templates</p>
        </div>
        <button onClick={() => { resetForm(); setShowCreate(true); }} className="flex items-center gap-2 px-4 py-2.5 bg-gradient-to-r from-primary-600 to-primary-700 text-white rounded-xl font-medium hover:from-primary-700 hover:to-primary-800 shadow-lg shadow-primary-200">
          <Plus className="w-5 h-5" />
          New Template
        </button>
      </div>

      {/* Channel Filter */}
      <div className="flex gap-2">
        {['', 'email', 'whatsapp', 'sms'].map((ch) => (
          <button key={ch} onClick={() => setChannelFilter(ch)} className={`px-3 py-1.5 rounded-lg text-sm font-medium ${channelFilter === ch ? 'bg-primary-100 text-primary-700' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'}`}>
            {ch || 'All'}
          </button>
        ))}
      </div>

      {/* Create/Edit Form */}
      {showCreate && (
        <div className="bg-white rounded-2xl p-6 shadow-sm border border-gray-100">
          <h3 className="text-lg font-semibold mb-4">{editId ? 'Edit' : 'Create'} Template</h3>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Name</label>
                <input type="text" value={formName} onChange={(e) => setFormName(e.target.value)} className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none" required />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Channel</label>
                <select value={formChannel} onChange={(e) => setFormChannel(e.target.value)} className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none">
                  <option value="email">Email</option>
                  <option value="whatsapp">WhatsApp</option>
                  <option value="sms">SMS</option>
                </select>
              </div>
            </div>
            {formChannel === 'email' && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Subject</label>
                <input type="text" value={formSubject} onChange={(e) => setFormSubject(e.target.value)} className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none" required />
              </div>
            )}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Body <span className="text-gray-400 font-normal">(use {'{{name}}'}, {'{{email}}'}, {'{{offer}}'} for personalization)</span>
              </label>
              <textarea value={formBody} onChange={(e) => setFormBody(e.target.value)} rows={6} className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none resize-none font-mono text-sm" required />
            </div>
            <div className="flex justify-end gap-3">
              <button type="button" onClick={resetForm} className="px-4 py-2 text-gray-600 hover:bg-gray-100 rounded-xl">Cancel</button>
              <button type="submit" disabled={createMutation.isPending} className="px-6 py-2 bg-primary-600 text-white rounded-xl font-medium hover:bg-primary-700 disabled:opacity-50">
                {createMutation.isPending ? 'Saving...' : editId ? 'Update' : 'Create'}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Preview Modal */}
      {preview && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 max-h-[80vh] overflow-y-auto">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-semibold">Template Preview</h3>
              <button onClick={() => setPreview(null)} className="p-1 hover:bg-gray-100 rounded-lg"><X className="w-5 h-5" /></button>
            </div>
            <div className="bg-gray-50 rounded-xl p-4 whitespace-pre-wrap text-sm font-mono">{preview}</div>
          </div>
        </div>
      )}

      {/* Template Grid */}
      {isLoading ? (
        <div className="text-center py-12 text-gray-400">
          <div className="w-8 h-8 border-4 border-primary-500 border-t-transparent rounded-full animate-spin mx-auto mb-3" />
        </div>
      ) : list.length === 0 ? (
        <div className="bg-white rounded-2xl p-12 text-center shadow-sm border border-gray-100">
          <FileText className="w-10 h-10 mx-auto mb-3 text-gray-300" />
          <p className="text-lg font-medium text-gray-400">No templates yet</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {list.map((t) => {
            const Icon = ChannelIcons[t.channel] || Mail;
            return (
              <div key={t.id} className="bg-white rounded-2xl p-5 shadow-sm border border-gray-100 hover:shadow-md transition-shadow">
                <div className="flex items-start justify-between mb-3">
                  <div className="flex items-center gap-2">
                    <div className={`w-8 h-8 rounded-lg flex items-center justify-center ${getChannelColor(t.channel)}`}>
                      <Icon className="w-4 h-4" />
                    </div>
                    <div>
                      <p className="font-semibold text-gray-900 text-sm">{t.name}</p>
                      <p className="text-xs text-gray-500">{t.channel}</p>
                    </div>
                  </div>
                </div>
                {t.subject && <p className="text-sm text-gray-600 mb-2 font-medium">{t.subject}</p>}
                <p className="text-sm text-gray-500 line-clamp-3 mb-4">{t.body}</p>
                <div className="flex items-center justify-between pt-3 border-t border-gray-100">
                  <span className="text-xs text-gray-400">{formatDate(t.updatedAt)}</span>
                  <div className="flex gap-1">
                    <button onClick={() => handlePreview(t.id)} className="p-1.5 text-gray-400 hover:text-primary-600 hover:bg-primary-50 rounded-lg"><Eye className="w-4 h-4" /></button>
                    <button onClick={() => startEdit(t)} className="p-1.5 text-gray-400 hover:text-amber-600 hover:bg-amber-50 rounded-lg"><Edit2 className="w-4 h-4" /></button>
                    <button onClick={() => deleteMutation.mutate(t.id)} className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg"><Trash2 className="w-4 h-4" /></button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}