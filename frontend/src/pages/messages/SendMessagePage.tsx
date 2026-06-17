import { useState, useEffect, useRef } from 'react';
import { Mail, MessageSquare, Smartphone, Send, Users, User, Eye, Loader2, ChevronDown, Search, X, Sparkles, Edit3, Code2, Save, RefreshCw, Clock, FileText, Paperclip } from 'lucide-react';
import toast from 'react-hot-toast';
import axiosInstance from '../../api/axiosInstance';
import SendProgressCard from '../../components/messages/SendProgressCard';
import ActiveSenderBanner from '../../components/messages/ActiveSenderBanner';
import WhatsAppPreview from '../../components/messages/WhatsAppPreview';
import { buildAvatarUrl } from '../../config/brand';

type Channel = 'email' | 'whatsapp' | 'sms';

interface Contact {
  id: string;
  fullName: string;
  email: string;
  phone: string;
  whatsAppNumber: string;
}

interface Group {
  id: string;
  name: string;
  contactCount: number;
}

interface Template {
  id: string;
  name: string;
  channel: string;
  subject?: string;
  body: string;
}

interface SignatureSettings {
  smtpFromName?: string;
  smtpFromEmail?: string;
  signatureDesignation?: string;
  signaturePhone?: string;
  companyWebsite?: string;
  signatureImageUrl?: string;
}

export default function SendMessagePage() {
  const [channel, setChannel] = useState<Channel>('email');
  const [sendTo, setSendTo] = useState<'contacts' | 'group'>('group');
  const [selectedContacts, setSelectedContacts] = useState<Contact[]>([]);
  const [selectedGroup, setSelectedGroup] = useState<Group | null>(null);
  const [selectedTemplate, setSelectedTemplate] = useState<Template | null>(null);
  const [subject, setSubject] = useState('');
  const [messageBody, setMessageBody] = useState('');
  const [showPreview] = useState(false);
  const [sending, setSending] = useState(false);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [groups, setGroups] = useState<Group[]>([]);
  const [templates, setTemplates] = useState<Template[]>([]);
  const [contactSearch, setContactSearch] = useState('');
  const [showContactDropdown, setShowContactDropdown] = useState(false);
  const [showGroupDropdown, setShowGroupDropdown] = useState(false);
  const [showTemplateDropdown, setShowTemplateDropdown] = useState(false);

  // Editor view mode for email body: rendered preview / inline edit / raw HTML
  const [editorMode, setEditorMode] = useState<'preview' | 'edit' | 'html'>('preview');
  // Compose-from-scratch (no template) — rich vs html
  const [composeMode, setComposeMode] = useState<'rich' | 'html'>('rich');
  const richComposeRef = useRef<HTMLDivElement | null>(null);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  // Draft holds in-progress edits (committed to messageBody on Save)
  const [draftBody, setDraftBody] = useState('');
  const [draftSubject, setDraftSubject] = useState('');
  const editableRef = useRef<HTMLDivElement | null>(null);

  // Constrain every image so a large pasted source never blows up the email layout. Rich/visual
  // editing (links, buttons, images) lives in the Template editor — Compose stays simple.
  const normalizeEditorImages = (el: HTMLElement | null) => {
    if (!el) return;
    el.querySelectorAll('img').forEach((img) => {
      img.style.maxWidth = '100%';
      img.style.height = 'auto';
      img.removeAttribute('width');
      img.removeAttribute('height');
    });
  };

  // Signature settings — loaded once from user's Settings page
  const [signatureSettings, setSignatureSettings] = useState<SignatureSettings>({});
  // Preview recipient — first contact of selected group (real personalization preview)
  const [previewRecipient, setPreviewRecipient] = useState<Contact | null>(null);
  // Live send-progress modal — opens after Send EMAIL click, polls campaign status
  const [progressCampaign, setProgressCampaign] = useState<{ id: string; channel: string } | null>(null);
  // Scheduling — when true, Send EMAIL queues a future-dated campaign instead of sending immediately
  const [scheduleMode, setScheduleMode] = useState(false);
  const [scheduledAt, setScheduledAt] = useState('');

  // L1 — WhatsApp media attachment (image/document/video). Uploaded once, attached to the template.
  const [waMediaUrl, setWaMediaUrl] = useState<string | null>(null);
  const [waMediaType, setWaMediaType] = useState<string | null>(null);
  const [waMediaFileName, setWaMediaFileName] = useState<string | null>(null);
  const [waMediaSize, setWaMediaSize] = useState<number | null>(null);
  const [waMediaUploading, setWaMediaUploading] = useState(false);

  const uploadWhatsAppMedia = async (file: File) => {
    setWaMediaUploading(true);
    try {
      const form = new FormData();
      form.append('file', file);
      const res: any = await axiosInstance.post('/me/whatsapp-media/upload', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      const data = res?.data;
      if (!data?.url) throw new Error('No URL returned');
      setWaMediaUrl(data.url);
      setWaMediaType(data.mediaType);
      setWaMediaFileName(data.fileName);
      setWaMediaSize(typeof data.sizeBytes === 'number' ? data.sizeBytes : null);
      toast.success(`${data.mediaType} attached`);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Media upload failed');
    } finally {
      setWaMediaUploading(false);
    }
  };

  const clearWhatsAppMedia = () => {
    setWaMediaUrl(null);
    setWaMediaType(null);
    setWaMediaFileName(null);
    setWaMediaSize(null);
  };

  // Human-readable file size for the media card / preview.
  const formatBytes = (bytes: number | null): string => {
    if (!bytes || bytes <= 0) return '';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  useEffect(() => {
    loadData();
  }, []);

  useEffect(() => {
    if (selectedTemplate) {
      setSubject(selectedTemplate.subject || '');
      setMessageBody(selectedTemplate.body);
      setDraftSubject(selectedTemplate.subject || '');
      setDraftBody(selectedTemplate.body);
      setEditorMode('preview');
      setHasUnsavedChanges(false);
    }
  }, [selectedTemplate]);

  // Replace placeholders for preview — uses REAL data when available:
  //   - Contact info → first recipient of selected group/contacts
  //   - Sender info → user's saved Settings (Signature tab)
  // Falls back to demo placeholders only when nothing is configured.
  const renderWithSampleData = (html: string): string => {
    if (!html) return '';
    const name = previewRecipient?.fullName || 'John Doe';
    const firstName = (previewRecipient?.fullName || 'John').split(' ')[0];
    const email = previewRecipient?.email || 'recipient@example.com';
    const phone = previewRecipient?.phone || '+1 234 567 890';
    const senderName = signatureSettings.smtpFromName || 'Your Name';
    const senderEmail = signatureSettings.smtpFromEmail || 'your@email.com';
    const senderDesignation = signatureSettings.signatureDesignation || 'Your Designation';
    const senderPhone = signatureSettings.signaturePhone || '+xx xxx xxxxxxx';
    const companyName = signatureSettings.smtpFromName || 'Your Company';
    const companyWebsite = signatureSettings.companyWebsite || 'https://yourwebsite.com';
    // Signature image — saved URL OR admin-configured avatar service (centralised via brand config)
    const signatureImage = signatureSettings.signatureImageUrl ||
      buildAvatarUrl(senderName);

    return html
      .replace(/\{\{name\}\}/g, name)
      .replace(/\{\{first_name\}\}/g, firstName)
      .replace(/\{\{email\}\}/g, email)
      .replace(/\{\{phone\}\}/g, phone)
      .replace(/\{\{sender_name\}\}/g, senderName)
      .replace(/\{\{sender_email\}\}/g, senderEmail)
      .replace(/\{\{sender_designation\}\}/g, senderDesignation)
      .replace(/\{\{sender_phone\}\}/g, senderPhone)
      .replace(/\{\{signature_image\}\}/g, signatureImage)
      .replace(/\{\{company_name\}\}/g, companyName)
      .replace(/\{\{company_website\}\}/g, companyWebsite)
      .replace(/\{\{current_year\}\}/g, new Date().getFullYear().toString())
      .replace(/\{\{current_date\}\}/g, new Date().toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' }));
  };

  const startEditing = () => {
    setDraftBody(messageBody);
    setDraftSubject(subject);
    setHasUnsavedChanges(false);
    setEditorMode('edit');
  };

  // When entering edit mode, set the contentEditable innerHTML ONCE — using the raw HTML
  // (with placeholders preserved). React's dangerouslySetInnerHTML would overwrite
  // user edits on every re-render, so we manage innerHTML manually.
  useEffect(() => {
    if (editorMode === 'edit' && editableRef.current) {
      editableRef.current.innerHTML = draftBody;
    }
    // Intentionally NOT depending on draftBody — only sync when entering edit mode
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editorMode, selectedTemplate]);

  const saveChanges = () => {
    // Grab the current edited HTML from the contentEditable element
    const liveHtml = editableRef.current?.innerHTML ?? draftBody;
    setMessageBody(liveHtml);
    setSubject(draftSubject);
    setDraftBody(liveHtml);
    setHasUnsavedChanges(false);
    setEditorMode('preview');
    toast.success('Changes saved');
  };

  const discardChanges = () => {
    setDraftBody(messageBody);
    setDraftSubject(subject);
    setHasUnsavedChanges(false);
    setEditorMode('preview');
    // Force re-sync of contentEditable next time it mounts
  };

  const loadData = async () => {
    try {
      const [contactsRes, groupsRes, templatesRes, settingsRes]: any[] = await Promise.all([
        axiosInstance.get('/contacts?pageSize=100'),
        axiosInstance.get('/contacts/groups'),
        axiosInstance.get('/templates'),
        axiosInstance.get('/settings/smtp').catch(() => ({ data: null })),
      ]);
      setContacts(contactsRes?.data || []);
      setGroups(groupsRes?.data || []);
      setTemplates(templatesRes?.data || []);
      if (settingsRes?.data) {
        setSignatureSettings({
          smtpFromName: settingsRes.data.smtpFromName,
          smtpFromEmail: settingsRes.data.smtpFromEmail,
          signatureDesignation: settingsRes.data.signatureDesignation,
          signaturePhone: settingsRes.data.signaturePhone,
          companyWebsite: settingsRes.data.companyWebsite,
          signatureImageUrl: settingsRes.data.signatureImageUrl,
        });
      }
      // For regular users, /settings/smtp returns nothing. Load /me/signature instead — it
      // returns the merged personal+org signature actually rendered into their emails.
      try {
        const meSig: any = await axiosInstance.get('/me/signature', { _silent: true } as any);
        if (meSig?.data) {
          setSignatureSettings(prev => ({
            ...prev,
            // From Name = the SENDER user's full name (not the org's static FromName)
            smtpFromName: meSig.data.fullName || prev.smtpFromName,
            smtpFromEmail: meSig.data.orgFromEmail || prev.smtpFromEmail,
            signatureDesignation: meSig.data.effectiveDesignation || prev.signatureDesignation,
            signaturePhone: meSig.data.effectivePhone || prev.signaturePhone,
            companyWebsite: meSig.data.orgCompanyWebsite || prev.companyWebsite,
            signatureImageUrl: meSig.data.effectiveImageUrl || prev.signatureImageUrl,
          }));
        }
      } catch { /* silent */ }
    } catch {
      toast.error('Failed to load data');
    }
  };

  // When group changes, fetch its first contact for preview personalization
  useEffect(() => {
    const fetchFirstContact = async () => {
      if (!selectedGroup) { setPreviewRecipient(null); return; }
      try {
        const res: any = await axiosInstance.get(`/contacts?groupId=${selectedGroup.id}&pageSize=1`, { _silent: true } as any);
        const items = Array.isArray(res?.data) ? res.data : (res?.data?.items || []);
        setPreviewRecipient(items[0] || null);
      } catch {
        setPreviewRecipient(null);
      }
    };
    fetchFirstContact();
  }, [selectedGroup]);

  // When individual contacts selected, use the first one as preview recipient
  useEffect(() => {
    if (sendTo === 'contacts' && selectedContacts.length > 0) {
      setPreviewRecipient(selectedContacts[0]);
    }
  }, [selectedContacts, sendTo]);

  // Refetch signature settings whenever window regains focus
  // (e.g. user updates settings in another tab and returns here)
  const refreshSignatureSettings = async () => {
    try {
      // Primary source for non-admins: /me/signature (merged personal + org)
      const me: any = await axiosInstance.get('/me/signature', { _silent: true } as any);
      if (me?.data) {
        setSignatureSettings({
          smtpFromName: me.data.fullName,
          smtpFromEmail: me.data.orgFromEmail,
          signatureDesignation: me.data.effectiveDesignation,
          signaturePhone: me.data.effectivePhone,
          companyWebsite: me.data.orgCompanyWebsite,
          signatureImageUrl: me.data.effectiveImageUrl,
        });
        return;
      }
    } catch { /* fall through */ }
    try {
      const res: any = await axiosInstance.get('/settings/smtp', { _silent: true } as any);
      if (res?.data) {
        setSignatureSettings({
          smtpFromName: res.data.smtpFromName,
          smtpFromEmail: res.data.smtpFromEmail,
          signatureDesignation: res.data.signatureDesignation,
          signaturePhone: res.data.signaturePhone,
          companyWebsite: res.data.companyWebsite,
          signatureImageUrl: res.data.signatureImageUrl,
        });
      }
    } catch { /* silently ignore */ }
  };

  useEffect(() => {
    const onFocus = () => refreshSignatureSettings();
    const onSignatureUpdated = () => refreshSignatureSettings();
    window.addEventListener('focus', onFocus);
    window.addEventListener('signature-updated', onSignatureUpdated);
    return () => {
      window.removeEventListener('focus', onFocus);
      window.removeEventListener('signature-updated', onSignatureUpdated);
    };
  }, []);

  const handleSend = async () => {
    if (!messageBody.trim()) {
      toast.error('Please enter a message');
      return;
    }
    if (sendTo === 'group' && !selectedGroup) {
      toast.error('Please select a contact group');
      return;
    }
    if (sendTo === 'contacts' && selectedContacts.length === 0) {
      toast.error('Please select at least one contact');
      return;
    }

    setSending(true);
    try {
      const silentConfig = { _silent: true } as any;

      // Step 1: Resolve template — REUSE existing one if unchanged, otherwise create a new one
      // (Previously this always created a new "Quick Send" template, polluting the user's
      // template list every time they sent the same campaign.)
      let templateId: string | undefined = selectedTemplate?.id;
      const templateBodyChanged = selectedTemplate && messageBody !== selectedTemplate.body;
      const templateSubjectChanged = selectedTemplate && channel === 'email' && subject !== (selectedTemplate.subject || '');
      const composedFromScratch = !selectedTemplate;

      // L1 — a WhatsApp media attachment also requires a (new) template to carry it.
      const whatsappMediaAttached = channel === 'whatsapp' && !!waMediaUrl;
      if (composedFromScratch || templateBodyChanged || templateSubjectChanged || whatsappMediaAttached) {
        // Need to create a new template. Use a friendlier name when based on an existing one.
        const name = selectedTemplate
          ? `${selectedTemplate.name} (edited ${new Date().toLocaleDateString()})`
          : `Quick Send - ${new Date().toLocaleString()}`;
        const templateRes: any = await axiosInstance.post('/templates', {
          name,
          channel,
          subject: channel === 'email' ? subject : null,
          body: messageBody,
          // L1 — WhatsApp media (null for email/sms or when no attachment)
          mediaUrl: whatsappMediaAttached ? waMediaUrl : null,
          mediaType: whatsappMediaAttached ? waMediaType : null,
          mediaFileName: whatsappMediaAttached ? waMediaFileName : null,
        }, silentConfig);
        templateId = templateRes?.data?.id;
      }

      if (!templateId) {
        toast.error('Failed to prepare message template. Please try again.');
        return;
      }

      // Step 2: Get or create group
      let groupId = selectedGroup?.id;

      if (sendTo === 'contacts' && selectedContacts.length > 0) {
        const groupRes: any = await axiosInstance.post('/contacts/groups', {
          name: `Quick Send Group - ${Date.now()}`,
          description: 'Auto-created for quick send',
          shareWithTeam: false,
        }, silentConfig);
        groupId = groupRes?.data?.id;

        if (groupId) {
          // Use the existing bulk-assign endpoint — POST /contacts/assign-group
          // (The previous POST /contacts/groups/{id}/contacts endpoint doesn't exist; that's why
          // the temp group ended up empty and "Campaign group has no contacts" was thrown.)
          try {
            await axiosInstance.post('/contacts/assign-group', {
              contactIds: selectedContacts.map(c => c.id),
              groupId,
            }, silentConfig);
          } catch (err: any) {
            toast.error('Failed to add selected contacts to the quick-send group.');
            return;
          }
        }
      }

      if (!groupId) {
        toast.error('No contact group selected. Please select a group or individual contacts.');
        return;
      }

      // Step 3: Create campaign
      // Name the campaign after its content so it's recognisable in the list:
      //  - email  -> the subject line
      //  - sms/wa -> first line of the body (HTML stripped, trimmed to 60 chars)
      //  - empty  -> fall back to the old timestamped name
      const bodyPreview = messageBody
        .replace(/<[^>]+>/g, ' ')
        .replace(/\s+/g, ' ')
        .trim()
        .slice(0, 60);
      const campaignName = (
        channel === 'email' && subject.trim()
          ? subject.trim()
          : bodyPreview || `Quick ${channel.toUpperCase()} - ${new Date().toLocaleString()}`
      ).slice(0, 150); // server caps the name at 150 chars
      const campaignRes: any = await axiosInstance.post('/campaigns', {
        name: campaignName,
        templateId,
        channel,
        groupId,
      }, silentConfig);

      const campaignId = campaignRes?.data?.id;
      if (!campaignId) {
        toast.error('Failed to create campaign. Please try again.');
        return;
      }

      // Step 4: Send campaign
      // Pass scheduledAt (ISO UTC) when the user picked a future time
      const sendBody = scheduleMode && scheduledAt
        ? { scheduledAt: new Date(scheduledAt).toISOString() }
        : {};
      await axiosInstance.post(`/campaigns/${campaignId}/send`, sendBody, silentConfig);

      // Show the inline live-progress card in the right panel — non-blocking
      setProgressCampaign({ id: campaignId, channel });
      toast.success(`Campaign queued — track progress on the right panel.`, { duration: 4000, icon: '🚀' });

      // Reset form (modal stays open with the live progress)
      setSubject('');
      setMessageBody('');
      setSelectedContacts([]);
      setSelectedGroup(null);
      setSelectedTemplate(null);
    } catch (err: any) {
      const msg = err?.response?.data?.message || err?.message || 'Failed to send message';
      toast.error(msg, { duration: 6000 });
    } finally {
      setSending(false);
    }
  };

  // Server-side contact search (individual mode). The page only pre-loads the first 100 contacts, so a
  // purely client-side filter misses anyone beyond that (e.g. contact #101). This queries the API as the
  // user types so search covers ALL contacts, scaling to thousands. Debounced 300ms.
  const [contactResults, setContactResults] = useState<Contact[]>([]);
  useEffect(() => {
    const term = contactSearch.trim();
    if (sendTo !== 'contacts' || !term) { setContactResults([]); return; }
    const t = setTimeout(async () => {
      try {
        const res: any = await axiosInstance.get(
          `/contacts?search=${encodeURIComponent(term)}&pageSize=20`, { _silent: true } as any);
        const items = Array.isArray(res?.data) ? res.data : (res?.data?.items || res?.data?.data || []);
        setContactResults(items);
      } catch { setContactResults([]); }
    }, 300);
    return () => clearTimeout(t);
  }, [contactSearch, sendTo]);

  // Prefer server results (cover all pages); fall back to the pre-loaded list if the search hasn't run yet.
  const filteredContacts = contactResults.length > 0
    ? contactResults
    : contacts.filter(c =>
        c.fullName?.toLowerCase().includes(contactSearch.toLowerCase()) ||
        c.email?.toLowerCase().includes(contactSearch.toLowerCase()) ||
        c.phone?.includes(contactSearch)
      );

  const channelTemplates = templates.filter(t => t.channel?.toLowerCase() === channel);

  const channelOptions = [
    { id: 'email' as Channel, label: 'Email', icon: Mail, color: 'blue', desc: 'HTML email with subject line' },
    { id: 'whatsapp' as Channel, label: 'WhatsApp', icon: MessageSquare, color: 'green', desc: 'Meta Cloud API message' },
    { id: 'sms' as Channel, label: 'SMS', icon: Smartphone, color: 'purple', desc: 'Text message via gateway' },
  ];

  const getContactDisplay = (c: Contact) => {
    if (channel === 'email') return c.email || 'No email';
    if (channel === 'whatsapp') return c.whatsAppNumber || 'No WhatsApp';
    return c.phone || 'No phone';
  };

  const inputClass = "w-full px-4 py-2.5 border border-gray-200 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none transition-all text-gray-700 bg-gray-50 focus:bg-white";

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <div className="p-2.5 bg-gradient-to-br from-blue-500 to-blue-700 rounded-xl">
          <Send className="w-6 h-6 text-white" />
        </div>
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Send Message</h1>
          <p className="text-gray-500 text-sm">Compose and send messages to your contacts</p>
        </div>
      </div>

      {/* M2 — pre-send transparency: provider + from-address + credential health */}
      <ActiveSenderBanner channel={channel} />

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {/* Left: Compose */}
        <div className="lg:col-span-2 space-y-5">
          {/* Step 1: Channel */}
          <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-5">
            <div className="flex items-center gap-2 mb-4">
              <div className="w-6 h-6 bg-blue-100 rounded-full flex items-center justify-center text-blue-700 font-bold text-xs">1</div>
              <h3 className="font-semibold text-gray-900">Select Channel</h3>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3">
              {channelOptions.map(opt => (
                <button
                  key={opt.id}
                  onClick={() => { setChannel(opt.id); setSelectedTemplate(null); }}
                  className={`p-4 rounded-xl border-2 transition-all text-left ${
                    channel === opt.id
                      ? `border-${opt.color}-500 bg-${opt.color}-50 shadow-sm`
                      : 'border-gray-100 hover:border-gray-200 bg-white'
                  }`}
                >
                  <opt.icon className={`w-6 h-6 mb-2 ${channel === opt.id ? `text-${opt.color}-600` : 'text-gray-400'}`} />
                  <p className={`font-semibold text-sm ${channel === opt.id ? `text-${opt.color}-900` : 'text-gray-700'}`}>{opt.label}</p>
                  <p className={`text-xs mt-0.5 ${channel === opt.id ? `text-${opt.color}-600` : 'text-gray-400'}`}>{opt.desc}</p>
                </button>
              ))}
            </div>
          </div>

          {/* Step 2: Recipients */}
          <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-5">
            <div className="flex items-center gap-2 mb-4">
              <div className="w-6 h-6 bg-blue-100 rounded-full flex items-center justify-center text-blue-700 font-bold text-xs">2</div>
              <h3 className="font-semibold text-gray-900">Select Recipients</h3>
            </div>

            {/* Toggle: Group / Individual */}
            <div className="flex gap-2 mb-4">
              <button
                onClick={() => setSendTo('group')}
                className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all ${
                  sendTo === 'group' ? 'bg-blue-100 text-blue-700' : 'bg-gray-50 text-gray-500 hover:bg-gray-100'
                }`}
              >
                <Users className="w-4 h-4" /> Contact Group
              </button>
              <button
                onClick={() => setSendTo('contacts')}
                className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all ${
                  sendTo === 'contacts' ? 'bg-blue-100 text-blue-700' : 'bg-gray-50 text-gray-500 hover:bg-gray-100'
                }`}
              >
                <User className="w-4 h-4" /> Individual Contacts
              </button>
            </div>

            {/* Group Picker */}
            {sendTo === 'group' && (
              <div className="relative">
                <button
                  onClick={() => setShowGroupDropdown(!showGroupDropdown)}
                  className={`${inputClass} flex items-center justify-between cursor-pointer`}
                >
                  <span className={selectedGroup ? 'text-gray-900' : 'text-gray-400'}>
                    {selectedGroup ? `${selectedGroup.name} (${selectedGroup.contactCount} contacts)` : 'Select a contact group...'}
                  </span>
                  <ChevronDown className="w-4 h-4 text-gray-400" />
                </button>
                {showGroupDropdown && (
                  <div className="absolute z-10 w-full mt-1 bg-white border border-gray-200 rounded-lg shadow-lg max-h-48 overflow-y-auto">
                    {groups.length === 0 ? (
                      <div className="p-3 text-sm text-gray-400 text-center">No groups found. Create one in Contacts page.</div>
                    ) : (
                      groups.map(g => (
                        <button
                          key={g.id}
                          onClick={() => { setSelectedGroup(g); setShowGroupDropdown(false); }}
                          className="w-full text-left px-4 py-2.5 hover:bg-blue-50 text-sm flex justify-between items-center"
                        >
                          <span className="font-medium text-gray-900">{g.name}</span>
                          <span className="text-xs text-gray-400">{g.contactCount} contacts</span>
                        </button>
                      ))
                    )}
                  </div>
                )}
              </div>
            )}

            {/* Contact Picker */}
            {sendTo === 'contacts' && (
              <div>
                {/* Selected contacts pills */}
                {selectedContacts.length > 0 && (
                  <div className="flex flex-wrap gap-2 mb-3">
                    {selectedContacts.map(c => (
                      <span key={c.id} className="flex items-center gap-1 bg-blue-100 text-blue-800 text-xs font-medium px-2.5 py-1 rounded-full">
                        {c.fullName}
                        <button onClick={() => setSelectedContacts(prev => prev.filter(x => x.id !== c.id))} className="hover:text-red-600">
                          <X className="w-3 h-3" />
                        </button>
                      </span>
                    ))}
                  </div>
                )}
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                  <input
                    type="text"
                    className={inputClass + ' pl-10'}
                    placeholder="Search contacts by name, email, or phone..."
                    value={contactSearch}
                    onChange={e => setContactSearch(e.target.value)}
                    onFocus={() => setShowContactDropdown(true)}
                  />
                  {showContactDropdown && contactSearch && (
                    <div className="absolute z-10 w-full mt-1 bg-white border border-gray-200 rounded-lg shadow-lg max-h-48 overflow-y-auto">
                      {filteredContacts.length === 0 ? (
                        <div className="p-3 text-sm text-gray-400 text-center">No contacts found</div>
                      ) : (
                        filteredContacts.slice(0, 10).map(c => (
                          <button
                            key={c.id}
                            onClick={() => {
                              if (!selectedContacts.find(x => x.id === c.id)) {
                                setSelectedContacts(prev => [...prev, c]);
                              }
                              setContactSearch('');
                              setShowContactDropdown(false);
                            }}
                            className="w-full text-left px-4 py-2.5 hover:bg-blue-50 text-sm"
                          >
                            <p className="font-medium text-gray-900">{c.fullName}</p>
                            <p className="text-xs text-gray-400">{getContactDisplay(c)}</p>
                          </button>
                        ))
                      )}
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>

          {/* Step 3: Compose */}
          <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-5">
            <div className="flex items-center justify-between mb-4">
              <div className="flex items-center gap-2">
                <div className="w-6 h-6 bg-blue-100 rounded-full flex items-center justify-center text-blue-700 font-bold text-xs">3</div>
                <h3 className="font-semibold text-gray-900">Compose Message</h3>
              </div>
              {/* Template picker */}
              <div className="relative">
                <button
                  onClick={() => setShowTemplateDropdown(!showTemplateDropdown)}
                  className="flex items-center gap-1.5 px-3 py-1.5 bg-amber-50 text-amber-700 rounded-lg text-xs font-medium hover:bg-amber-100 transition-colors"
                >
                  <Sparkles className="w-3.5 h-3.5" />
                  {selectedTemplate ? selectedTemplate.name : 'Use Template'}
                </button>
                {showTemplateDropdown && (
                  <div className="absolute right-0 z-10 w-64 mt-1 bg-white border border-gray-200 rounded-lg shadow-lg max-h-48 overflow-y-auto">
                    <button
                      onClick={() => { setSelectedTemplate(null); setSubject(''); setMessageBody(''); setShowTemplateDropdown(false); }}
                      className="w-full text-left px-4 py-2.5 hover:bg-gray-50 text-sm text-gray-500 border-b border-gray-100"
                    >
                      Clear template
                    </button>
                    {channelTemplates.length === 0 ? (
                      <div className="p-3 text-sm text-gray-400 text-center">No {channel} templates found</div>
                    ) : (
                      channelTemplates.map(t => (
                        <button
                          key={t.id}
                          onClick={() => { setSelectedTemplate(t); setShowTemplateDropdown(false); }}
                          className="w-full text-left px-4 py-2.5 hover:bg-blue-50 text-sm"
                        >
                          <p className="font-medium text-gray-900">{t.name}</p>
                          {t.subject && <p className="text-xs text-gray-400 truncate">{t.subject}</p>}
                        </button>
                      ))
                    )}
                  </div>
                )}
              </div>
            </div>

            {/* === EMAIL with TEMPLATE selected — Smart preview/edit/html modes === */}
            {channel === 'email' && selectedTemplate && (
              <>
                {/* Subject — editable in edit mode, read-only in preview */}
                <div className="mb-4">
                  <label className="block text-sm font-medium text-gray-700 mb-1.5">Subject *</label>
                  <input
                    type="text"
                    className={inputClass + (editorMode !== 'edit' ? ' opacity-90' : '')}
                    placeholder="Enter email subject..."
                    value={editorMode === 'edit' ? draftSubject : subject}
                    onChange={e => {
                      if (editorMode === 'edit') {
                        setDraftSubject(e.target.value);
                        setHasUnsavedChanges(true);
                      } else {
                        setSubject(e.target.value);
                      }
                    }}
                    readOnly={editorMode === 'preview'}
                  />
                  {previewRecipient && subject && subject.includes('{{') && editorMode === 'preview' && (
                    <p className="text-xs text-blue-600 mt-1.5 italic">
                      📨 Recipient sees: &ldquo;{renderWithSampleData(subject)}&rdquo;
                    </p>
                  )}
                </div>

                {/* Editor mode toolbar */}
                <div className="flex items-center justify-between mb-2">
                  <label className="block text-sm font-medium text-gray-700">Email Body *</label>
                  <div className="inline-flex bg-gray-100 rounded-lg p-0.5">
                    <button
                      type="button"
                      onClick={() => editorMode === 'edit' && hasUnsavedChanges ? null : setEditorMode('preview')}
                      className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium transition-all ${
                        editorMode === 'preview' ? 'bg-white shadow-sm text-blue-700' : 'text-gray-500 hover:text-gray-700'
                      }`}
                    >
                      <Eye className="w-3.5 h-3.5" /> Preview
                    </button>
                    <button
                      type="button"
                      onClick={startEditing}
                      className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium transition-all ${
                        editorMode === 'edit' ? 'bg-white shadow-sm text-blue-700' : 'text-gray-500 hover:text-gray-700'
                      }`}
                    >
                      <Edit3 className="w-3.5 h-3.5" /> Edit
                    </button>
                    <button
                      type="button"
                      onClick={() => setEditorMode('html')}
                      className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium transition-all ${
                        editorMode === 'html' ? 'bg-white shadow-sm text-blue-700' : 'text-gray-500 hover:text-gray-700'
                      }`}
                    >
                      <Code2 className="w-3.5 h-3.5" /> HTML
                    </button>
                  </div>
                </div>

                {/* PREVIEW MODE — sandboxed iframe rendering */}
                {editorMode === 'preview' && (
                  <div className="border border-gray-200 rounded-lg overflow-hidden bg-gray-50">
                    <div className="px-3 py-2 bg-gray-100 border-b border-gray-200 flex items-center justify-between text-xs text-gray-500">
                      <span className="flex items-center gap-1.5">
                        <Eye className="w-3 h-3" /> Rendered Preview
                        {previewRecipient ? (
                          <span className="text-gray-700 font-medium ml-1">
                            — showing as <span className="text-blue-700">{previewRecipient.fullName}</span>
                            <span className="text-gray-400"> ({previewRecipient.email})</span>
                          </span>
                        ) : (
                          <span className="text-gray-400 ml-1">— sample data (select a group to preview real recipient)</span>
                        )}
                      </span>
                      <div className="flex items-center gap-3">
                        <button
                          onClick={async () => {
                            await refreshSignatureSettings();
                            toast.success('Signature refreshed from settings');
                          }}
                          className="flex items-center gap-1 text-gray-500 hover:text-gray-700 font-medium"
                          title="Reload signature from your saved Settings"
                        >
                          <RefreshCw className="w-3 h-3" /> Refresh Signature
                        </button>
                        <button
                          onClick={startEditing}
                          className="flex items-center gap-1 text-blue-600 hover:text-blue-800 font-medium"
                        >
                          <Edit3 className="w-3 h-3" /> Edit Content
                        </button>
                      </div>
                    </div>
                    <iframe
                      // Force remount whenever the template OR body content changes —
                      // React's srcDoc update is unreliable on first mount, so we use a stable key
                      // derived from the template id + a content hash. This guarantees the iframe
                      // renders the latest content immediately after template selection.
                      key={`${selectedTemplate?.id ?? 'none'}-${messageBody.length}-${previewRecipient?.id ?? 'no-recipient'}`}
                      title="Email Body Preview"
                      srcDoc={renderWithSampleData(messageBody)}
                      sandbox=""
                      className="w-full bg-white h-[400px] md:h-[600px]"
                      style={{ border: 'none' }}
                    />
                    {(!signatureSettings.smtpFromName || !signatureSettings.signatureDesignation) && (
                      <div className="px-3 py-2 bg-amber-50 border-t border-amber-100 text-xs text-amber-700 flex items-center gap-1.5">
                        💡 Signature not configured fully. Go to <strong>Settings → Signature</strong> to customize your professional signature.
                      </div>
                    )}
                  </div>
                )}

                {/* EDIT MODE — contentEditable WYSIWYG */}
                {editorMode === 'edit' && (
                  <div className="border-2 border-blue-200 rounded-lg overflow-hidden bg-white">
                    <div className="px-3 py-2 bg-blue-50 border-b border-blue-200 flex items-center justify-between text-xs">
                      <span className="flex items-center gap-1.5 text-blue-700 font-medium">
                        <Edit3 className="w-3 h-3" /> Edit Mode {hasUnsavedChanges && <span className="text-amber-600">• Unsaved changes</span>}
                      </span>
                      <div className="flex items-center gap-2">
                        <button
                          onClick={discardChanges}
                          className="flex items-center gap-1 px-2 py-1 text-gray-600 hover:bg-white rounded text-xs"
                        >
                          <RefreshCw className="w-3 h-3" /> Discard
                        </button>
                        <button
                          onClick={saveChanges}
                          className="flex items-center gap-1 px-2.5 py-1 bg-blue-600 text-white rounded hover:bg-blue-700 text-xs font-medium"
                        >
                          <Save className="w-3 h-3" /> Save Changes
                        </button>
                      </div>
                    </div>
                    <div
                      ref={editableRef}
                      contentEditable
                      suppressContentEditableWarning
                      onInput={(e) => { normalizeEditorImages(e.currentTarget as HTMLDivElement); setHasUnsavedChanges(true); }}
                      className="p-4 max-h-[480px] overflow-y-auto focus:outline-none prose prose-sm max-w-none"
                      /* innerHTML is set imperatively in useEffect — see startEditing */
                    />
                    <div className="px-3 py-2 bg-amber-50 border-t border-amber-100 text-xs text-amber-700 flex items-center gap-1.5">
                      <Sparkles className="w-3 h-3" /> Tip: Placeholders like <code className="bg-amber-100 px-1 rounded mx-0.5">{'{{first_name}}'}</code> are preserved — edit the surrounding text. Click <strong>Save Changes</strong> to apply.
                    </div>
                  </div>
                )}

                {/* HTML MODE — raw code editor for power users */}
                {editorMode === 'html' && (
                  <div>
                    <textarea
                      className={inputClass + ' min-h-[400px] font-mono text-xs leading-relaxed'}
                      value={messageBody}
                      onChange={e => setMessageBody(e.target.value)}
                    />
                    <p className="text-xs text-gray-500 mt-2">⚠️ Raw HTML mode — edit carefully. Changes save automatically.</p>
                  </div>
                )}
              </>
            )}

            {/* === EMAIL without template OR non-email channel === */}
            {(channel !== 'email' || !selectedTemplate) && (
              <>
                {channel === 'email' && (
                  <div className="mb-4">
                    <label className="block text-sm font-medium text-gray-700 mb-1.5">Subject *</label>
                    <input type="text" className={inputClass} placeholder="Enter email subject..." value={subject} onChange={e => setSubject(e.target.value)} />
                  </div>
                )}

                {/* Email: smart Rich/HTML compose. Non-email: plain textarea. */}
                {channel === 'email' ? (
                  <div>
                    <div className="flex items-center justify-between mb-2">
                      <label className="block text-sm font-medium text-gray-700">Email Body *</label>
                      <div className="inline-flex bg-gray-100 rounded-lg p-0.5">
                        <button
                          type="button"
                          onClick={() => setComposeMode('rich')}
                          className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium transition-all ${
                            composeMode === 'rich' ? 'bg-white shadow-sm text-blue-700' : 'text-gray-500'
                          }`}
                        >
                          <Edit3 className="w-3.5 h-3.5" /> Rich
                        </button>
                        <button
                          type="button"
                          onClick={() => setComposeMode('html')}
                          className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium transition-all ${
                            composeMode === 'html' ? 'bg-white shadow-sm text-blue-700' : 'text-gray-500'
                          }`}
                        >
                          <Code2 className="w-3.5 h-3.5" /> HTML
                        </button>
                      </div>
                    </div>

                    {composeMode === 'rich' ? (
                      <>
                        <div
                          ref={richComposeRef}
                          contentEditable
                          suppressContentEditableWarning
                          // Preserve HTML formatting on paste (Gmail, Outlook etc. send rich clipboard data)
                          onPaste={(e) => {
                            const html = e.clipboardData.getData('text/html');
                            if (html) {
                              e.preventDefault();
                              document.execCommand('insertHTML', false, html);
                            }
                            // else: default plain-text paste behaviour kicks in
                            // Cap any pasted images so they never blow up the layout.
                            setTimeout(() => {
                              normalizeEditorImages(richComposeRef.current);
                              setMessageBody(richComposeRef.current?.innerHTML ?? '');
                            }, 0);
                          }}
                          onInput={(e) => { normalizeEditorImages(e.currentTarget as HTMLDivElement); setMessageBody((e.currentTarget as HTMLDivElement).innerHTML); }}
                          className="min-h-[260px] max-h-[480px] overflow-y-auto px-4 py-3 border border-gray-200 rounded-xl bg-gray-50 focus:bg-white focus:ring-2 focus:ring-blue-500 focus:border-transparent outline-none prose prose-sm max-w-none"
                          data-placeholder="Paste your formatted email here, or type a new one. Formatting is preserved."
                        />
                        <p className="text-xs text-gray-500 mt-2">
                          💡 <strong>Rich mode</strong> — paste formatted content from any email/document and the styling is kept.
                          Switch to <strong>HTML</strong> if you want to inspect or hand-edit the raw markup.
                        </p>
                      </>
                    ) : (
                      <>
                        <textarea
                          className={inputClass + ' min-h-[260px] font-mono text-xs leading-relaxed'}
                          placeholder={'<h2>Hello {{name}},</h2>\n<p>Your message here...</p>'}
                          value={messageBody}
                          onChange={e => setMessageBody(e.target.value)}
                        />
                        <p className="text-xs text-gray-500 mt-2">
                          ⚠️ Raw HTML — edit carefully. Switch back to <strong>Rich</strong> for visual editing.
                        </p>
                      </>
                    )}
                  </div>
                ) : (
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1.5">Message *</label>
                    <textarea
                      className={inputClass + ' min-h-[200px] text-sm'}
                      placeholder="Hi {{name}}, your message here..."
                      value={messageBody}
                      onChange={e => setMessageBody(e.target.value)}
                    />

                    {/* L1 — WhatsApp media attachment (image / document / video) */}
                    {channel === 'whatsapp' && (
                      <div className="mt-3 border border-dashed border-gray-300 rounded-lg p-3 bg-gray-50">
                        {!waMediaUrl ? (
                          <label className="flex items-center gap-2 cursor-pointer text-sm text-gray-600 hover:text-blue-600">
                            <input
                              type="file"
                              className="hidden"
                              accept=".png,.jpg,.jpeg,.webp,.mp4,.3gp,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt"
                              disabled={waMediaUploading}
                              onChange={e => { const f = e.target.files?.[0]; if (f) uploadWhatsAppMedia(f); e.target.value = ''; }}
                            />
                            {waMediaUploading ? (
                              <><Loader2 className="w-4 h-4 animate-spin" /> Uploading…</>
                            ) : (
                              <><Paperclip className="w-4 h-4" /> Attach media (image / PDF / video) — optional</>
                            )}
                          </label>
                        ) : (
                          <div className="flex items-center gap-3">
                            {waMediaType === 'image' ? (
                              <img src={waMediaUrl} alt="preview" className="w-16 h-16 object-cover rounded-md border" />
                            ) : waMediaType === 'video' ? (
                              <div className="w-16 h-16 rounded-md border bg-gray-900 flex items-center justify-center text-white">
                                <Smartphone className="w-6 h-6" />
                              </div>
                            ) : (
                              <div className="w-16 h-16 rounded-md border bg-red-50 flex flex-col items-center justify-center text-red-600">
                                <FileText className="w-6 h-6" />
                                <span className="text-[10px] font-semibold uppercase mt-0.5">
                                  {(waMediaFileName?.split('.').pop() || 'doc').toUpperCase()}
                                </span>
                              </div>
                            )}
                            <div className="flex-1 min-w-0">
                              <p className="text-sm font-medium text-gray-800 truncate">{waMediaFileName || waMediaUrl}</p>
                              <p className="text-xs text-gray-500">
                                {waMediaType}{waMediaSize ? ` • ${formatBytes(waMediaSize)}` : ''} • caption = message text
                              </p>
                            </div>
                            <button type="button" onClick={clearWhatsAppMedia} className="text-red-500 hover:text-red-700 text-sm font-medium">Remove</button>
                          </div>
                        )}
                        <p className="text-xs text-gray-400 mt-2">Max 16 MB. The message text becomes the media caption.</p>
                      </div>
                    )}
                  </div>
                )}
              </>
            )}

            <div className="flex items-center gap-4 mt-3">
              <p className="text-xs text-gray-400">
                Available placeholders: <code className="bg-gray-100 px-1 rounded">{'{{name}}'}</code> <code className="bg-gray-100 px-1 rounded">{'{{email}}'}</code> <code className="bg-gray-100 px-1 rounded">{'{{phone}}'}</code> <code className="bg-gray-100 px-1 rounded">{'{{sender_name}}'}</code> <code className="bg-gray-100 px-1 rounded">{'{{company_website}}'}</code>
              </p>
            </div>
          </div>

          {/* Schedule toggle + date picker */}
          <div className="bg-white rounded-xl border border-gray-100 p-3 flex flex-wrap items-center gap-3">
            <div className="inline-flex bg-gray-100 rounded-lg p-0.5">
              <button
                type="button"
                onClick={() => setScheduleMode(false)}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium transition-all ${
                  !scheduleMode ? 'bg-white shadow-sm text-blue-700' : 'text-gray-500 hover:text-gray-700'
                }`}
              >
                <Send className="w-3.5 h-3.5" /> Send now
              </button>
              <button
                type="button"
                onClick={() => setScheduleMode(true)}
                className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium transition-all ${
                  scheduleMode ? 'bg-white shadow-sm text-blue-700' : 'text-gray-500 hover:text-gray-700'
                }`}
              >
                <Clock className="w-3.5 h-3.5" /> Schedule
              </button>
            </div>
            {scheduleMode && (
              <>
                <input
                  type="datetime-local"
                  value={scheduledAt}
                  onChange={e => setScheduledAt(e.target.value)}
                  min={new Date(Date.now() + 60_000).toISOString().slice(0, 16)}
                  className="px-3 py-1.5 text-sm border border-gray-200 rounded-lg bg-gray-50 focus:bg-white focus:ring-2 focus:ring-blue-500 outline-none"
                />
                {scheduledAt && (
                  <span className="text-xs text-blue-700">
                    Will send {new Date(scheduledAt).toLocaleString()} ({Math.max(0, Math.round((new Date(scheduledAt).getTime() - Date.now()) / 60000))} min from now)
                  </span>
                )}
              </>
            )}
          </div>

          {/* Send Button */}
          <div className="flex items-center gap-3">
            <button
              onClick={() => {
                if (editorMode === 'edit' && hasUnsavedChanges) {
                  toast.error('You have unsaved changes. Save or discard them before sending.');
                  return;
                }
                if (scheduleMode && (!scheduledAt || new Date(scheduledAt).getTime() <= Date.now())) {
                  toast.error('Please pick a future date/time before scheduling.');
                  return;
                }
                handleSend();
              }}
              disabled={sending || !messageBody.trim() || (editorMode === 'edit' && hasUnsavedChanges)}
              className="flex items-center gap-2 px-8 py-3 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl hover:from-blue-700 hover:to-blue-800 transition-all font-semibold shadow-lg shadow-blue-200 disabled:opacity-50 disabled:cursor-not-allowed"
              title={editorMode === 'edit' && hasUnsavedChanges ? 'Save your changes first' : ''}
            >
              {sending ? <Loader2 className="w-5 h-5 animate-spin" /> : scheduleMode ? <Clock className="w-5 h-5" /> : <Send className="w-5 h-5" />}
              {sending ? (scheduleMode ? 'Scheduling...' : 'Sending...') : (scheduleMode ? `Schedule ${channel.toUpperCase()}` : `Send ${channel.toUpperCase()}`)}
            </button>
            {editorMode === 'edit' && hasUnsavedChanges && (
              <span className="flex items-center gap-1 text-sm text-amber-600 font-medium">
                <Edit3 className="w-4 h-4" /> Unsaved changes — save them first
              </span>
            )}
          </div>
        </div>

        {/* Right: Info & Preview */}
        <div className="space-y-5">
          {/* Live Send Progress — appears at top while/after sending */}
          {progressCampaign && (
            <SendProgressCard
              campaignId={progressCampaign.id}
              channel={progressCampaign.channel}
              onDismiss={() => setProgressCampaign(null)}
            />
          )}

          {/* Quick Stats */}
          <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-5">
            <h3 className="font-semibold text-gray-900 mb-3">Send Summary</h3>
            <div className="space-y-3">
              <div className="flex justify-between text-sm">
                <span className="text-gray-500">Channel</span>
                <span className="font-medium text-gray-900 capitalize">{channel}</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500">Recipients</span>
                <span className="font-medium text-gray-900">
                  {sendTo === 'group'
                    ? selectedGroup ? `${selectedGroup.contactCount} contacts` : 'None selected'
                    : `${selectedContacts.length} contacts`
                  }
                </span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-gray-500">Template</span>
                <span className="font-medium text-gray-900">{selectedTemplate?.name || 'Custom'}</span>
              </div>
              {channel === 'email' && (
                <div className="flex justify-between text-sm">
                  <span className="text-gray-500">Subject</span>
                  <span className="font-medium text-gray-900 truncate ml-4">{subject || '—'}</span>
                </div>
              )}
            </div>
          </div>

          {/* Preview */}
          {showPreview && (
            <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-5">
              <h3 className="font-semibold text-gray-900 mb-3 flex items-center gap-2">
                <Eye className="w-4 h-4" /> Preview
              </h3>
              {channel === 'email' ? (
                <div className="border rounded-lg p-4 bg-gray-50">
                  <p className="text-xs text-gray-400 mb-1">Subject: {subject || '(no subject)'}</p>
                  <div className="border-t pt-2 mt-2" dangerouslySetInnerHTML={{
                    __html: messageBody
                      .replace(/\{\{name\}\}/g, 'John Doe')
                      .replace(/\{\{email\}\}/g, 'john@example.com')
                      .replace(/\{\{phone\}\}/g, '+1234567890')
                  }} />
                </div>
              ) : (
                <div className="bg-green-50 rounded-2xl rounded-tl-none p-4 max-w-xs">
                  <p className="text-sm text-gray-800 whitespace-pre-wrap">
                    {messageBody
                      .replace(/\{\{name\}\}/g, 'John Doe')
                      .replace(/\{\{email\}\}/g, 'john@example.com')
                      .replace(/\{\{phone\}\}/g, '+1234567890')
                    }
                  </p>
                  <p className="text-[10px] text-gray-400 mt-1 text-right">
                    {new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                  </p>
                </div>
              )}
            </div>
          )}

          {/* Channel Info */}
          <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-5">
            <h3 className="font-semibold text-gray-900 mb-3">Channel Info</h3>
            {channel === 'email' && (
              <div className="space-y-2 text-sm text-gray-600">
                <p>Supports: SMTP, SendGrid, Brevo, Mailgun</p>
                <p>HTML formatting supported</p>
                <p>Configure in Settings &rarr; Email SMTP</p>
              </div>
            )}
            {channel === 'whatsapp' && (
              <div className="space-y-2 text-sm text-gray-600">
                <p>Meta WhatsApp Cloud API</p>
                <p>1,000 conversations/month FREE</p>
                <p>Configure in Settings &rarr; WhatsApp</p>
              </div>
            )}
            {channel === 'sms' && (
              <div className="space-y-2 text-sm text-gray-600">
                <p>Twilio SMS Gateway</p>
                <p>Pay-per-message pricing</p>
                <p>Configure in Settings &rarr; SMS</p>
              </div>
            )}
          </div>

          {/* L1 polish — live WhatsApp preview (recipient's-eye view) */}
          {channel === 'whatsapp' && (
            <WhatsAppPreview
              message={messageBody}
              mediaUrl={waMediaUrl}
              mediaType={waMediaType}
              mediaFileName={waMediaFileName}
              mediaSizeLabel={formatBytes(waMediaSize)}
              sample={previewRecipient ? { name: previewRecipient.fullName, email: previewRecipient.email, phone: previewRecipient.whatsAppNumber || previewRecipient.phone } : null}
            />
          )}
        </div>
      </div>

    </div>
  );
}
