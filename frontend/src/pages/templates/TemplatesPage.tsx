import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2, Edit2, Eye, Mail, MessageCircle, Smartphone, FileText, X, Share2, Lock, Sparkles, Globe2, Users as UsersIcon, User as UserIcon, Check, Image as ImageIcon, Layers, Send, Loader2, Save } from 'lucide-react';
import { templateApi } from '../../api/templateApi';
import { ChannelPreview, ChannelLogo, CHANNEL_META } from '../../components/creatives/ChannelPreview';
import type { TemplateDto } from '../../types/contact.types';
import axiosInstance from '../../api/axiosInstance';
import { formatDate, getChannelColor } from '../../utils/formatters';
import toast from 'react-hot-toast';
import { useAuthStore } from '../../store/authStore';
import { smtpGroupsApi } from '../../api/smtpGroupsApi';

export default function TemplatesPage() {
  const queryClient = useQueryClient();
  const user = useAuthStore((s) => s.user);
  const isAdmin = user?.role?.toLowerCase() === 'admin';
  const [channelFilter, setChannelFilter] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const [preview, setPreview] = useState<string | null>(null);   // server-rendered HTML (email/code view)
  const [previewTpl, setPreviewTpl] = useState<TemplateDto | null>(null); // the template being previewed
  const [previewMode, setPreviewMode] = useState<'rendered' | 'code'>('rendered');

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

  const shareMutation = useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: any }) =>
      axiosInstance.post(`/templates/${id}/share`, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['templates'] });
      toast.success('Sharing updated');
      setShareModal(null);
    },
  });

  // Share modal state
  const [shareModal, setShareModal] = useState<any | null>(null);
  // Grouped-edit ("set") modal state
  const [editSet, setEditSet] = useState<{ id: string; name: string; items: TemplateDto[] } | null>(null);
  const navigate = useNavigate();

  const resetForm = () => {
    setShowCreate(false);
    setEditId(null);
    setFormName(''); setFormChannel('email'); setFormSubject(''); setFormBody('');
  };

  const startEdit = (t: any) => {
    setEditId(t.id);
    setFormName(t.name);
    // Normalize channel to lowercase — API returns "Email"/"WhatsApp" but our form values are lowercase.
    // Without normalization, the channel===email check fails and the Subject field disappears.
    setFormChannel((t.channel || 'email').toLowerCase());
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

  const handlePreview = async (t: TemplateDto) => {
    setPreviewTpl(t);
    setPreviewMode('rendered');
    try {
      const res = await templateApi.preview(t.id, {
        name: 'John Doe',
        first_name: 'John',
        email: 'john@example.com',
        phone: '+1 234-567-890',
        offer: '30%',
        sender_name: 'Ahsan Yaqoob',
        sender_email: 'imahsanyaqoob@gmail.com',
        sender_designation: 'Customer Success Manager',
        sender_phone: '+92 300 1234567',
        company_name: 'IzyLrn',
        company_website: 'https://izylrn.com/en',
        current_year: new Date().getFullYear().toString(),
        current_date: new Date().toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' }),
      });
      setPreview(res.data);
    } catch { /* handled */ }
  };

  const list: TemplateDto[] = (templates?.data as any) || [];
  const ChannelIcons: Record<string, any> = { email: Mail, whatsapp: MessageCircle, sms: Smartphone, instagram: MessageCircle, facebook: MessageCircle };

  // Group templates created together ("sets") vs standalone.
  const chOrder = ['whatsapp', 'instagram', 'facebook', 'email', 'sms'];
  const groupMap = new Map<string, TemplateDto[]>();
  const standalone: TemplateDto[] = [];
  list.forEach((t) => {
    if (t.templateGroupId) { const k = t.templateGroupId; (groupMap.get(k) || groupMap.set(k, []).get(k)!).push(t); }
    else standalone.push(t);
  });
  const sets = Array.from(groupMap.entries()).map(([id, items]) => ({
    id, name: items[0].templateGroupName || 'Template set',
    items: items.slice().sort((a, b) => chOrder.indexOf(a.channel.toLowerCase()) - chOrder.indexOf(b.channel.toLowerCase())),
  })).sort((a, b) => (b.items[0]?.updatedAt || '').localeCompare(a.items[0]?.updatedAt || ''));

  const newCampaignFromSet = (items: TemplateDto[]) => {
    const pick = items.find((i) => i.channel.toLowerCase() === 'email') || items.find((i) => i.channel.toLowerCase() === 'whatsapp') || items[0];
    navigate('/campaigns', { state: { templateId: pick.id, channel: pick.channel.toLowerCase(), name: pick.templateGroupName } });
  };
  const deleteSet = async (items: TemplateDto[]) => {
    if (!window.confirm(`Delete all ${items.length} templates in this set?`)) return;
    try { await Promise.all(items.map((i) => templateApi.delete(i.id))); queryClient.invalidateQueries({ queryKey: ['templates'] }); toast.success('Set deleted'); }
    catch { toast.error('Could not delete the set'); }
  };

  const stripHtml = (s: string) => s.replace(/<style[^>]*>[\s\S]*?<\/style>/gi, '').replace(/<[^>]+>/g, ' ').replace(/&nbsp;/g, ' ').replace(/&[a-z]+;/g, ' ').replace(/\s+/g, ' ').trim();

  // Standalone template card (reused for the non-grouped grid).
  const renderCard = (t: any) => {
    const Icon = ChannelIcons[t.channel?.toLowerCase()] || ChannelIcons[t.channel] || Mail;
    const isOwned = !t.userId || t.userId === user?.id;
    const isSharedFromAdmin = t.isShared && !isOwned;
    return (
      <div key={t.id} className={`bg-white rounded-2xl p-5 shadow-sm border transition-shadow hover:shadow-md ${isSharedFromAdmin ? 'border-amber-200 ring-1 ring-amber-100' : 'border-gray-100'}`}>
        <div className="flex items-start justify-between mb-3">
          <div className="flex items-center gap-2">
            <div className={`w-8 h-8 rounded-lg flex items-center justify-center ${getChannelColor(t.channel)}`}><Icon className="w-4 h-4" /></div>
            <div>
              <p className="font-semibold text-gray-900 text-sm flex items-center gap-1.5">{t.name}
                {isSharedFromAdmin && <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 bg-amber-100 text-amber-700 text-[9px] font-semibold rounded-full uppercase tracking-wider"><Sparkles className="w-2.5 h-2.5" /> Shared by admin</span>}
                {isOwned && t.isShared && (() => {
                  const scope = (t.shareScope || 'global').toLowerCase();
                  const cfg = scope === 'global' ? { Icon: Globe2, label: 'Global', cls: 'bg-emerald-100 text-emerald-700' }
                    : scope === 'groups' ? { Icon: UsersIcon, label: `${(t.sharedWithGroupIds || []).length} group(s)`, cls: 'bg-blue-100 text-blue-700' }
                    : { Icon: UserIcon, label: `${(t.sharedWithUserIds || []).length} user(s)`, cls: 'bg-purple-100 text-purple-700' };
                  return <span className={`inline-flex items-center gap-0.5 px-1.5 py-0.5 ${cfg.cls} text-[9px] font-semibold rounded-full uppercase tracking-wider`}><cfg.Icon className="w-2.5 h-2.5" /> {cfg.label}</span>;
                })()}
              </p>
              <p className="text-xs text-gray-500 capitalize flex items-center gap-1.5">{t.channel}
                {t.mediaUrl && <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 bg-indigo-50 text-indigo-600 text-[9px] font-semibold rounded-full"><ImageIcon className="w-2.5 h-2.5" /> image</span>}
              </p>
            </div>
          </div>
        </div>
        {t.subject && <p className="text-sm text-gray-600 mb-2 font-medium line-clamp-1">{t.subject}</p>}
        <p className="text-sm text-gray-500 line-clamp-3 mb-4">{stripHtml(t.body).slice(0, 180)}</p>
        <div className="flex items-center justify-between pt-3 border-t border-gray-100">
          <span className="text-xs text-gray-400">{formatDate(t.updatedAt)}</span>
          <div className="flex gap-1">
            <button onClick={() => handlePreview(t)} className="p-1.5 text-gray-400 hover:text-primary-600 hover:bg-primary-50 rounded-lg" title="Preview"><Eye className="w-4 h-4" /></button>
            {isAdmin && isOwned && <button onClick={() => setShareModal(t)} className={`p-1.5 rounded-lg ${t.isShared ? 'text-emerald-600 bg-emerald-50 hover:bg-emerald-100' : 'text-gray-400 hover:text-emerald-600 hover:bg-emerald-50'}`} title="Manage sharing">{t.isShared ? <Share2 className="w-4 h-4" /> : <Lock className="w-4 h-4" />}</button>}
            {isOwned && <>
              <button onClick={() => startEdit(t)} className="p-1.5 text-gray-400 hover:text-amber-600 hover:bg-amber-50 rounded-lg" title="Edit"><Edit2 className="w-4 h-4" /></button>
              <button onClick={() => deleteMutation.mutate(t.id)} className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg" title="Delete"><Trash2 className="w-4 h-4" /></button>
            </>}
          </div>
        </div>
      </div>
    );
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4 rounded-3xl px-6 py-5 text-white shadow-xl" style={{ background: 'linear-gradient(120deg,#4f46e5,#7c3aed 55%,#db2777)' }}>
        <div className="w-12 h-12 rounded-2xl bg-white/20 grid place-items-center text-2xl">🗂️</div>
        <div>
          <h1 className="text-2xl font-bold">Templates</h1>
          <p className="text-white/80 text-sm mt-0.5">Saved copy for every channel — grouped by the campaign they were created in</p>
        </div>
        <button onClick={() => { resetForm(); setShowCreate(true); }} className="ml-auto flex items-center gap-2 px-4 py-2.5 bg-white text-indigo-700 rounded-xl font-semibold hover:bg-indigo-50 shadow-lg">
          <Plus className="w-5 h-5" />
          New Template
        </button>
      </div>

      {/* Channel Filter */}
      <div className="flex gap-2">
        {['', 'email', 'whatsapp', 'sms', 'instagram', 'facebook'].map((ch) => (
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
                  <option value="instagram">Instagram</option>
                  <option value="facebook">Facebook</option>
                </select>
              </div>
            </div>
            {formChannel === 'email' && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Email Subject Line <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={formSubject}
                  onChange={(e) => setFormSubject(e.target.value)}
                  placeholder="e.g., A note from {{sender_name}} — {{company_name}}"
                  className="w-full px-4 py-2.5 border border-gray-200 rounded-xl focus:ring-2 focus:ring-primary-500 outline-none"
                  required
                />
                <p className="text-xs text-gray-500 mt-1.5">
                  💡 The line recipients see in their inbox. Use <code className="bg-gray-100 px-1 rounded">{'{{first_name}}'}</code> to personalize.
                </p>
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

      {/* Preview Modal — channel-aware (platform-style render + image if attached) */}
      {previewTpl && (() => {
        const ch = (previewTpl.channel || 'email').toLowerCase();
        const meta = CHANNEL_META[ch] || CHANNEL_META.email;
        const isEmail = ch === 'email';
        const closeModal = () => { setPreviewTpl(null); setPreview(null); };
        return (
          <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4" onClick={(e) => e.target === e.currentTarget && closeModal()}>
            <div className="bg-white rounded-2xl w-full max-w-2xl max-h-[90vh] flex flex-col shadow-2xl overflow-hidden">
              {/* Header */}
              <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
                <div className="flex items-center gap-3">
                  <span className={`w-9 h-9 rounded-xl grid place-items-center bg-gradient-to-br ${meta.head}`}><ChannelLogo channel={ch} size={20} invert /></span>
                  <div>
                    <h3 className="text-lg font-semibold text-gray-900">{meta.label} Preview</h3>
                    <p className="text-xs text-gray-500 mt-0.5">Sample data — John Doe</p>
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <button onClick={() => setPreviewMode((m) => (m === 'rendered' ? 'code' : 'rendered'))} className="px-3 py-1.5 text-xs font-medium bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-lg" title="Toggle rendered / code view">
                    {previewMode === 'rendered' ? '<> View Code' : '🖼️ View Rendered'}
                  </button>
                  <button onClick={closeModal} className="p-1.5 hover:bg-gray-100 rounded-lg"><X className="w-5 h-5" /></button>
                </div>
              </div>

              {/* Body */}
              <div className="flex-1 overflow-auto p-6 bg-gradient-to-br from-indigo-50/40 to-slate-100">
                {previewMode === 'code' ? (
                  <pre className="w-full min-h-[300px] overflow-auto bg-gray-900 text-green-300 text-xs p-4 rounded-xl whitespace-pre-wrap font-mono leading-relaxed">{previewTpl.body}</pre>
                ) : isEmail ? (
                  // Email keeps the full server-rendered HTML (merge tags + inline image) in an iframe.
                  <iframe srcDoc={preview || previewTpl.body} title="Email Preview" className="w-full h-[60vh] min-h-[420px] bg-white rounded-xl border border-gray-200 shadow-inner" sandbox="" />
                ) : (
                  <div className="flex justify-center py-2">
                    <ChannelPreview channel={ch} img={previewTpl.mediaUrl} omitImageWhenEmpty fields={{ text: previewTpl.body, subject: previewTpl.subject }} />
                  </div>
                )}
                {!isEmail && !previewTpl.mediaUrl && previewMode === 'rendered' && (
                  <p className="text-center text-xs text-gray-400 mt-3">This template was saved without an image — content only.</p>
                )}
              </div>
            </div>
          </div>
        );
      })()}

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
        <div className="space-y-8">
          {/* ===== Template sets (created together) ===== */}
          {sets.length > 0 && (
            <div>
              <div className="flex items-center gap-2.5 mb-3">
                <Layers className="w-5 h-5 text-indigo-600" />
                <h2 className="text-sm font-bold text-gray-800">Template sets</h2>
                <span className="text-[11px] font-semibold text-indigo-700 bg-indigo-50 px-2.5 py-0.5 rounded-full">created together</span>
                <div className="flex-1 h-px bg-gradient-to-r from-gray-200 to-transparent" />
              </div>
              <div className="space-y-4">
                {sets.map((set) => (
                  <div key={set.id} className="bg-white rounded-2xl border border-gray-100 overflow-hidden shadow-sm">
                    <div className="flex items-center gap-3 px-5 py-3.5 text-white" style={{ background: 'linear-gradient(120deg,#1e293b,#4338ca)' }}>
                      <div className="w-9 h-9 rounded-xl bg-white/15 grid place-items-center text-lg">✨</div>
                      <div className="min-w-0">
                        <b className="text-sm block truncate">{set.name}</b>
                        <small className="text-white/70 text-[11.5px]">{set.items.length} channel{set.items.length > 1 ? 's' : ''} · {formatDate(set.items[0].updatedAt)}</small>
                      </div>
                      <div className="ml-auto flex gap-2">
                        <button onClick={() => setEditSet(set)} className="px-3 py-1.5 bg-white text-indigo-700 rounded-lg text-xs font-bold inline-flex items-center gap-1.5"><Edit2 className="w-3.5 h-3.5" /> Edit set</button>
                        <button onClick={() => newCampaignFromSet(set.items)} className="px-3 py-1.5 bg-white/15 text-white rounded-lg text-xs font-semibold inline-flex items-center gap-1.5"><Send className="w-3.5 h-3.5" /> Campaign</button>
                        <button onClick={() => deleteSet(set.items)} className="px-2.5 py-1.5 bg-white/15 text-white rounded-lg text-xs"><Trash2 className="w-3.5 h-3.5" /></button>
                      </div>
                    </div>
                    <div className="grid gap-3.5 p-4" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(230px, 1fr))' }}>
                      {set.items.map((t) => {
                        const ch = t.channel.toLowerCase();
                        const meta = CHANNEL_META[ch] || CHANNEL_META.email;
                        return (
                          <div key={t.id} className="border border-gray-200 rounded-xl overflow-hidden hover:shadow-md transition-shadow">
                            <div className={`flex items-center gap-2 px-3 py-2 text-white bg-gradient-to-r ${meta.head}`}>
                              <ChannelLogo channel={ch} size={15} invert /><b className="text-xs">{meta.label}</b>
                              {t.mediaUrl && <span className="ml-auto text-[10px] bg-white/25 px-1.5 py-0.5 rounded-full">🖼</span>}
                            </div>
                            <p className="px-3 py-2.5 text-xs text-gray-500 leading-snug h-[68px] overflow-hidden">{stripHtml(t.body).slice(0, 130)}</p>
                            <div className="flex gap-1.5 px-3 py-2 border-t border-gray-100">
                              <button onClick={() => handlePreview(t)} className="flex-1 text-[11.5px] font-semibold text-gray-500 border border-gray-200 rounded-lg py-1.5 hover:text-indigo-600">👁 Preview</button>
                              <button onClick={() => startEdit(t)} className="flex-1 text-[11.5px] font-semibold text-gray-500 border border-gray-200 rounded-lg py-1.5 hover:text-amber-600">✎ Edit</button>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* ===== Standalone templates ===== */}
          {standalone.length > 0 && (
            <div>
              <div className="flex items-center gap-2.5 mb-3">
                <FileText className="w-5 h-5 text-gray-500" />
                <h2 className="text-sm font-bold text-gray-800">Standalone templates</h2>
                <div className="flex-1 h-px bg-gradient-to-r from-gray-200 to-transparent" />
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                {standalone.map(renderCard)}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Grouped-edit ("set") modal */}
      {editSet && (
        <GroupEditModal
          set={editSet}
          onClose={() => setEditSet(null)}
          onSaved={() => { queryClient.invalidateQueries({ queryKey: ['templates'] }); setEditSet(null); }}
        />
      )}

      {/* Share Settings Modal */}
      {shareModal && (
        <ShareModal
          template={shareModal}
          onClose={() => setShareModal(null)}
          onSave={(payload) => shareMutation.mutate({ id: shareModal.id, payload })}
          saving={shareMutation.isPending}
        />
      )}
    </div>
  );
}

function ShareModal({ template, onClose, onSave, saving }: {
  template: any;
  onClose: () => void;
  onSave: (payload: any) => void;
  saving: boolean;
}) {
  const [isShared, setIsShared] = useState<boolean>(!!template.isShared);
  const [scope, setScope] = useState<'global' | 'groups' | 'users'>(
    (template.shareScope || 'global').toLowerCase()
  );
  const [groupIds, setGroupIds] = useState<Set<string>>(new Set(template.sharedWithGroupIds || []));
  const [userIds, setUserIds] = useState<Set<string>>(new Set(template.sharedWithUserIds || []));

  const { data: groupsRes } = useQuery({
    queryKey: ['smtp-groups-list'],
    queryFn: () => smtpGroupsApi.list(),
  });
  const { data: usersRes } = useQuery({
    queryKey: ['user-assignments-for-share'],
    queryFn: () => smtpGroupsApi.userAssignments(),
  });
  const groups: any[] = groupsRes?.data || [];
  const users: any[] = usersRes?.data || [];

  const toggle = (set: Set<string>, id: string, setter: (s: Set<string>) => void) => {
    const next = new Set(set);
    if (next.has(id)) next.delete(id); else next.add(id);
    setter(next);
  };

  const save = () => {
    onSave({
      isShared,
      shareScope: scope,
      sharedWithGroupIds: scope === 'groups' ? Array.from(groupIds) : [],
      sharedWithUserIds: scope === 'users' ? Array.from(userIds) : [],
    });
  };

  const ScopeCard = ({ id, Icon, title, subtitle }: any) => (
    <button
      type="button"
      onClick={() => setScope(id)}
      disabled={!isShared}
      className={`flex items-start gap-3 p-3 rounded-xl border-2 text-left transition-all ${
        !isShared ? 'opacity-40 cursor-not-allowed border-gray-100' :
        scope === id ? 'border-primary-500 bg-primary-50' : 'border-gray-100 hover:border-gray-200'
      }`}
    >
      <Icon className={`w-5 h-5 mt-0.5 flex-shrink-0 ${scope === id && isShared ? 'text-primary-600' : 'text-gray-400'}`} />
      <div className="min-w-0">
        <p className={`text-sm font-semibold ${scope === id && isShared ? 'text-primary-900' : 'text-gray-700'}`}>{title}</p>
        <p className="text-xs text-gray-500 mt-0.5">{subtitle}</p>
      </div>
    </button>
  );

  return (
    <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl w-full max-w-2xl max-h-[90vh] flex flex-col shadow-2xl">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between">
          <div>
            <h3 className="text-lg font-semibold text-gray-900">Share Template</h3>
            <p className="text-xs text-gray-500 mt-0.5">{template.name}</p>
          </div>
          <button onClick={onClose} className="p-1 hover:bg-gray-100 rounded-lg"><X className="w-5 h-5" /></button>
        </div>

        <div className="flex-1 overflow-y-auto p-6 space-y-5">
          {/* Master toggle */}
          <label className="flex items-center justify-between p-4 bg-gray-50 rounded-xl cursor-pointer">
            <div className="flex items-center gap-3">
              {isShared ? <Share2 className="w-5 h-5 text-emerald-600" /> : <Lock className="w-5 h-5 text-gray-400" />}
              <div>
                <p className="text-sm font-semibold text-gray-900">{isShared ? 'Shared' : 'Private'}</p>
                <p className="text-xs text-gray-500">{isShared ? 'Other users may see this template based on scope below.' : 'Only you can see this template.'}</p>
              </div>
            </div>
            <input
              type="checkbox"
              checked={isShared}
              onChange={(e) => setIsShared(e.target.checked)}
              className="w-5 h-5 text-emerald-600 rounded"
            />
          </label>

          {/* Scope picker */}
          <div>
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Who can see it?</p>
            <div className="grid grid-cols-3 gap-2">
              <ScopeCard id="global" Icon={Globe2} title="Global"     subtitle="All users in the platform" />
              <ScopeCard id="groups" Icon={UsersIcon} title="By group" subtitle="Users in selected SMTP groups" />
              <ScopeCard id="users"  Icon={UserIcon} title="By user"  subtitle="Only specifically picked users" />
            </div>
          </div>

          {/* Groups picker */}
          {isShared && scope === 'groups' && (
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
                Select groups ({groupIds.size} selected)
              </p>
              <div className="space-y-1 border border-gray-100 rounded-xl p-2 max-h-64 overflow-y-auto">
                {groups.length === 0 ? (
                  <p className="text-xs text-gray-400 text-center py-4">No SMTP groups yet — create one first in Admin → SMTP Groups.</p>
                ) : groups.map((g) => (
                  <label key={g.id} className="flex items-center gap-3 p-2 rounded-lg hover:bg-gray-50 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={groupIds.has(g.id)}
                      onChange={() => toggle(groupIds, g.id, setGroupIds)}
                      className="w-4 h-4 text-primary-600 rounded"
                    />
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-gray-900 flex items-center gap-1.5">
                        {g.name}
                        {g.isDefault && (
                          <span className="px-1.5 py-0.5 bg-amber-100 text-amber-700 text-[9px] font-semibold rounded-full uppercase">Default</span>
                        )}
                      </p>
                      <p className="text-xs text-gray-500">{g.assignedUserCount} user(s) assigned</p>
                    </div>
                    {groupIds.has(g.id) && <Check className="w-4 h-4 text-primary-600" />}
                  </label>
                ))}
              </div>
            </div>
          )}

          {/* Users picker */}
          {isShared && scope === 'users' && (
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">
                Select users ({userIds.size} selected)
              </p>
              <div className="space-y-1 border border-gray-100 rounded-xl p-2 max-h-64 overflow-y-auto">
                {users.length === 0 ? (
                  <p className="text-xs text-gray-400 text-center py-4">No app users yet.</p>
                ) : users.map((u) => (
                  <label key={u.userId} className="flex items-center gap-3 p-2 rounded-lg hover:bg-gray-50 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={userIds.has(u.userId)}
                      onChange={() => toggle(userIds, u.userId, setUserIds)}
                      className="w-4 h-4 text-primary-600 rounded"
                    />
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-gray-900">{u.fullName}</p>
                      <p className="text-xs text-gray-500 truncate">
                        {u.email}
                        {u.smtpGroupName && <span className="ml-2 text-blue-600">· {u.smtpGroupName}</span>}
                      </p>
                    </div>
                    {userIds.has(u.userId) && <Check className="w-4 h-4 text-primary-600" />}
                  </label>
                ))}
              </div>
            </div>
          )}
        </div>

        <div className="px-6 py-4 border-t border-gray-100 flex justify-end gap-3">
          <button onClick={onClose} className="px-4 py-2 text-gray-600 hover:bg-gray-100 rounded-lg">Cancel</button>
          <button
            onClick={save}
            disabled={saving}
            className="px-5 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:opacity-50 font-medium"
          >
            {saving ? 'Saving...' : 'Save Sharing'}
          </button>
        </div>
      </div>
    </div>
  );
}

/** Edit all channels of a Creative-Studio "set" together (or individually), then Save all. */
function GroupEditModal({ set, onClose, onSaved }: {
  set: { id: string; name: string; items: TemplateDto[] };
  onClose: () => void;
  onSaved: () => void;
}) {
  const items = set.items;
  const [edits, setEdits] = useState<Record<string, { subject: string; body: string }>>(() => {
    const m: Record<string, { subject: string; body: string }> = {};
    items.forEach((i) => { m[i.id] = { subject: i.subject || '', body: i.body || '' }; });
    return m;
  });
  const hasIG = items.some((i) => i.channel.toLowerCase() === 'instagram');
  const hasFB = items.some((i) => i.channel.toLowerCase() === 'facebook');
  const [sync, setSync] = useState(true);
  const [saving, setSaving] = useState(false);

  const setBody = (id: string, channel: string, body: string) => {
    setEdits((prev) => {
      const next = { ...prev, [id]: { ...prev[id], body } };
      const c = channel.toLowerCase();
      if (sync && (c === 'instagram' || c === 'facebook')) {
        items.forEach((i) => { const ic = i.channel.toLowerCase(); if ((ic === 'instagram' || ic === 'facebook') && i.id !== id) next[i.id] = { ...next[i.id], body }; });
      }
      return next;
    });
  };
  const setSubject = (id: string, subject: string) => setEdits((p) => ({ ...p, [id]: { ...p[id], subject } }));

  const saveAll = async () => {
    setSaving(true);
    try {
      await Promise.all(items.map((i) => {
        const e = edits[i.id];
        return templateApi.update(i.id, {
          name: i.name, channel: i.channel,
          subject: i.channel.toLowerCase() === 'email' ? (e.subject || i.name) : (i.subject || undefined),
          body: e.body, isActive: true,
          // preserve the attached image (UpdateTemplateDto would otherwise null it)
          mediaUrl: i.mediaUrl, mediaType: i.mediaType, mediaFileName: i.mediaFileName,
        } as any);
      }));
      toast.success(`Saved ${items.length} template(s)`);
      onSaved();
    } catch (err: any) { toast.error(err?.response?.data?.message || 'Save failed'); }
    finally { setSaving(false); }
  };

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4" onClick={(e) => e.target === e.currentTarget && onClose()}>
      <div className="bg-white rounded-2xl w-full max-w-3xl max-h-[92vh] flex flex-col shadow-2xl overflow-hidden">
        <div className="flex items-center gap-3 px-6 py-4 text-white" style={{ background: 'linear-gradient(120deg,#4f46e5,#7c3aed,#db2777)' }}>
          <div className="w-9 h-9 rounded-xl bg-white/18 grid place-items-center text-lg">✨</div>
          <div><b className="text-base">Edit set — {set.name}</b><small className="block text-white/80 text-xs">Edit all channels together, then Save all</small></div>
          <button onClick={onClose} className="ml-auto w-8 h-8 rounded-lg bg-white/20 grid place-items-center"><X className="w-5 h-5" /></button>
        </div>

        <div className="p-4 overflow-auto space-y-3.5">
          {hasIG && hasFB && (
            <label className="flex items-center gap-2.5 text-sm text-gray-600 px-1 cursor-pointer">
              <span onClick={() => setSync((v) => !v)} className={`relative w-11 h-6 rounded-full transition-all ${sync ? 'bg-gradient-to-r from-indigo-500 to-purple-600' : 'bg-gray-300'}`}>
                <span className={`absolute top-0.5 w-5 h-5 rounded-full bg-white shadow transition-all ${sync ? 'left-[22px]' : 'left-0.5'}`} />
              </span>
              Keep Instagram &amp; Facebook in sync (editing one updates both)
            </label>
          )}
          {items.map((i) => {
            const ch = i.channel.toLowerCase();
            const meta = CHANNEL_META[ch] || CHANNEL_META.email;
            const shared = sync && (ch === 'instagram' || ch === 'facebook') && hasIG && hasFB;
            return (
              <div key={i.id} className="border border-gray-200 rounded-xl overflow-hidden">
                <div className={`flex items-center gap-2 px-3 py-2 text-white bg-gradient-to-r ${meta.head}`}>
                  <ChannelLogo channel={ch} size={15} invert /><b className="text-[13px]">{meta.label}</b>
                  {i.mediaUrl && <span className="text-[10px] bg-white/22 px-2 py-0.5 rounded-full">🖼 image</span>}
                  {shared && <span className="ml-auto text-[10px] bg-white/22 px-2 py-0.5 rounded-full">🔗 shared</span>}
                </div>
                {ch === 'email' && (
                  <input value={edits[i.id].subject} onChange={(e) => setSubject(i.id, e.target.value)} placeholder="Subject"
                    className="w-full px-3 py-2 border-b border-gray-100 text-sm outline-none" />
                )}
                <textarea value={edits[i.id].body} onChange={(e) => setBody(i.id, ch, e.target.value)} rows={4}
                  className="w-full px-3 py-2.5 text-[13px] outline-none resize-y" />
              </div>
            );
          })}
        </div>

        <div className="flex items-center gap-3 px-6 py-4 border-t border-gray-100">
          <span className="text-[11.5px] text-gray-400 mr-auto">Saving updates each template (the attached image is kept).</span>
          <button onClick={onClose} className="px-4 py-2 text-gray-600 hover:bg-gray-100 rounded-lg text-sm">Cancel</button>
          <button onClick={saveAll} disabled={saving} className="px-5 py-2 bg-gradient-to-r from-indigo-600 to-purple-600 text-white rounded-lg disabled:opacity-50 font-semibold text-sm inline-flex items-center gap-2">
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />} Save all ({items.length})
          </button>
        </div>
      </div>
    </div>
  );
}