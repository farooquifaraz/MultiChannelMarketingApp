import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plug, Loader2, Sparkles, Image as ImageIcon, CreditCard, Check, Save, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { integrationsApi, type CategoryCredentials, type CredentialRow } from '../../api/integrationsApi';
import { useAuthStore } from '../../store/authStore';

/**
 * Provider catalogue. `model` is only a sensible DEFAULT — it stays fully editable per provider
 * (nothing is hard-coded into the generation logic). `cost` is an informational per-image estimate.
 */
interface ProviderDef {
  key: string; label: string; free: boolean; needsKey: boolean;
  model?: string; note?: string; secondary?: string; cost?: string;
}

const CATALOG: Record<string, { title: string; subtitle: string; icon: any; providers: ProviderDef[] }> = {
  ai: {
    title: 'AI Text', subtitle: 'Inbox replies · chat · AI Copywriter', icon: Sparkles,
    providers: [
      { key: 'gemini', label: 'Google Gemini', free: true, needsKey: true, model: 'gemini-2.5-flash', note: 'Free tier' },
      { key: 'openai', label: 'OpenAI (GPT)', free: false, needsKey: true, model: 'gpt-4o' },
      { key: 'anthropic', label: 'Anthropic Claude', free: false, needsKey: true, model: 'claude-sonnet-4-5' },
      { key: 'grok', label: 'xAI Grok', free: false, needsKey: true, model: 'grok-2' },
      { key: 'openai-compatible', label: 'OpenAI-compatible', free: true, needsKey: true, model: 'llama-3.3-70b-versatile', note: 'Groq / Ollama / OpenRouter' },
      { key: 'disabled', label: 'Disabled', free: true, needsKey: false, note: 'Turn AI off' },
    ],
  },
  image: {
    title: 'Image Generation', subtitle: 'Banner Studio', icon: ImageIcon,
    providers: [
      { key: 'flux', label: 'FLUX (Black Forest Labs)', free: false, needsKey: true, model: 'flux-dev', note: 'Cheapest real photos', cost: '~$0.025 · 400/$10' },
      { key: 'gemini', label: 'Google Nano Banana', free: false, needsKey: true, model: 'gemini-2.5-flash-image', note: 'Gemini 2.5 Flash Image', cost: '~$0.039/img' },
      { key: 'dalle', label: 'OpenAI gpt-image-1', free: false, needsKey: true, model: 'gpt-image-1', note: 'Needs verified org', cost: '~$0.04/img' },
      { key: 'stability', label: 'Stability AI', free: false, needsKey: true, model: 'core', cost: '~$0.03/img' },
      { key: 'huggingface', label: 'Hugging Face', free: true, needsKey: true, model: 'black-forest-labs/FLUX.1-schnell', note: 'Free token' },
      { key: 'pollinations', label: 'Pollinations.ai', free: true, needsKey: false, note: 'Free · optional token' },
      { key: 'mock', label: 'Built-in placeholder', free: true, needsKey: false, note: 'Always works · no key' },
      { key: 'disabled', label: 'Disabled', free: true, needsKey: false, note: 'Turn images off' },
    ],
  },
  payment: {
    title: 'Payments', subtitle: 'Plan upgrades & checkout', icon: CreditCard,
    providers: [
      { key: 'stripe', label: 'Stripe', free: false, needsKey: true, secondary: 'Webhook signing secret', note: 'Cards + Apple/Google Pay' },
      { key: 'mock', label: 'Test mode', free: true, needsKey: false, note: 'Instant · no real charge' },
      { key: 'disabled', label: 'Disabled', free: true, needsKey: false, note: 'Turn payments off' },
    ],
  },
};

/** Brand badge — colour + monogram per provider (no external assets, always renders). */
const BRAND: Record<string, { bg: string; fg: string; mark: string }> = {
  gemini: { bg: 'linear-gradient(135deg,#4285F4,#9b72cb,#d96570)', fg: '#fff', mark: '✦' },
  openai: { bg: '#000', fg: '#fff', mark: '◯' },
  'openai-compatible': { bg: '#111827', fg: '#fff', mark: '⌘' },
  anthropic: { bg: '#d97757', fg: '#fff', mark: 'A' },
  grok: { bg: '#000', fg: '#fff', mark: '𝕏' },
  flux: { bg: '#0a0a0a', fg: '#fff', mark: 'F' },
  dalle: { bg: '#10a37f', fg: '#fff', mark: '◯' },
  stability: { bg: '#7c3aed', fg: '#fff', mark: 'S' },
  huggingface: { bg: '#FFD21E', fg: '#000', mark: '🤗' },
  pollinations: { bg: '#16a34a', fg: '#fff', mark: 'P' },
  mock: { bg: '#94a3b8', fg: '#fff', mark: '▦' },
  stripe: { bg: '#635bff', fg: '#fff', mark: 'S' },
  disabled: { bg: '#e5e7eb', fg: '#9ca3af', mark: '∅' },
};

function Logo({ provider, size = 40 }: { provider: string; size?: number }) {
  const b = BRAND[provider] || BRAND.disabled;
  return (
    <span style={{ background: b.bg, color: b.fg, width: size, height: size, fontSize: size * 0.45 }}
      className="rounded-xl flex items-center justify-center font-bold shrink-0 shadow-sm">
      {b.mark}
    </span>
  );
}

export default function IntegrationsPage() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const isAdmin = user?.role?.toLowerCase() === 'admin';

  const [data, setData] = useState<Record<string, CategoryCredentials>>({});
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!isAdmin) { toast.error('Admin access required'); navigate('/dashboard'); }
  }, [isAdmin, navigate]);

  const load = async () => {
    try {
      const cats = ['ai', 'image', 'payment'];
      const res = await Promise.all(cats.map((c) => integrationsApi.get(c)));
      const map: Record<string, CategoryCredentials> = {};
      cats.forEach((c, i) => { map[c] = (res[i] as any).data; });
      setData(map);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to load integrations');
    } finally { setLoading(false); }
  };
  useEffect(() => { if (isAdmin) load(); /* eslint-disable-next-line */ }, [isAdmin]);

  if (!isAdmin) return null;
  if (loading) return <div className="flex items-center justify-center h-64"><Loader2 className="w-6 h-6 animate-spin text-indigo-500" /></div>;

  return (
    <div className="max-w-6xl mx-auto space-y-8">
      <div className="flex items-center gap-3">
        <div className="p-2.5 bg-gradient-to-br from-indigo-500 to-purple-600 rounded-xl"><Plug className="w-6 h-6 text-white" /></div>
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Integrations</h1>
          <p className="text-gray-500 text-sm">Save each provider's key once, then flip its switch to enable it.</p>
        </div>
      </div>

      {(['ai', 'image', 'payment'] as const).map((cat) => (
        <Category key={cat} category={cat} state={data[cat]} onChanged={load} />
      ))}
    </div>
  );
}

function Category({ category, state, onChanged }: { category: string; state?: CategoryCredentials; onChanged: () => void }) {
  const cfg = CATALOG[category];
  const Icon = cfg.icon;
  const active = state?.activeProvider || 'disabled';
  const byProvider: Record<string, CredentialRow> = {};
  (state?.credentials || []).forEach((c) => { byProvider[c.provider] = c; });
  const activeDef = cfg.providers.find((p) => p.key === active);

  return (
    <section>
      <div className="flex items-center justify-between gap-2 mb-3">
        <div className="flex items-center gap-2.5">
          <Icon className="w-5 h-5 text-indigo-500" />
          <h2 className="font-bold text-gray-900 text-lg">{cfg.title}</h2>
          <span className="text-xs text-gray-400">· {cfg.subtitle}</span>
        </div>
        <div className="text-xs text-gray-400">active: <span className="font-semibold text-indigo-600">{activeDef?.label || active}</span></div>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {cfg.providers.map((p) => (
          <ProviderCard key={p.key} category={category} def={p} saved={byProvider[p.key]} isActive={active === p.key} onChanged={onChanged} />
        ))}
      </div>
    </section>
  );
}

function ProviderCard({ category, def, saved, isActive, onChanged }:
  { category: string; def: ProviderDef; saved?: CredentialRow; isActive: boolean; onChanged: () => void }) {
  const [editing, setEditing] = useState(false);
  const [key, setKey] = useState('');
  const [secondary, setSecondary] = useState('');
  const [model, setModel] = useState(saved?.model || def.model || '');
  const [busy, setBusy] = useState(false);

  const activate = async () => {
    if (isActive) return;
    setBusy(true);
    try { await integrationsApi.activate(category, def.key); toast.success(`${def.label} enabled`); onChanged(); }
    catch (e: any) { toast.error(e?.response?.data?.message || 'Could not enable'); }
    finally { setBusy(false); }
  };

  const save = async () => {
    setBusy(true);
    try {
      await integrationsApi.saveKey(category, def.key, {
        apiKey: key.trim() || null, model: model.trim() || null, secondarySecret: secondary.trim() || null,
      });
      toast.success(`${def.label} saved`);
      setKey(''); setSecondary(''); setEditing(false);
      onChanged();
    } catch (e: any) { toast.error(e?.response?.data?.message || 'Save failed'); }
    finally { setBusy(false); }
  };

  const ringCls = isActive
    ? 'border-transparent shadow-lg shadow-indigo-200/60 [background:linear-gradient(#fff,#fff)_padding-box,linear-gradient(135deg,#6366f1,#a855f7)_border-box] border-2'
    : 'border border-gray-200 hover:border-gray-300';

  return (
    <div className={`rounded-2xl p-4 bg-white flex flex-col ${ringCls} transition`}>
      {/* header */}
      <div className="flex items-start gap-3">
        <Logo provider={def.key} />
        <div className="min-w-0 flex-1">
          <div className="font-semibold text-gray-900 text-sm leading-tight truncate">{def.label}</div>
          <div className="flex items-center gap-1.5 mt-1 flex-wrap">
            {def.free && <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200">free</span>}
            {def.cost && <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-gray-100 text-gray-500">{def.cost}</span>}
            {isActive && <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-indigo-600 text-white inline-flex items-center gap-0.5"><Check className="w-2.5 h-2.5" />enabled</span>}
          </div>
        </div>
      </div>

      {/* body */}
      <div className="mt-3 flex-1 space-y-2">
        {def.needsKey ? (
          editing ? (
            <>
              <input type="password" value={key} onChange={(e) => setKey(e.target.value)}
                placeholder={saved?.keyMasked ? `${saved.keyMasked} (keep)` : 'Paste API key'}
                className="w-full px-2.5 py-1.5 border border-gray-200 rounded-lg text-xs font-mono focus:ring-2 focus:ring-indigo-400 outline-none" />
              {def.secondary && (
                <input type="password" value={secondary} onChange={(e) => setSecondary(e.target.value)}
                  placeholder={saved?.secondarySecretSet ? '•••• webhook secret (keep)' : def.secondary}
                  className="w-full px-2.5 py-1.5 border border-gray-200 rounded-lg text-xs font-mono focus:ring-2 focus:ring-indigo-400 outline-none" />
              )}
              <input value={model} onChange={(e) => setModel(e.target.value)} placeholder="model (editable)"
                className="w-full px-2.5 py-1.5 border border-gray-200 rounded-lg text-xs font-mono focus:ring-2 focus:ring-indigo-400 outline-none" />
            </>
          ) : (
            <>
              <div className="px-2.5 py-1.5 bg-gray-50 border border-gray-100 rounded-lg text-xs font-mono text-gray-500 truncate">
                {saved?.hasKey ? <>🔑 {saved.keyMasked}</> : <span className="text-gray-400">no key yet</span>}
              </div>
              {(saved?.model || def.model) && (
                <div className="px-2.5 py-1.5 bg-gray-50 border border-gray-100 rounded-lg text-xs font-mono text-gray-500 truncate">{saved?.model || def.model}</div>
              )}
            </>
          )
        ) : (
          <div className="text-xs text-gray-400 px-1 py-1.5">{def.note}</div>
        )}
      </div>

      {/* footer */}
      <div className="mt-3 flex items-center justify-between gap-2">
        <button onClick={activate} disabled={busy || isActive}
          title={isActive ? 'Enabled' : 'Enable this provider'}
          className={`relative w-11 h-6 rounded-full transition shrink-0 ${isActive ? 'bg-indigo-600' : 'bg-gray-300'} ${busy ? 'opacity-50' : ''}`}>
          <span className={`absolute top-0.5 w-5 h-5 rounded-full bg-white transition-all ${isActive ? 'left-[22px]' : 'left-0.5'}`} />
        </button>

        {def.needsKey && (
          editing ? (
            <div className="flex items-center gap-1">
              <button onClick={save} disabled={busy} className="px-3 py-1.5 bg-indigo-600 text-white rounded-lg text-xs font-semibold hover:bg-indigo-700 disabled:opacity-50 inline-flex items-center gap-1">
                {busy ? <Loader2 className="w-3 h-3 animate-spin" /> : <Save className="w-3 h-3" />} Save
              </button>
              <button onClick={() => { setEditing(false); setKey(''); setSecondary(''); }} className="p-1.5 text-gray-400 hover:text-gray-600"><X className="w-4 h-4" /></button>
            </div>
          ) : (
            <button onClick={() => setEditing(true)} className="px-3 py-1.5 bg-white border border-gray-200 rounded-lg text-xs font-semibold text-gray-700 hover:border-indigo-300 hover:text-indigo-600">
              {saved?.hasKey ? 'Edit key' : 'Add key'}
            </button>
          )
        )}
      </div>
    </div>
  );
}
