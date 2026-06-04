import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Plug, Loader2, Sparkles, Image as ImageIcon, CreditCard, Check, KeyRound, Save, ExternalLink,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { adminApi } from '../../api/settingsApi';
import { useAuthStore } from '../../store/authStore';

/** Provider catalogues per category (label, free?, key needed?, default model, docs). */
const AI_PROVIDERS = [
  { key: 'disabled', label: 'Disabled', free: true, needsKey: false },
  { key: 'anthropic', label: 'Anthropic (Claude)', free: false, needsKey: true, model: 'claude-sonnet-4-5' },
  { key: 'openai', label: 'OpenAI (GPT)', free: false, needsKey: true, model: 'gpt-4o' },
  { key: 'gemini', label: 'Google Gemini', free: true, needsKey: true, model: 'gemini-1.5-flash', note: 'Free tier on AI Studio' },
  { key: 'grok', label: 'xAI (Grok)', free: false, needsKey: true, model: 'grok-2' },
  { key: 'openai-compatible', label: 'OpenAI-compatible (Groq/Ollama/OpenRouter)', free: true, needsKey: true, model: 'llama-3.3-70b-versatile', note: 'Groq/Ollama have free tiers' },
];

const IMAGE_PROVIDERS = [
  { key: 'disabled', label: 'Disabled', free: true, needsKey: false },
  { key: 'mock', label: 'Built-in placeholder', free: true, needsKey: false, note: 'Works offline, no key' },
  { key: 'pollinations', label: 'Pollinations.ai', free: true, needsKey: false, note: '100% FREE · no key needed' },
  { key: 'huggingface', label: 'Hugging Face', free: true, needsKey: true, model: 'black-forest-labs/FLUX.1-schnell', note: 'Free tier · free token' },
  { key: 'gemini', label: 'Google Gemini (Imagen)', free: true, needsKey: true, model: 'gemini-2.0-flash-preview-image-generation', note: 'Free tier on AI Studio' },
  { key: 'dalle', label: 'OpenAI DALL·E 3', free: false, needsKey: true, model: 'dall-e-3' },
  { key: 'stability', label: 'Stability AI', free: false, needsKey: true, model: 'core' },
];

const PAYMENT_PROVIDERS = [
  { key: 'disabled', label: 'Disabled', free: true, needsKey: false },
  { key: 'mock', label: 'Test mode (instant activate)', free: true, needsKey: false, note: 'No real charge — for testing' },
  { key: 'stripe', label: 'Stripe', free: false, needsKey: true, note: 'Cards + Apple/Google Pay (AED)' },
];

function StatusBadge({ active, configured }: { active: boolean; configured: boolean }) {
  if (active && configured) return <span className="text-xs px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 inline-flex items-center gap-1"><Check className="w-3 h-3" /> Active</span>;
  if (active) return <span className="text-xs px-2 py-0.5 rounded-full bg-amber-50 text-amber-700 border border-amber-200">Selected · key needed</span>;
  return <span className="text-xs px-2 py-0.5 rounded-full bg-gray-100 text-gray-500 border border-gray-200">Inactive</span>;
}

export default function IntegrationsPage() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const isAdmin = user?.role?.toLowerCase() === 'admin';

  const [s, setS] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  // New-key inputs (kept separate so we only send a key when the admin actually types one).
  const [aiKey, setAiKey] = useState('');
  const [imgKey, setImgKey] = useState('');
  const [payKey, setPayKey] = useState('');
  const [paySecret, setPaySecret] = useState('');

  useEffect(() => {
    if (!isAdmin) { toast.error('Admin access required'); navigate('/dashboard'); }
  }, [isAdmin, navigate]);

  const load = async () => {
    try {
      const res: any = await adminApi.getSystemSettings();
      setS(res.data);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to load settings');
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { if (isAdmin) load(); /* eslint-disable-next-line */ }, [isAdmin]);

  const save = async () => {
    setSaving(true);
    try {
      // Send the full settings object; only include a key when the admin entered a new one
      // (blank → null → backend keeps the existing key).
      const payload: any = { ...s };
      payload.aiApiKey = aiKey.trim() ? aiKey.trim() : null;
      payload.imageApiKey = imgKey.trim() ? imgKey.trim() : null;
      payload.paymentApiKey = payKey.trim() ? payKey.trim() : null;
      payload.paymentWebhookSecret = paySecret.trim() ? paySecret.trim() : null;
      // Don't accidentally clear the AI fallback key.
      payload.aiFallbackApiKey = payload.aiFallbackApiKey?.trim?.() ? payload.aiFallbackApiKey : null;

      const res: any = await adminApi.updateSystemSettings(payload);
      toast.success('Integrations saved');
      setAiKey(''); setImgKey(''); setPayKey(''); setPaySecret('');
      setS(res.data);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Save failed');
    } finally {
      setSaving(false);
    }
  };

  if (!isAdmin) return null;
  if (loading || !s) return <div className="flex items-center justify-center h-64"><Loader2 className="w-6 h-6 animate-spin text-indigo-500" /></div>;

  const aiActive = s.aiProvider && s.aiProvider !== 'disabled';
  const imgActive = s.imageProvider && s.imageProvider !== 'disabled';
  const payActive = s.paymentProvider && s.paymentProvider !== 'disabled';
  const aiSel = AI_PROVIDERS.find((p) => p.key === s.aiProvider);
  const imgSel = IMAGE_PROVIDERS.find((p) => p.key === s.imageProvider);
  const paySel = PAYMENT_PROVIDERS.find((p) => p.key === s.paymentProvider);

  const inputCls = 'w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-400 outline-none';
  const labelCls = 'block text-xs font-medium text-gray-500 mb-1';

  return (
    <div className="space-y-6 pb-24">
      <div className="flex items-center gap-3">
        <div className="p-2.5 bg-gradient-to-br from-indigo-500 to-purple-600 rounded-xl">
          <Plug className="w-6 h-6 text-white" />
        </div>
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Integrations</h1>
          <p className="text-gray-500 text-sm">Connect &amp; enable your AI, image, and payment providers in one place.</p>
        </div>
      </div>

      {/* AI TEXT */}
      <section className="bg-white rounded-2xl shadow-sm border border-gray-100 p-6 space-y-4">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <div className="flex items-center gap-2"><Sparkles className="w-5 h-5 text-indigo-500" /><h2 className="font-semibold text-gray-900">AI Text (inbox replies + chat)</h2></div>
          <StatusBadge active={!!aiActive} configured={!!s.aiApiKeyMasked} />
        </div>
        <div className="grid sm:grid-cols-2 gap-4">
          <div>
            <label className={labelCls}>Provider</label>
            <select value={s.aiProvider || 'disabled'} onChange={(e) => setS({ ...s, aiProvider: e.target.value, aiModel: AI_PROVIDERS.find(p=>p.key===e.target.value)?.model || s.aiModel })} className={inputCls}>
              {AI_PROVIDERS.map((p) => <option key={p.key} value={p.key}>{p.label}{p.free ? ' · free' : ''}</option>)}
            </select>
            {aiSel?.note && <p className="text-xs text-emerald-600 mt-1">{aiSel.note}</p>}
          </div>
          <div>
            <label className={labelCls}>Model</label>
            <input value={s.aiModel || ''} onChange={(e) => setS({ ...s, aiModel: e.target.value })} className={inputCls} />
          </div>
          {aiSel?.needsKey && (
            <div className="sm:col-span-2">
              <label className={labelCls}><KeyRound className="w-3 h-3 inline mr-1" />API Key {s.aiApiKeyMasked && <span className="text-emerald-600">(saved: {s.aiApiKeyMasked} — leave blank to keep)</span>}</label>
              <input type="password" value={aiKey} onChange={(e) => setAiKey(e.target.value)} placeholder={s.aiApiKeyMasked ? '•••• keep existing' : 'Paste API key'} className={inputCls} />
            </div>
          )}
        </div>
      </section>

      {/* IMAGE */}
      <section className="bg-white rounded-2xl shadow-sm border border-gray-100 p-6 space-y-4">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <div className="flex items-center gap-2"><ImageIcon className="w-5 h-5 text-indigo-500" /><h2 className="font-semibold text-gray-900">Image Generation (Banner Studio)</h2></div>
          <StatusBadge active={!!imgActive} configured={!imgSel?.needsKey || !!s.imageApiKeyMasked} />
        </div>
        <div className="grid sm:grid-cols-2 gap-4">
          <div>
            <label className={labelCls}>Provider</label>
            <select value={s.imageProvider || 'mock'} onChange={(e) => setS({ ...s, imageProvider: e.target.value, imageModel: IMAGE_PROVIDERS.find(p=>p.key===e.target.value)?.model || s.imageModel })} className={inputCls}>
              {IMAGE_PROVIDERS.map((p) => <option key={p.key} value={p.key}>{p.label}{p.free ? ' · free' : ''}</option>)}
            </select>
            {imgSel?.note && <p className="text-xs text-emerald-600 mt-1">{imgSel.note}</p>}
          </div>
          <div>
            <label className={labelCls}>Model</label>
            <input value={s.imageModel || ''} onChange={(e) => setS({ ...s, imageModel: e.target.value })} className={inputCls} placeholder="(provider default)" />
          </div>
          {imgSel?.needsKey && (
            <div className="sm:col-span-2">
              <label className={labelCls}><KeyRound className="w-3 h-3 inline mr-1" />API Key {s.imageApiKeyMasked && <span className="text-emerald-600">(saved: {s.imageApiKeyMasked} — leave blank to keep)</span>}</label>
              <input type="password" value={imgKey} onChange={(e) => setImgKey(e.target.value)} placeholder={s.imageApiKeyMasked ? '•••• keep existing' : 'Paste API key / token'} className={inputCls} />
            </div>
          )}
        </div>
      </section>

      {/* PAYMENTS */}
      <section className="bg-white rounded-2xl shadow-sm border border-gray-100 p-6 space-y-4">
        <div className="flex items-center justify-between gap-2 flex-wrap">
          <div className="flex items-center gap-2"><CreditCard className="w-5 h-5 text-indigo-500" /><h2 className="font-semibold text-gray-900">Payments (plan upgrades)</h2></div>
          <StatusBadge active={!!payActive} configured={!paySel?.needsKey || !!s.paymentApiKeyMasked} />
        </div>
        <div className="grid sm:grid-cols-2 gap-4">
          <div>
            <label className={labelCls}>Provider</label>
            <select value={s.paymentProvider || 'mock'} onChange={(e) => setS({ ...s, paymentProvider: e.target.value })} className={inputCls}>
              {PAYMENT_PROVIDERS.map((p) => <option key={p.key} value={p.key}>{p.label}{p.free ? ' · free' : ''}</option>)}
            </select>
            {paySel?.note && <p className="text-xs text-emerald-600 mt-1">{paySel.note}</p>}
          </div>
          {paySel?.needsKey && (
            <>
              <div>
                <label className={labelCls}><KeyRound className="w-3 h-3 inline mr-1" />Secret Key {s.paymentApiKeyMasked && <span className="text-emerald-600">(saved — blank=keep)</span>}</label>
                <input type="password" value={payKey} onChange={(e) => setPayKey(e.target.value)} placeholder={s.paymentApiKeyMasked ? '•••• keep existing' : 'sk_live_…'} className={inputCls} />
              </div>
              <div>
                <label className={labelCls}>Webhook signing secret {s.paymentWebhookSecretSet && <span className="text-emerald-600">(saved — blank=keep)</span>}</label>
                <input type="password" value={paySecret} onChange={(e) => setPaySecret(e.target.value)} placeholder={s.paymentWebhookSecretSet ? '•••• keep existing' : 'whsec_…'} className={inputCls} />
              </div>
              <p className="sm:col-span-2 text-xs text-gray-500 flex items-center gap-1">
                <ExternalLink className="w-3 h-3" /> Point your Stripe webhook to <code className="bg-gray-100 px-1 rounded">/api/v1/webhooks/payments/stripe</code>
              </p>
            </>
          )}
        </div>
      </section>

      {/* sticky save */}
      <div className="fixed bottom-0 left-0 right-0 sm:left-64 bg-white/90 backdrop-blur border-t border-gray-200 px-6 py-3 flex justify-end">
        <button onClick={save} disabled={saving} className="px-5 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 inline-flex items-center gap-2">
          {saving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
          {saving ? 'Saving…' : 'Save integrations'}
        </button>
      </div>
    </div>
  );
}
