import { useState, useEffect } from 'react';
import { Mail, MessageSquare, Smartphone, Send, Users, User, FileText, Eye, Loader2, CheckCircle, ChevronDown, Search, X, Sparkles } from 'lucide-react';
import toast from 'react-hot-toast';
import axiosInstance from '../../api/axiosInstance';

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

export default function SendMessagePage() {
  const [channel, setChannel] = useState<Channel>('email');
  const [sendTo, setSendTo] = useState<'contacts' | 'group'>('group');
  const [selectedContacts, setSelectedContacts] = useState<Contact[]>([]);
  const [selectedGroup, setSelectedGroup] = useState<Group | null>(null);
  const [selectedTemplate, setSelectedTemplate] = useState<Template | null>(null);
  const [subject, setSubject] = useState('');
  const [messageBody, setMessageBody] = useState('');
  const [showPreview, setShowPreview] = useState(false);
  const [sending, setSending] = useState(false);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [groups, setGroups] = useState<Group[]>([]);
  const [templates, setTemplates] = useState<Template[]>([]);
  const [contactSearch, setContactSearch] = useState('');
  const [showContactDropdown, setShowContactDropdown] = useState(false);
  const [showGroupDropdown, setShowGroupDropdown] = useState(false);
  const [showTemplateDropdown, setShowTemplateDropdown] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  useEffect(() => {
    if (selectedTemplate) {
      setSubject(selectedTemplate.subject || '');
      setMessageBody(selectedTemplate.body);
    }
  }, [selectedTemplate]);

  const loadData = async () => {
    try {
      const [contactsRes, groupsRes, templatesRes]: any[] = await Promise.all([
        axiosInstance.get('/contacts?pageSize=100'),
        axiosInstance.get('/contacts/groups'),
        axiosInstance.get('/templates'),
      ]);
      setContacts(contactsRes?.data || []);
      setGroups(groupsRes?.data || []);
      setTemplates(templatesRes?.data || []);
    } catch {
      toast.error('Failed to load data');
    }
  };

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

      // Step 1: Create template
      const templateRes: any = await axiosInstance.post('/templates', {
        name: `Quick Send - ${new Date().toLocaleString()}`,
        channel,
        subject: channel === 'email' ? subject : null,
        body: messageBody,
      }, silentConfig);

      const templateId = templateRes?.data?.id;
      if (!templateId) {
        toast.error('Failed to create message template. Please try again.');
        return;
      }

      // Step 2: Get or create group
      let groupId = selectedGroup?.id;

      if (sendTo === 'contacts' && selectedContacts.length > 0) {
        const groupRes: any = await axiosInstance.post('/contacts/groups', {
          name: `Quick Send Group - ${Date.now()}`,
          description: 'Auto-created for quick send',
        }, silentConfig);
        groupId = groupRes?.data?.id;

        // Add selected contacts to temp group
        if (groupId) {
          for (const contact of selectedContacts) {
            try {
              await axiosInstance.post(`/contacts/groups/${groupId}/contacts`, {
                contactId: contact.id,
              }, silentConfig);
            } catch {
              // Contact may already be in group
            }
          }
        }
      }

      if (!groupId) {
        toast.error('No contact group selected. Please select a group or individual contacts.');
        return;
      }

      // Step 3: Create campaign
      const campaignRes: any = await axiosInstance.post('/campaigns', {
        name: `Quick ${channel.toUpperCase()} - ${new Date().toLocaleString()}`,
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
      await axiosInstance.post(`/campaigns/${campaignId}/send`, {}, silentConfig);

      toast.success(`${channel.toUpperCase()} campaign queued successfully! You'll be notified when complete.`, { duration: 5000 });

      // Reset form
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

  const filteredContacts = contacts.filter(c =>
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

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left: Compose */}
        <div className="lg:col-span-2 space-y-5">
          {/* Step 1: Channel */}
          <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-5">
            <div className="flex items-center gap-2 mb-4">
              <div className="w-6 h-6 bg-blue-100 rounded-full flex items-center justify-center text-blue-700 font-bold text-xs">1</div>
              <h3 className="font-semibold text-gray-900">Select Channel</h3>
            </div>
            <div className="grid grid-cols-3 gap-3">
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

            {channel === 'email' && (
              <div className="mb-4">
                <label className="block text-sm font-medium text-gray-700 mb-1.5">Subject *</label>
                <input type="text" className={inputClass} placeholder="Enter email subject..." value={subject} onChange={e => setSubject(e.target.value)} />
              </div>
            )}

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1.5">
                {channel === 'email' ? 'Email Body (HTML supported)' : 'Message'} *
              </label>
              <textarea
                className={inputClass + ' min-h-[200px] font-mono text-sm'}
                placeholder={channel === 'email'
                  ? '<h2>Hello {{name}},</h2>\n<p>Your personalized message here...</p>'
                  : 'Hi {{name}}, your message here...'
                }
                value={messageBody}
                onChange={e => setMessageBody(e.target.value)}
              />
              <div className="flex items-center gap-4 mt-2">
                <p className="text-xs text-gray-400">Available placeholders: <code className="bg-gray-100 px-1 rounded">{'{{name}}'}</code> <code className="bg-gray-100 px-1 rounded">{'{{email}}'}</code> <code className="bg-gray-100 px-1 rounded">{'{{phone}}'}</code></p>
              </div>
            </div>
          </div>

          {/* Send Button */}
          <div className="flex items-center gap-3">
            <button
              onClick={handleSend}
              disabled={sending || !messageBody.trim()}
              className="flex items-center gap-2 px-8 py-3 bg-gradient-to-r from-blue-600 to-blue-700 text-white rounded-xl hover:from-blue-700 hover:to-blue-800 transition-all font-semibold shadow-lg shadow-blue-200 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {sending ? <Loader2 className="w-5 h-5 animate-spin" /> : <Send className="w-5 h-5" />}
              {sending ? 'Sending...' : `Send ${channel.toUpperCase()}`}
            </button>
            <button
              onClick={() => setShowPreview(!showPreview)}
              className="flex items-center gap-2 px-5 py-3 bg-white border border-gray-200 text-gray-700 rounded-xl hover:bg-gray-50 transition-all font-medium"
            >
              <Eye className="w-4 h-4" />
              Preview
            </button>
          </div>
        </div>

        {/* Right: Info & Preview */}
        <div className="space-y-5">
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
        </div>
      </div>
    </div>
  );
}
