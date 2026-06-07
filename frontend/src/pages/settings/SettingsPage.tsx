import { useState, useEffect, useRef } from 'react';
import { Mail, MessageSquare, Smartphone, Bell, Save, FlaskConical, Eye, EyeOff, Settings, CheckCircle2, Loader2, PenTool, Briefcase, Phone, Globe, Image as ImageIcon, Upload, X, Shield, Zap, Clock, Sparkles, RotateCcw, Plug } from 'lucide-react';
import toast from 'react-hot-toast';
import IntegrationsPage from '../admin/IntegrationsPage';
import { settingsApi, adminApi, meApi, type CreateSmtpSettings, type SystemSettings as SystemSettingsType, type MySignature } from '../../api/settingsApi';
import { buildAvatarUrl } from '../../config/brand';
import { useAuthStore } from '../../store/authStore';

type TabType = 'email' | 'signature' | 'whatsapp' | 'sms' | 'notifications' | 'admin' | 'mysignature' | 'ai' | 'integrations';

export default function SettingsPage() {
  const [activeTab, setActiveTab] = useState<TabType>('notifications');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [testEmail, setTestEmail] = useState('');
  const [uploadingImage, setUploadingImage] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const user = useAuthStore((s) => s.user);
  const isAdmin = user?.role?.toLowerCase() === 'admin';
  // When user logs in as admin, default to Email SMTP tab (most important); otherwise notifications.
  useEffect(() => {
    setActiveTab(isAdmin ? 'email' : 'mysignature');
  }, [isAdmin]);

  // My Signature state
  const [mySig, setMySig] = useState<MySignature | null>(null);
  const [savingMySig, setSavingMySig] = useState(false);
  const [uploadingMySigImage, setUploadingMySigImage] = useState(false);
  const mySigFileRef = useRef<HTMLInputElement | null>(null);

  const loadMySignature = async () => {
    try {
      const res: any = await meApi.getSignature();
      if (res?.data) setMySig(res.data);
    } catch { /* silent */ }
  };
  useEffect(() => { loadMySignature(); }, []);

  const handleSaveMySig = async () => {
    if (!mySig) return;
    setSavingMySig(true);
    try {
      const res: any = await meApi.saveSignature({
        signatureDesignation: mySig.signatureDesignation,
        signaturePhone: mySig.signaturePhone,
        signatureImageUrl: mySig.signatureImageUrl,
      });
      if (res?.data) setMySig(res.data);
      // Broadcast so SendMessagePage refreshes preview
      window.dispatchEvent(new CustomEvent('signature-updated'));
      toast.success('Your signature was saved.');
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to save');
    } finally {
      setSavingMySig(false);
    }
  };

  const handleMySigImageUpload = async (file: File) => {
    if (file.size > 2 * 1024 * 1024) { toast.error('Max 2 MB'); return; }
    setUploadingMySigImage(true);
    try {
      const res: any = await meApi.uploadImage(file);
      const url = res?.data?.url;
      if (url && mySig) setMySig({ ...mySig, signatureImageUrl: url });
      toast.success('Image uploaded. Click Save to apply.');
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Upload failed');
    } finally {
      setUploadingMySigImage(false);
      if (mySigFileRef.current) mySigFileRef.current.value = '';
    }
  };
  const [systemSettings, setSystemSettings] = useState<SystemSettingsType | null>(null);
  const [savingAdmin, setSavingAdmin] = useState(false);

  // Suggest SMTP host from email domain (common providers)
  const suggestSmtpHost = (email: string): string => {
    const domain = email.split('@')[1]?.toLowerCase().trim() ?? '';
    const map: Record<string, string> = {
      'gmail.com': 'smtp.gmail.com',
      'googlemail.com': 'smtp.gmail.com',
      'outlook.com': 'smtp.office365.com',
      'hotmail.com': 'smtp.office365.com',
      'live.com': 'smtp.office365.com',
      'yahoo.com': 'smtp.mail.yahoo.com',
      'zoho.com': 'smtp.zoho.com',
      'icloud.com': 'smtp.mail.me.com',
    };
    if (map[domain]) return map[domain];
    // Hostinger / custom domains commonly use smtp.hostinger.com or smtp.{domain}
    if (domain && domain !== '') return `smtp.${domain}`;
    return '';
  };
  const [settings, setSettings] = useState<CreateSmtpSettings>({
    smtpHost: '',
    smtpPort: 587,
    smtpUsername: '',
    smtpPassword: '',
    smtpFromEmail: '',
    smtpFromName: '',
    smtpEnableSsl: true,
    smtpUseDefaultCredentials: false,
    smtpTimeout: 30000,
    whatsAppApiKey: '',
    whatsAppPhoneNumberId: '',
    whatsAppBusinessAccountId: '',
    smsApiKey: '',
    smsApiSecret: '',
    smsSenderNumber: '',
    emailProvider: 'smtp',
    sendGridApiKey: '',
    brevoApiKey: '',
    mailgunApiKey: '',
    mailgunDomain: '',
    notifyOnCampaignComplete: true,
    notifyOnMessageFailed: true,
    notificationEmail: '',
    signatureDesignation: '',
    signaturePhone: '',
    companyWebsite: '',
    signatureImageUrl: '',
  });

  useEffect(() => {
    loadSettings();
    loadSystemSettings();
  }, []);

  const loadSystemSettings = async () => {
    try {
      const res: any = await adminApi.getSystemSettings();
      if (res?.data) setSystemSettings(res.data);
    } catch { /* not critical */ }
  };

  const handleSaveSystemSettings = async () => {
    if (!systemSettings) return;
    setSavingAdmin(true);
    try {
      // Smart payload: only send aiApiKey if the admin actually typed a new value.
      // null = "no change" on the backend (preserves the existing stored key).
      const payload: any = { ...systemSettings };
      if (!payload.aiApiKey || payload.aiApiKey.trim() === '') {
        payload.aiApiKey = null; // signal "keep existing"
      }
      // Day 10: same "keep existing" semantics for the fallback key.
      if (!payload.aiFallbackApiKey || payload.aiFallbackApiKey.trim() === '') {
        payload.aiFallbackApiKey = null;
      }
      const res: any = await adminApi.updateSystemSettings(payload);
      // Backend returns the refreshed DTO with the new masked key — sync local state.
      const fresh = res?.data ?? res;
      if (fresh?.platformName !== undefined) {
        setSystemSettings({ ...fresh, aiApiKey: '' }); // clear raw-key field after save
      }
    } catch (err: any) {
      const msg = err?.response?.data?.message || 'Failed to save platform settings';
      toast.error(msg);
      throw err; // let caller handle
    } finally {
      setSavingAdmin(false);
    }
  };

  const loadSettings = async () => {
    try {
      const res: any = await settingsApi.getSmtpSettings();
      if (res?.data) {
        setSettings(prev => ({
          ...prev,
          smtpHost: res.data.smtpHost || '',
          smtpPort: res.data.smtpPort || 587,
          smtpUsername: res.data.smtpUsername || '',
          smtpFromEmail: res.data.smtpFromEmail || '',
          smtpFromName: res.data.smtpFromName || '',
          smtpEnableSsl: res.data.smtpEnableSsl ?? true,
          smtpUseDefaultCredentials: res.data.smtpUseDefaultCredentials ?? false,
          smtpTimeout: res.data.smtpTimeout || 30000,
          whatsAppApiKey: res.data.whatsAppApiKey || '',
          whatsAppPhoneNumberId: res.data.whatsAppPhoneNumberId || '',
          whatsAppBusinessAccountId: res.data.whatsAppBusinessAccountId || '',
          smsApiKey: res.data.smsApiKey || '',
          smsSenderNumber: res.data.smsSenderNumber || '',
          emailProvider: res.data.emailProvider || 'smtp',
          sendGridApiKey: res.data.sendGridApiKey || '',
          brevoApiKey: res.data.brevoApiKey || '',
          mailgunApiKey: res.data.mailgunApiKey || '',
          mailgunDomain: res.data.mailgunDomain || '',
          notifyOnCampaignComplete: res.data.notifyOnCampaignComplete ?? true,
          notifyOnMessageFailed: res.data.notifyOnMessageFailed ?? true,
          notificationEmail: res.data.notificationEmail || '',
          signatureDesignation: res.data.signatureDesignation || '',
          signaturePhone: res.data.signaturePhone || '',
          companyWebsite: res.data.companyWebsite || '',
          signatureImageUrl: res.data.signatureImageUrl || '',
        }));
      }
    } catch {
      // No settings yet, that's ok
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      await settingsApi.saveSmtpSettings(settings);
      // Broadcast that signature changed — listeners (e.g. SendMessagePage) will reload
      window.dispatchEvent(new CustomEvent('signature-updated'));
      toast.success('Settings saved successfully!');
    } catch {
      toast.error('Failed to save settings');
    } finally {
      setSaving(false);
    }
  };

  const handleImageUpload = async (file: File) => {
    // Client-side validation
    const allowedTypes = ['image/png', 'image/jpeg', 'image/jpg', 'image/gif', 'image/webp'];
    if (!allowedTypes.includes(file.type)) {
      toast.error('Unsupported file type. Please use PNG, JPG, GIF, or WEBP.');
      return;
    }
    if (file.size > 2 * 1024 * 1024) {
      toast.error('Image too large. Max size is 2 MB.');
      return;
    }
    setUploadingImage(true);
    try {
      const res: any = await settingsApi.uploadSignatureImage(file);
      const url = res?.data?.url;
      if (url) {
        setSettings(prev => ({ ...prev, signatureImageUrl: url }));
        toast.success('Image uploaded! Click Save Settings to apply.');
      } else {
        toast.error('Upload succeeded but no URL returned.');
      }
    } catch (err: any) {
      const msg = err?.response?.data?.message || err?.message || 'Upload failed';
      toast.error(msg);
    } finally {
      setUploadingImage(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  // Validate required fields for the chosen provider — returns map of field → error
  const validateProviderFields = (): Record<string, string> => {
    const errors: Record<string, string> = {};
    const provider = settings.emailProvider;

    if (!testEmail || !testEmail.includes('@')) {
      errors.testEmail = 'Enter a valid test email address';
    }

    if (provider === 'smtp') {
      if (!settings.smtpHost?.trim()) errors.smtpHost = 'SMTP host is required (e.g., smtp.hostinger.com, smtp.gmail.com)';
      if (!settings.smtpUsername?.trim()) errors.smtpUsername = 'SMTP username is required';
      if (!settings.smtpPassword?.trim()) errors.smtpPassword = 'SMTP password is required';
      if (!settings.smtpFromEmail?.trim()) errors.smtpFromEmail = 'From email is required';
    } else if (provider === 'sendgrid') {
      if (!settings.sendGridApiKey?.trim()) errors.sendGridApiKey = 'SendGrid API key is required';
      if (!settings.smtpFromEmail?.trim()) errors.smtpFromEmail = 'From email is required';
    } else if (provider === 'brevo') {
      if (!settings.brevoApiKey?.trim()) errors.brevoApiKey = 'Brevo API key is required';
      if (!settings.smtpFromEmail?.trim()) errors.smtpFromEmail = 'From email is required';
    } else if (provider === 'mailgun') {
      if (!settings.mailgunApiKey?.trim()) errors.mailgunApiKey = 'Mailgun API key is required';
      if (!settings.mailgunDomain?.trim()) errors.mailgunDomain = 'Mailgun domain is required';
      if (!settings.smtpFromEmail?.trim()) errors.smtpFromEmail = 'From email is required';
    }
    return errors;
  };

  const handleTestSmtp = async () => {
    // 1. Client-side validation FIRST — surface all errors inline so the user knows what's missing
    const errors = validateProviderFields();
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      const firstError = Object.values(errors)[0];
      toast.error(firstError, { duration: 5000 });
      // Scroll to first error field
      const firstFieldName = Object.keys(errors)[0];
      const el = document.querySelector(`[data-field="${firstFieldName}"]`) as HTMLElement | null;
      el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      el?.focus();
      return;
    }

    setTesting(true);
    try {
      // 2. Auto-save the current settings BEFORE testing — so the test uses what's in the form,
      //    not whatever was saved earlier. This fixes the common "I filled it in but didn't save" trap.
      await settingsApi.saveSmtpSettings(settings);

      // 3. Run the test
      const res: any = await settingsApi.testSmtpConnection(testEmail);
      if (res?.data?.success === false) {
        toast.error(res?.data?.message || 'Failed to send test email.', { duration: 6000 });
      } else {
        toast.success(`Test email sent successfully to ${testEmail}! Check the inbox.`, { duration: 6000 });
      }
    } catch (err: any) {
      const msg = err?.response?.data?.message || err?.response?.data?.Message || err?.message || 'Failed to send test email.';
      toast.error(msg, { duration: 7000 });
    } finally {
      setTesting(false);
    }
  };

  // Clear specific field error when user starts typing
  const clearFieldError = (field: string) => {
    if (fieldErrors[field]) {
      setFieldErrors(prev => { const { [field]: _, ...rest } = prev; return rest; });
    }
  };

  // Email-infrastructure tabs are admin-only (Email SMTP, Signature, WhatsApp, SMS).
  // EVERY user gets "My Signature" — personal designation/phone/image rendered into their emails.
  const tabs = [
    ...(isAdmin ? [
      { id: 'email' as TabType, label: 'Email SMTP', icon: Mail, color: 'text-blue-600' },
      { id: 'signature' as TabType, label: 'Org Signature', icon: PenTool, color: 'text-primary-600' },
      { id: 'whatsapp' as TabType, label: 'WhatsApp', icon: MessageSquare, color: 'text-green-600' },
      { id: 'sms' as TabType, label: 'SMS', icon: Smartphone, color: 'text-purple-600' },
    ] : []),
    { id: 'mysignature' as TabType, label: 'My Signature', icon: PenTool, color: 'text-pink-600' },
    { id: 'notifications' as TabType, label: 'Notifications', icon: Bell, color: 'text-amber-600' },
    ...(isAdmin ? [{ id: 'admin' as TabType, label: 'Admin', icon: Shield, color: 'text-red-600' }] : []),
    ...(isAdmin ? [{ id: 'integrations' as TabType, label: 'Integrations', icon: Plug, color: 'text-primary-600' }] : []),
    // AI Assistant tab removed — AI providers & keys are now managed in the Integrations tab
    // (per-provider vault). AI reply behaviour runs on sensible defaults.
  ];

  const inputClass = "w-full px-4 py-2.5 border border-gray-200 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all text-gray-700 bg-gray-50 focus:bg-white";
  const labelClass = "block text-sm font-medium text-gray-700 mb-1.5";

  if (loading) {
    return (
      <div className="flex items-center justify-center h-64">
        <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="p-2.5 bg-blue-100 rounded-xl">
            <Settings className="w-6 h-6 text-blue-600" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-gray-900">Channel Settings</h1>
            <p className="text-gray-500 text-sm">Configure your messaging channels and notification preferences</p>
          </div>
        </div>
        {/* Hide Save button for non-admins on tabs they don't own. Admin gets full save. */}
        {(isAdmin || activeTab === 'notifications') && (
          <button
            onClick={handleSave}
            disabled={saving}
            className="flex items-center gap-2 px-5 py-2.5 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-medium shadow-sm disabled:opacity-50"
          >
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            {saving ? 'Saving...' : 'Save Settings'}
          </button>
        )}
      </div>

      {/* Banner for non-admins explaining centralized email config */}
      {!isAdmin && (
        <div className="bg-gradient-to-r from-blue-50 to-primary-50 rounded-xl border border-blue-200 p-4 flex items-start gap-3">
          <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center flex-shrink-0">
            <Shield className="w-5 h-5 text-blue-600" />
          </div>
          <div className="flex-1">
            <p className="text-sm font-semibold text-blue-900">Email infrastructure is managed by your admin</p>
            <p className="text-xs text-blue-700 mt-1">
              You don't need to configure SMTP, sender info, or signature &mdash; your admin has set that up centrally.
              All your campaigns will use the admin's email provider and branded signature.
              Just compose, pick recipients, and send. ✉️
            </p>
            <p className="text-xs text-blue-600 mt-2">
              👇 You can still manage your own notification preferences below.
            </p>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-100">
        <div className="flex border-b border-gray-100">
          {tabs.map(tab => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`flex items-center gap-2 px-6 py-4 text-sm font-medium transition-all border-b-2 ${
                activeTab === tab.id
                  ? 'border-blue-600 text-blue-600 bg-blue-50/50'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:bg-gray-50'
              }`}
            >
              <tab.icon className={`w-4 h-4 ${activeTab === tab.id ? tab.color : ''}`} />
              {tab.label}
            </button>
          ))}
        </div>

        <div className="p-6">
          {/* Email SMTP Tab */}
          {activeTab === 'email' && (
            <div className="space-y-6">
              <div className="flex items-center gap-2 mb-4">
                <Mail className="w-5 h-5 text-blue-600" />
                <h3 className="text-lg font-semibold text-gray-900">SMTP Configuration</h3>
              </div>
              <p className="text-sm text-gray-500 -mt-4 mb-6">Configure your email server settings to send campaigns via your own email account.</p>

              {/* Email Provider Selector */}
              <div className="mb-6">
                <label className={labelClass}>Email Provider</label>
                <div className="grid grid-cols-4 gap-2">
                  {[
                    { id: 'smtp', label: 'SMTP', desc: 'Gmail, Hostinger, etc.' },
                    { id: 'sendgrid', label: 'SendGrid', desc: '100/day free' },
                    { id: 'brevo', label: 'Brevo', desc: '300/day free' },
                    { id: 'mailgun', label: 'Mailgun', desc: '1000/mo free' },
                  ].map(p => (
                    <button
                      key={p.id}
                      onClick={() => setSettings({...settings, emailProvider: p.id})}
                      className={`p-3 rounded-lg border-2 text-left transition-all ${
                        settings.emailProvider === p.id
                          ? 'border-blue-500 bg-blue-50'
                          : 'border-gray-100 hover:border-gray-200'
                      }`}
                    >
                      <p className={`text-sm font-semibold ${settings.emailProvider === p.id ? 'text-blue-700' : 'text-gray-700'}`}>{p.label}</p>
                      <p className="text-[10px] text-gray-400">{p.desc}</p>
                    </button>
                  ))}
                </div>
              </div>

              {/* SMTP fields */}
              {settings.emailProvider === 'smtp' && (<><div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                <div>
                  <label className={labelClass}>SMTP Host *</label>
                  <input
                    type="text"
                    data-field="smtpHost"
                    className={inputClass + (fieldErrors.smtpHost ? ' border-red-400 ring-2 ring-red-100' : '')}
                    placeholder="smtp.hostinger.com"
                    value={settings.smtpHost}
                    onChange={e => { setSettings({...settings, smtpHost: e.target.value}); clearFieldError('smtpHost'); }}
                    onBlur={() => {
                      // Auto-suggest if empty and we have username/from
                      if (!settings.smtpHost?.trim()) {
                        const sugg = suggestSmtpHost(settings.smtpUsername || settings.smtpFromEmail || '');
                        if (sugg) setSettings(s => ({...s, smtpHost: sugg}));
                      }
                    }}
                  />
                  {fieldErrors.smtpHost && <p className="text-xs text-red-600 mt-1">{fieldErrors.smtpHost}</p>}
                  {!fieldErrors.smtpHost && !settings.smtpHost && (settings.smtpUsername || settings.smtpFromEmail) && (
                    <p className="text-xs text-blue-600 mt-1">
                      💡 Suggested: <button type="button" onClick={() => setSettings({...settings, smtpHost: suggestSmtpHost(settings.smtpUsername || settings.smtpFromEmail || '')})} className="underline font-medium">{suggestSmtpHost(settings.smtpUsername || settings.smtpFromEmail || '') || '—'}</button>
                    </p>
                  )}
                </div>
                <div>
                  <label className={labelClass}>SMTP Port *</label>
                  <input type="number" className={inputClass} placeholder="587" value={settings.smtpPort} onChange={e => setSettings({...settings, smtpPort: parseInt(e.target.value) || 587})} />
                  <p className="text-xs text-gray-400 mt-1">Common: 587 (TLS), 465 (SSL), 25 (none)</p>
                </div>
                <div>
                  <label className={labelClass}>Username / Email *</label>
                  <input
                    type="text"
                    data-field="smtpUsername"
                    className={inputClass + (fieldErrors.smtpUsername ? ' border-red-400 ring-2 ring-red-100' : '')}
                    placeholder="students@izylrn.com"
                    value={settings.smtpUsername}
                    onChange={e => { setSettings({...settings, smtpUsername: e.target.value}); clearFieldError('smtpUsername'); }}
                  />
                  {fieldErrors.smtpUsername && <p className="text-xs text-red-600 mt-1">{fieldErrors.smtpUsername}</p>}
                </div>
                <div>
                  <label className={labelClass}>Password *</label>
                  <div className="relative">
                    <input
                      type={showPassword ? 'text' : 'password'}
                      data-field="smtpPassword"
                      className={inputClass + ' pr-10' + (fieldErrors.smtpPassword ? ' border-red-400 ring-2 ring-red-100' : '')}
                      placeholder="Enter SMTP password"
                      value={settings.smtpPassword}
                      onChange={e => { setSettings({...settings, smtpPassword: e.target.value}); clearFieldError('smtpPassword'); }}
                    />
                    <button type="button" onClick={() => setShowPassword(!showPassword)} className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600">
                      {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                  {fieldErrors.smtpPassword && <p className="text-xs text-red-600 mt-1">{fieldErrors.smtpPassword}</p>}
                  {!fieldErrors.smtpPassword && (
                    <p className="text-xs text-gray-400 mt-1">
                      ⚠️ For Gmail: use an <a href="https://myaccount.google.com/apppasswords" target="_blank" rel="noreferrer" className="text-blue-600 underline">App Password</a>, not your regular password.
                    </p>
                  )}
                </div>
                <div>
                  <label className={labelClass}>From Email *</label>
                  <input
                    type="email"
                    data-field="smtpFromEmail"
                    className={inputClass + (fieldErrors.smtpFromEmail ? ' border-red-400 ring-2 ring-red-100' : '')}
                    placeholder="students@izylrn.com"
                    value={settings.smtpFromEmail}
                    onChange={e => { setSettings({...settings, smtpFromEmail: e.target.value}); clearFieldError('smtpFromEmail'); }}
                  />
                  {fieldErrors.smtpFromEmail && <p className="text-xs text-red-600 mt-1">{fieldErrors.smtpFromEmail}</p>}
                </div>
                <div>
                  <label className={labelClass}>From Name</label>
                  <input type="text" className={inputClass} placeholder="Faraz Ahmed" value={settings.smtpFromName || ''} onChange={e => setSettings({...settings, smtpFromName: e.target.value})} />
                </div>
              </div>

              <div className="flex items-center gap-6 pt-2">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" checked={settings.smtpEnableSsl} onChange={e => setSettings({...settings, smtpEnableSsl: e.target.checked})} className="w-4 h-4 text-blue-600 rounded border-gray-300 focus:ring-blue-500" />
                  <span className="text-sm text-gray-700">Enable SSL/TLS <span className="text-xs text-gray-400">(recommended)</span></span>
                </label>
              </div>
              </>)}

              {/* SendGrid fields */}
              {settings.emailProvider === 'sendgrid' && (
                <div className="space-y-4">
                  <div>
                    <label className={labelClass}>SendGrid API Key *</label>
                    <input
                      type="password"
                      data-field="sendGridApiKey"
                      className={inputClass + (fieldErrors.sendGridApiKey ? ' border-red-400 ring-2 ring-red-100' : '')}
                      placeholder="SG.xxxxx..."
                      value={settings.sendGridApiKey || ''}
                      onChange={e => { setSettings({...settings, sendGridApiKey: e.target.value}); clearFieldError('sendGridApiKey'); }}
                    />
                    {fieldErrors.sendGridApiKey && <p className="text-xs text-red-600 mt-1">{fieldErrors.sendGridApiKey}</p>}
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className={labelClass}>From Email *</label>
                      <input
                        type="email"
                        data-field="smtpFromEmail"
                        className={inputClass + (fieldErrors.smtpFromEmail ? ' border-red-400 ring-2 ring-red-100' : '')}
                        placeholder="sender@yourdomain.com"
                        value={settings.smtpFromEmail}
                        onChange={e => { setSettings({...settings, smtpFromEmail: e.target.value}); clearFieldError('smtpFromEmail'); }}
                      />
                      {fieldErrors.smtpFromEmail && <p className="text-xs text-red-600 mt-1">{fieldErrors.smtpFromEmail}</p>}
                    </div>
                    <div>
                      <label className={labelClass}>From Name</label>
                      <input type="text" className={inputClass} placeholder="Your Brand" value={settings.smtpFromName || ''} onChange={e => setSettings({...settings, smtpFromName: e.target.value})} />
                    </div>
                  </div>
                </div>
              )}

              {/* Brevo fields */}
              {settings.emailProvider === 'brevo' && (
                <div className="space-y-4">
                  <div>
                    <label className={labelClass}>Brevo API Key *</label>
                    <input
                      type="password"
                      data-field="brevoApiKey"
                      className={inputClass + (fieldErrors.brevoApiKey ? ' border-red-400 ring-2 ring-red-100' : '')}
                      placeholder="xkeysib-xxxxx..."
                      value={settings.brevoApiKey || ''}
                      onChange={e => { setSettings({...settings, brevoApiKey: e.target.value}); clearFieldError('brevoApiKey'); }}
                    />
                    {fieldErrors.brevoApiKey && <p className="text-xs text-red-600 mt-1">{fieldErrors.brevoApiKey}</p>}
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className={labelClass}>From Email *</label>
                      <input
                        type="email"
                        data-field="smtpFromEmail"
                        className={inputClass + (fieldErrors.smtpFromEmail ? ' border-red-400 ring-2 ring-red-100' : '')}
                        placeholder="sender@yourdomain.com"
                        value={settings.smtpFromEmail}
                        onChange={e => { setSettings({...settings, smtpFromEmail: e.target.value}); clearFieldError('smtpFromEmail'); }}
                      />
                      {fieldErrors.smtpFromEmail && <p className="text-xs text-red-600 mt-1">{fieldErrors.smtpFromEmail}</p>}
                    </div>
                    <div>
                      <label className={labelClass}>From Name</label>
                      <input type="text" className={inputClass} placeholder="Your Brand" value={settings.smtpFromName || ''} onChange={e => setSettings({...settings, smtpFromName: e.target.value})} />
                    </div>
                  </div>
                </div>
              )}

              {/* Mailgun fields */}
              {settings.emailProvider === 'mailgun' && (
                <div className="space-y-4">
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className={labelClass}>Mailgun API Key *</label>
                      <input
                        type="password"
                        data-field="mailgunApiKey"
                        className={inputClass + (fieldErrors.mailgunApiKey ? ' border-red-400 ring-2 ring-red-100' : '')}
                        placeholder="key-xxxxx..."
                        value={settings.mailgunApiKey || ''}
                        onChange={e => { setSettings({...settings, mailgunApiKey: e.target.value}); clearFieldError('mailgunApiKey'); }}
                      />
                      {fieldErrors.mailgunApiKey && <p className="text-xs text-red-600 mt-1">{fieldErrors.mailgunApiKey}</p>}
                    </div>
                    <div>
                      <label className={labelClass}>Mailgun Domain *</label>
                      <input
                        type="text"
                        data-field="mailgunDomain"
                        className={inputClass + (fieldErrors.mailgunDomain ? ' border-red-400 ring-2 ring-red-100' : '')}
                        placeholder="mg.yourdomain.com"
                        value={settings.mailgunDomain || ''}
                        onChange={e => { setSettings({...settings, mailgunDomain: e.target.value}); clearFieldError('mailgunDomain'); }}
                      />
                      {fieldErrors.mailgunDomain && <p className="text-xs text-red-600 mt-1">{fieldErrors.mailgunDomain}</p>}
                    </div>
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className={labelClass}>From Email *</label>
                      <input
                        type="email"
                        data-field="smtpFromEmail"
                        className={inputClass + (fieldErrors.smtpFromEmail ? ' border-red-400 ring-2 ring-red-100' : '')}
                        placeholder="sender@yourdomain.com"
                        value={settings.smtpFromEmail}
                        onChange={e => { setSettings({...settings, smtpFromEmail: e.target.value}); clearFieldError('smtpFromEmail'); }}
                      />
                      {fieldErrors.smtpFromEmail && <p className="text-xs text-red-600 mt-1">{fieldErrors.smtpFromEmail}</p>}
                    </div>
                    <div>
                      <label className={labelClass}>From Name</label>
                      <input type="text" className={inputClass} placeholder="Your Brand" value={settings.smtpFromName || ''} onChange={e => setSettings({...settings, smtpFromName: e.target.value})} />
                    </div>
                  </div>
                </div>
              )}

              {/* Test Connection */}
              <div className="mt-6 p-4 bg-blue-50 rounded-xl border border-blue-100">
                <h4 className="text-sm font-semibold text-blue-900 mb-1 flex items-center gap-2">
                  <FlaskConical className="w-4 h-4" /> Test SMTP Connection
                </h4>
                <p className="text-xs text-blue-700 mb-3">
                  Fill the SMTP fields above, then enter a test email here. We&apos;ll auto-save your settings and send a test message.
                </p>
                <div className="flex gap-3">
                  <input
                    type="email"
                    data-field="testEmail"
                    className={inputClass + ' flex-1' + (fieldErrors.testEmail ? ' border-red-400 ring-2 ring-red-100' : '')}
                    placeholder="Enter test email address (e.g., yourself@gmail.com)"
                    value={testEmail}
                    onChange={e => { setTestEmail(e.target.value); clearFieldError('testEmail'); }}
                  />
                  <button onClick={handleTestSmtp} disabled={testing} className="flex items-center gap-2 px-5 py-2.5 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-medium whitespace-nowrap disabled:opacity-50">
                    {testing ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle2 className="w-4 h-4" />}
                    {testing ? 'Sending...' : 'Save & Send Test'}
                  </button>
                </div>
                {fieldErrors.testEmail && <p className="text-xs text-red-600 mt-1.5">{fieldErrors.testEmail}</p>}
                {Object.keys(fieldErrors).length > 1 && (
                  <p className="text-xs text-red-600 mt-2">
                    ⚠️ Please fix the highlighted fields above before sending the test.
                  </p>
                )}
              </div>
            </div>
          )}

          {/* Signature Tab */}
          {activeTab === 'signature' && (
            <div className="space-y-6">
              <div className="flex items-center gap-2 mb-4">
                <PenTool className="w-5 h-5 text-primary-600" />
                <h3 className="text-lg font-semibold text-gray-900">Email Signature</h3>
              </div>
              <p className="text-sm text-gray-500 -mt-4 mb-4">
                Configure your professional signature. These values will appear in templates wherever the placeholders below are used &mdash; no more hardcoded defaults.
              </p>

              {/* Placeholder reference */}
              <div className="p-4 bg-primary-50 rounded-xl border border-primary-100 mb-4">
                <p className="text-xs font-semibold text-primary-900 mb-2">📌 Available Placeholders in Templates:</p>
                <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs text-primary-800">
                  <div><code className="bg-white px-1.5 py-0.5 rounded">{'{{sender_name}}'}</code> &rarr; From Name (Email tab)</div>
                  <div><code className="bg-white px-1.5 py-0.5 rounded">{'{{sender_email}}'}</code> &rarr; From Email (Email tab)</div>
                  <div><code className="bg-white px-1.5 py-0.5 rounded">{'{{sender_designation}}'}</code> &rarr; Designation below</div>
                  <div><code className="bg-white px-1.5 py-0.5 rounded">{'{{sender_phone}}'}</code> &rarr; Phone below</div>
                  <div><code className="bg-white px-1.5 py-0.5 rounded">{'{{signature_image}}'}</code> &rarr; Profile picture below</div>
                  <div><code className="bg-white px-1.5 py-0.5 rounded">{'{{company_website}}'}</code> &rarr; Website below</div>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                <div>
                  <label className={labelClass}>
                    <Briefcase className="inline w-4 h-4 mr-1 text-primary-500" />
                    Designation / Job Title
                  </label>
                  <input
                    type="text"
                    className={inputClass}
                    placeholder="e.g., Founder & CEO, Marketing Manager"
                    value={settings.signatureDesignation || ''}
                    onChange={e => setSettings({...settings, signatureDesignation: e.target.value})}
                  />
                </div>
                <div>
                  <label className={labelClass}>
                    <Phone className="inline w-4 h-4 mr-1 text-primary-500" />
                    Phone Number
                  </label>
                  <input
                    type="tel"
                    className={inputClass}
                    placeholder="e.g., +92 300 1234567"
                    value={settings.signaturePhone || ''}
                    onChange={e => setSettings({...settings, signaturePhone: e.target.value})}
                  />
                </div>
                <div className="md:col-span-2">
                  <label className={labelClass}>
                    <Globe className="inline w-4 h-4 mr-1 text-primary-500" />
                    Company Website
                  </label>
                  <input
                    type="url"
                    className={inputClass}
                    placeholder="e.g., https://izylrn.com/en"
                    value={settings.companyWebsite || ''}
                    onChange={e => setSettings({...settings, companyWebsite: e.target.value})}
                  />
                </div>
                <div className="md:col-span-2">
                  <label className={labelClass}>
                    <ImageIcon className="inline w-4 h-4 mr-1 text-primary-500" />
                    Signature Image / Profile Picture
                  </label>

                  {/* Upload button + URL input + thumbnail */}
                  <div className="flex items-start gap-3">
                    {/* Live thumbnail */}
                    {settings.signatureImageUrl ? (
                      <div className="relative flex-shrink-0">
                        <img
                          src={settings.signatureImageUrl}
                          alt="Current"
                          className="w-16 h-16 rounded-lg object-cover border-2 border-primary-100"
                          onError={(e) => { (e.target as HTMLImageElement).src = buildAvatarUrl(settings.smtpFromName || 'A', undefined, 128, '6366f1', 'fff'); }}
                        />
                        <button
                          type="button"
                          onClick={() => setSettings({...settings, signatureImageUrl: ''})}
                          className="absolute -top-1.5 -right-1.5 w-5 h-5 bg-red-500 text-white rounded-full flex items-center justify-center hover:bg-red-600"
                          title="Remove image"
                        >
                          <X className="w-3 h-3" />
                        </button>
                      </div>
                    ) : (
                      <div className="w-16 h-16 rounded-lg border-2 border-dashed border-gray-300 flex items-center justify-center bg-gray-50 flex-shrink-0">
                        <ImageIcon className="w-6 h-6 text-gray-300" />
                      </div>
                    )}

                    {/* URL + upload */}
                    <div className="flex-1 space-y-2">
                      <input
                        type="url"
                        className={inputClass}
                        placeholder="Paste image URL or click Upload"
                        value={settings.signatureImageUrl || ''}
                        onChange={e => setSettings({...settings, signatureImageUrl: e.target.value})}
                      />
                      <div className="flex items-center gap-2">
                        <input
                          ref={fileInputRef}
                          type="file"
                          accept="image/png,image/jpeg,image/gif,image/webp"
                          onChange={(e) => { const f = e.target.files?.[0]; if (f) handleImageUpload(f); }}
                          className="hidden"
                        />
                        <button
                          type="button"
                          onClick={() => fileInputRef.current?.click()}
                          disabled={uploadingImage}
                          className="flex items-center gap-2 px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:opacity-50 text-sm font-medium"
                        >
                          {uploadingImage ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload className="w-4 h-4" />}
                          {uploadingImage ? 'Uploading...' : 'Upload Image'}
                        </button>
                        <span className="text-xs text-gray-400">PNG, JPG, GIF, WEBP &middot; Max 2 MB</span>
                      </div>
                    </div>
                  </div>

                  <p className="text-xs text-gray-500 mt-2">
                    💡 <strong>Tip:</strong> Square images (200×200px) look best. Upload directly or paste a hosted URL (Cloudinary, ImgBB).
                    Leave blank to auto-generate an avatar from your initials.
                  </p>
                </div>
              </div>

              {/* Live signature preview */}
              <div className="mt-4 p-5 bg-gray-50 rounded-xl border border-gray-200">
                <p className="text-xs font-semibold text-gray-500 mb-3 uppercase tracking-wide">Live Signature Preview</p>
                <div className="bg-white p-4 rounded-lg border border-gray-100">
                  <div className="flex items-start gap-4">
                    {/* Profile image with fallback to colored initial */}
                    {settings.signatureImageUrl ? (
                      <img
                        src={settings.signatureImageUrl}
                        alt="Signature"
                        className="w-14 h-14 rounded-full object-cover border-2 border-primary-100 flex-shrink-0"
                        onError={(e) => {
                          (e.target as HTMLImageElement).style.display = 'none';
                          (e.target as HTMLImageElement).parentElement?.querySelector('.avatar-fallback')?.classList.remove('hidden');
                        }}
                      />
                    ) : null}
                    <div
                      className={`avatar-fallback w-14 h-14 rounded-full bg-gradient-to-br from-primary-500 to-pink-500 text-white flex items-center justify-center text-xl font-bold flex-shrink-0 ${settings.signatureImageUrl ? 'hidden' : ''}`}
                    >
                      {(settings.smtpFromName || 'A').charAt(0).toUpperCase()}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="font-bold text-gray-900">{settings.smtpFromName || 'Your Name'}</p>
                      <p className="text-sm text-primary-600 font-medium">
                        {settings.signatureDesignation || 'Your Designation'}
                      </p>
                      <div className="mt-2 space-y-1 text-sm text-gray-600">
                        <p className="flex items-center gap-1.5">
                          <Mail className="w-4 h-4 text-primary-500 flex-shrink-0" />
                          <span>{settings.smtpFromEmail || 'your@email.com'}</span>
                        </p>
                        <p className="flex items-center gap-1.5">
                          <Phone className="w-4 h-4 text-pink-500 flex-shrink-0" />
                          <span>{settings.signaturePhone || '+xx xxx xxxxxxx'}</span>
                        </p>
                        <p className="flex items-center gap-1.5">
                          <Globe className="w-4 h-4 text-blue-500 flex-shrink-0" />
                          <a href={settings.companyWebsite || '#'} className="text-primary-600 hover:underline">
                            {settings.companyWebsite || 'https://yourwebsite.com'}
                          </a>
                        </p>
                      </div>
                    </div>
                  </div>
                </div>
                {!settings.signatureImageUrl && (
                  <p className="text-xs text-amber-600 mt-3 flex items-center gap-1">
                    <ImageIcon className="w-3 h-3" /> No image set &mdash; recipients will see an auto-generated avatar from your initials.
                  </p>
                )}
                <p className="text-xs text-gray-400 mt-2">💡 Tip: Make sure to also set <strong>From Name</strong> and <strong>From Email</strong> in the Email SMTP tab.</p>
              </div>
            </div>
          )}

          {/* WhatsApp Tab */}
          {activeTab === 'whatsapp' && (
            <div className="space-y-6">
              <div className="flex items-center gap-2 mb-4">
                <MessageSquare className="w-5 h-5 text-green-600" />
                <h3 className="text-lg font-semibold text-gray-900">WhatsApp Business API</h3>
              </div>
              <p className="text-sm text-gray-500 -mt-4 mb-6">Configure your Meta WhatsApp Business API credentials to send WhatsApp messages.</p>

              <div className="p-4 bg-amber-50 rounded-xl border border-amber-200 mb-6">
                <p className="text-sm text-amber-800">Currently using mock service. Configure your Meta Business API credentials to send real WhatsApp messages.</p>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                <div className="md:col-span-2">
                  <label className={labelClass}>WhatsApp API Key</label>
                  <input type="password" className={inputClass} placeholder="Enter your Meta API key" value={settings.whatsAppApiKey || ''} onChange={e => setSettings({...settings, whatsAppApiKey: e.target.value})} />
                </div>
                <div>
                  <label className={labelClass}>Phone Number ID</label>
                  <input type="text" className={inputClass} placeholder="Meta Phone Number ID" value={settings.whatsAppPhoneNumberId || ''} onChange={e => setSettings({...settings, whatsAppPhoneNumberId: e.target.value})} />
                </div>
                <div>
                  <label className={labelClass}>Business Account ID</label>
                  <input type="text" className={inputClass} placeholder="WhatsApp Business Account ID" value={settings.whatsAppBusinessAccountId || ''} onChange={e => setSettings({...settings, whatsAppBusinessAccountId: e.target.value})} />
                </div>
              </div>
            </div>
          )}

          {/* SMS Tab */}
          {activeTab === 'sms' && (
            <div className="space-y-6">
              <div className="flex items-center gap-2 mb-4">
                <Smartphone className="w-5 h-5 text-purple-600" />
                <h3 className="text-lg font-semibold text-gray-900">SMS Configuration</h3>
              </div>
              <p className="text-sm text-gray-500 -mt-4 mb-6">Configure your SMS gateway credentials (e.g., Twilio) to send SMS messages.</p>

              <div className="p-4 bg-amber-50 rounded-xl border border-amber-200 mb-6">
                <p className="text-sm text-amber-800">Currently using mock service. Configure your SMS provider credentials to send real SMS messages.</p>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                <div>
                  <label className={labelClass}>API Key / Account SID</label>
                  <input type="password" className={inputClass} placeholder="Enter API key" value={settings.smsApiKey || ''} onChange={e => setSettings({...settings, smsApiKey: e.target.value})} />
                </div>
                <div>
                  <label className={labelClass}>API Secret / Auth Token</label>
                  <input type="password" className={inputClass} placeholder="Enter API secret" value={settings.smsApiSecret || ''} onChange={e => setSettings({...settings, smsApiSecret: e.target.value})} />
                </div>
                <div>
                  <label className={labelClass}>Sender Number</label>
                  <input type="text" className={inputClass} placeholder="+1234567890" value={settings.smsSenderNumber || ''} onChange={e => setSettings({...settings, smsSenderNumber: e.target.value})} />
                </div>
              </div>
            </div>
          )}

          {/* === My Signature Tab — available to ALL users === */}
          {activeTab === 'mysignature' && (
            <div className="space-y-6">
              <div className="flex items-center gap-2 mb-2">
                <PenTool className="w-5 h-5 text-pink-600" />
                <h3 className="text-lg font-semibold text-gray-900">My Signature</h3>
              </div>
              <p className="text-sm text-gray-500 -mt-2">
                These details appear in <strong>your</strong> outgoing emails &mdash;
                rendered into placeholders <code className="bg-gray-100 px-1 rounded text-xs">{'{{sender_designation}}'}</code>,
                <code className="bg-gray-100 px-1 rounded text-xs">{'{{sender_phone}}'}</code>,
                <code className="bg-gray-100 px-1 rounded text-xs">{'{{signature_image}}'}</code>.
                Your <strong>name</strong> ({mySig?.fullName || '—'}) and the <strong>org From email</strong> ({mySig?.orgFromEmail || '—'}) come from the SMTP group <strong>{mySig?.orgSmtpGroupName || '(no group)'}</strong> managed by your admin.
              </p>

              {/* Org context (read-only) */}
              <div className="p-4 bg-blue-50 rounded-xl border border-blue-100">
                <p className="text-xs font-semibold text-blue-900 mb-2">📌 Org context (admin-managed, read-only):</p>
                <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs text-blue-800">
                  <div>From Email: <strong>{mySig?.orgFromEmail || '—'}</strong></div>
                  <div>Company Name: <strong>{mySig?.orgCompanyName || '—'}</strong></div>
                  <div>Company Website: <strong>{mySig?.orgCompanyWebsite || '—'}</strong></div>
                  <div>SMTP Group: <strong>{mySig?.orgSmtpGroupName || '—'}</strong></div>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                <div>
                  <label className={labelClass}>
                    <Briefcase className="inline w-4 h-4 mr-1 text-pink-500" />
                    Designation / Job Title
                  </label>
                  <input
                    type="text"
                    className={inputClass}
                    placeholder={mySig?.effectiveDesignation && !mySig.signatureDesignation
                      ? `Org default: ${mySig.effectiveDesignation}`
                      : 'e.g., Account Manager'}
                    value={mySig?.signatureDesignation ?? ''}
                    onChange={e => setMySig(mySig ? { ...mySig, signatureDesignation: e.target.value } : null)}
                  />
                  <p className="text-xs text-gray-500 mt-1">Leave blank to use org default.</p>
                </div>
                <div>
                  <label className={labelClass}>
                    <Phone className="inline w-4 h-4 mr-1 text-pink-500" />
                    Phone Number
                  </label>
                  <input
                    type="tel"
                    className={inputClass}
                    placeholder={mySig?.effectivePhone && !mySig.signaturePhone
                      ? `Org default: ${mySig.effectivePhone}`
                      : 'e.g., +92 300 1234567'}
                    value={mySig?.signaturePhone ?? ''}
                    onChange={e => setMySig(mySig ? { ...mySig, signaturePhone: e.target.value } : null)}
                  />
                </div>

                {/* Signature image upload */}
                <div className="md:col-span-2">
                  <label className={labelClass}>
                    <ImageIcon className="inline w-4 h-4 mr-1 text-pink-500" />
                    Profile Picture / Headshot
                  </label>
                  <div className="flex items-start gap-3">
                    {mySig?.signatureImageUrl || mySig?.effectiveImageUrl ? (
                      <div className="relative flex-shrink-0">
                        <img
                          src={mySig?.signatureImageUrl || mySig?.effectiveImageUrl}
                          alt="Current"
                          className="w-16 h-16 rounded-lg object-cover border-2 border-pink-100"
                          onError={(e) => { (e.target as HTMLImageElement).src = buildAvatarUrl(mySig?.fullName || 'U', undefined, 128, 'ec4899', 'fff'); }}
                        />
                        {mySig?.signatureImageUrl && (
                          <button
                            type="button"
                            onClick={() => mySig && setMySig({ ...mySig, signatureImageUrl: '' })}
                            className="absolute -top-1.5 -right-1.5 w-5 h-5 bg-red-500 text-white rounded-full flex items-center justify-center hover:bg-red-600"
                            title="Remove my image (use org default)"
                          >
                            <X className="w-3 h-3" />
                          </button>
                        )}
                      </div>
                    ) : (
                      <div className="w-16 h-16 rounded-lg border-2 border-dashed border-gray-300 flex items-center justify-center bg-gray-50 flex-shrink-0">
                        <ImageIcon className="w-6 h-6 text-gray-300" />
                      </div>
                    )}
                    <div className="flex-1 space-y-2">
                      <input
                        type="url"
                        className={inputClass}
                        placeholder="Paste image URL or click Upload"
                        value={mySig?.signatureImageUrl ?? ''}
                        onChange={e => setMySig(mySig ? { ...mySig, signatureImageUrl: e.target.value } : null)}
                      />
                      <div className="flex items-center gap-2">
                        <input
                          ref={mySigFileRef}
                          type="file"
                          accept="image/png,image/jpeg,image/gif,image/webp"
                          onChange={(e) => { const f = e.target.files?.[0]; if (f) handleMySigImageUpload(f); }}
                          className="hidden"
                        />
                        <button
                          type="button"
                          onClick={() => mySigFileRef.current?.click()}
                          disabled={uploadingMySigImage}
                          className="flex items-center gap-2 px-4 py-2 bg-pink-600 text-white rounded-lg hover:bg-pink-700 disabled:opacity-50 text-sm font-medium"
                        >
                          {uploadingMySigImage ? <Loader2 className="w-4 h-4 animate-spin" /> : <Upload className="w-4 h-4" />}
                          {uploadingMySigImage ? 'Uploading...' : 'Upload My Photo'}
                        </button>
                        <span className="text-xs text-gray-400">PNG/JPG/GIF/WEBP · Max 2 MB</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              {/* Live preview */}
              <div className="p-5 bg-gray-50 rounded-xl border border-gray-200">
                <p className="text-xs font-semibold text-gray-500 mb-3 uppercase tracking-wide">Live Signature Preview</p>
                <div className="bg-white p-4 rounded-lg border border-gray-100">
                  <div className="flex items-start gap-4">
                    <img
                      src={mySig?.signatureImageUrl || mySig?.effectiveImageUrl ||
                        buildAvatarUrl(mySig?.fullName || 'U', undefined, 128, 'ec4899', 'fff')}
                      alt={mySig?.fullName}
                      className="w-14 h-14 rounded-full object-cover border-2 border-pink-100 flex-shrink-0"
                      onError={(e) => { (e.target as HTMLImageElement).src = buildAvatarUrl(mySig?.fullName || 'U', undefined, 128, 'ec4899', 'fff'); }}
                    />
                    <div className="flex-1 min-w-0">
                      <p className="font-bold text-gray-900">{mySig?.fullName || 'Your Name'}</p>
                      <p className="text-sm text-pink-600 font-medium">{mySig?.signatureDesignation || mySig?.effectiveDesignation || 'Your Designation'}</p>
                      <div className="mt-2 space-y-1 text-sm text-gray-600">
                        <p className="flex items-center gap-1.5"><Mail className="w-4 h-4 text-pink-500" /> {mySig?.orgFromEmail || 'your@email.com'}</p>
                        <p className="flex items-center gap-1.5"><Phone className="w-4 h-4 text-pink-500" /> {mySig?.signaturePhone || mySig?.effectivePhone || '+xx xxx xxxxxxx'}</p>
                        <p className="flex items-center gap-1.5"><Globe className="w-4 h-4 text-pink-500" /> <a href={mySig?.orgCompanyWebsite || '#'} className="text-pink-600 hover:underline">{mySig?.orgCompanyWebsite || 'https://yourwebsite.com'}</a></p>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <button
                onClick={handleSaveMySig}
                disabled={savingMySig}
                className="flex items-center gap-2 px-6 py-3 bg-pink-600 text-white rounded-lg hover:bg-pink-700 disabled:opacity-50 font-medium shadow-md"
              >
                {savingMySig ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                {savingMySig ? 'Saving...' : 'Save My Signature'}
              </button>
            </div>
          )}

          {/* Notifications Tab */}
          {activeTab === 'notifications' && (
            <div className="space-y-6">
              <div className="flex items-center gap-2 mb-4">
                <Bell className="w-5 h-5 text-amber-600" />
                <h3 className="text-lg font-semibold text-gray-900">Notification Preferences</h3>
              </div>
              <p className="text-sm text-gray-500 -mt-4 mb-6">Choose how you want to be notified about campaign results and message delivery.</p>

              <div className="space-y-4">
                <label className="flex items-center justify-between p-4 bg-gray-50 rounded-xl cursor-pointer hover:bg-gray-100 transition-colors">
                  <div>
                    <p className="font-medium text-gray-900">Campaign Completion Notifications</p>
                    <p className="text-sm text-gray-500">Get notified when a campaign finishes sending</p>
                  </div>
                  <input type="checkbox" checked={settings.notifyOnCampaignComplete} onChange={e => setSettings({...settings, notifyOnCampaignComplete: e.target.checked})} className="w-5 h-5 text-blue-600 rounded border-gray-300 focus:ring-blue-500" />
                </label>
                <label className="flex items-center justify-between p-4 bg-gray-50 rounded-xl cursor-pointer hover:bg-gray-100 transition-colors">
                  <div>
                    <p className="font-medium text-gray-900">Failed Message Alerts</p>
                    <p className="text-sm text-gray-500">Get alerts when messages fail to deliver</p>
                  </div>
                  <input type="checkbox" checked={settings.notifyOnMessageFailed} onChange={e => setSettings({...settings, notifyOnMessageFailed: e.target.checked})} className="w-5 h-5 text-blue-600 rounded border-gray-300 focus:ring-blue-500" />
                </label>
              </div>

              <div className="mt-4">
                <label className={labelClass}>Notification Email</label>
                <p className="text-xs text-gray-500 mb-2">Receive campaign summary emails at this address. Uses your configured SMTP to send.</p>
                <input type="email" className={inputClass} placeholder="your@email.com" value={settings.notificationEmail || ''} onChange={e => setSettings({...settings, notificationEmail: e.target.value})} />
              </div>
            </div>
          )}

          {/* === ADMIN Tab — Platform-wide settings (visible only to admins) === */}
          {activeTab === 'ai' && isAdmin && systemSettings && (
            <AiAssistantTab settings={systemSettings} setSettings={setSystemSettings} onSave={handleSaveSystemSettings} saving={savingAdmin} />
          )}

          {/* === INTEGRATIONS Tab — per-provider credential vault (AI + image + payments) === */}
          {activeTab === 'integrations' && isAdmin && (
            <IntegrationsPage />
          )}

          {activeTab === 'admin' && isAdmin && (
            <div className="space-y-6">
              <div className="flex items-center gap-2 mb-4">
                <Shield className="w-5 h-5 text-red-600" />
                <h3 className="text-lg font-semibold text-gray-900">Platform Settings</h3>
                <span className="px-2 py-0.5 bg-red-100 text-red-700 text-[10px] font-semibold rounded-full uppercase tracking-wide">Admin Only</span>
              </div>
              <p className="text-sm text-gray-500 -mt-4 mb-6">
                These settings affect <strong>every user</strong> on the platform. Changes apply to all campaigns sent after saving.
              </p>

              {!systemSettings ? (
                <div className="flex items-center justify-center py-8">
                  <Loader2 className="w-6 h-6 animate-spin text-red-600" />
                </div>
              ) : (
                <>
                  {/* === Send-rate / interval settings === */}
                  <div className="bg-gradient-to-br from-red-50 to-orange-50 rounded-xl border border-red-100 p-5">
                    <h4 className="flex items-center gap-2 text-sm font-semibold text-red-900 mb-1">
                      <Zap className="w-4 h-4" /> Email Sending Rate
                    </h4>
                    <p className="text-xs text-red-700 mb-4">
                      Slower rates = better deliverability + less likelihood of spam flagging. Higher rates = faster campaign completion.
                    </p>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                      <div>
                        <label className={labelClass}>
                          <Clock className="inline w-4 h-4 mr-1 text-red-500" />
                          Delay Between Emails (ms)
                        </label>
                        <input
                          type="number"
                          min="0"
                          step="100"
                          className={inputClass}
                          value={systemSettings.delayBetweenMessagesMs}
                          onChange={e => setSystemSettings({...systemSettings, delayBetweenMessagesMs: parseInt(e.target.value) || 0})}
                        />
                        <p className="text-xs text-gray-500 mt-1">
                          Current: <strong>{(systemSettings.delayBetweenMessagesMs / 1000).toFixed(2)}s</strong> per email.
                          {' '}Recommended: 1000-2000ms for Gmail SMTP, 100-300ms for SendGrid/Brevo.
                        </p>
                      </div>

                      <div>
                        <label className={labelClass}>
                          Max Emails / Minute (0 = unlimited)
                        </label>
                        <input
                          type="number"
                          min="0"
                          step="5"
                          className={inputClass}
                          value={systemSettings.maxMessagesPerMinute}
                          onChange={e => setSystemSettings({...systemSettings, maxMessagesPerMinute: parseInt(e.target.value) || 0})}
                        />
                        <p className="text-xs text-gray-500 mt-1">
                          Useful for Gmail's ~500/day limit. Try <strong>30</strong> for safe Gmail throttling.
                        </p>
                      </div>

                      <div>
                        <label className={labelClass}>Batch Size</label>
                        <input
                          type="number"
                          min="1"
                          max="500"
                          className={inputClass}
                          value={systemSettings.batchSize}
                          onChange={e => setSystemSettings({...systemSettings, batchSize: parseInt(e.target.value) || 50})}
                        />
                        <p className="text-xs text-gray-500 mt-1">Emails processed per batch (default 50).</p>
                      </div>

                      <div>
                        <label className={labelClass}>Delay Between Batches (ms)</label>
                        <input
                          type="number"
                          min="0"
                          step="100"
                          className={inputClass}
                          value={systemSettings.delayBetweenBatchesMs}
                          onChange={e => setSystemSettings({...systemSettings, delayBetweenBatchesMs: parseInt(e.target.value) || 0})}
                        />
                        <p className="text-xs text-gray-500 mt-1">Pause between batches (default 500ms).</p>
                      </div>
                    </div>

                    {/* Computed insight */}
                    <div className="mt-4 p-3 bg-white rounded-lg border border-red-100">
                      <p className="text-xs text-gray-600">
                        <strong className="text-red-900">📊 Estimated send time</strong> for a campaign of 100 recipients:
                        <br />
                        <span className="font-mono text-xs">
                          ~{Math.ceil((100 * systemSettings.delayBetweenMessagesMs + Math.floor(100 / Math.max(systemSettings.batchSize, 1)) * systemSettings.delayBetweenBatchesMs) / 1000)}s
                        </span>
                        {' '}({((100 * systemSettings.delayBetweenMessagesMs) / 60000).toFixed(1)} min)
                      </p>
                    </div>
                  </div>

                  {/* === Sharing settings === */}
                  <div className="bg-gradient-to-br from-blue-50 to-primary-50 rounded-xl border border-blue-100 p-5">
                    <h4 className="text-sm font-semibold text-blue-900 mb-1 flex items-center gap-2">
                      🤝 Resource Sharing
                    </h4>
                    <p className="text-xs text-blue-700 mb-4">
                      Allow regular users to see templates/contacts that admins mark as "shared".
                    </p>

                    <div className="space-y-2">
                      <label className="flex items-center justify-between p-3 bg-white rounded-lg cursor-pointer hover:bg-gray-50 transition-colors">
                        <div>
                          <p className="font-medium text-gray-900 text-sm">Allow users to see shared templates</p>
                          <p className="text-xs text-gray-500">When admin marks a template as shared, other users can use it.</p>
                        </div>
                        <input
                          type="checkbox"
                          checked={systemSettings.allowUsersToSeeSharedTemplates}
                          onChange={e => setSystemSettings({...systemSettings, allowUsersToSeeSharedTemplates: e.target.checked})}
                          className="w-5 h-5 text-blue-600 rounded border-gray-300 focus:ring-blue-500"
                        />
                      </label>

                      <label className="flex items-center justify-between p-3 bg-white rounded-lg cursor-pointer hover:bg-gray-50 transition-colors">
                        <div>
                          <p className="font-medium text-gray-900 text-sm">Allow users to see shared contacts</p>
                          <p className="text-xs text-gray-500">When admin marks contacts as shared, other users can access them.</p>
                        </div>
                        <input
                          type="checkbox"
                          checked={systemSettings.allowUsersToSeeSharedContacts}
                          onChange={e => setSystemSettings({...systemSettings, allowUsersToSeeSharedContacts: e.target.checked})}
                          className="w-5 h-5 text-blue-600 rounded border-gray-300 focus:ring-blue-500"
                        />
                      </label>
                    </div>
                  </div>

                  {/* === Branding === */}
                  <div className="bg-gradient-to-br from-purple-50 to-pink-50 rounded-xl border border-purple-100 p-5">
                    <h4 className="text-sm font-semibold text-purple-900 mb-1 flex items-center gap-2">🎨 Platform Branding</h4>
                    <p className="text-xs text-purple-700 mb-4">Replaces the "MarketPro" name + logo across login/sidebar — changes apply immediately for everyone.</p>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                      <div>
                        <label className={labelClass}>Platform Name</label>
                        <input type="text" className={inputClass} value={systemSettings.platformName ?? ''}
                          onChange={e => setSystemSettings({...systemSettings, platformName: e.target.value})} />
                      </div>
                      <div>
                        <label className={labelClass}>Primary Color (hex)</label>
                        <div className="flex gap-2">
                          <input type="color" className="w-12 h-10 border border-gray-200 rounded cursor-pointer"
                            value={systemSettings.primaryColor ?? '#4f46e5'}
                            onChange={e => setSystemSettings({...systemSettings, primaryColor: e.target.value})} />
                          <input type="text" className={inputClass + ' flex-1'} value={systemSettings.primaryColor ?? ''}
                            onChange={e => setSystemSettings({...systemSettings, primaryColor: e.target.value})} placeholder="#4f46e5" />
                        </div>
                      </div>
                      <div className="md:col-span-2">
                        <label className={labelClass}>Logo URL (square, ~64×64)</label>
                        <input type="url" className={inputClass} value={systemSettings.logoUrl ?? ''}
                          onChange={e => setSystemSettings({...systemSettings, logoUrl: e.target.value})}
                          placeholder="https://yourcdn.com/logo.svg (blank = default lightning bolt)" />
                      </div>
                      <div className="md:col-span-2">
                        <label className={labelClass}>Avatar Service URL Template</label>
                        <input type="text" className={inputClass + ' font-mono text-xs'} value={systemSettings.avatarServiceUrl ?? ''}
                          onChange={e => setSystemSettings({...systemSettings, avatarServiceUrl: e.target.value})}
                          placeholder="https://ui-avatars.com/api/?name={name}&size={size}&background={bg}&color={fg}" />
                        <p className="text-xs text-gray-500 mt-1">
                          Placeholders: <code className="bg-white px-1">{'{name}'}</code> <code className="bg-white px-1">{'{size}'}</code> <code className="bg-white px-1">{'{bg}'}</code> <code className="bg-white px-1">{'{fg}'}</code>.
                          Swap providers (Gravatar, self-hosted) without redeploying.
                        </p>
                      </div>
                    </div>
                  </div>

                  {/* === Localization === */}
                  <div className="bg-gradient-to-br from-emerald-50 to-teal-50 rounded-xl border border-emerald-100 p-5">
                    <h4 className="text-sm font-semibold text-emerald-900 mb-1 flex items-center gap-2">🌍 Localization</h4>
                    <p className="text-xs text-emerald-700 mb-4">Default locale and date format used in rendered templates ({'{{current_date}}'}).</p>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                      <div>
                        <label className={labelClass}>Default Locale</label>
                        <input type="text" className={inputClass} value={systemSettings.defaultLocale ?? ''}
                          onChange={e => setSystemSettings({...systemSettings, defaultLocale: e.target.value})}
                          placeholder="en-US" />
                        <p className="text-xs text-gray-500 mt-1">e.g., en-US, hi-IN, ar-AE, fr-FR</p>
                      </div>
                      <div>
                        <label className={labelClass}>Default Date Format</label>
                        <input type="text" className={inputClass} value={systemSettings.defaultDateFormat ?? ''}
                          onChange={e => setSystemSettings({...systemSettings, defaultDateFormat: e.target.value})}
                          placeholder="MMMM dd, yyyy" />
                        <p className="text-xs text-gray-500 mt-1">.NET date format string (e.g., dd/MM/yyyy, yyyy-MM-dd)</p>
                      </div>
                    </div>
                  </div>

                  {/* === Limits === */}
                  <div className="bg-gradient-to-br from-gray-50 to-slate-50 rounded-xl border border-gray-200 p-5">
                    <h4 className="text-sm font-semibold text-gray-900 mb-1 flex items-center gap-2">⚙️ Limits & Validation</h4>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-3">
                      <div>
                        <label className={labelClass}>Password Min Length</label>
                        <input type="number" min={4} max={64} className={inputClass}
                          value={systemSettings.passwordMinLength}
                          onChange={e => setSystemSettings({...systemSettings, passwordMinLength: parseInt(e.target.value) || 8})} />
                      </div>
                      <div>
                        <label className={labelClass}>Max File Upload (MB)</label>
                        <input type="number" min={1} max={1024} className={inputClass}
                          value={systemSettings.maxFileUploadSizeMb}
                          onChange={e => setSystemSettings({...systemSettings, maxFileUploadSizeMb: parseInt(e.target.value) || 10})} />
                      </div>
                    </div>
                  </div>

                  <p className="text-xs text-gray-500">
                    Last updated: {new Date(systemSettings.updatedAt).toLocaleString()}
                  </p>

                  <button
                    onClick={handleSaveSystemSettings}
                    disabled={savingAdmin}
                    className="flex items-center gap-2 px-6 py-3 bg-red-600 text-white rounded-lg hover:bg-red-700 disabled:opacity-50 font-medium shadow-md"
                  >
                    {savingAdmin ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                    {savingAdmin ? 'Saving...' : 'Save Platform Settings'}
                  </button>
                </>
              )}
            </div>
          )}
        </div>
      </div>

      {/* For non-admin users: show a small read-only info card about current send rate */}
      {!isAdmin && systemSettings && (
        <div className="bg-gradient-to-r from-blue-50 to-primary-50 rounded-xl border border-blue-100 p-4 flex items-center gap-3">
          <div className="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
            <Clock className="w-5 h-5 text-blue-600" />
          </div>
          <div className="flex-1">
            <p className="text-sm font-medium text-gray-900">Platform Send Rate</p>
            <p className="text-xs text-gray-600 mt-0.5">
              Emails are sent with a <strong>{(systemSettings.delayBetweenMessagesMs / 1000).toFixed(2)}s</strong> delay between each
              {systemSettings.maxMessagesPerMinute > 0 ? `, max ${systemSettings.maxMessagesPerMinute}/minute` : ''}.
              {' '}Contact your admin to adjust.
            </p>
          </div>
        </div>
      )}
    </div>
  );
}

// ================================================================
// === Day 7 G8 — AI Assistant admin tab ===========================
// ================================================================
const AI_PRESETS: Array<{
  key: string;
  label: string;
  provider: string;
  baseUrl?: string;
  model: string;
  blurb: string;
  cost: 'paid' | 'free' | 'free-tier' | 'mixed';
}> = [
  { key: 'claude', label: '🟣 Claude Sonnet', provider: 'anthropic', model: 'claude-sonnet-4-5', cost: 'paid', blurb: 'Best for nuanced tone + multi-language' },
  { key: 'gpt', label: '🟢 GPT-4o mini', provider: 'openai', model: 'gpt-4o-mini', cost: 'paid', blurb: 'Cheap + popular' },
  { key: 'gemini', label: '🔵 Gemini Flash', provider: 'gemini', model: 'gemini-2.5-flash', cost: 'free-tier', blurb: 'Google — generous free tier' },
  { key: 'grok', label: '⚫ Grok', provider: 'grok', model: 'grok-4', cost: 'paid', blurb: 'xAI — premium tier' },
  { key: 'groq', label: '⚡ Groq (FREE cloud)', provider: 'openai-compatible', baseUrl: 'https://api.groq.com/openai/v1', model: 'llama-3.3-70b-versatile', cost: 'free', blurb: 'Free Llama-3 70B via Groq cloud' },
  { key: 'ollama', label: '🏠 Ollama (FREE local)', provider: 'openai-compatible', baseUrl: 'http://localhost:11434/v1', model: 'llama3.2', cost: 'free', blurb: 'Runs offline on your machine — no API key, no cost' },
  { key: 'custom', label: '🔧 Custom OpenAI-compatible', provider: 'openai-compatible', baseUrl: '', model: '', cost: 'mixed', blurb: 'OpenRouter / LM Studio / any custom endpoint' },
];

const PROVIDER_LABEL: Record<string, string> = {
  disabled: 'Disabled', anthropic: 'Anthropic Claude', openai: 'OpenAI GPT', gemini: 'Google Gemini', grok: 'xAI Grok', 'openai-compatible': 'OpenAI-compatible (Ollama / Groq / Custom)',
};

function AiAssistantTab({ settings, setSettings, onSave, saving }: { settings: any; setSettings: (s: any) => void; onSave: () => Promise<void> | void; saving: boolean }) {
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<string | null>(null);
  const [showTestModal, setShowTestModal] = useState(false);
  const [apiKeyInput, setApiKeyInput] = useState('');

  // ✓ Saved state — when the backend has a stored key AND the admin hasn't typed a new one
  const hasStoredKey = !!settings.aiApiKeyMasked && settings.aiApiKeyMasked.length > 0;
  const hasUnsavedKeyChange = apiKeyInput.length > 0;

  // Configuration status — drives the badge at the top
  const configStatus: 'ready' | 'partial' | 'disabled' =
    settings.aiProvider === 'disabled' ? 'disabled'
    : (settings.aiProvider === 'openai-compatible' && !settings.aiBaseUrl)
        || (settings.aiProvider !== 'openai-compatible' && !hasStoredKey && !hasUnsavedKeyChange) ? 'partial'
    : 'ready';

  const applyPreset = (key: string) => {
    const p = AI_PRESETS.find(x => x.key === key);
    if (!p) return;
    setSettings({
      ...settings,
      aiProvider: p.provider,
      aiBaseUrl: p.baseUrl ?? '',
      aiModel: p.model,
    });
  };

  const resetPrompt = async () => {
    try {
      const res: any = await adminApi.getAiDefaultPrompt();
      const prompt = res?.data?.prompt;
      if (prompt) setSettings({ ...settings, aiSystemPrompt: prompt });
    } catch { /* ignore */ }
  };

  const runTest = async () => {
    setTesting(true); setTestResult(null);
    try {
      // If admin has typed a new key, use that; otherwise let the backend use the saved key.
      // The backend AdminAiController falls back to GetRawAiApiKeyAsync() when body.apiKey is blank.
      const res: any = await adminApi.testAi({
        provider: settings.aiProvider,
        apiKey: apiKeyInput || undefined,
        baseUrl: settings.aiBaseUrl,
        model: settings.aiModel,
        systemPrompt: settings.aiSystemPrompt,
      });
      if (res?.success === false) {
        setTestResult(`❌ ${res?.message || 'Test failed'}`);
      } else {
        const text = res?.data?.rawText || 'No text returned';
        const provider = res?.data?.provider || settings.aiProvider;
        const tokens = `${res?.data?.inputTokens || 0} in / ${res?.data?.outputTokens || 0} out`;
        const latency = `${res?.data?.latencyMs || 0}ms`;
        setTestResult(`✅ Response from ${provider} (${tokens}, ${latency}):\n\n${text}`);
      }
    } catch (e: any) {
      setTestResult(`❌ ${e?.response?.data?.message || e?.message || 'Test failed'}`);
    } finally { setTesting(false); }
  };

  const handleSave = async () => {
    try {
      await onSave();
      setApiKeyInput(''); // clear input — saved state will show via aiApiKeyMasked
      toast.success(`AI settings saved. Provider '${settings.aiProvider}' is now ${settings.aiProvider === 'disabled' ? 'OFF' : 'active'}.`);
    } catch { /* parent already toasted */ }
  };

  const ic = "w-full px-4 py-2.5 border border-gray-200 rounded-lg focus:ring-2 focus:ring-purple-500 focus:border-transparent outline-none transition-all text-gray-700 bg-gray-50 focus:bg-white";
  const lc = "block text-sm font-medium text-gray-700 mb-1.5";

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-2 mb-4 flex-wrap">
        <Sparkles className="w-5 h-5 text-purple-600" />
        <h3 className="text-lg font-semibold text-gray-900">AI Assistant</h3>
        <span className="px-2 py-0.5 bg-purple-100 text-purple-700 text-[10px] font-semibold rounded-full uppercase tracking-wide">Inbox AI Replies</span>
        {configStatus === 'ready' && (
          <span className="flex items-center gap-1 px-2.5 py-1 bg-green-100 text-green-800 text-xs font-semibold rounded-full">
            <span className="w-2 h-2 bg-green-500 rounded-full animate-pulse" /> AI Active
          </span>
        )}
        {configStatus === 'partial' && (
          <span className="flex items-center gap-1 px-2.5 py-1 bg-amber-100 text-amber-800 text-xs font-semibold rounded-full">
            ⚠ Setup incomplete — paste API key + Save
          </span>
        )}
        {configStatus === 'disabled' && (
          <span className="flex items-center gap-1 px-2.5 py-1 bg-gray-100 text-gray-700 text-xs font-semibold rounded-full">
            ⏸ Disabled
          </span>
        )}
      </div>
      <p className="text-sm text-gray-500 -mt-4">Generate suggested replies for incoming inbox messages. Admin can switch providers anytime — no redeploy.</p>

      {/* Single-source-of-truth banner: keys live in the Integrations vault (per-provider, no key loss) */}
      <div className="bg-primary-50 border border-primary-200 rounded-xl px-4 py-3 flex items-center justify-between gap-3 flex-wrap">
        <p className="text-sm text-primary-900">
          <b>Providers &amp; API keys are now managed in Integrations</b> — each provider keeps its own key
          (no more losing a key when you switch). This tab is just for AI <b>behaviour</b> (prompt, creativity, limits).
        </p>
        <a href="/admin/integrations" className="shrink-0 px-3 py-1.5 bg-primary-600 text-white rounded-lg text-sm font-medium hover:bg-primary-700">
          Open Integrations →
        </a>
      </div>

      {/* Preset chooser */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
        {AI_PRESETS.map(p => {
          const active = settings.aiProvider === p.provider && settings.aiModel === p.model;
          return (
            <button key={p.key} type="button" onClick={() => applyPreset(p.key)}
              className={`text-left p-3 rounded-xl border-2 transition-all ${active ? 'border-purple-500 bg-purple-50' : 'border-gray-100 hover:border-purple-200'}`}>
              <div className="flex items-center justify-between mb-1">
                <p className={`text-sm font-semibold ${active ? 'text-purple-700' : 'text-gray-700'}`}>{p.label}</p>
                {p.cost === 'free' && <span className="text-[10px] px-1.5 py-0.5 bg-green-100 text-green-700 rounded-full font-semibold">FREE</span>}
                {p.cost === 'free-tier' && <span className="text-[10px] px-1.5 py-0.5 bg-emerald-100 text-emerald-700 rounded-full font-semibold">FREE TIER</span>}
                {p.cost === 'paid' && <span className="text-[10px] px-1.5 py-0.5 bg-gray-100 text-gray-600 rounded-full">Paid</span>}
              </div>
              <p className="text-[11px] text-gray-500">{p.blurb}</p>
              <p className="text-[10px] text-gray-400 mt-1 truncate">{p.model || '(custom)'}</p>
            </button>
          );
        })}
      </div>

      {/* Detail config */}
      <div className="bg-gradient-to-br from-purple-50 to-pink-50 rounded-xl border border-purple-100 p-5 space-y-4">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className={lc}>Provider</label>
            <select className={ic} value={settings.aiProvider || 'disabled'}
              onChange={e => setSettings({...settings, aiProvider: e.target.value})}>
              <option value="disabled">Disabled (no AI generation)</option>
              <option value="anthropic">Anthropic Claude</option>
              <option value="openai">OpenAI GPT</option>
              <option value="gemini">Google Gemini</option>
              <option value="grok">xAI Grok</option>
              <option value="openai-compatible">OpenAI-compatible (Ollama / Groq / OpenRouter / Custom)</option>
            </select>
            <p className="text-xs text-gray-500 mt-1">{PROVIDER_LABEL[settings.aiProvider || 'disabled']}</p>
          </div>
          <div>
            <label className={lc}>Model</label>
            <input type="text" className={ic} value={settings.aiModel || ''}
              onChange={e => setSettings({...settings, aiModel: e.target.value})}
              placeholder="e.g. claude-sonnet-4-5 or llama3.2" />
          </div>
          {settings.aiProvider === 'openai-compatible' && (
            <div className="md:col-span-2">
              <label className={lc}>Base URL</label>
              <input type="url" className={ic} value={settings.aiBaseUrl || ''}
                onChange={e => setSettings({...settings, aiBaseUrl: e.target.value})}
                placeholder="http://localhost:11434/v1 or https://api.groq.com/openai/v1" />
              <p className="text-xs text-gray-500 mt-1">Ollama localhost doesn't need an API key. Groq does.</p>
            </div>
          )}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="text-sm font-medium text-gray-700">API Key</label>
              {hasStoredKey && !hasUnsavedKeyChange && (
                <span className="flex items-center gap-1 text-[11px] text-green-700 font-semibold">
                  <CheckCircle2 className="w-3 h-3" /> Saved ({settings.aiApiKeyMasked})
                </span>
              )}
              {hasUnsavedKeyChange && (
                <span className="text-[11px] text-amber-700 font-semibold">⚠ New key — click Save</span>
              )}
            </div>
            <input
              type="password"
              className={ic + (hasUnsavedKeyChange ? ' border-amber-300 ring-2 ring-amber-100' : '')}
              value={apiKeyInput}
              onChange={e => { setApiKeyInput(e.target.value); setSettings({...settings, aiApiKey: e.target.value}); }}
              placeholder={hasStoredKey ? '••••••• (leave blank to keep saved key)' : 'Paste API key here'}
              autoComplete="off"
            />
            {hasStoredKey && !hasUnsavedKeyChange && (
              <p className="text-xs text-gray-500 mt-1">💡 Test button will use the saved key automatically. To change the key, type a new one and click Save.</p>
            )}
            {!hasStoredKey && !hasUnsavedKeyChange && settings.aiProvider !== 'openai-compatible' && (
              <p className="text-xs text-amber-700 mt-1">⚠ No API key saved yet — paste one and click <strong>Save AI Settings</strong> below.</p>
            )}
          </div>
          <div>
            <label className={lc}>Timeout (seconds)</label>
            <input type="number" min="5" max="300" className={ic} value={settings.aiTimeoutSeconds ?? 30}
              onChange={e => setSettings({...settings, aiTimeoutSeconds: parseInt(e.target.value) || 30})} />
          </div>
          <div>
            <label className={lc}>Max output tokens</label>
            <input type="number" min="100" max="8000" step="50" className={ic} value={settings.aiMaxTokens ?? 800}
              onChange={e => setSettings({...settings, aiMaxTokens: parseInt(e.target.value) || 800})} />
          </div>
          <div>
            <label className={lc}>Temperature ({(settings.aiTemperature ?? 0.4).toFixed(2)})</label>
            <input type="range" min="0" max="1" step="0.05" className="w-full"
              value={settings.aiTemperature ?? 0.4}
              onChange={e => setSettings({...settings, aiTemperature: parseFloat(e.target.value)})} />
          </div>
        </div>

        {/* System prompt */}
        <div>
          <div className="flex items-center justify-between mb-1.5">
            <label className={lc}>System Prompt</label>
            <button type="button" onClick={resetPrompt} className="flex items-center gap-1 text-xs text-purple-700 hover:text-purple-900 font-medium">
              <RotateCcw className="w-3 h-3" /> Reset to default
            </button>
          </div>
          <textarea
            rows={10}
            className={ic + ' font-mono text-xs'}
            value={settings.aiSystemPrompt || ''}
            onChange={e => setSettings({...settings, aiSystemPrompt: e.target.value})}
            placeholder="System prompt…"
          />
        </div>

        <label className="flex items-start gap-3 p-3 bg-white rounded-lg cursor-pointer">
          <input type="checkbox" className="w-4 h-4 mt-0.5"
            checked={!!settings.aiAllowSendRecipientPii}
            onChange={e => setSettings({...settings, aiAllowSendRecipientPii: e.target.checked})} />
          <div>
            <p className="text-sm font-medium text-gray-900">Allow sending recipient PII to AI provider</p>
            <p className="text-xs text-gray-500 mt-0.5">If off, the recipient's name + email are masked when building the prompt. The reply quality may drop.</p>
          </div>
        </label>

        {/* === Day 10: Fallback provider (auto-failover on quota) === */}
        <div className="rounded-xl border border-amber-200 bg-amber-50/40 p-4 space-y-3">
          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold text-amber-900">🛟 Fallback Provider (auto-failover)</span>
            <span className="text-[10px] px-1.5 py-0.5 bg-amber-100 text-amber-700 rounded-full font-medium uppercase">on quota / 429</span>
          </div>
          <p className="text-xs text-amber-800">
            If your PRIMARY provider hits a quota / rate-limit error, the app automatically retries the same
            request through this fallback. Tip: set primary = a paid provider, fallback = a FREE one (Groq) so you never get blocked.
          </p>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className={lc}>Fallback Provider</label>
              <select className={ic} value={settings.aiFallbackProvider || 'disabled'}
                onChange={e => setSettings({...settings, aiFallbackProvider: e.target.value})}>
                <option value="disabled">Disabled (no fallback)</option>
                <option value="openai-compatible">Groq / Ollama / OpenAI-compatible (FREE)</option>
                <option value="gemini">Google Gemini</option>
                <option value="openai">OpenAI GPT</option>
                <option value="anthropic">Anthropic Claude</option>
                <option value="grok">xAI Grok</option>
              </select>
            </div>
            <div>
              <label className={lc}>Fallback Model</label>
              <input type="text" className={ic} value={settings.aiFallbackModel || ''}
                onChange={e => setSettings({...settings, aiFallbackModel: e.target.value})}
                placeholder="e.g. llama-3.3-70b-versatile (Groq)" />
            </div>
            {settings.aiFallbackProvider === 'openai-compatible' && (
              <div className="md:col-span-2">
                <label className={lc}>Fallback Base URL</label>
                <input type="url" className={ic} value={settings.aiFallbackBaseUrl || ''}
                  onChange={e => setSettings({...settings, aiFallbackBaseUrl: e.target.value})}
                  placeholder="https://api.groq.com/openai/v1" />
              </div>
            )}
            {settings.aiFallbackProvider !== 'disabled' && (
              <div className="md:col-span-2">
                <label className={lc}>
                  Fallback API Key
                  {settings.aiFallbackApiKeyMasked && <span className="text-xs text-green-700 ml-2">✓ saved ({settings.aiFallbackApiKeyMasked})</span>}
                </label>
                <input type="password" className={ic} value={settings.aiFallbackApiKey || ''}
                  onChange={e => setSettings({...settings, aiFallbackApiKey: e.target.value})}
                  placeholder={settings.aiFallbackApiKeyMasked ? '••••••• (leave blank to keep saved key)' : 'Paste fallback API key'} />
              </div>
            )}
          </div>
        </div>

        {/* Inbox polling cron */}
        <div>
          <label className={lc}>Inbox Polling Cron (Hangfire) <span className="text-xs text-gray-400">— restart API to apply</span></label>
          <input type="text" className={ic + ' font-mono text-xs'} value={settings.inboxPollingCron || '*/2 * * * *'}
            onChange={e => setSettings({...settings, inboxPollingCron: e.target.value})} />
          <p className="text-xs text-gray-500 mt-1">Default: every 2 minutes. Empty string disables polling.</p>
        </div>

        {/* Test + Save */}
        <div className="flex items-center gap-3 pt-3 border-t border-purple-100 flex-wrap">
          <button type="button" onClick={handleSave}
            disabled={saving}
            className="flex items-center gap-2 px-6 py-2.5 bg-gradient-to-r from-purple-600 to-pink-600 text-white rounded-lg hover:shadow-lg disabled:opacity-50 font-semibold shadow">
            {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            {saving ? 'Saving…' : 'Save AI Settings'}
          </button>
          <button type="button" onClick={() => { setShowTestModal(true); runTest(); }} disabled={testing || settings.aiProvider === 'disabled'}
            className="flex items-center gap-2 px-5 py-2 border border-purple-300 text-purple-700 rounded-lg hover:bg-purple-50 disabled:opacity-50 font-medium">
            {testing ? <Loader2 className="w-4 h-4 animate-spin" /> : <FlaskConical className="w-4 h-4" />}
            Test
          </button>
          <div className="text-xs text-gray-500 max-w-md">
            <p>💡 <strong>Save first</strong>, then Inbox AI suggestions will start working. Test sends a sample reply to verify the provider.</p>
          </div>
        </div>
      </div>

      {/* Test modal */}
      {showTestModal && (
        <div className="fixed inset-0 bg-black/40 backdrop-blur-sm z-50 flex items-center justify-center p-4" onClick={() => setShowTestModal(false)}>
          <div className="bg-white rounded-2xl shadow-2xl max-w-2xl w-full max-h-[80vh] overflow-hidden" onClick={(e) => e.stopPropagation()}>
            <div className="px-5 py-4 border-b border-gray-100 flex items-center justify-between">
              <h3 className="text-base font-semibold">AI Test Result</h3>
              <button onClick={() => setShowTestModal(false)} className="p-1 hover:bg-gray-100 rounded"><X className="w-4 h-4" /></button>
            </div>
            <div className="p-5 max-h-[60vh] overflow-auto">
              {testing ? (
                <div className="flex flex-col items-center py-12 gap-2">
                  <Loader2 className="w-8 h-8 text-purple-600 animate-spin" />
                  <p className="text-sm text-gray-500">Querying {settings.aiProvider}…</p>
                </div>
              ) : (
                <pre className="text-xs font-mono bg-gray-50 p-4 rounded-lg whitespace-pre-wrap">{testResult || '(no result)'}</pre>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
