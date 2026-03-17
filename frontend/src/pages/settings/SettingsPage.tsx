import { useState, useEffect } from 'react';
import { Mail, MessageSquare, Smartphone, Bell, Save, FlaskConical, Eye, EyeOff, Settings, CheckCircle2, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { settingsApi, type CreateSmtpSettings } from '../../api/settingsApi';

type TabType = 'email' | 'whatsapp' | 'sms' | 'notifications';

export default function SettingsPage() {
  const [activeTab, setActiveTab] = useState<TabType>('email');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [testEmail, setTestEmail] = useState('');
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
  });

  useEffect(() => {
    loadSettings();
  }, []);

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
      toast.success('Settings saved successfully!');
    } catch {
      toast.error('Failed to save settings');
    } finally {
      setSaving(false);
    }
  };

  const handleTestSmtp = async () => {
    if (!testEmail) {
      toast.error('Please enter a test email address');
      return;
    }
    setTesting(true);
    try {
      const res: any = await settingsApi.testSmtpConnection(testEmail);
      if (res?.data?.success === false) {
        toast.error(res?.data?.message || 'Failed to send test email.', { duration: 6000 });
      } else {
        toast.success('Test email sent successfully! Check your inbox.');
      }
    } catch (err: any) {
      const msg = err?.response?.data?.message || err?.response?.data?.Message || err?.message || 'Failed to send test email.';
      toast.error(msg, { duration: 6000 });
    } finally {
      setTesting(false);
    }
  };

  const tabs = [
    { id: 'email' as TabType, label: 'Email SMTP', icon: Mail, color: 'text-blue-600' },
    { id: 'whatsapp' as TabType, label: 'WhatsApp', icon: MessageSquare, color: 'text-green-600' },
    { id: 'sms' as TabType, label: 'SMS', icon: Smartphone, color: 'text-purple-600' },
    { id: 'notifications' as TabType, label: 'Notifications', icon: Bell, color: 'text-amber-600' },
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
        <button
          onClick={handleSave}
          disabled={saving}
          className="flex items-center gap-2 px-5 py-2.5 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-medium shadow-sm disabled:opacity-50"
        >
          {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
          {saving ? 'Saving...' : 'Save Settings'}
        </button>
      </div>

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
                  <input type="text" className={inputClass} placeholder="smtp.hostinger.com" value={settings.smtpHost} onChange={e => setSettings({...settings, smtpHost: e.target.value})} />
                </div>
                <div>
                  <label className={labelClass}>SMTP Port *</label>
                  <input type="number" className={inputClass} placeholder="587" value={settings.smtpPort} onChange={e => setSettings({...settings, smtpPort: parseInt(e.target.value) || 587})} />
                </div>
                <div>
                  <label className={labelClass}>Username / Email *</label>
                  <input type="text" className={inputClass} placeholder="students@izylrn.com" value={settings.smtpUsername} onChange={e => setSettings({...settings, smtpUsername: e.target.value})} />
                </div>
                <div>
                  <label className={labelClass}>Password *</label>
                  <div className="relative">
                    <input type={showPassword ? 'text' : 'password'} className={inputClass + ' pr-10'} placeholder="Enter SMTP password" value={settings.smtpPassword} onChange={e => setSettings({...settings, smtpPassword: e.target.value})} />
                    <button type="button" onClick={() => setShowPassword(!showPassword)} className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600">
                      {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                </div>
                <div>
                  <label className={labelClass}>From Email *</label>
                  <input type="email" className={inputClass} placeholder="students@izylrn.com" value={settings.smtpFromEmail} onChange={e => setSettings({...settings, smtpFromEmail: e.target.value})} />
                </div>
                <div>
                  <label className={labelClass}>From Name</label>
                  <input type="text" className={inputClass} placeholder="MarketPro" value={settings.smtpFromName || ''} onChange={e => setSettings({...settings, smtpFromName: e.target.value})} />
                </div>
              </div>

              <div className="flex items-center gap-6 pt-2">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" checked={settings.smtpEnableSsl} onChange={e => setSettings({...settings, smtpEnableSsl: e.target.checked})} className="w-4 h-4 text-blue-600 rounded border-gray-300 focus:ring-blue-500" />
                  <span className="text-sm text-gray-700">Enable SSL/TLS</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" checked={settings.smtpUseDefaultCredentials} onChange={e => setSettings({...settings, smtpUseDefaultCredentials: e.target.checked})} className="w-4 h-4 text-blue-600 rounded border-gray-300 focus:ring-blue-500" />
                  <span className="text-sm text-gray-700">Use Default Credentials</span>
                </label>
              </div>
              </>)}

              {/* SendGrid fields */}
              {settings.emailProvider === 'sendgrid' && (
                <div className="space-y-4">
                  <div>
                    <label className={labelClass}>SendGrid API Key *</label>
                    <input type="password" className={inputClass} placeholder="SG.xxxxx..." value={settings.sendGridApiKey || ''} onChange={e => setSettings({...settings, sendGridApiKey: e.target.value})} />
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className={labelClass}>From Email *</label>
                      <input type="email" className={inputClass} placeholder="sender@yourdomain.com" value={settings.smtpFromEmail} onChange={e => setSettings({...settings, smtpFromEmail: e.target.value})} />
                    </div>
                    <div>
                      <label className={labelClass}>From Name</label>
                      <input type="text" className={inputClass} placeholder="MarketPro" value={settings.smtpFromName || ''} onChange={e => setSettings({...settings, smtpFromName: e.target.value})} />
                    </div>
                  </div>
                </div>
              )}

              {/* Brevo fields */}
              {settings.emailProvider === 'brevo' && (
                <div className="space-y-4">
                  <div>
                    <label className={labelClass}>Brevo API Key *</label>
                    <input type="password" className={inputClass} placeholder="xkeysib-xxxxx..." value={settings.brevoApiKey || ''} onChange={e => setSettings({...settings, brevoApiKey: e.target.value})} />
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className={labelClass}>From Email *</label>
                      <input type="email" className={inputClass} placeholder="sender@yourdomain.com" value={settings.smtpFromEmail} onChange={e => setSettings({...settings, smtpFromEmail: e.target.value})} />
                    </div>
                    <div>
                      <label className={labelClass}>From Name</label>
                      <input type="text" className={inputClass} placeholder="MarketPro" value={settings.smtpFromName || ''} onChange={e => setSettings({...settings, smtpFromName: e.target.value})} />
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
                      <input type="password" className={inputClass} placeholder="key-xxxxx..." value={settings.mailgunApiKey || ''} onChange={e => setSettings({...settings, mailgunApiKey: e.target.value})} />
                    </div>
                    <div>
                      <label className={labelClass}>Mailgun Domain *</label>
                      <input type="text" className={inputClass} placeholder="mg.yourdomain.com" value={settings.mailgunDomain || ''} onChange={e => setSettings({...settings, mailgunDomain: e.target.value})} />
                    </div>
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className={labelClass}>From Email *</label>
                      <input type="email" className={inputClass} placeholder="sender@yourdomain.com" value={settings.smtpFromEmail} onChange={e => setSettings({...settings, smtpFromEmail: e.target.value})} />
                    </div>
                    <div>
                      <label className={labelClass}>From Name</label>
                      <input type="text" className={inputClass} placeholder="MarketPro" value={settings.smtpFromName || ''} onChange={e => setSettings({...settings, smtpFromName: e.target.value})} />
                    </div>
                  </div>
                </div>
              )}

              {/* Test Connection */}
              <div className="mt-6 p-4 bg-blue-50 rounded-xl border border-blue-100">
                <h4 className="text-sm font-semibold text-blue-900 mb-3 flex items-center gap-2">
                  <FlaskConical className="w-4 h-4" /> Test SMTP Connection
                </h4>
                <div className="flex gap-3">
                  <input type="email" className={inputClass + ' flex-1'} placeholder="Enter test email address" value={testEmail} onChange={e => setTestEmail(e.target.value)} />
                  <button onClick={handleTestSmtp} disabled={testing} className="flex items-center gap-2 px-5 py-2.5 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-medium whitespace-nowrap disabled:opacity-50">
                    {testing ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle2 className="w-4 h-4" />}
                    {testing ? 'Sending...' : 'Send Test'}
                  </button>
                </div>
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
        </div>
      </div>
    </div>
  );
}
