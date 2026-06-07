import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  PenLine, Loader2, Sparkles, Copy, Lock, Unlock, RefreshCw, Pencil, Check,
  Image as ImageIcon, Save, Send, Wand2, Link2, ChevronDown, RotateCcw,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { creativesApi, type MarketingContent, type ImageSizeOption } from '../../api/creativesApi';
import { templateApi } from '../../api/templateApi';
import { ChannelPreview, ChannelLogo, CHANNEL_META } from '../../components/creatives/ChannelPreview';

type ChannelId = 'whatsapp' | 'instagram' | 'facebook' | 'email';

interface Version {
  text: string; subject?: string; preview?: string; statusText?: string;
  reelsHook?: string; storyCta?: string; headline?: string; cta?: string; shared?: boolean;
}

const ORDER: ChannelId[] = ['whatsapp', 'instagram', 'facebook', 'email'];
const GROUP_LETTERS = ['A', 'B', 'C', 'D'];
const GBADGE: Record<string, string> = {
  A: 'from-indigo-500 to-indigo-700', B: 'from-green-500 to-emerald-600',
  C: 'from-pink-500 to-purple-600', D: 'from-amber-500 to-orange-600',
};
const SUB: Record<ChannelId, string> = { whatsapp: 'Broadcast + status', instagram: 'Caption + hashtags', facebook: 'Post + headline', email: 'Subject + HTML body' };
const EXAMPLE = '5BR Villa at The Oasis by Emaar, Dubailand. AED 16,500,000. Private pool, rooftop terrace, smart home, Italian marble. 5 min to Global Village. Handover Q4 2026. Agent: Ahmed, +971 50 123 4567.';

const PRESETS: { id: string; label: string; desc: string; group: Record<ChannelId, string> }[] = [
  { id: 'sep', label: 'All separate', desc: 'Tailored copy per channel', group: { whatsapp: 'A', instagram: 'B', facebook: 'C', email: 'D' } },
  { id: 'igfb', label: 'Instagram + Facebook', desc: 'One social post for both', group: { whatsapp: 'A', instagram: 'B', facebook: 'B', email: 'D' } },
  { id: 'fbwa', label: 'Facebook + WhatsApp', desc: 'Shared copy', group: { whatsapp: 'A', instagram: 'B', facebook: 'A', email: 'D' } },
  { id: 'social', label: 'All social together', desc: 'One post for IG + FB + WhatsApp', group: { whatsapp: 'A', instagram: 'A', facebook: 'A', email: 'D' } },
];

export default function ContentStudioPage({ embedded = false, onSendToBanner }: { embedded?: boolean; onSendToBanner?: (prompt: string) => void } = {}) {
  const navigate = useNavigate();
  const [mode, setMode] = useState<'simple' | 'guided'>('simple');
  const [gstep, setGstep] = useState(1);

  const [brief, setBrief] = useState('');
  const [name, setName] = useState('');
  const [brandName, setBrandName] = useState('');
  const [promptBusy, setPromptBusy] = useState(false);
  const [enabled, setEnabled] = useState<Record<ChannelId, boolean>>({ whatsapp: true, instagram: true, facebook: true, email: true });
  const [group, setGroup] = useState<Record<ChannelId, string>>({ whatsapp: 'A', instagram: 'B', facebook: 'B', email: 'D' });
  const [presetId, setPresetId] = useState('igfb');
  const [combineOpen, setCombineOpen] = useState(false);
  const [advanced, setAdvanced] = useState(false);

  const [busy, setBusy] = useState(false);
  const [provider, setProvider] = useState('');
  const [versions, setVersions] = useState<Partial<Record<ChannelId, Version[]>>>({});
  const [current, setCurrent] = useState<Partial<Record<ChannelId, number>>>({});
  const [locked, setLocked] = useState<Partial<Record<ChannelId, boolean>>>({});
  const [vw, setVw] = useState<Partial<Record<ChannelId, 'copy' | 'preview'>>>({});
  const [regen, setRegen] = useState<Partial<Record<ChannelId, boolean>>>({});
  const [editing, setEditing] = useState<ChannelId | null>(null);
  const [editText, setEditText] = useState('');
  const [editSubject, setEditSubject] = useState('');
  const [savedIds, setSavedIds] = useState<Partial<Record<ChannelId, string>>>({});
  const [groupId, setGroupId] = useState('');

  const [imagePrompt, setImagePrompt] = useState('');
  const [heroImageUrl, setHeroImageUrl] = useState<string | null>(null);
  const [imgBusy, setImgBusy] = useState(false);
  const [sizes, setSizes] = useState<ImageSizeOption[]>([]);
  const [size, setSize] = useState('1024x1024');
  const [saving, setSaving] = useState(false);

  useEffect(() => { creativesApi.sizes().then((r: any) => setSizes(r.data || [])).catch(() => {}); }, []);

  const enabledList = ORDER.filter((c) => enabled[c]);
  const groupsMap: Record<string, ChannelId[]> = {};
  enabledList.forEach((c) => { (groupsMap[group[c]] ||= []).push(c); });
  const leadOf = (c: ChannelId) => groupsMap[group[c]][0];
  const distinctGroups = Object.keys(groupsMap);
  const hasResult = Object.keys(versions).length > 0;

  const extract = (c: ChannelId, d: MarketingContent): Version => {
    if (c === 'whatsapp') return { text: d.whatsApp?.broadcast || '', statusText: d.whatsApp?.statusText };
    if (c === 'instagram') return { text: d.instagram?.caption || '', reelsHook: d.instagram?.reelsHook, storyCta: d.instagram?.storyCta };
    if (c === 'facebook') return { text: d.facebook?.post || '', headline: d.facebook?.headline, cta: d.facebook?.cta };
    return { text: d.email?.body || '', subject: d.email?.subject, preview: d.email?.preview };
  };

  const toggleChannel = (c: ChannelId) => setEnabled((p) => ({ ...p, [c]: !p[c] }));
  const applyPreset = (p: typeof PRESETS[number]) => { setPresetId(p.id); setGroup(p.group); };
  const cycleGroup = (c: ChannelId) => { setPresetId('custom'); setGroup((p) => ({ ...p, [c]: GROUP_LETTERS[(GROUP_LETTERS.indexOf(p[c]) + 1) % GROUP_LETTERS.length] })); };

  const generate = async () => {
    if (brief.trim().length < 20) { toast.error('Please add a longer brief (min 20 chars)'); return; }
    if (enabledList.length === 0) { toast.error('Switch on at least one channel'); return; }
    setBusy(true);
    try {
      const leads = Array.from(new Set(enabledList.map(leadOf)));
      const env: any = await creativesApi.content(brief.trim(), leads);
      const data: MarketingContent = env.data;
      const leadVer: Record<string, Version> = {};
      leads.forEach((l) => { leadVer[l] = extract(l, data); });

      const nv: any = {}, ncur: any = {}, nlock: any = {}, nview: any = {};
      enabledList.forEach((c) => {
        const l = leadOf(c);
        nv[c] = [c === l ? leadVer[l] : { text: leadVer[l].text, shared: true, subject: c === 'email' ? (leadVer[l].subject || 'Your update') : undefined }];
        ncur[c] = 0; nlock[c] = false; nview[c] = 'preview';
      });
      setVersions(nv); setCurrent(ncur); setLocked(nlock); setVw(nview); setSavedIds({});
      // Fresh "set" id for this batch — every template saved from it groups together.
      setGroupId((globalThis.crypto?.randomUUID?.() as string) || `${Date.now()}-${enabledList.join('')}`);
      setImagePrompt(data.imagePrompt || ''); setProvider(data.provider || '');
      // AI-suggested name + brand — fill only when the user hasn't typed their own (stays editable).
      if (!name.trim() && data.campaignName) setName(data.campaignName);
      if (!brandName.trim() && data.brandName) setBrandName(data.brandName);
      if (mode === 'guided') setGstep(3);
      toast.success(`Generated ${distinctGroups.length} piece(s) with ${data.provider || 'AI'}`);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Generation failed — check the active AI provider in Settings → Integrations');
    } finally { setBusy(false); }
  };

  const regenerate = async (c: ChannelId) => {
    setRegen((p) => ({ ...p, [c]: true }));
    try {
      const env: any = await creativesApi.content(brief.trim(), [c]);
      const v = extract(c, env.data);
      setVersions((p) => { const arr = [...(p[c] || []), v]; setCurrent((cc) => ({ ...cc, [c]: arr.length - 1 })); return { ...p, [c]: arr }; });
      setLocked((p) => ({ ...p, [c]: false }));
      toast.success(`${CHANNEL_META[c].label}: new version added`);
    } catch (err: any) { toast.error(err?.response?.data?.message || 'Regenerate failed'); }
    finally { setRegen((p) => ({ ...p, [c]: false })); }
  };

  const startEdit = (c: ChannelId) => { const v = versions[c]![current[c]!]; setEditing(c); setEditText(v.text); setEditSubject(v.subject || ''); };
  const saveEdit = () => {
    if (!editing) return; const c = editing;
    setVersions((p) => { const base = p[c]![current[c]!]; const arr = [...(p[c] || []), { ...base, text: editText, subject: c === 'email' ? editSubject : base.subject, shared: false }]; setCurrent((cc) => ({ ...cc, [c]: arr.length - 1 })); return { ...p, [c]: arr }; });
    setEditing(null); toast.success('Saved as a new version');
  };

  const regeneratePrompt = async () => {
    if (brief.trim().length < 20) { toast.error('Add a brief first'); return; }
    setPromptBusy(true);
    try {
      const env: any = await creativesApi.imagePrompt(brief.trim());
      const p = env.data?.imagePrompt;
      if (p) { setImagePrompt(p); toast.success('New image prompt generated'); }
    } catch (err: any) { toast.error(err?.response?.data?.message || 'Could not regenerate prompt'); }
    finally { setPromptBusy(false); }
  };

  const genImage = async () => {
    if (!imagePrompt) return; setImgBusy(true);
    try {
      const env: any = await creativesApi.generate(imagePrompt, size, null);
      const a = env.data;
      if (a.status === 'completed' && a.imageUrl) { setHeroImageUrl(a.imageUrl); toast.success('Image generated — previews updated'); }
      else toast.error(a.errorMessage || 'Image generation failed — try another provider in Integrations');
    } catch (err: any) { toast.error(err?.response?.data?.message || 'Image generation failed'); }
    finally { setImgBusy(false); }
  };

  const titleFor = (c: ChannelId) => `${(name || brief).trim().slice(0, 40) || 'Untitled'} – ${CHANNEL_META[c].label}`;
  const buildDto = (c: ChannelId) => {
    const v = versions[c]![current[c]!];
    const emailBody = c === 'email' && heroImageUrl
      ? `<img src="${heroImageUrl}" alt="" style="max-width:100%;border-radius:10px;margin-bottom:14px"/>${v.text}`
      : v.text;
    return {
      name: titleFor(c), channel: c,
      subject: c === 'email' ? (v.subject || titleFor(c)) : (c === 'facebook' ? (v.headline || undefined) : undefined),
      body: emailBody,
      mediaUrl: heroImageUrl || null,
      mediaType: heroImageUrl ? 'image' : null,
      templateGroupId: groupId || null,
      templateGroupName: (name || brief).trim().slice(0, 60) || 'Creative Studio set',
    };
  };

  const ensureSaved = async (c: ChannelId): Promise<string | undefined> => {
    if (savedIds[c]) return savedIds[c];
    const res: any = await templateApi.create(buildDto(c));
    const id = res.data?.id;
    setSavedIds((p) => ({ ...p, [c]: id }));
    return id;
  };

  const saveTemplates = async () => {
    const lockedList = enabledList.filter((c) => locked[c]);
    if (lockedList.length === 0) { toast.error('Lock at least one channel first (🔒)'); return; }
    setSaving(true);
    try {
      let made = 0, reused = 0;
      for (const c of lockedList) { if (savedIds[c]) reused++; else { await ensureSaved(c); made++; } }
      toast.success(`${made} template(s) saved${reused ? `, ${reused} already saved` : ''} — see Templates`);
    } catch (err: any) { toast.error(err?.response?.data?.message || 'Saving templates failed'); }
    finally { setSaving(false); }
  };

  const createCampaign = async (c: ChannelId) => {
    if (!locked[c]) { toast.error(`Lock ${CHANNEL_META[c].label} first (🔒)`); return; }
    setSaving(true);
    try {
      const id = await ensureSaved(c);
      toast.success(`${CHANNEL_META[c].label} template ready — opening campaign…`);
      navigate('/campaigns', { state: { templateId: id, channel: c, name: (name || brief).trim().slice(0, 40) } });
    } catch (err: any) { toast.error(err?.response?.data?.message || 'Could not start campaign'); }
    finally { setSaving(false); }
  };

  const lockedCount = enabledList.filter((c) => locked[c]).length;
  const resetAll = () => { setVersions({}); setSavedIds({}); setHeroImageUrl(null); setImagePrompt(''); setGstep(1); };

  /* ---------- building blocks ---------- */
  const BriefBlock = (
    <div className="grid md:grid-cols-3 gap-3">
      <textarea value={brief} onChange={(e) => setBrief(e.target.value)} rows={4} maxLength={10000} placeholder={EXAMPLE}
        className="md:col-span-2 w-full px-3 py-2.5 border border-gray-200 rounded-xl text-sm focus:ring-2 focus:ring-indigo-400 outline-none resize-y" />
      <div className="space-y-2.5">
        <div>
          <label className="block text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1.5">Campaign name <span className="font-normal normal-case text-indigo-500">✨ AI-suggested</span></label>
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Auto-filled after Generate — editable"
            className="w-full px-3 py-2.5 border border-gray-200 rounded-xl text-sm focus:ring-2 focus:ring-indigo-400 outline-none" />
          <p className="text-[11px] text-gray-400 mt-1">AI fills this from your brief; change it anytime.</p>
        </div>
        <div>
          <label className="block text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1.5">Brand / sender name <span className="font-normal normal-case text-indigo-500">✨ AI-suggested</span></label>
          <input value={brandName} onChange={(e) => setBrandName(e.target.value)} placeholder="Auto-filled from your brief — editable"
            className="w-full px-3 py-2.5 border border-gray-200 rounded-xl text-sm focus:ring-2 focus:ring-indigo-400 outline-none" />
          <p className="text-[11px] text-gray-400 mt-1">Shown as the sender/handle in every preview.</p>
        </div>
      </div>
    </div>
  );

  const ChannelChips = (
    <div className="flex flex-wrap gap-2.5">
      {ORDER.map((c) => (
        <button key={c} onClick={() => toggleChannel(c)} title={SUB[c]}
          className={`inline-flex items-center gap-2 rounded-full px-3.5 py-2 text-sm font-semibold border-2 transition-all ${enabled[c] ? `border-transparent text-white bg-gradient-to-br ${CHANNEL_META[c].head}` : 'border-gray-200 bg-white text-gray-600'}`}>
          <span className={enabled[c] ? 'bg-white/90 rounded p-0.5' : ''}><ChannelLogo channel={c} size={16} /></span>
          {CHANNEL_META[c].label}
          <span className={`w-4 h-4 rounded-full grid place-items-center ${enabled[c] ? 'bg-white/30' : 'border-2 border-gray-300'}`}>{enabled[c] && <Check className="w-3 h-3 text-white" />}</span>
        </button>
      ))}
    </div>
  );

  const CombineBar = enabledList.length > 1 && (
    <div className="relative">
      <button onClick={() => setCombineOpen((v) => !v)}
        className="inline-flex items-center gap-2 rounded-xl px-3.5 py-2 text-sm font-semibold text-indigo-700 border border-dashed border-indigo-300 bg-gradient-to-r from-indigo-50 to-purple-50">
        <Link2 className="w-4 h-4" /> Combine: {presetId === 'custom' ? 'Custom' : (PRESETS.find((p) => p.id === presetId)?.label || 'All separate')}
        <ChevronDown className="w-4 h-4" />
      </button>
      {combineOpen && (
        <div className="absolute z-20 mt-2 w-[320px] bg-white border border-gray-200 rounded-2xl shadow-xl p-3">
          <p className="text-[11px] font-semibold text-gray-500 uppercase tracking-wide mb-2">Which channels share one copy?</p>
          <div className="grid grid-cols-2 gap-2">
            {PRESETS.map((p) => (
              <button key={p.id} onClick={() => applyPreset(p)}
                className={`text-left rounded-xl p-2.5 border ${presetId === p.id ? 'border-indigo-400 bg-indigo-50' : 'border-gray-200 hover:bg-gray-50'}`}>
                <div className="text-[12.5px] font-semibold text-gray-800">{p.label}</div>
                <div className="text-[10.5px] text-gray-500">{p.desc}</div>
              </button>
            ))}
          </div>
          <button onClick={() => setAdvanced((v) => !v)} className="mt-2 text-[11px] font-semibold text-indigo-600">{advanced ? '− Hide' : '+ Advanced'} grouping</button>
          {advanced && (
            <div className="mt-2 flex flex-wrap gap-2">
              {enabledList.map((c) => (
                <div key={c} className="flex items-center gap-1.5 bg-gray-50 border border-gray-200 rounded-lg px-2 py-1">
                  <ChannelLogo channel={c} size={14} /><span className="text-[11px] font-medium">{CHANNEL_META[c].label}</span>
                  <button onClick={() => cycleGroup(c)} className={`text-[10px] font-extrabold text-white rounded px-1.5 py-0.5 bg-gradient-to-br ${GBADGE[group[c]]}`}>{group[c]}</button>
                </div>
              ))}
            </div>
          )}
          <p className="text-[11px] text-gray-600 mt-2">Generates <b>{distinctGroups.length}</b> piece(s); same colour = one shared copy.</p>
        </div>
      )}
    </div>
  );

  const GenerateBtn = (
    <button onClick={generate} disabled={busy}
      className="px-5 py-2.5 bg-gradient-to-r from-indigo-600 to-purple-600 text-white rounded-xl text-sm font-semibold hover:brightness-105 disabled:opacity-50 inline-flex items-center gap-2 shadow">
      {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <Sparkles className="w-4 h-4" />}{busy ? 'Writing…' : 'Generate'}
    </button>
  );

  const ResultsBlock = (
    <>
      {/* image bar */}
      {imagePrompt && (
        <div className="rounded-2xl border border-indigo-200 bg-gradient-to-br from-indigo-50 to-purple-50 p-4 flex gap-4 flex-wrap items-center">
          <div className="w-24 h-24 rounded-xl shrink-0 overflow-hidden border border-indigo-100 grid place-items-center text-center text-[10px] text-gray-400"
            style={heroImageUrl ? { backgroundImage: `url(${heroImageUrl})`, backgroundSize: 'cover', backgroundPosition: 'center' } : { background: '#eef2f7' }}>
            {!heroImageUrl && '📷 No image'}
          </div>
          <div className="flex-1 min-w-[240px]">
            <div className="flex items-center gap-2">
              <p className="text-xs font-semibold text-indigo-700">🖼️ Matching hero image</p>
              <button onClick={regeneratePrompt} disabled={promptBusy} className="text-[11px] font-semibold text-indigo-600 inline-flex items-center gap-1 hover:text-indigo-800 disabled:opacity-50">
                {promptBusy ? <Loader2 className="w-3 h-3 animate-spin" /> : <RefreshCw className="w-3 h-3" />} New prompt
              </button>
            </div>
            <p className="text-[12.5px] text-gray-600 mt-0.5 line-clamp-2">{imagePrompt}</p>
            <div className="mt-2.5 flex items-center gap-2 flex-wrap">
              <select value={size} onChange={(e) => setSize(e.target.value)} className="px-2.5 py-1.5 border border-gray-200 rounded-lg text-xs">
                {(sizes.length ? sizes : [{ token: '1024x1024', label: 'Square' } as any]).map((o) => <option key={o.token} value={o.token}>{o.label} ({o.token})</option>)}
              </select>
              <button onClick={genImage} disabled={imgBusy} className="px-3.5 py-1.5 bg-gradient-to-r from-indigo-600 to-purple-600 text-white rounded-lg text-xs font-semibold disabled:opacity-50 inline-flex items-center gap-1.5">
                {imgBusy ? <Loader2 className="w-4 h-4 animate-spin" /> : <ImageIcon className="w-4 h-4" />}{heroImageUrl ? 'Regenerate image' : 'Generate image'}
              </button>
              {onSendToBanner && <button onClick={() => onSendToBanner(imagePrompt)} className="px-3 py-1.5 bg-white border border-indigo-200 text-indigo-700 rounded-lg text-xs font-semibold inline-flex items-center gap-1.5"><Wand2 className="w-3.5 h-3.5" /> Banner Studio</button>}
            </div>
          </div>
        </div>
      )}

      {/* cards */}
      <div className="grid lg:grid-cols-2 gap-4">
        {enabledList.map((c) => {
          const arr = versions[c]; if (!arr) return null;
          const v = arr[current[c]!]; const view = vw[c] || 'preview';
          const shared = group[c] && groupsMap[group[c]].length > 1;
          return (
            <div key={c} className={`bg-white rounded-2xl border overflow-hidden flex flex-col ${locked[c] ? 'ring-2 ring-green-500 border-transparent' : 'border-gray-100'}`}>
              <div className={`px-4 py-2.5 text-white flex items-center gap-2 bg-gradient-to-r ${CHANNEL_META[c].head}`}>
                <span className="bg-white/90 rounded p-0.5"><ChannelLogo channel={c} size={16} /></span>
                <b className="text-sm">{CHANNEL_META[c].label}</b>
                {shared && <span className="text-[10px] bg-white/25 px-2 py-0.5 rounded-full">shared</span>}
                {savedIds[c] && <span className="text-[10px] bg-white/25 px-2 py-0.5 rounded-full">saved</span>}
                <div className="ml-auto flex bg-white/20 rounded-lg p-0.5 text-[11px] font-semibold">
                  <button onClick={() => setVw((p) => ({ ...p, [c]: 'preview' }))} className={`px-2.5 py-1 rounded-md ${view === 'preview' ? 'bg-white text-gray-900' : 'text-white/90'}`}>Preview</button>
                  <button onClick={() => setVw((p) => ({ ...p, [c]: 'copy' }))} className={`px-2.5 py-1 rounded-md ${view === 'copy' ? 'bg-white text-gray-900' : 'text-white/90'}`}>Copy</button>
                </div>
              </div>

              <div className="flex-1">
                {editing === c ? (
                  <div className="p-4 space-y-2">
                    {c === 'email' && <input value={editSubject} onChange={(e) => setEditSubject(e.target.value)} placeholder="Subject" className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm" />}
                    <textarea value={editText} onChange={(e) => setEditText(e.target.value)} rows={7} className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm resize-y" />
                    <div className="flex gap-2"><button onClick={saveEdit} className="px-3 py-1.5 bg-indigo-600 text-white rounded-lg text-xs font-semibold inline-flex items-center gap-1"><Check className="w-3.5 h-3.5" /> Save version</button><button onClick={() => setEditing(null)} className="px-3 py-1.5 bg-gray-100 text-gray-600 rounded-lg text-xs">Cancel</button></div>
                  </div>
                ) : view === 'preview' ? (
                  <div className="p-4 bg-slate-50 flex justify-center">
                    <ChannelPreview channel={c} img={heroImageUrl} brand={brandName} fields={{ text: v.text, subject: v.subject, isHtml: c === 'email' }} />
                  </div>
                ) : (
                  <div className="p-4 text-sm text-gray-700 overflow-auto max-h-[320px]">
                    {c === 'email' && <p className="font-semibold text-gray-900 mb-1">{v.subject}</p>}
                    {c === 'email' ? <div className="prose prose-sm max-w-none" dangerouslySetInnerHTML={{ __html: v.text }} /> : <pre className="whitespace-pre-wrap font-sans">{v.text}</pre>}
                  </div>
                )}
              </div>

              <div className="px-4 py-2.5 border-t border-gray-100 flex items-center gap-1.5 flex-wrap">
                <div className="flex gap-1.5 mr-auto items-center">
                  {arr.map((_, i) => (
                    <button key={i} onClick={() => setCurrent((p) => ({ ...p, [c]: i }))} title={`Version ${i + 1}`}
                      className={`h-2 rounded-full transition-all ${i === current[c] ? 'w-5 bg-indigo-500' : 'w-2 bg-gray-300'}`} />
                  ))}
                </div>
                <button onClick={() => navigator.clipboard.writeText(v.text).then(() => toast.success('Copied'))} className="w-8 h-8 grid place-items-center rounded-lg border border-gray-200 text-gray-500 hover:text-indigo-600" title="Copy"><Copy className="w-4 h-4" /></button>
                <button onClick={() => startEdit(c)} className="w-8 h-8 grid place-items-center rounded-lg border border-gray-200 text-gray-500 hover:text-indigo-600" title="Edit"><Pencil className="w-4 h-4" /></button>
                <button onClick={() => regenerate(c)} disabled={regen[c]} className="w-8 h-8 grid place-items-center rounded-lg border border-gray-200 text-gray-500 hover:text-indigo-600 disabled:opacity-40" title="Regenerate">{regen[c] ? <Loader2 className="w-4 h-4 animate-spin" /> : <RefreshCw className="w-4 h-4" />}</button>
                {locked[c]
                  ? <button onClick={() => setLocked((p) => ({ ...p, [c]: false }))} className="px-3 py-1.5 bg-gray-100 text-gray-600 rounded-lg text-xs font-semibold inline-flex items-center gap-1"><Unlock className="w-3.5 h-3.5" /> Unlock</button>
                  : <button onClick={() => setLocked((p) => ({ ...p, [c]: true }))} className="px-3 py-1.5 bg-gradient-to-r from-green-500 to-emerald-600 text-white rounded-lg text-xs font-semibold inline-flex items-center gap-1"><Lock className="w-3.5 h-3.5" /> Lock</button>}
              </div>
            </div>
          );
        })}
      </div>

      {/* sticky save bar */}
      <div className="sticky bottom-4 z-10 flex items-center gap-2.5 flex-wrap bg-slate-900 text-white rounded-2xl px-4 py-3 shadow-xl">
        <span className="text-sm font-semibold mr-auto inline-flex items-center gap-2"><Lock className="w-4 h-4 text-green-400" /> {lockedCount} of {enabledList.length} locked</span>
        {mode === 'guided' && <button onClick={resetAll} className="px-3 py-1.5 bg-white/10 rounded-lg text-xs font-semibold inline-flex items-center gap-1.5"><RotateCcw className="w-3.5 h-3.5" /> Start over</button>}
        <button onClick={saveTemplates} disabled={saving} className="px-3.5 py-1.5 bg-white/15 rounded-lg text-xs font-semibold inline-flex items-center gap-1.5 disabled:opacity-50"><Save className="w-3.5 h-3.5" /> Save to Templates</button>
        <button onClick={() => createCampaign('email')} disabled={saving || !enabled.email} className="px-3.5 py-1.5 bg-gradient-to-r from-indigo-500 to-blue-500 rounded-lg text-xs font-semibold inline-flex items-center gap-1.5 disabled:opacity-40"><Send className="w-3.5 h-3.5" /> Email campaign</button>
        <button onClick={() => createCampaign('whatsapp')} disabled={saving || !enabled.whatsapp} className="px-3.5 py-1.5 bg-gradient-to-r from-green-600 to-emerald-500 rounded-lg text-xs font-semibold inline-flex items-center gap-1.5 disabled:opacity-40"><Send className="w-3.5 h-3.5" /> WhatsApp campaign</button>
      </div>
      <p className="text-[11px] text-gray-400">Instagram &amp; Facebook are saved as templates (copy + image). One-click auto-posting via Meta login comes in the Social phase.</p>
    </>
  );

  /* ---------- layout ---------- */
  return (
    <div className="space-y-5" onClick={() => combineOpen && setCombineOpen(false)}>
      {!embedded && (
        <div className="flex items-center gap-3 flex-wrap">
          <div className="p-2.5 bg-gradient-to-br from-indigo-500 to-purple-600 rounded-xl"><PenLine className="w-6 h-6 text-white" /></div>
          <div><h1 className="text-2xl font-bold text-gray-900">AI Copywriter</h1><p className="text-gray-500 text-sm">One brief → multi-channel copy → preview → lock → templates → campaign.</p></div>
        </div>
      )}

      {/* mode toggle */}
      <div className="flex items-center justify-end" onClick={(e) => e.stopPropagation()}>
        <div className="inline-flex p-1 bg-gray-100 rounded-xl text-sm font-medium">
          <button onClick={() => setMode('simple')} className={`px-3 py-1.5 rounded-lg ${mode === 'simple' ? 'bg-white text-indigo-700 shadow-sm' : 'text-gray-500'}`}>One screen</button>
          <button onClick={() => { setMode('guided'); if (!hasResult) setGstep(1); }} className={`px-3 py-1.5 rounded-lg ${mode === 'guided' ? 'bg-white text-indigo-700 shadow-sm' : 'text-gray-500'}`}>Guided steps</button>
        </div>
      </div>

      {/* ===== SIMPLE ===== */}
      {mode === 'simple' && (
        <>
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5 space-y-4" onClick={(e) => e.stopPropagation()}>
            {BriefBlock}
            <div><p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Channels</p>{ChannelChips}</div>
            <div className="flex items-center gap-3 flex-wrap">{CombineBar}<div className="ml-auto">{GenerateBtn}</div></div>
            <p className="text-[11px] text-gray-400">Engine: <b>Groq (free)</b> · change in Settings → Integrations{provider && ` · last: ${provider}`}</p>
          </div>
          {hasResult && ResultsBlock}
        </>
      )}

      {/* ===== GUIDED ===== */}
      {mode === 'guided' && (
        <>
          <div className="flex gap-2.5" onClick={(e) => e.stopPropagation()}>
            {[{ n: 1, t: 'Brief & channels' }, { n: 2, t: 'Combine & generate' }, { n: 3, t: 'Review & save' }].map((s) => (
              <div key={s.n} onClick={() => (s.n < 3 || hasResult) && setGstep(s.n)}
                className={`flex-1 flex items-center gap-2.5 rounded-xl px-3.5 py-2.5 border cursor-pointer ${gstep === s.n ? 'border-transparent bg-gradient-to-br from-indigo-50 to-purple-50 shadow-sm' : 'border-gray-200 bg-white'}`}>
                <span className={`w-7 h-7 rounded-full grid place-items-center text-xs font-extrabold text-white ${gstep === s.n ? 'bg-gradient-to-br from-indigo-500 to-purple-600' : 'bg-gray-300'}`}>{s.n}</span>
                <span className="text-[13px] font-semibold text-gray-800">{s.t}</span>
              </div>
            ))}
          </div>

          {gstep === 1 && (
            <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5 space-y-4" onClick={(e) => e.stopPropagation()}>
              {BriefBlock}
              <div><p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Channels</p>{ChannelChips}</div>
              <div className="flex justify-end"><button onClick={() => setGstep(2)} className="px-5 py-2.5 bg-indigo-600 text-white rounded-xl text-sm font-semibold">Continue →</button></div>
            </div>
          )}
          {gstep === 2 && (
            <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5 space-y-4" onClick={(e) => e.stopPropagation()}>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Share the same post?</p>
              <div className="grid sm:grid-cols-2 gap-2.5">
                {PRESETS.map((p) => (
                  <button key={p.id} onClick={() => applyPreset(p)} className={`text-left rounded-xl p-3 border ${presetId === p.id ? 'border-indigo-400 bg-indigo-50' : 'border-gray-200 hover:bg-gray-50'}`}>
                    <div className="text-sm font-semibold text-gray-800">{p.label}</div><div className="text-[11px] text-gray-500">{p.desc}</div>
                  </button>
                ))}
              </div>
              <div className="flex justify-between"><button onClick={() => setGstep(1)} className="px-4 py-2.5 bg-gray-100 text-gray-600 rounded-xl text-sm font-semibold">← Back</button>{GenerateBtn}</div>
            </div>
          )}
          {gstep === 3 && (hasResult ? ResultsBlock : <div className="text-center text-gray-400 py-12">Generate first (Step 2).</div>)}
        </>
      )}
    </div>
  );
}
