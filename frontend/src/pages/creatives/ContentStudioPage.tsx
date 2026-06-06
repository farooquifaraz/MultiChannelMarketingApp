import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  PenLine, Loader2, Sparkles, Copy, Lock, Unlock, RefreshCw, Pencil, Check,
  Image as ImageIcon, Save, Send, ExternalLink, Wand2,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { creativesApi, type MarketingContent, type ImageSizeOption } from '../../api/creativesApi';
import { templateApi } from '../../api/templateApi';

type ChannelId = 'whatsapp' | 'instagram' | 'facebook' | 'email';

interface Version {
  text: string;            // primary copy (broadcast / caption / post / email body)
  subject?: string;        // email
  preview?: string;        // email inbox preview
  statusText?: string;     // whatsapp status
  reelsHook?: string;      // instagram
  storyCta?: string;       // instagram
  headline?: string;       // facebook
  cta?: string;            // facebook
  shared?: boolean;        // seeded from another channel (shared group)
}

const ORDER: ChannelId[] = ['whatsapp', 'instagram', 'facebook', 'email'];
const GROUP_LETTERS = ['A', 'B', 'C', 'D'];

const CH: Record<ChannelId, { label: string; slug: string; color: string; head: string; ring: string }> = {
  whatsapp:  { label: 'WhatsApp',  slug: 'whatsapp',  color: '25D366', head: 'from-green-500 to-emerald-600',  ring: 'ring-green-400' },
  instagram: { label: 'Instagram', slug: 'instagram', color: 'E4405F', head: 'from-pink-500 to-purple-600',    ring: 'ring-pink-400' },
  facebook:  { label: 'Facebook',  slug: 'facebook',  color: '1877F2', head: 'from-blue-500 to-blue-700',      ring: 'ring-blue-400' },
  email:     { label: 'Email',     slug: 'gmail',     color: 'EA4335', head: 'from-indigo-500 to-indigo-700',  ring: 'ring-indigo-400' },
};

const GBADGE: Record<string, string> = {
  A: 'from-indigo-500 to-indigo-700', B: 'from-green-500 to-emerald-600',
  C: 'from-pink-500 to-purple-600', D: 'from-amber-500 to-orange-600',
};

const EXAMPLE = 'Example: 5BR Villa at The Oasis by Emaar, Dubailand. AED 16,500,000. 8,500 sqft. Private pool, rooftop terrace, smart home, Italian marble. 5 min to Global Village. Handover Q4 2026. Agent: Ahmed, +971 50 123 4567, Luxe Properties.';

/** Brand logo with monogram fallback (simple-icons CDN). */
function Logo({ ch, size = 22 }: { ch: ChannelId; size?: number }) {
  const [err, setErr] = useState(false);
  const m = CH[ch];
  if (err) return <span style={{ width: size, height: size }} className="inline-grid place-items-center rounded text-[10px] font-bold text-white" >{m.label[0]}</span>;
  return <img src={`https://cdn.simpleicons.org/${m.slug}/${m.color}`} alt={m.label} width={size} height={size} onError={() => setErr(true)} />;
}

export default function ContentStudioPage({ embedded = false, onSendToBanner }: { embedded?: boolean; onSendToBanner?: (prompt: string) => void } = {}) {
  const navigate = useNavigate();
  const [brief, setBrief] = useState('');
  const [name, setName] = useState('');
  const [enabled, setEnabled] = useState<Record<ChannelId, boolean>>({ whatsapp: true, instagram: true, facebook: true, email: true });
  const [group, setGroup] = useState<Record<ChannelId, string>>({ whatsapp: 'A', instagram: 'B', facebook: 'C', email: 'D' });

  const [busy, setBusy] = useState(false);
  const [provider, setProvider] = useState('');
  const [versions, setVersions] = useState<Partial<Record<ChannelId, Version[]>>>({});
  const [current, setCurrent] = useState<Partial<Record<ChannelId, number>>>({});
  const [locked, setLocked] = useState<Partial<Record<ChannelId, boolean>>>({});
  const [view, setView] = useState<Partial<Record<ChannelId, 'copy' | 'preview'>>>({});
  const [regen, setRegen] = useState<Partial<Record<ChannelId, boolean>>>({});
  const [editing, setEditing] = useState<ChannelId | null>(null);
  const [editText, setEditText] = useState('');
  const [editSubject, setEditSubject] = useState('');

  const [imagePrompt, setImagePrompt] = useState('');
  const [heroImageUrl, setHeroImageUrl] = useState<string | null>(null);
  const [imgBusy, setImgBusy] = useState(false);
  const [sizes, setSizes] = useState<ImageSizeOption[]>([]);
  const [size, setSize] = useState('1024x1024');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    creativesApi.sizes().then((r: any) => { setSizes(r.data || []); }).catch(() => {});
  }, []);

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
  const cycleGroup = (c: ChannelId) =>
    setGroup((p) => ({ ...p, [c]: GROUP_LETTERS[(GROUP_LETTERS.indexOf(p[c]) + 1) % GROUP_LETTERS.length] }));
  const preset = (p: 'sep' | 'igfb' | 'fbwa' | 'social') => {
    if (p === 'sep') setGroup({ whatsapp: 'A', instagram: 'B', facebook: 'C', email: 'D' });
    if (p === 'igfb') setGroup({ whatsapp: 'A', instagram: 'B', facebook: 'B', email: 'D' });
    if (p === 'fbwa') setGroup({ whatsapp: 'A', instagram: 'B', facebook: 'A', email: 'D' });
    if (p === 'social') setGroup({ whatsapp: 'A', instagram: 'A', facebook: 'A', email: 'D' });
  };

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

      const nv: Partial<Record<ChannelId, Version[]>> = {};
      const ncur: Partial<Record<ChannelId, number>> = {};
      const nlock: Partial<Record<ChannelId, boolean>> = {};
      const nview: Partial<Record<ChannelId, 'copy' | 'preview'>> = {};
      enabledList.forEach((c) => {
        const l = leadOf(c);
        const v: Version = c === l
          ? leadVer[l]
          : { text: leadVer[l].text, shared: true, subject: c === 'email' ? (leadVer[l].subject || 'Your update') : undefined };
        nv[c] = [v]; ncur[c] = 0; nlock[c] = false; nview[c] = 'copy';
      });
      setVersions(nv); setCurrent(ncur); setLocked(nlock); setView(nview);
      setImagePrompt(data.imagePrompt || '');
      setProvider(data.provider || '');
      toast.success(`Generated ${distinctGroups.length} piece(s) with ${data.provider || 'AI'}`);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Generation failed — check the active AI provider in Settings → Integrations');
    } finally {
      setBusy(false);
    }
  };

  const regenerate = async (c: ChannelId) => {
    setRegen((p) => ({ ...p, [c]: true }));
    try {
      const env: any = await creativesApi.content(brief.trim(), [c]);
      const v = extract(c, env.data);
      setVersions((p) => {
        const arr = [...(p[c] || []), v];
        setCurrent((cc) => ({ ...cc, [c]: arr.length - 1 }));
        return { ...p, [c]: arr };
      });
      setLocked((p) => ({ ...p, [c]: false }));
      toast.success(`${CH[c].label}: new version added (v${(versions[c]?.length || 0) + 1})`);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Regenerate failed');
    } finally {
      setRegen((p) => ({ ...p, [c]: false }));
    }
  };

  const startEdit = (c: ChannelId) => {
    const v = versions[c]![current[c]!];
    setEditing(c); setEditText(v.text); setEditSubject(v.subject || '');
  };
  const saveEdit = () => {
    if (!editing) return;
    const c = editing;
    setVersions((p) => {
      const base = p[c]![current[c]!];
      const arr = [...(p[c] || []), { ...base, text: editText, subject: c === 'email' ? editSubject : base.subject, shared: false }];
      setCurrent((cc) => ({ ...cc, [c]: arr.length - 1 }));
      return { ...p, [c]: arr };
    });
    setEditing(null);
    toast.success('Saved as a new version');
  };

  const genImage = async () => {
    if (!imagePrompt) return;
    setImgBusy(true);
    try {
      const env: any = await creativesApi.generate(imagePrompt, size, null);
      const asset = env.data;
      if (asset.status === 'completed' && asset.imageUrl) {
        setHeroImageUrl(asset.imageUrl);
        toast.success('Image generated — previews updated');
      } else {
        toast.error(asset.errorMessage || 'Image generation failed — try another provider in Integrations');
      }
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Image generation failed');
    } finally {
      setImgBusy(false);
    }
  };

  const titleFor = (c: ChannelId) => `${(name || brief).trim().slice(0, 40) || 'Untitled'} – ${CH[c].label}`;
  const mediaForSend = heroImageUrl && /^https?:/i.test(heroImageUrl) ? heroImageUrl : null;

  const buildDto = (c: ChannelId) => {
    const v = versions[c]![current[c]!];
    return {
      name: titleFor(c),
      channel: c,
      subject: c === 'email' ? (v.subject || titleFor(c)) : (c === 'facebook' ? (v.headline || undefined) : undefined),
      body: v.text,
      mediaUrl: c !== 'email' ? mediaForSend : null,
      mediaType: c !== 'email' && mediaForSend ? 'image' : null,
    };
  };

  const saveTemplates = async () => {
    const lockedList = enabledList.filter((c) => locked[c]);
    if (lockedList.length === 0) { toast.error('Lock at least one channel first (🔒)'); return; }
    setSaving(true);
    try {
      for (const c of lockedList) await templateApi.create(buildDto(c));
      toast.success(`Saved ${lockedList.length} template(s) — find them in Templates`);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Saving templates failed');
    } finally {
      setSaving(false);
    }
  };

  const createCampaign = async (c: ChannelId) => {
    if (!locked[c]) { toast.error(`Lock ${CH[c].label} first (🔒) so it can be saved`); return; }
    setSaving(true);
    try {
      const res: any = await templateApi.create(buildDto(c));
      const tplId = res.data?.id;
      toast.success(`${CH[c].label} template saved — opening campaign…`);
      navigate('/campaigns', { state: { templateId: tplId, channel: c, name: (name || brief).trim().slice(0, 40) } });
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Could not start campaign');
    } finally {
      setSaving(false);
    }
  };

  const lockedCount = enabledList.filter((c) => locked[c]).length;

  return (
    <div className="space-y-6">
      {!embedded && (
        <div className="flex items-center gap-3">
          <div className="p-2.5 bg-gradient-to-br from-indigo-500 to-purple-600 rounded-xl"><PenLine className="w-6 h-6 text-white" /></div>
          <div>
            <h1 className="text-2xl font-bold text-gray-900">AI Copywriter</h1>
            <p className="text-gray-500 text-sm">One brief → multi-channel copy → preview → lock → templates → campaign.</p>
          </div>
        </div>
      )}

      {/* ===== 1 · Brief + channels ===== */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5 space-y-4">
        <div className="grid md:grid-cols-3 gap-3">
          <textarea
            value={brief} onChange={(e) => setBrief(e.target.value)} rows={4} maxLength={10000}
            placeholder={EXAMPLE}
            className="md:col-span-2 w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-400 outline-none resize-y"
          />
          <div className="space-y-2">
            <label className="block text-xs font-medium text-gray-500">Campaign / asset name (optional)</label>
            <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Oasis Villa launch"
              className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-400 outline-none" />
            <p className="text-[11px] text-gray-400">Used to name saved templates &amp; the campaign.</p>
          </div>
        </div>

        {/* channel toggles */}
        <div>
          <p className="text-sm font-semibold text-gray-700 mb-2">Which content do you need?</p>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
            {ORDER.map((c) => (
              <button key={c} onClick={() => toggleChannel(c)}
                className={`relative text-left rounded-2xl p-4 border-2 transition-all ${enabled[c] ? 'border-transparent shadow-md -translate-y-0.5 bg-gradient-to-br from-gray-50 to-white' : 'border-gray-200 bg-white opacity-70'}`}>
                <span className={`absolute top-3 right-3 w-10 h-6 rounded-full transition-all ${enabled[c] ? 'bg-green-500' : 'bg-gray-300'}`}>
                  <span className={`absolute top-0.5 w-5 h-5 rounded-full bg-white shadow transition-all ${enabled[c] ? 'left-[18px]' : 'left-0.5'}`} />
                </span>
                <div className="w-10 h-10 rounded-xl bg-white border border-gray-100 grid place-items-center mb-2"><Logo ch={c} /></div>
                <div className="font-semibold text-sm text-gray-900">{CH[c].label}</div>
                <div className="text-[11px] text-gray-500">
                  {c === 'whatsapp' && 'Broadcast + status'}{c === 'instagram' && 'Caption + hashtags'}
                  {c === 'facebook' && 'Post + headline'}{c === 'email' && 'Subject + HTML body'}
                </div>
              </button>
            ))}
          </div>
        </div>

        {/* combination builder */}
        {enabledList.length > 1 && (
          <div className="rounded-xl border border-dashed border-indigo-200 bg-gradient-to-r from-indigo-50 to-purple-50 p-4">
            <p className="text-sm font-semibold text-gray-800">🔗 Combine channels — which ones share the same copy?</p>
            <p className="text-xs text-gray-500 mb-3">Tap a colour badge to move a channel into a group. Same colour = one shared post. Different colours = tailored copy each.</p>
            <div className="flex flex-wrap gap-2 mb-3">
              <button onClick={() => preset('sep')} className="text-xs font-medium px-3 py-1.5 rounded-full border border-indigo-200 bg-white text-indigo-700 hover:bg-indigo-50">All separate</button>
              <button onClick={() => preset('igfb')} className="text-xs font-medium px-3 py-1.5 rounded-full border border-indigo-200 bg-white text-indigo-700 hover:bg-indigo-50">Instagram + Facebook</button>
              <button onClick={() => preset('fbwa')} className="text-xs font-medium px-3 py-1.5 rounded-full border border-indigo-200 bg-white text-indigo-700 hover:bg-indigo-50">Facebook + WhatsApp</button>
              <button onClick={() => preset('social')} className="text-xs font-medium px-3 py-1.5 rounded-full border border-indigo-200 bg-white text-indigo-700 hover:bg-indigo-50">All social together</button>
            </div>
            <div className="flex flex-wrap gap-2">
              {enabledList.map((c) => (
                <div key={c} className="flex items-center gap-2 bg-white border border-gray-200 rounded-xl px-3 py-2">
                  <Logo ch={c} size={18} /><span className="text-sm font-medium text-gray-700">{CH[c].label}</span>
                  <button onClick={() => cycleGroup(c)} title="Change group"
                    className={`text-[11px] font-extrabold text-white rounded-lg px-2.5 py-1 bg-gradient-to-br ${GBADGE[group[c]]}`}>{group[c]}</button>
                </div>
              ))}
            </div>
            <p className="text-xs text-gray-600 mt-3">
              <b>Will generate {distinctGroups.length} piece(s):</b>{' '}
              {distinctGroups.map((g) => {
                const list = groupsMap[g].map((x) => CH[x].label).join(' + ');
                const shared = groupsMap[g].length > 1;
                return <span key={g} className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 mr-1.5 mt-1 font-semibold ${shared ? 'bg-green-50 text-green-700 border border-green-200' : 'bg-white border border-gray-200'}`}>{shared ? '🔗 ' : ''}{list}{shared ? ' — 1 shared copy' : ''}</span>;
              })}
            </p>
          </div>
        )}

        <div className="flex items-center gap-3 flex-wrap">
          <button onClick={generate} disabled={busy} className="px-5 py-2.5 bg-gradient-to-r from-indigo-600 to-purple-600 text-white rounded-xl text-sm font-semibold hover:brightness-105 disabled:opacity-50 inline-flex items-center gap-2 shadow">
            {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <Sparkles className="w-4 h-4" />}{busy ? 'Writing…' : 'Generate selected content'}
          </button>
          <span className="text-xs text-gray-400">Default engine: <b>Groq (free)</b> · change in Settings → Integrations{provider && ` · last: ${provider}`}</span>
        </div>
      </div>

      {/* ===== 2 · Results ===== */}
      {hasResult && (
        <>
          <div className="grid lg:grid-cols-2 gap-4">
            {enabledList.map((c) => {
              const arr = versions[c]; if (!arr) return null;
              const v = arr[current[c]!]; const vw = view[c] || 'copy';
              return (
                <div key={c} className={`bg-white rounded-2xl border overflow-hidden flex flex-col ${locked[c] ? 'ring-2 ring-green-500 border-transparent' : 'border-gray-100'}`}>
                  <div className={`px-4 py-2.5 text-white flex items-center gap-2 bg-gradient-to-r ${CH[c].head}`}>
                    <span className="bg-white/90 rounded p-0.5"><Logo ch={c} size={16} /></span>
                    <b className="text-sm">{CH[c].label}</b>
                    {group[c] && groupsMap[group[c]].length > 1 && <span className="text-[10px] bg-white/25 px-2 py-0.5 rounded-full">shared {group[c]}</span>}
                    {locked[c] && <span className="text-[10px] bg-white/25 px-2 py-0.5 rounded-full inline-flex items-center gap-1"><Lock className="w-3 h-3" /> Locked</span>}
                    <div className="ml-auto flex bg-white/20 rounded-lg p-0.5 text-[11px] font-semibold">
                      <button onClick={() => setView((p) => ({ ...p, [c]: 'copy' }))} className={`px-2.5 py-1 rounded-md ${vw === 'copy' ? 'bg-white text-gray-900' : 'text-white/90'}`}>Copy</button>
                      <button onClick={() => setView((p) => ({ ...p, [c]: 'preview' }))} className={`px-2.5 py-1 rounded-md ${vw === 'preview' ? 'bg-white text-gray-900' : 'text-white/90'}`}>Preview</button>
                    </div>
                  </div>

                  <div className="flex-1">
                    {editing === c ? (
                      <div className="p-4 space-y-2">
                        {c === 'email' && <input value={editSubject} onChange={(e) => setEditSubject(e.target.value)} placeholder="Subject" className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm" />}
                        <textarea value={editText} onChange={(e) => setEditText(e.target.value)} rows={8} className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm resize-y" />
                        <div className="flex gap-2">
                          <button onClick={saveEdit} className="px-3 py-1.5 bg-indigo-600 text-white rounded-lg text-xs font-semibold inline-flex items-center gap-1"><Check className="w-3.5 h-3.5" /> Save as new version</button>
                          <button onClick={() => setEditing(null)} className="px-3 py-1.5 bg-gray-100 text-gray-600 rounded-lg text-xs">Cancel</button>
                        </div>
                      </div>
                    ) : vw === 'copy' ? (
                      <div className="p-4 text-sm text-gray-700 overflow-auto max-h-[340px]">
                        {c === 'email' && <p className="font-semibold text-gray-900 mb-1">{v.subject}</p>}
                        {c === 'email'
                          ? <div className="prose prose-sm max-w-none" dangerouslySetInnerHTML={{ __html: v.text }} />
                          : <pre className="whitespace-pre-wrap font-sans">{v.text}</pre>}
                        {c === 'whatsapp' && v.statusText && <p className="mt-3 pt-3 border-t text-xs text-gray-500"><b>Status:</b> {v.statusText}</p>}
                        {c === 'instagram' && v.reelsHook && <p className="mt-2 text-xs text-gray-500"><b>Reels:</b> {v.reelsHook}</p>}
                        {c === 'facebook' && v.cta && <p className="mt-2 text-xs text-gray-500"><b>Button:</b> {v.cta}</p>}
                      </div>
                    ) : (
                      <div className="p-4 bg-slate-50 flex justify-center"><Preview ch={c} v={v} img={heroImageUrl} /></div>
                    )}
                  </div>

                  <div className="px-4 py-2.5 border-t border-gray-100 flex items-center gap-2 flex-wrap">
                    <div className="flex gap-1.5 mr-auto">
                      {arr.map((_, i) => (
                        <button key={i} onClick={() => setCurrent((p) => ({ ...p, [c]: i }))}
                          className={`text-[11px] px-2 py-1 rounded-full border ${i === current[c] ? 'bg-indigo-50 border-indigo-200 text-indigo-600 font-bold' : 'bg-gray-50 border-transparent text-gray-500'}`}>
                          v{i + 1}{i === current[c] ? ' ✓' : ''}
                        </button>
                      ))}
                    </div>
                    <button onClick={() => navigator.clipboard.writeText(v.text).then(() => toast.success('Copied'))} className="p-1.5 text-gray-400 hover:text-indigo-600" title="Copy"><Copy className="w-4 h-4" /></button>
                    <button onClick={() => startEdit(c)} className="p-1.5 text-gray-400 hover:text-indigo-600" title="Edit"><Pencil className="w-4 h-4" /></button>
                    <button onClick={() => regenerate(c)} disabled={regen[c]} className="p-1.5 text-gray-400 hover:text-indigo-600 disabled:opacity-40" title="Regenerate">{regen[c] ? <Loader2 className="w-4 h-4 animate-spin" /> : <RefreshCw className="w-4 h-4" />}</button>
                    {locked[c]
                      ? <button onClick={() => setLocked((p) => ({ ...p, [c]: false }))} className="px-3 py-1.5 bg-gray-100 text-gray-600 rounded-lg text-xs font-semibold inline-flex items-center gap-1"><Unlock className="w-3.5 h-3.5" /> Unlock</button>
                      : <button onClick={() => setLocked((p) => ({ ...p, [c]: true }))} className="px-3 py-1.5 bg-gradient-to-r from-green-500 to-emerald-600 text-white rounded-lg text-xs font-semibold inline-flex items-center gap-1"><Lock className="w-3.5 h-3.5" /> Lock &amp; keep</button>}
                  </div>
                </div>
              );
            })}
          </div>

          {/* image prompt + generation */}
          {imagePrompt && (
            <div className="rounded-2xl border border-indigo-200 bg-gradient-to-br from-indigo-50 to-purple-50 p-4 flex gap-4 flex-wrap items-center">
              <div className="w-28 h-28 rounded-xl shrink-0 overflow-hidden border border-indigo-100 grid place-items-center text-center text-[10px] text-gray-400"
                style={heroImageUrl ? { backgroundImage: `url(${heroImageUrl})`, backgroundSize: 'cover', backgroundPosition: 'center' } : { background: '#eef2f7' }}>
                {!heroImageUrl && '📷 No image yet'}
              </div>
              <div className="flex-1 min-w-[240px]">
                <p className="text-xs font-semibold text-indigo-700">🖼️ Matching hero-image prompt</p>
                <p className="text-sm text-gray-700 mt-0.5">{imagePrompt}</p>
                <div className="mt-3 flex items-center gap-2 flex-wrap">
                  <select value={size} onChange={(e) => setSize(e.target.value)} className="px-2.5 py-2 border border-gray-200 rounded-lg text-xs">
                    {(sizes.length ? sizes : [{ token: '1024x1024', label: 'Square' } as any]).map((o) => <option key={o.token} value={o.token}>{o.label} ({o.token})</option>)}
                  </select>
                  <button onClick={genImage} disabled={imgBusy} className="px-4 py-2 bg-gradient-to-r from-indigo-600 to-purple-600 text-white rounded-lg text-xs font-semibold hover:brightness-105 disabled:opacity-50 inline-flex items-center gap-2">
                    {imgBusy ? <Loader2 className="w-4 h-4 animate-spin" /> : <ImageIcon className="w-4 h-4" />}{heroImageUrl ? 'Regenerate image' : 'Generate this image'}
                  </button>
                  {onSendToBanner && <button onClick={() => onSendToBanner(imagePrompt)} className="px-3 py-2 bg-white border border-indigo-200 text-indigo-700 rounded-lg text-xs font-semibold inline-flex items-center gap-1.5"><Wand2 className="w-3.5 h-3.5" /> Open in Banner Studio</button>}
                  <span className="text-[11px] text-gray-500">Before generating, Previews show copy without an image; after, it fills every Preview.</span>
                </div>
              </div>
            </div>
          )}

          {/* 3 · Save & launch */}
          <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
            <div className="flex items-center gap-2 mb-1">
              <h3 className="text-lg font-semibold text-gray-900">Save &amp; launch</h3>
              <span className="text-xs text-gray-500">{lockedCount} channel(s) locked</span>
            </div>
            <p className="text-sm text-gray-500 mb-4">Locked channels are saved as their own templates. Email &amp; WhatsApp can go straight into a campaign draft → delivery.</p>
            <div className="grid md:grid-cols-3 gap-3">
              <button onClick={saveTemplates} disabled={saving} className="text-left rounded-2xl p-4 text-white bg-gradient-to-br from-indigo-500 to-purple-600 disabled:opacity-60">
                <div className="flex items-center gap-2 font-semibold"><Save className="w-4 h-4" /> Save to Templates</div>
                <p className="text-xs opacity-90 mt-1">All locked channels → channel-tagged templates.</p>
              </button>
              <button onClick={() => createCampaign('email')} disabled={saving || !enabled.email} className="text-left rounded-2xl p-4 text-white bg-gradient-to-br from-indigo-600 to-blue-600 disabled:opacity-40">
                <div className="flex items-center gap-2 font-semibold"><Send className="w-4 h-4" /> Email campaign</div>
                <p className="text-xs opacity-90 mt-1">Save template + open a campaign draft.</p>
              </button>
              <button onClick={() => createCampaign('whatsapp')} disabled={saving || !enabled.whatsapp} className="text-left rounded-2xl p-4 text-white bg-gradient-to-br from-green-600 to-emerald-500 disabled:opacity-40">
                <div className="flex items-center gap-2 font-semibold"><Send className="w-4 h-4" /> WhatsApp campaign</div>
                <p className="text-xs opacity-90 mt-1">Save template + open a campaign draft.</p>
              </button>
            </div>
            <p className="text-xs text-gray-400 mt-3 border-t border-dashed pt-3 inline-flex items-start gap-1.5">
              <ExternalLink className="w-3.5 h-3.5 mt-0.5" />
              Instagram &amp; Facebook are saved as templates now (with copy + image). One-click auto-posting via Meta login arrives in the Social phase.
            </p>
          </div>
        </>
      )}
    </div>
  );
}

/* ===== platform-style previews ===== */
function NoImg() {
  return <div className="w-full h-full grid place-items-center text-[11px] text-gray-400 border border-dashed border-gray-300 rounded text-center px-2">🖼️ No image yet — Generate below</div>;
}
function Hero({ img, className }: { img: string | null; className: string }) {
  return img
    ? <div className={className} style={{ backgroundImage: `url(${img})`, backgroundSize: 'cover', backgroundPosition: 'center' }} />
    : <div className={className}><NoImg /></div>;
}

function Preview({ ch, v, img }: { ch: ChannelId; v: Version; img: string | null }) {
  if (ch === 'whatsapp') {
    return (
      <div className="w-[290px] bg-[#0b141a] rounded-2xl p-2.5">
        <div className="flex items-center gap-2 text-gray-200 text-[13px] px-1 pb-2"><span className="w-6 h-6 rounded-full bg-gradient-to-br from-green-500 to-emerald-600" /> Luxe Properties</div>
        <div className="bg-[#005c4b] text-gray-100 rounded-lg rounded-tr-sm p-1.5 ml-auto max-w-[240px] text-[12.5px] leading-snug">
          <Hero img={img} className="h-[150px] rounded mb-1.5 overflow-hidden" />
          <span className="whitespace-pre-wrap">{v.text.slice(0, 320)}</span>
          <span className="block text-right text-[10px] text-gray-400 mt-1">11:24 ✓✓</span>
        </div>
      </div>
    );
  }
  if (ch === 'instagram') {
    return (
      <div className="w-[300px] bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="flex items-center gap-2 p-2.5"><span className="w-7 h-7 rounded-full bg-gradient-to-br from-amber-400 via-pink-500 to-purple-600" /><b className="text-[13px]">luxe.properties</b><span className="ml-auto text-gray-400">•••</span></div>
        <Hero img={img} className="h-[280px] w-full" />
        <div className="flex gap-3 px-3 py-2 text-lg">❤️ 💬 ✈️ <span className="ml-auto">🔖</span></div>
        <div className="px-3 pb-3 text-[12.5px] leading-snug"><b>luxe.properties</b> {v.text.slice(0, 180)}</div>
      </div>
    );
  }
  if (ch === 'facebook') {
    return (
      <div className="w-[310px] bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="flex items-center gap-2 p-2.5"><span className="w-9 h-9 rounded-full bg-gradient-to-br from-blue-500 to-blue-700" /><div><b className="text-[13px] block leading-tight">{v.headline || 'Luxe Properties'}</b><small className="text-[11px] text-gray-500">Sponsored · 🌐</small></div></div>
        <div className="px-3 pb-2 text-[13px] leading-snug whitespace-pre-wrap">{v.text.slice(0, 220)}</div>
        <Hero img={img} className="h-[210px] w-full" />
        <div className="flex gap-4 px-3 py-2 text-[13px] text-gray-500 border-t">👍❤️ 128 · 14 comments · 9 shares</div>
      </div>
    );
  }
  // email
  return (
    <div className="w-[320px] bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div className="px-3 py-2.5 border-b"><div className="font-bold text-[13.5px]">{v.subject || 'Subject'}</div><div className="text-[11px] text-gray-500 mt-0.5">Luxe Properties &lt;ahmed@luxe.ae&gt; · to you</div></div>
      <Hero img={img} className="h-[120px] w-full" />
      <div className="px-3 py-3 text-[12.5px] leading-snug text-gray-700 max-h-[180px] overflow-auto prose prose-sm" dangerouslySetInnerHTML={{ __html: v.text }} />
    </div>
  );
}
