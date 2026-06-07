import { useState, useEffect } from 'react';
import { Mail, Plus, Trash2, Edit2, Star, Users, Send, Loader2, X, Eye, EyeOff, CheckCircle2, Server, Sparkles, Copy, MessageSquare, RefreshCw } from 'lucide-react';
import { Navigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { smtpGroupsApi, type SmtpGroup, type CreateSmtpGroup, type UserAssignment, type WhatsAppTemplate } from '../../api/smtpGroupsApi';
import { useAuthStore } from '../../store/authStore';

const emptyGroup: CreateSmtpGroup = {
  name: '',
  description: '',
  isDefault: false,
  isActive: true,
  emailProvider: 'smtp',
  smtpPort: 587,
  smtpEnableSsl: true,
  smtpTimeout: 30000,
};

export default function SmtpGroupsPage() {
  const user = useAuthStore((s) => s.user);
  const isAdmin = user?.role?.toLowerCase() === 'admin';

  const [groups, setGroups] = useState<SmtpGroup[]>([]);
  const [assignments, setAssignments] = useState<UserAssignment[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [editId, setEditId] = useState<string | null>(null);
  const [form, setForm] = useState<CreateSmtpGroup>(emptyGroup);
  const [showPassword, setShowPassword] = useState(false);
  const [saving, setSaving] = useState(false);
  const [testingGroupId, setTestingGroupId] = useState<string | null>(null);
  const [testEmail, setTestEmail] = useState('');
  const [showAssignFor, setShowAssignFor] = useState<SmtpGroup | null>(null);

  // L2 — WhatsApp templates modal
  const [waFor, setWaFor] = useState<SmtpGroup | null>(null);
  const [waTemplates, setWaTemplates] = useState<WhatsAppTemplate[]>([]);
  const [waLoading, setWaLoading] = useState(false);
  const [waSyncing, setWaSyncing] = useState(false);

  const openWaTemplates = async (g: SmtpGroup) => {
    setWaFor(g);
    setWaTemplates([]);
    setWaLoading(true);
    try {
      const res: any = await smtpGroupsApi.listWhatsAppTemplates(g.id);
      setWaTemplates(res.data || []);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to load templates');
    } finally {
      setWaLoading(false);
    }
  };

  const syncWaTemplates = async () => {
    if (!waFor) return;
    setWaSyncing(true);
    try {
      const res: any = await smtpGroupsApi.syncWhatsAppTemplates(waFor.id);
      toast.success(res?.message || res?.data?.message || 'Synced');
      const list: any = await smtpGroupsApi.listWhatsAppTemplates(waFor.id);
      setWaTemplates(list.data || []);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Sync failed — check WhatsApp credentials on this group');
    } finally {
      setWaSyncing(false);
    }
  };

  // Admin-only page — block at the route boundary
  if (user && !isAdmin) return <Navigate to="/dashboard" replace />;

  const loadAll = async () => {
    try {
      const [groupsRes, usersRes]: any[] = await Promise.all([
        smtpGroupsApi.list(),
        smtpGroupsApi.userAssignments(),
      ]);
      setGroups(groupsRes.data || []);
      setAssignments(usersRes.data || []);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to load groups');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadAll(); }, []);

  // M1 — when editing an existing group, this is the saved version (from API).
  // It carries the *Masked credential fields so we can show "✅ Currently configured: xkey…9gU1eB"
  // without ever leaking the real secret back into a form field.
  const editingGroup: SmtpGroup | null = editId ? (groups.find(g => g.id === editId) ?? null) : null;

  /**
   * M1 — render a "credential configured" hint below sensitive inputs. Three cases:
   *   (a) creating a brand-new group → no hint (the input itself is the only state)
   *   (b) editing, secret IS set → green ✅ pill with masked tail + "leave blank to keep" copy
   *   (c) editing, secret is NOT set → amber ⚠ pill prompting the admin to fill it
   * Backend already preserves the existing secret when the field is blank, so this is
   * purely a UX clarification of the long-standing behavior.
   */
  const credentialIndicator = (masked: string | null | undefined, label: string) => {
    if (!editId) return null; // case (a)
    if (masked) {
      return (
        <p className="mt-1.5 flex items-center gap-1.5 text-xs text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-md px-2 py-1">
          <CheckCircle2 className="w-3.5 h-3.5 flex-shrink-0" />
          <span><strong>{label} configured:</strong> <code className="font-mono">{masked}</code> — leave blank to keep, or paste a new one to replace.</span>
        </p>
      );
    }
    return (
      <p className="mt-1.5 flex items-center gap-1.5 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-md px-2 py-1">
        <span className="flex-shrink-0">⚠</span>
        <span><strong>{label} not set</strong> — paste a key to enable this group, otherwise sends through it will fail.</span>
      </p>
    );
  };

  const resetForm = () => {
    setForm(emptyGroup);
    setEditId(null);
    setShowForm(false);
    setShowPassword(false);
  };

  const startEdit = (g: SmtpGroup) => {
    setEditId(g.id);
    setForm({
      name: g.name,
      description: g.description ?? '',
      isDefault: g.isDefault,
      isActive: g.isActive,
      emailProvider: g.emailProvider,
      smtpHost: g.smtpHost ?? '',
      smtpPort: g.smtpPort,
      smtpUsername: g.smtpUsername ?? '',
      smtpPassword: '',
      smtpEnableSsl: g.smtpEnableSsl,
      smtpTimeout: g.smtpTimeout,
      mailgunDomain: g.mailgunDomain ?? '',
      fromEmail: g.fromEmail ?? '',
      fromName: g.fromName ?? '',
      signatureDesignation: g.signatureDesignation ?? '',
      signaturePhone: g.signaturePhone ?? '',
      companyWebsite: g.companyWebsite ?? '',
      signatureImageUrl: g.signatureImageUrl ?? '',
      delayBetweenMessagesMs: g.delayBetweenMessagesMs ?? null,
      maxMessagesPerMinute: g.maxMessagesPerMinute ?? null,
      // Day 7 G8
      sendGridWebhookSecret: null,
      brevoWebhookSecret: null,
      mailgunWebhookSecret: null,
      enableInboxPolling: g.enableInboxPolling ?? false,
      imapHost: g.imapHost ?? '',
      imapPort: g.imapPort ?? 993,
      imapEnableSsl: g.imapEnableSsl ?? true,
      imapUsername: g.imapUsername ?? '',
      imapPassword: null,
      imapFolder: g.imapFolder ?? 'INBOX',
      inboxPollingIntervalMinutes: g.inboxPollingIntervalMinutes ?? null,
      defaultInboxOwnerUserId: g.defaultInboxOwnerUserId ?? null,
    });
    setShowForm(true);
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    try {
      if (editId) await smtpGroupsApi.update(editId, form);
      else await smtpGroupsApi.create(form);
      toast.success(editId ? 'Group updated' : 'Group created');
      resetForm();
      loadAll();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Save failed');
    } finally {
      setSaving(false);
    }
  };

  const removeGroup = async (g: SmtpGroup) => {
    if (!confirm(`Delete "${g.name}"? Users assigned to this group must be reassigned first.`)) return;
    try {
      await smtpGroupsApi.delete(g.id);
      toast.success('Group deleted');
      loadAll();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Delete failed');
    }
  };

  const makeDefault = async (g: SmtpGroup) => {
    try {
      await smtpGroupsApi.setDefault(g.id);
      toast.success(`"${g.name}" is now the default group`);
      loadAll();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed');
    }
  };

  const [cloningId, setCloningId] = useState<string | null>(null);
  const cloneGroup = async (g: SmtpGroup) => {
    setCloningId(g.id);
    try {
      const res: any = await smtpGroupsApi.clone(g.id);
      const clone = res.data as SmtpGroup;
      toast.success(`Cloned to "${clone.name}". Opening editor to set the new From email & credentials…`, { duration: 5000 });
      await loadAll();
      // Open the clone in the edit form so the admin can immediately tweak From + creds.
      startEdit(clone);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Clone failed');
    } finally { setCloningId(null); }
  };

  const runTest = async (g: SmtpGroup) => {
    if (!testEmail) { toast.error('Enter a test email address'); return; }
    setTestingGroupId(g.id);
    try {
      await smtpGroupsApi.test(g.id, testEmail);
      toast.success(`Test email sent to ${testEmail}`);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Test failed', { duration: 6000 });
    } finally { setTestingGroupId(null); }
  };

  const inputCls = "w-full px-3 py-2 border border-gray-200 rounded-lg focus:ring-2 focus:ring-primary-500 outline-none text-sm bg-gray-50 focus:bg-white";
  const labelCls = "block text-xs font-medium text-gray-700 mb-1";

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <Loader2 className="w-8 h-8 animate-spin text-primary-600" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-3">
          <div className="p-2.5 bg-gradient-to-br from-primary-500 to-purple-600 rounded-xl">
            <Server className="w-6 h-6 text-white" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
              SMTP Groups
              <span className="px-2 py-0.5 bg-red-100 text-red-700 text-[10px] font-semibold rounded-full uppercase tracking-wide">Admin</span>
            </h1>
            <p className="text-gray-500 text-sm mt-0.5">Bundle provider credentials + signature into reusable groups. Assign users to a group — their campaigns route through it.</p>
          </div>
        </div>
        <button
          onClick={() => { resetForm(); setShowForm(true); }}
          className="flex items-center gap-2 px-4 py-2.5 bg-primary-600 text-white rounded-xl hover:bg-primary-700 font-medium shadow-sm"
        >
          <Plus className="w-4 h-4" /> New Group
        </button>
      </div>

      {/* Test email widget */}
      <div className="bg-white rounded-xl border border-gray-100 p-4 flex items-center gap-3">
        <Sparkles className="w-4 h-4 text-primary-500 flex-shrink-0" />
        <label className="text-xs text-gray-600 font-medium whitespace-nowrap">Test send to:</label>
        <input
          type="email"
          value={testEmail}
          onChange={e => setTestEmail(e.target.value)}
          placeholder="your-email@example.com"
          className={inputCls}
        />
      </div>

      {/* Form */}
      {showForm && (
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-6">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-lg font-semibold">{editId ? 'Edit Group' : 'Create SMTP Group'}</h3>
            <button onClick={resetForm} className="p-1 hover:bg-gray-100 rounded-lg"><X className="w-5 h-5" /></button>
          </div>

          <form onSubmit={submit} className="space-y-5">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className={labelCls}>Group Name *</label>
                <input type="text" className={inputCls} placeholder="e.g., Marketing Team" value={form.name} onChange={e => setForm({...form, name: e.target.value})} required />
              </div>
              <div>
                <label className={labelCls}>Description</label>
                <input type="text" className={inputCls} placeholder="What is this group for?" value={form.description ?? ''} onChange={e => setForm({...form, description: e.target.value})} />
              </div>
            </div>

            <div className="flex items-center gap-4">
              <label className="flex items-center gap-2 cursor-pointer">
                <input type="checkbox" checked={form.isDefault} onChange={e => setForm({...form, isDefault: e.target.checked})} className="w-4 h-4 text-primary-600 rounded" />
                <span className="text-sm text-gray-700">Use as <strong>default</strong> group (for users without assignment)</span>
              </label>
              <label className="flex items-center gap-2 cursor-pointer">
                <input type="checkbox" checked={form.isActive} onChange={e => setForm({...form, isActive: e.target.checked})} className="w-4 h-4 text-primary-600 rounded" />
                <span className="text-sm text-gray-700">Active</span>
              </label>
            </div>

            {/* Provider picker */}
            <div>
              <label className={labelCls}>Email Provider</label>
              <div className="grid grid-cols-4 gap-2">
                {['smtp', 'sendgrid', 'brevo', 'mailgun'].map(p => (
                  <button
                    key={p}
                    type="button"
                    onClick={() => setForm({...form, emailProvider: p})}
                    className={`p-2 rounded-lg border-2 text-sm font-medium transition-all ${
                      form.emailProvider === p ? 'border-primary-500 bg-primary-50 text-primary-700' : 'border-gray-100 hover:border-gray-200 text-gray-600'
                    }`}
                  >
                    {p.toUpperCase()}
                  </button>
                ))}
              </div>
            </div>

            {/* Provider-specific fields */}
            {form.emailProvider === 'smtp' && (
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className={labelCls}>SMTP Host *</label>
                  <input type="text" className={inputCls} placeholder="smtp.gmail.com" value={form.smtpHost ?? ''} onChange={e => setForm({...form, smtpHost: e.target.value})} />
                </div>
                <div>
                  <label className={labelCls}>Port *</label>
                  <input type="number" className={inputCls} value={form.smtpPort} onChange={e => setForm({...form, smtpPort: parseInt(e.target.value) || 587})} />
                </div>
                <div>
                  <label className={labelCls}>Username *</label>
                  <input type="text" className={inputCls} value={form.smtpUsername ?? ''} onChange={e => setForm({...form, smtpUsername: e.target.value})} />
                </div>
                <div>
                  <label className={labelCls}>Password {editId && <span className="text-gray-400 font-normal">(leave blank to keep)</span>}</label>
                  <div className="relative">
                    <input type={showPassword ? 'text' : 'password'} className={inputCls + ' pr-10'} value={form.smtpPassword ?? ''} onChange={e => setForm({...form, smtpPassword: e.target.value})} />
                    <button type="button" onClick={() => setShowPassword(!showPassword)} className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400">
                      {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                  {/* M1 — boolean indicator from backend (SmtpPasswordSet); we don't have the masked value for SMTP */}
                  {editId && editingGroup?.smtpPasswordSet
                    ? credentialIndicator('••••••••', 'SMTP password')
                    : credentialIndicator(null, 'SMTP password')}
                </div>
                <label className="flex items-center gap-2 col-span-2">
                  <input type="checkbox" checked={form.smtpEnableSsl} onChange={e => setForm({...form, smtpEnableSsl: e.target.checked})} className="w-4 h-4 text-primary-600 rounded" />
                  <span className="text-sm text-gray-700">Enable SSL/TLS (recommended)</span>
                </label>
              </div>
            )}

            {form.emailProvider === 'sendgrid' && (
              <div>
                <label className={labelCls}>SendGrid API Key * {editId && <span className="text-gray-400 font-normal">(leave blank to keep)</span>}</label>
                <input type="password" className={inputCls} placeholder="SG.xxxxx..." value={form.sendGridApiKey ?? ''} onChange={e => setForm({...form, sendGridApiKey: e.target.value})} />
                {credentialIndicator(editingGroup?.sendGridApiKeyMasked, 'SendGrid API key')}
              </div>
            )}

            {form.emailProvider === 'brevo' && (
              <div>
                <label className={labelCls}>Brevo API Key * {editId && <span className="text-gray-400 font-normal">(leave blank to keep)</span>}</label>
                <input type="password" className={inputCls} placeholder="xkeysib-xxxxx..." value={form.brevoApiKey ?? ''} onChange={e => setForm({...form, brevoApiKey: e.target.value})} />
                {credentialIndicator(editingGroup?.brevoApiKeyMasked, 'Brevo API key')}
              </div>
            )}

            {form.emailProvider === 'mailgun' && (
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className={labelCls}>Mailgun API Key * {editId && <span className="text-gray-400 font-normal">(leave blank to keep)</span>}</label>
                  <input type="password" className={inputCls} value={form.mailgunApiKey ?? ''} onChange={e => setForm({...form, mailgunApiKey: e.target.value})} />
                  {credentialIndicator(editingGroup?.mailgunApiKeyMasked, 'Mailgun API key')}
                </div>
                <div>
                  <label className={labelCls}>Mailgun Domain *</label>
                  <input type="text" className={inputCls} value={form.mailgunDomain ?? ''} onChange={e => setForm({...form, mailgunDomain: e.target.value})} />
                </div>
              </div>
            )}

            {/* From info — applies to all providers */}
            <div className="pt-4 border-t border-gray-100">
              <p className="text-sm font-semibold text-gray-900 mb-3">From Address (visible to recipients)</p>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className={labelCls}>From Email *</label>
                  <input type="email" className={inputCls} placeholder="sender@yourdomain.com" value={form.fromEmail ?? ''} onChange={e => setForm({...form, fromEmail: e.target.value})} />
                </div>
                <div>
                  <label className={labelCls}>From Name</label>
                  <input type="text" className={inputCls} placeholder="Marketing Team" value={form.fromName ?? ''} onChange={e => setForm({...form, fromName: e.target.value})} />
                </div>
              </div>
            </div>

            {/* Signature */}
            <div className="pt-4 border-t border-gray-100">
              <p className="text-sm font-semibold text-gray-900 mb-3">Signature (used in {`{{sender_*}}, {{company_*}}`} placeholders)</p>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className={labelCls}>Designation</label>
                  <input type="text" className={inputCls} placeholder="Marketing Manager" value={form.signatureDesignation ?? ''} onChange={e => setForm({...form, signatureDesignation: e.target.value})} />
                </div>
                <div>
                  <label className={labelCls}>Phone</label>
                  <input type="tel" className={inputCls} placeholder="+1 555 0100" value={form.signaturePhone ?? ''} onChange={e => setForm({...form, signaturePhone: e.target.value})} />
                </div>
                <div>
                  <label className={labelCls}>Company Website</label>
                  <input type="url" className={inputCls} placeholder="https://yourcompany.com" value={form.companyWebsite ?? ''} onChange={e => setForm({...form, companyWebsite: e.target.value})} />
                </div>
                <div>
                  <label className={labelCls}>Signature Image URL</label>
                  <input type="url" className={inputCls} placeholder="https://yourcdn.com/logo.png" value={form.signatureImageUrl ?? ''} onChange={e => setForm({...form, signatureImageUrl: e.target.value})} />
                </div>
              </div>
            </div>

            {/* === Per-group rate-limit overrides === */}
            <div className="bg-gradient-to-br from-amber-50 to-orange-50 rounded-xl p-5 border border-amber-100">
              <h3 className="text-sm font-semibold text-amber-900 mb-1">⏱️ Per-Group Rate Limits (optional)</h3>
              <p className="text-xs text-amber-700 mb-4">
                When set, these override the global Platform Settings for campaigns sent through this group.
                Useful for slow providers (Gmail SMTP) vs fast ones (SendGrid, Brevo). Leave blank to inherit global.
              </p>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className={labelCls}>Delay Between Messages (ms)</label>
                  <input
                    type="number"
                    min="0"
                    step="100"
                    className={inputCls}
                    placeholder="e.g. 1000 for Gmail, blank to inherit"
                    value={form.delayBetweenMessagesMs ?? ''}
                    onChange={e => setForm({
                      ...form,
                      delayBetweenMessagesMs: e.target.value === '' ? null : parseInt(e.target.value),
                    })}
                  />
                </div>
                <div>
                  <label className={labelCls}>Max Messages Per Minute</label>
                  <input
                    type="number"
                    min="0"
                    step="5"
                    className={inputCls}
                    placeholder="e.g. 30 for Gmail throttle, blank to inherit"
                    value={form.maxMessagesPerMinute ?? ''}
                    onChange={e => setForm({
                      ...form,
                      maxMessagesPerMinute: e.target.value === '' ? null : parseInt(e.target.value),
                    })}
                  />
                  <p className="text-xs text-amber-700 mt-1">0 = unlimited within this group.</p>
                </div>
              </div>
            </div>

            {/* === Day 7 G2 webhook secrets === */}
            <div className="bg-gradient-to-br from-blue-50 to-cyan-50 rounded-xl p-5 border border-blue-100">
              <h3 className="text-sm font-semibold text-blue-900 mb-1">🔔 Delivery Webhook Secrets (optional)</h3>
              <p className="text-xs text-blue-700 mb-4">
                Paste signing secrets from your provider dashboard. Webhook URL pattern:
                <code className="ml-1 px-1 bg-white text-[10px]">{`{PublicBaseUrl}/api/v1/webhooks/{provider}?smtpGroupId=${editId ?? '...'}`}</code>
              </p>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div>
                  <label className={labelCls}>SendGrid Webhook Secret</label>
                  <input
                    type="password" className={inputCls} placeholder="bearer token"
                    value={form.sendGridWebhookSecret ?? ''}
                    onChange={e => setForm({...form, sendGridWebhookSecret: e.target.value})}
                  />
                </div>
                <div>
                  <label className={labelCls}>Brevo Webhook Token</label>
                  <input
                    type="password" className={inputCls} placeholder="appended as ?token=..."
                    value={form.brevoWebhookSecret ?? ''}
                    onChange={e => setForm({...form, brevoWebhookSecret: e.target.value})}
                  />
                </div>
                <div>
                  <label className={labelCls}>Mailgun Signing Key</label>
                  <input
                    type="password" className={inputCls} placeholder="HMAC key"
                    value={form.mailgunWebhookSecret ?? ''}
                    onChange={e => setForm({...form, mailgunWebhookSecret: e.target.value})}
                  />
                </div>
              </div>
            </div>

            {/* === Day 7 G3 IMAP polling === */}
            <div className="bg-gradient-to-br from-purple-50 to-pink-50 rounded-xl p-5 border border-purple-100">
              <h3 className="text-sm font-semibold text-purple-900 mb-1">📬 Inbox Polling (IMAP)</h3>
              <p className="text-xs text-purple-700 mb-4">
                When enabled, replies pulled via IMAP appear in the App Inbox. Falls back to SMTP credentials if you leave IMAP fields blank.
              </p>
              <label className="flex items-center gap-2 p-3 bg-white rounded-lg cursor-pointer hover:bg-gray-50 mb-4">
                <input
                  type="checkbox" className="w-4 h-4"
                  checked={!!form.enableInboxPolling}
                  onChange={e => setForm({...form, enableInboxPolling: e.target.checked})}
                />
                <span className="text-sm font-medium text-gray-900">Enable inbox polling for this group</span>
              </label>
              {form.enableInboxPolling && (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className={labelCls}>IMAP Host</label>
                    <input type="text" className={inputCls} placeholder="imap.gmail.com (blank = reuse SMTP host)"
                      value={form.imapHost ?? ''} onChange={e => setForm({...form, imapHost: e.target.value})} />
                  </div>
                  <div>
                    <label className={labelCls}>IMAP Port</label>
                    <input type="number" className={inputCls} value={form.imapPort ?? 993}
                      onChange={e => setForm({...form, imapPort: parseInt(e.target.value) || 993})} />
                  </div>
                  <div>
                    <label className={labelCls}>IMAP Username</label>
                    <input type="text" className={inputCls} placeholder="blank = reuse SMTP username"
                      value={form.imapUsername ?? ''} onChange={e => setForm({...form, imapUsername: e.target.value})} />
                  </div>
                  <div>
                    <label className={labelCls}>IMAP Password</label>
                    <input type="password" className={inputCls} placeholder="blank = reuse SMTP password"
                      value={form.imapPassword ?? ''} onChange={e => setForm({...form, imapPassword: e.target.value})} />
                  </div>
                  <div>
                    <label className={labelCls}>Folder</label>
                    <input type="text" className={inputCls} value={form.imapFolder ?? 'INBOX'}
                      onChange={e => setForm({...form, imapFolder: e.target.value})} />
                  </div>
                  <div>
                    <label className={labelCls}>Override poll interval (minutes)</label>
                    <input type="number" min="1" className={inputCls} placeholder="blank = use global cron"
                      value={form.inboxPollingIntervalMinutes ?? ''}
                      onChange={e => setForm({...form, inboxPollingIntervalMinutes: e.target.value === '' ? null : parseInt(e.target.value)})} />
                  </div>
                  <label className="flex items-center gap-2 col-span-full">
                    <input type="checkbox" checked={!!form.imapEnableSsl} onChange={e => setForm({...form, imapEnableSsl: e.target.checked})} className="w-4 h-4" />
                    <span className="text-sm text-gray-700">Use SSL/TLS</span>
                  </label>
                  <p className="text-xs text-amber-800 bg-amber-50 p-2 rounded col-span-full">
                    💡 <strong>Catch-all owner:</strong> when an inbound reply doesn't match any campaign or contact, it goes to the
                    group creator's inbox. To route it elsewhere, ask your admin to set <code>DefaultInboxOwnerUserId</code> via API.
                  </p>
                </div>
              )}
            </div>

            <div className="flex justify-end gap-3 pt-4 border-t border-gray-100">
              <button type="button" onClick={resetForm} className="px-4 py-2 text-gray-600 hover:bg-gray-100 rounded-lg">Cancel</button>
              <button type="submit" disabled={saving} className="px-5 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:opacity-50 font-medium">
                {saving ? 'Saving...' : (editId ? 'Update Group' : 'Create Group')}
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Group cards */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {groups.map(g => (
          <div key={g.id} className={`bg-white rounded-2xl border p-5 ${g.isDefault ? 'border-amber-200 ring-1 ring-amber-100' : 'border-gray-100'}`}>
            <div className="flex items-start justify-between mb-3">
              <div className="flex items-center gap-2.5">
                <div className={`w-10 h-10 rounded-lg flex items-center justify-center ${g.isActive ? 'bg-primary-100 text-primary-600' : 'bg-gray-100 text-gray-400'}`}>
                  <Mail className="w-5 h-5" />
                </div>
                <div>
                  <p className="font-semibold text-gray-900 flex items-center gap-1.5">
                    {g.name}
                    {g.isDefault && (
                      <span className="flex items-center gap-0.5 px-1.5 py-0.5 bg-amber-100 text-amber-700 text-[9px] font-semibold rounded-full uppercase tracking-wider">
                        <Star className="w-2.5 h-2.5" /> Default
                      </span>
                    )}
                    {!g.isActive && (
                      <span className="px-1.5 py-0.5 bg-gray-100 text-gray-500 text-[9px] font-semibold rounded-full uppercase">Inactive</span>
                    )}
                  </p>
                  <p className="text-xs text-gray-500">{g.description || `Provider: ${g.emailProvider.toUpperCase()}`}</p>
                </div>
              </div>
              <div className="text-right">
                <p className="text-xs text-gray-500">Users</p>
                <p className="text-xl font-bold text-primary-600">{g.assignedUserCount}</p>
              </div>
            </div>

            <div className="text-xs text-gray-600 space-y-1 pb-3 mb-3 border-b border-gray-100">
              <p><strong>From:</strong> {g.fromName ? `${g.fromName} <${g.fromEmail}>` : g.fromEmail || '—'}</p>
              <p><strong>Provider:</strong> {g.emailProvider.toUpperCase()}{g.smtpHost ? ` (${g.smtpHost}:${g.smtpPort})` : ''}</p>
              {g.signatureDesignation && <p><strong>Signature:</strong> {g.signatureDesignation}</p>}
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <button onClick={() => setShowAssignFor(g)} className="flex items-center gap-1 px-2.5 py-1.5 text-xs bg-primary-50 text-primary-700 rounded-lg hover:bg-primary-100">
                <Users className="w-3 h-3" /> Assign Users
              </button>
              <button onClick={() => runTest(g)} disabled={testingGroupId === g.id} className="flex items-center gap-1 px-2.5 py-1.5 text-xs bg-blue-50 text-blue-700 rounded-lg hover:bg-blue-100 disabled:opacity-50">
                {testingGroupId === g.id ? <Loader2 className="w-3 h-3 animate-spin" /> : <Send className="w-3 h-3" />}
                Test Send
              </button>
              <button onClick={() => openWaTemplates(g)} className="flex items-center gap-1 px-2.5 py-1.5 text-xs bg-green-50 text-green-700 rounded-lg hover:bg-green-100" title="WhatsApp approved templates (Meta Business)">
                <MessageSquare className="w-3 h-3" /> WA Templates
              </button>
              {!g.isDefault && (
                <button onClick={() => makeDefault(g)} className="flex items-center gap-1 px-2.5 py-1.5 text-xs bg-amber-50 text-amber-700 rounded-lg hover:bg-amber-100">
                  <Star className="w-3 h-3" /> Make Default
                </button>
              )}
              <button onClick={() => startEdit(g)} className="flex items-center gap-1 px-2.5 py-1.5 text-xs bg-gray-50 text-gray-700 rounded-lg hover:bg-gray-100">
                <Edit2 className="w-3 h-3" /> Edit
              </button>
              <button onClick={() => cloneGroup(g)} disabled={cloningId === g.id} className="flex items-center gap-1 px-2.5 py-1.5 text-xs bg-violet-50 text-violet-700 rounded-lg hover:bg-violet-100 disabled:opacity-50" title="Duplicate this group, then tweak the From email & credentials">
                {cloningId === g.id ? <Loader2 className="w-3 h-3 animate-spin" /> : <Copy className="w-3 h-3" />} Clone
              </button>
              {!g.isDefault && (
                <button onClick={() => removeGroup(g)} className="flex items-center gap-1 px-2.5 py-1.5 text-xs bg-red-50 text-red-700 rounded-lg hover:bg-red-100">
                  <Trash2 className="w-3 h-3" /> Delete
                </button>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* Assign modal */}
      {showAssignFor && (
        <AssignUsersModal
          group={showAssignFor}
          allAssignments={assignments}
          onClose={() => { setShowAssignFor(null); loadAll(); }}
        />
      )}

      {/* L2 — WhatsApp templates modal */}
      {waFor && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4" onClick={() => setWaFor(null)}>
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-2xl max-h-[85vh] overflow-hidden flex flex-col" onClick={e => e.stopPropagation()}>
            <div className="flex items-center justify-between p-4 border-b border-gray-100">
              <div>
                <h3 className="font-semibold text-gray-900 flex items-center gap-2">
                  <MessageSquare className="w-4 h-4 text-green-600" /> WhatsApp Templates — {waFor.name}
                </h3>
                <p className="text-xs text-gray-500 mt-0.5">Approved templates synced from Meta Business Manager</p>
              </div>
              <div className="flex items-center gap-2">
                <button onClick={syncWaTemplates} disabled={waSyncing}
                        className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50">
                  {waSyncing ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <RefreshCw className="w-3.5 h-3.5" />}
                  {waSyncing ? 'Syncing…' : 'Sync from Meta'}
                </button>
                <button onClick={() => setWaFor(null)} className="p-1 hover:bg-gray-100 rounded-lg"><X className="w-5 h-5" /></button>
              </div>
            </div>
            <div className="p-4 overflow-y-auto">
              {waLoading ? (
                <div className="flex items-center justify-center py-10 text-gray-400"><Loader2 className="w-5 h-5 animate-spin" /></div>
              ) : waTemplates.length === 0 ? (
                <div className="text-center py-10 text-sm text-gray-500">
                  <MessageSquare className="w-8 h-8 mx-auto mb-2 text-gray-300" />
                  No templates cached yet.<br />
                  Click <strong>Sync from Meta</strong> to pull approved templates (needs WhatsApp Business Account ID + access token on this group).
                </div>
              ) : (
                <div className="space-y-2">
                  {waTemplates.map(t => (
                    <div key={t.id} className="border border-gray-100 rounded-lg p-3">
                      <div className="flex items-center justify-between gap-2 flex-wrap">
                        <div className="flex items-center gap-2">
                          <span className="font-medium text-sm text-gray-900">{t.name}</span>
                          <span className="text-xs px-1.5 py-0.5 rounded bg-gray-100 text-gray-600">{t.language}</span>
                          <span className="text-xs px-1.5 py-0.5 rounded bg-primary-50 text-primary-600">{t.category}</span>
                          {t.headerType && <span className="text-xs px-1.5 py-0.5 rounded bg-purple-50 text-purple-600">{t.headerType}</span>}
                        </div>
                        <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${
                          t.status === 'APPROVED' ? 'bg-emerald-100 text-emerald-700'
                          : t.status === 'REJECTED' ? 'bg-red-100 text-red-700'
                          : 'bg-amber-100 text-amber-700'}`}>{t.status}</span>
                      </div>
                      {t.bodyText && <p className="text-xs text-gray-600 mt-1.5 whitespace-pre-wrap">{t.bodyText}</p>}
                      {t.variableCount > 0 && <p className="text-[11px] text-gray-400 mt-1">{t.variableCount} variable(s): {'{{1}}'} … {`{{${t.variableCount}}}`}</p>}
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function AssignUsersModal({ group, allAssignments, onClose }: {
  group: SmtpGroup;
  allAssignments: UserAssignment[];
  onClose: () => void;
}) {
  // Admins manage email infrastructure — they don't send campaigns themselves,
  // so they shouldn't appear in the assignable user list.
  const assignableUsers = allAssignments.filter(u => u.role?.toLowerCase() !== 'admin');
  const [selected, setSelected] = useState<Set<string>>(
    new Set(assignableUsers.filter(u => u.smtpGroupId === group.id).map(u => u.userId))
  );
  const [saving, setSaving] = useState(false);

  const toggle = (id: string) => {
    const next = new Set(selected);
    if (next.has(id)) next.delete(id); else next.add(id);
    setSelected(next);
  };

  const save = async () => {
    setSaving(true);
    try {
      // Compute deltas only over assignable (non-admin) users
      const originallySelected = new Set(assignableUsers.filter(u => u.smtpGroupId === group.id).map(u => u.userId));
      const newlySelected = Array.from(selected).filter(id => !originallySelected.has(id));
      const removed = Array.from(originallySelected).filter(id => !selected.has(id));

      if (newlySelected.length > 0) await smtpGroupsApi.assignUsers(group.id, newlySelected);
      if (removed.length > 0) await smtpGroupsApi.unassignUsers(removed);

      toast.success('Assignments updated');
      onClose();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed');
    } finally { setSaving(false); }
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl w-full max-w-lg max-h-[80vh] flex flex-col">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <div>
            <h3 className="font-semibold text-gray-900">Assign Users</h3>
            <p className="text-xs text-gray-500 mt-0.5">
              Group: <strong>{group.name}</strong>
              <span className="text-gray-400"> · {assignableUsers.length} assignable user{assignableUsers.length === 1 ? '' : 's'}</span>
            </p>
          </div>
          <button onClick={onClose} className="p-1 hover:bg-gray-100 rounded"><X className="w-5 h-5" /></button>
        </div>

        <div className="flex-1 overflow-y-auto p-4 space-y-1">
          {assignableUsers.length === 0 ? (
            <div className="text-center py-8 text-sm text-gray-400">
              No regular users available to assign yet.
            </div>
          ) : assignableUsers.map(u => {
            const isCurrentlyHere = u.smtpGroupId === group.id;
            const isElsewhere = u.smtpGroupId && u.smtpGroupId !== group.id;
            return (
              <label key={u.userId} className="flex items-center gap-3 p-2.5 rounded-lg hover:bg-gray-50 cursor-pointer">
                <input
                  type="checkbox"
                  checked={selected.has(u.userId)}
                  onChange={() => toggle(u.userId)}
                  className="w-4 h-4 text-primary-600 rounded"
                />
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-gray-900 flex items-center gap-1.5">
                    {u.fullName}
                    {isCurrentlyHere && <CheckCircle2 className="w-3 h-3 text-emerald-500" />}
                  </p>
                  <p className="text-xs text-gray-500 truncate">
                    {u.email}
                    {isElsewhere && <span className="ml-2 text-amber-600">(currently in: {u.smtpGroupName})</span>}
                  </p>
                </div>
              </label>
            );
          })}
        </div>

        <div className="p-4 border-t border-gray-100 flex justify-end gap-2">
          <button onClick={onClose} className="px-4 py-2 text-gray-600 hover:bg-gray-100 rounded-lg">Cancel</button>
          <button onClick={save} disabled={saving} className="px-5 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:opacity-50 font-medium">
            {saving ? 'Saving...' : 'Save Assignments'}
          </button>
        </div>
      </div>
    </div>
  );
}
