import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Plug, Loader2, Sparkles, Image as ImageIcon, CreditCard, Check, Save, Pencil, X } from 'lucide-react';
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
    title: 'AI Text', subtitle: 'Powers inbox replies, chat & the AI Copywriter', icon: Sparkles,
    providers: [
      { key: 'gemini', label: 'Google Gemini', free: true, needsKey: true, model: 'gemini-2.5-flash', note: 'Generous free tier' },
      { key: 'openai', label: 'OpenAI (GPT)', free: false, needsKey: true, model: 'gpt-4o' },
      { key: 'anthropic', label: 'Anthropic (Claude)', free: false, needsKey: true, model: 'claude-sonnet-4-5' },
      { key: 'grok', label: 'xAI (Grok)', free: false, needsKey: true, model: 'grok-2' },
      { key: 'openai-compatible', label: 'OpenAI-compatible (Groq / Ollama / OpenRouter)', free: true, needsKey: true, model: 'llama-3.3-70b-versatile', note: 'Groq & Ollama have free tiers' },
      { key: 'disabled', label: 'Disabled', free: true, needsKey: false },
    ],
  },
  image: {
    title: 'Image Generation', subtitle: 'Powers the Banner Studio', icon: ImageIcon,
    providers: [
      { key: 'flux', label: 'FLUX — Black Forest Labs', free: false, needsKey: true, model: 'flux-dev', note: 'Cheapest real photos · ~$0.025/img (~400 per $10)', cost: '~$0.025/img' },
      { key: 'gemini', label: 'Google Nano Banana', free: false, needsKey: true, model: 'gemini-2.5-flash-image', note: 'Gemini 2.5 Flash Image · ~$0.039/img', cost: '~$0.039/img' },
      { key: 'dalle', label: 'OpenAI — gpt-image-1', free: false, needsKey: true, model: 'gpt-image-1', note: 'Needs verified org + credits', cost: '~$0.04/img' },
      { key: 'stability', label: 'Stability AI', free: false, needsKey: true, model: 'core', cost: '~$0.03/img' },
      { key: 'huggingface', label: 'Hugging Face', free: true, needsKey: true, model: 'black-forest-labs/FLUX.1-schnell', note: 'Free token' },
      { key: 'pollinations', label: 'Pollinations.ai', free: true, needsKey: false, note: 'Free · optional token raises limit' },
      { key: 'mock', label: 'Built-in placeholder', free: true, needsKey: false, note: 'Always works · no key' },
      { key: 'disabled', label: 'Disabled', free: true, needsKey: false },
    ],
  },
  payment: {
    title: 'Payments', subtitle: 'Powers plan upgrades & checkout', icon: CreditCard,
    providers: [
      { key: 'stripe', label: 'Stripe', free: false, needsKey: true, secondary: 'Webhook signing secret', note: 'Cards + Apple / Google Pay' },
      { key: 'mock', label: 'Test mode', free: true, needsKey: false, note: 'Activates instantly · no real charge' },
      { key: 'disabled', label: 'Disabled', free: true, needsKey: false },
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

function Logo({ provider }: { provider: string }) {
  const b = BRAND[provider] || BRAND.disabled;
  return (
    <span style={{ background: b.bg, color: b.fg }}
      className="w-9 h-9 rounded-lg flex items-center justify-center text-base font-bold shrink-0 shadow-sm">
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
    <div className="max-w-4xl mx-auto space-y-6">
      <div className="flex items-center gap-3">
        <div className="p-2.5 bg-gradient-to-br from-indigo-500 to-purple-600 rounded-xl"><Plug className="w-6 h-6 text-white" /></div>
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Integrations</h1>
          <p className="text-gray-500 text-sm">Save each provider's key once, then flip the switch to enable it — just like swapping accounts.</p>
        </div>
      </div>

      {(['ai', 'image', 'payment'] as const).map((cat) => (
        <CategoryCard key={cat} category={cat} state={data[cat]} onChanged={load} />
      ))}
    </div>
  );
}

function CategoryCard({ category, state, onChanged }: { category: string; state?: CategoryCredentials; onChanged: () => void }) {
  const cfg = CATALOG[category];
  const Icon = cfg.icon;
  const active = state?.activeProvider || 'disabled';
  const byProvider: Record<string, CredentialRow> = {};
  (state?.credentials || []).forEach((c) => { byProvider[c.provider] = c; });
  const activeDef = cfg.providers.find((p) => p.key === active);

  return (
    <section className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
      <div className="flex items-center justify-between gap-2 px-5 py-4 border-b border-gray-100 bg-gray-50/60">
        <div className="flex items-center gap-2.5">
          <Icon className="w-5 h-5 text-indigo-500" />
          <div>
            <h2 className="font-semibold text-gray-900 leading-tight">{cfg.title}</h2>
            <p className="text-xs text-gray-500">{cfg.subtitle}</p>
          </div>
        </div>
        <div className="text-right">
          <span className="text-[11px] text-gray-400">Active</span>
          <div className="text-sm font-medium text-indigo-600 capitalize">{activeDef?.label || active}</div>
        </div>
      </div>
      <div className="divide-y divide-gray-50">
        {cfg.providers.map((p) => (
          <ProviderRow key={p.key} category={category} def={p} saved={byProvider[p.key]} isActive={active === p.key} onChanged={onChanged} />
        ))}
      </div>
    </section>
  );
}

function ProviderRow({ category, def, saved, isActive, onChanged }:
  { category: string; def: ProviderDef; saved?: CredentialRow; isActive: boolean; onChanged: () => void }) {
  const [editing, setEditing] = useState(false);
  const [key, setKey] = useState('');
  const [secondary, setSecondary] = useState('');
  const [model, setModel] = useState(saved?.model || def.model || '');
  const [busy, setBusy] = useState(false);

  const open = isActive || editing;

  const activate = async () => {
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

  return (
    <div className={isActive ? 'bg-indigo-50/40' : ''}>
      {/* Row header */}
      <div className="flex items-center gap-3 px-5 py-3">
        <input type="radio" checked={isActive} onChange={activate} disabled={busy} className="accent-indigo-600 w-4 h-4 shrink-0" title="Enable this provider" />
        <Logo provider={def.key} />
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-sm font-medium text-gray-900">{def.label}</span>
            {def.free && <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200">free</span>}
            {isActive && <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-indigo-600 text-white inline-flex items-center gap-1"><Check className="w-3 h-3" />enabled</span>}
            {def.cost && <span className="text-[10px] text-gray-400">{def.cost}</span>}
          </div>
          <div className="text-xs text-gray-400 truncate">
            {saved?.hasKey ? <>key <span className="font-mono text-gray-500">{saved.keyMasked}</span></> : (def.needsKey ? 'no key yet' : def.note)}
            {def.needsKey && def.note ? ` · ${def.note}` : ''}
          </div>
        </div>
        {def.needsKey && !open && (
          <button onClick={() => setEditing(true)} className="text-gray-400 hover:text-indigo-600 p-1.5 rounded-lg hover:bg-white" title="Configure key">
            <Pencil className="w-4 h-4" />
          </button>
        )}
      </div>

      {/* Expanded editor (active provider, or when editing) */}
      {def.needsKey && open && (
        <div className="px-5 pb-4 pt-1 ml-12 space-y-2">
          <div>
            <label className="block text-[11px] font-medium text-gray-500 mb-1">API key</label>
            <input type="password" value={key} onChange={(e) => setKey(e.target.value)}
              placeholder={saved?.keyMasked ? `${saved.keyMasked}  (leave blank to keep)` : 'Paste API key / token'}
              className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-400 outline-none font-mono" />
          </div>
          {def.secondary && (
            <div>
              <label className="block text-[11px] font-medium text-gray-500 mb-1">{def.secondary}</label>
              <input type="password" value={secondary} onChange={(e) => setSecondary(e.target.value)}
                placeholder={saved?.secondarySecretSet ? '•••• (leave blank to keep)' : def.secondary}
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-400 outline-none font-mono" />
            </div>
          )}
          <div className="flex items-center gap-2">
            <div className="flex-1">
              <label className="block text-[11px] font-medium text-gray-500 mb-1">Model (editable)</label>
              <input value={model} onChange={(e) => setModel(e.target.value)} placeholder="(provider default)"
                className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-400 outline-none font-mono" />
            </div>
            <button onClick={save} disabled={busy}
              className="self-end px-4 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 inline-flex items-center gap-1.5">
              {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />} Save
            </button>
            {!isActive && (
              <button onClick={() => { setEditing(false); setKey(''); setSecondary(''); }} className="self-end p-2 text-gray-400 hover:text-gray-600" title="Cancel">
                <X className="w-4 h-4" />
              </button>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
