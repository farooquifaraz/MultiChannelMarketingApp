import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2, Edit2, Eye, Mail, MessageCircle, Smartphone, FileText, X, Share2, Lock, Sparkles, Globe2, Users as UsersIcon, User as UserIcon, Check } from 'lucide-react';
import { templateApi } from '../../api/templateApi';
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
  const [preview, setPreview] = useState<string | null>(null);
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

  const handlePreview = async (id: string) => {
    try {
      const res = await templateApi.preview(id, {
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
      setPreviewMode('rendered');
      setPreview(res.data);
    } catch { /* handled */ }
  };

  const list = templates?.data || [];
  const ChannelIcons: Record<string, any> = { email: Mail, whatsapp: MessageCircle, sms: Smartphone, instagram: MessageCircle, facebook: MessageCircle };

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

      {/* Preview Modal */}
      {preview && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-2xl w-full max-w-3xl max-h-[90vh] flex flex-col shadow-2xl">
            {/* Header */}
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100">
              <div>
                <h3 className="text-lg font-semibold text-gray-900">Email Preview</h3>
                <p className="text-xs text-gray-500 mt-0.5">Sample data — John Doe</p>
              </div>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => setPreviewMode((m) => (m === 'rendered' ? 'code' : 'rendered'))}
                  className="px-3 py-1.5 text-xs font-medium bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-lg transition-colors"
                  title="Toggle rendered / code view"
                >
                  {previewMode === 'rendered' ? '<> View Code' : '🖼️ View Rendered'}
                </button>
                <button onClick={() => setPreview(null)} className="p-1.5 hover:bg-gray-100 rounded-lg">
                  <X className="w-5 h-5" />
                </button>
              </div>
            </div>

            {/* Body */}
            <div className="flex-1 overflow-hidden p-4 bg-gray-100">
              {previewMode === 'rendered' ? (
                <iframe
                  srcDoc={preview}
                  title="Email Preview"
                  className="w-full h-full min-h-[500px] bg-white rounded-xl border border-gray-200 shadow-inner"
                  sandbox=""
                />
              ) : (
                <pre className="w-full h-full min-h-[500px] overflow-auto bg-gray-900 text-green-300 text-xs p-4 rounded-xl whitespace-pre-wrap font-mono leading-relaxed">
                  {preview}
                </pre>
              )}
            </div>
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
          {list.map((t: any) => {
            const Icon = ChannelIcons[t.channel?.toLowerCase()] || ChannelIcons[t.channel] || Mail;
            // A template is "shared FROM admin" (read-only for current user) if isShared AND userId !== current user
            const isOwned = !t.userId || t.userId === user?.id;
            const isSharedFromAdmin = t.isShared && !isOwned;
            return (
              <div
                key={t.id}
                className={`bg-white rounded-2xl p-5 shadow-sm border transition-shadow hover:shadow-md ${
                  isSharedFromAdmin ? 'border-amber-200 ring-1 ring-amber-100' : 'border-gray-100'
                }`}
              >
                <div className="flex items-start justify-between mb-3">
                  <div className="flex items-center gap-2">
                    <div className={`w-8 h-8 rounded-lg flex items-center justify-center ${getChannelColor(t.channel)}`}>
                      <Icon className="w-4 h-4" />
                    </div>
                    <div>
                      <p className="font-semibold text-gray-900 text-sm flex items-center gap-1.5">
                        {t.name}
                        {isSharedFromAdmin && (
                          <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 bg-amber-100 text-amber-700 text-[9px] font-semibold rounded-full uppercase tracking-wider">
                            <Sparkles className="w-2.5 h-2.5" /> Shared by admin
                          </span>
                        )}
                        {isOwned && t.isShared && (() => {
                          const scope = (t.shareScope || 'global').toLowerCase();
                          const cfg = scope === 'global'
                            ? { Icon: Globe2, label: 'Global', cls: 'bg-emerald-100 text-emerald-700' }
                            : scope === 'groups'
                            ? { Icon: UsersIcon, label: `${(t.sharedWithGroupIds || []).length} group(s)`, cls: 'bg-blue-100 text-blue-700' }
                            : { Icon: UserIcon, label: `${(t.sharedWithUserIds || []).length} user(s)`, cls: 'bg-purple-100 text-purple-700' };
                          return (
                            <span className={`inline-flex items-center gap-0.5 px-1.5 py-0.5 ${cfg.cls} text-[9px] font-semibold rounded-full uppercase tracking-wider`}>
                              <cfg.Icon className="w-2.5 h-2.5" /> {cfg.label}
                            </span>
                          );
                        })()}
                      </p>
                      <p className="text-xs text-gray-500">{t.channel}</p>
                    </div>
                  </div>
                </div>
                {t.subject && <p className="text-sm text-gray-600 mb-2 font-medium line-clamp-1">{t.subject}</p>}
                <p className="text-sm text-gray-500 line-clamp-3 mb-4">
                  {/* Strip HTML tags + entities for a clean snippet */}
                  {t.body
                    .replace(/<style[^>]*>[\s\S]*?<\/style>/gi, '')
                    .replace(/<script[^>]*>[\s\S]*?<\/script>/gi, '')
                    .replace(/<[^>]+>/g, ' ')
                    .replace(/&nbsp;/g, ' ')
                    .replace(/&[a-z]+;/g, ' ')
                    .replace(/\s+/g, ' ')
                    .trim()
                    .slice(0, 180)}
                </p>
                <div className="flex items-center justify-between pt-3 border-t border-gray-100">
                  <span className="text-xs text-gray-400">{formatDate(t.updatedAt)}</span>
                  <div className="flex gap-1">
                    <button onClick={() => handlePreview(t.id)} className="p-1.5 text-gray-400 hover:text-primary-600 hover:bg-primary-50 rounded-lg" title="Preview"><Eye className="w-4 h-4" /></button>
                    {/* Share — admins only, only on their own templates */}
                    {isAdmin && isOwned && (
                      <button
                        onClick={() => setShareModal(t)}
                        className={`p-1.5 rounded-lg ${
                          t.isShared
                            ? 'text-emerald-600 bg-emerald-50 hover:bg-emerald-100'
                            : 'text-gray-400 hover:text-emerald-600 hover:bg-emerald-50'
                        }`}
                        title="Manage sharing"
                      >
                        {t.isShared ? <Share2 className="w-4 h-4" /> : <Lock className="w-4 h-4" />}
                      </button>
                    )}
                    {/* Edit & Delete only for owned templates */}
                    {isOwned && (
                      <>
                        <button onClick={() => startEdit(t)} className="p-1.5 text-gray-400 hover:text-amber-600 hover:bg-amber-50 rounded-lg" title="Edit"><Edit2 className="w-4 h-4" /></button>
                        <button onClick={() => deleteMutation.mutate(t.id)} className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg" title="Delete"><Trash2 className="w-4 h-4" /></button>
                      </>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
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