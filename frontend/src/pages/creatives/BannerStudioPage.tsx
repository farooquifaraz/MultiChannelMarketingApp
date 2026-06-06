import { useState, useEffect } from 'react';
import { Image as ImageIcon, Loader2, Sparkles, AlertCircle, Download, Palette, Plus, Trash2, Star, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { creativesApi, type GeneratedAsset, type ImageSizeOption, type PromptPreset } from '../../api/creativesApi';
import { brandKitsApi, type BrandKit } from '../../api/brandKitsApi';

export default function BannerStudioPage() {
  const [prompt, setPrompt] = useState('');
  const [size, setSize] = useState('1024x1024');
  const [sizes, setSizes] = useState<ImageSizeOption[]>([]);
  const [presets, setPresets] = useState<PromptPreset[]>([]);
  const [assets, setAssets] = useState<GeneratedAsset[]>([]);
  const [kits, setKits] = useState<BrandKit[]>([]);
  const [brandKitId, setBrandKitId] = useState('');
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);
  const [showKits, setShowKits] = useState(false);
  const [broken, setBroken] = useState<Set<string>>(new Set());

  // New brand kit form
  const [kName, setKName] = useState('');
  const [kPrimary, setKPrimary] = useState('#4f46e5');
  const [kSecondary, setKSecondary] = useState('#a855f7');
  const [kFont, setKFont] = useState('Inter');

  const load = async () => {
    try {
      const [s, p, a, k]: any[] = await Promise.all([
        creativesApi.sizes(), creativesApi.presets(), creativesApi.assets(), brandKitsApi.list(),
      ]);
      setSizes(s.data || []);
      setPresets(p.data || []);
      setAssets(a.data || []);
      setKits(k.data || []);
      if (s.data?.length && !s.data.find((o: ImageSizeOption) => o.token === size)) setSize(s.data[0].token);
      const def = (k.data || []).find((x: BrandKit) => x.isDefault);
      if (def) setBrandKitId(def.id);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Failed to load studio');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, []);

  const generate = async () => {
    if (!prompt.trim()) { toast.error('Describe the banner you want'); return; }
    setGenerating(true);
    try {
      const res: any = await creativesApi.generate(prompt.trim(), size, brandKitId || null);
      const asset: GeneratedAsset = res.data;
      if (asset.status === 'failed') toast.error(asset.errorMessage || 'Generation failed');
      else toast.success('Banner generated');
      setAssets((prev) => [asset, ...prev]);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Generation failed');
    } finally {
      setGenerating(false);
    }
  };

  const createKit = async () => {
    if (!kName.trim()) { toast.error('Brand kit name required'); return; }
    try {
      await brandKitsApi.create({
        name: kName.trim(), primaryColor: kPrimary, secondaryColor: kSecondary,
        fontFamily: kFont, isDefault: kits.length === 0,
      });
      toast.success('Brand kit saved');
      setKName('');
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Could not save brand kit');
    }
  };

  const deleteKit = async (id: string) => {
    try {
      await brandKitsApi.remove(id);
      if (brandKitId === id) setBrandKitId('');
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Delete failed');
    }
  };

  const deleteAsset = async (id: string) => {
    // optimistic remove
    const prev = assets;
    setAssets((a) => a.filter((x) => x.id !== id));
    try {
      await creativesApi.deleteAsset(id);
    } catch (err: any) {
      setAssets(prev); // restore on failure
      toast.error(err?.response?.data?.message || 'Delete failed');
    }
  };

  const makeDefault = async (k: BrandKit) => {
    try {
      await brandKitsApi.update(k.id, { ...k, isDefault: true });
      await load();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Update failed');
    }
  };

  if (loading) {
    return <div className="flex items-center justify-center h-64"><Loader2 className="w-6 h-6 animate-spin text-indigo-500" /></div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <div className="flex items-center gap-3">
          <div className="p-2.5 bg-gradient-to-br from-indigo-500 to-purple-600 rounded-xl">
            <ImageIcon className="w-6 h-6 text-white" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-gray-900">Banner Studio</h1>
            <p className="text-gray-500 text-sm">Generate on-brand banner &amp; flyer images from a text prompt.</p>
          </div>
        </div>
        <button onClick={() => setShowKits((v) => !v)} className="inline-flex items-center gap-2 px-3 py-2 text-sm border border-gray-200 rounded-lg hover:bg-gray-50">
          <Palette className="w-4 h-4" /> Brand Kits ({kits.length})
        </button>
      </div>

      <div className="bg-indigo-50 border border-indigo-100 rounded-xl px-4 py-3 text-sm text-indigo-900">
        <b>What it does:</b> type a prompt (or click a preset) → AI generates a <b>banner / flyer image</b>.
        Apply a <b>Brand Kit</b> (logo, colours, font) so it matches your brand. Switch image engines anytime
        in <b>Integrations</b> (FLUX, Nano Banana, etc.). Tip: the <b>AI Copywriter</b> gives you a ready image prompt.
      </div>

      {/* Brand kits manager */}
      {showKits && (
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5 space-y-4">
          <div className="flex gap-2 flex-wrap items-end">
            <div>
              <label className="block text-xs text-gray-500 mb-1">Name</label>
              <input value={kName} onChange={(e) => setKName(e.target.value)} placeholder="Acme Realty" className="px-3 py-2 border border-gray-200 rounded-lg text-sm" />
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">Primary</label>
              <input type="color" value={kPrimary} onChange={(e) => setKPrimary(e.target.value)} className="h-9 w-14 border border-gray-200 rounded-lg" />
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">Secondary</label>
              <input type="color" value={kSecondary} onChange={(e) => setKSecondary(e.target.value)} className="h-9 w-14 border border-gray-200 rounded-lg" />
            </div>
            <div>
              <label className="block text-xs text-gray-500 mb-1">Font</label>
              <input value={kFont} onChange={(e) => setKFont(e.target.value)} className="px-3 py-2 border border-gray-200 rounded-lg text-sm w-28" />
            </div>
            <button onClick={createKit} className="px-4 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 inline-flex items-center gap-1">
              <Plus className="w-4 h-4" /> Add
            </button>
          </div>
          <div className="flex flex-wrap gap-2">
            {kits.map((k) => (
              <div key={k.id} className="flex items-center gap-2 border border-gray-200 rounded-lg px-3 py-2">
                <span className="w-4 h-4 rounded-full" style={{ background: k.primaryColor }} />
                <span className="text-sm text-gray-700">{k.name}</span>
                {k.isDefault
                  ? <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-amber-50 text-amber-700 border border-amber-200">default</span>
                  : <button onClick={() => makeDefault(k)} title="Make default" className="text-gray-400 hover:text-amber-500"><Star className="w-3.5 h-3.5" /></button>}
                <button onClick={() => deleteKit(k.id)} title="Delete" className="text-gray-400 hover:text-rose-500"><Trash2 className="w-3.5 h-3.5" /></button>
              </div>
            ))}
            {kits.length === 0 && <span className="text-sm text-gray-400">No brand kits yet.</span>}
          </div>
        </div>
      )}

      {/* Composer */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5 space-y-3">
        {/* Preset chips */}
        <div className="flex flex-wrap gap-2">
          {presets.map((p) => (
            <button key={p.title} onClick={() => setPrompt(p.prompt)} className="text-xs px-3 py-1.5 rounded-full bg-indigo-50 text-indigo-700 hover:bg-indigo-100 border border-indigo-100">
              {p.title}
            </button>
          ))}
        </div>
        <textarea
          value={prompt}
          onChange={(e) => setPrompt(e.target.value)}
          maxLength={1000}
          rows={3}
          placeholder="e.g. A modern Dubai real-estate flyer for a luxury 3-bedroom apartment, gold and navy, elegant"
          className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-400 outline-none resize-y"
        />
        <div className="flex gap-2 flex-wrap items-center">
          <select value={size} onChange={(e) => setSize(e.target.value)} className="px-3 py-2 border border-gray-200 rounded-lg text-sm">
            {sizes.map((o) => <option key={o.token} value={o.token}>{o.label} ({o.token})</option>)}
          </select>
          <select value={brandKitId} onChange={(e) => setBrandKitId(e.target.value)} className="px-3 py-2 border border-gray-200 rounded-lg text-sm">
            <option value="">No brand kit</option>
            {kits.map((k) => <option key={k.id} value={k.id}>{k.name}{k.isDefault ? ' (default)' : ''}</option>)}
          </select>
          <button onClick={generate} disabled={generating} className="px-4 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 inline-flex items-center gap-2">
            {generating ? <Loader2 className="w-4 h-4 animate-spin" /> : <Sparkles className="w-4 h-4" />}
            {generating ? 'Generating…' : 'Generate'}
          </button>
          <span className="text-xs text-gray-400 ml-auto">{prompt.length}/1000</span>
        </div>
      </div>

      {/* Gallery — fixed small cards that reflow (don't balloon on zoom/wide screens) */}
      <div className="grid gap-3" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))' }}>
        {assets.map((a) => (
          <div key={a.id} className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden relative group">
            <button
              onClick={() => deleteAsset(a.id)}
              title="Delete banner"
              className="absolute top-2 right-2 z-10 p-1.5 rounded-full bg-white/90 text-gray-500 hover:text-rose-600 hover:bg-white shadow-sm border border-gray-200 opacity-0 group-hover:opacity-100 transition-opacity"
            >
              <X className="w-4 h-4" />
            </button>
            {a.status === 'completed' && a.imageUrl && !broken.has(a.id) ? (
              <a href={a.imageUrl} download={`banner-${a.id}`} title="Open / download">
                <img
                  src={a.imageUrl}
                  alt={a.prompt}
                  className="w-full aspect-square object-cover bg-gray-50"
                  onError={() => setBroken((prev) => new Set(prev).add(a.id))}
                />
              </a>
            ) : (
              <div className="w-full aspect-square flex flex-col items-center justify-center bg-rose-50 text-rose-500 gap-2 p-4 text-center">
                <AlertCircle className="w-6 h-6" />
                <span className="text-xs">{broken.has(a.id) ? 'Image could not load (provider rate-limited?). Try again or switch provider.' : (a.errorMessage || 'Failed')}</span>
              </div>
            )}
            <div className="p-3">
              <p className="text-sm text-gray-700 line-clamp-2">{a.prompt}</p>
              {a.status === 'completed' && a.errorMessage && (
                <p className="mt-1 text-[11px] text-amber-600 line-clamp-2" title={a.errorMessage}>⚠ {a.errorMessage}</p>
              )}
              <div className="flex items-center justify-between mt-2 text-xs text-gray-400">
                <span>{a.size} · {a.provider}</span>
                {a.status === 'completed' && a.imageUrl && (
                  <a href={a.imageUrl} download={`banner-${a.id}.svg`} className="inline-flex items-center gap-1 text-indigo-500 hover:text-indigo-700">
                    <Download className="w-3.5 h-3.5" /> Save
                  </a>
                )}
              </div>
            </div>
          </div>
        ))}
        {assets.length === 0 && (
          <div className="col-span-full text-center text-gray-400 py-12">No banners yet — generate your first above.</div>
        )}
      </div>
    </div>
  );
}
