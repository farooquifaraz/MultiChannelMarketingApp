import { useState } from 'react';
import { PenLine, Loader2, Sparkles, Copy, MessageSquare, Instagram, Mail, Image as ImageIcon } from 'lucide-react';
import toast from 'react-hot-toast';
import { creativesApi, type MarketingContent } from '../../api/creativesApi';

const EXAMPLE = 'Example: 5BR Villa at The Oasis by Emaar, Dubailand. AED 16,500,000. 8,500 sqft. Private pool, rooftop terrace, smart home, Italian marble. 5 min to Global Village. Handover Q4 2026. Agent: Ahmed, +971 50 123 4567, Luxe Properties.';

export default function ContentStudioPage() {
  const [brief, setBrief] = useState('');
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<MarketingContent | null>(null);

  const generate = async () => {
    if (brief.trim().length < 20) { toast.error('Please paste a longer brief (min 20 chars)'); return; }
    setBusy(true);
    try {
      const res: any = await creativesApi.content(brief.trim());
      setResult(res.data);
      toast.success(`Generated with ${res.data.provider || 'AI'}`);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Generation failed — check the active AI provider in Integrations');
    } finally {
      setBusy(false);
    }
  };

  const copy = (text: string) => { navigator.clipboard.writeText(text); toast.success('Copied'); };

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <div className="p-2.5 bg-gradient-to-br from-indigo-500 to-purple-600 rounded-xl"><PenLine className="w-6 h-6 text-white" /></div>
        <div>
          <h1 className="text-2xl font-bold text-gray-900">AI Copywriter</h1>
          <p className="text-gray-500 text-sm">Paste a property/offer brief → get ready-to-send WhatsApp, Instagram &amp; Email copy.</p>
        </div>
      </div>

      <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5 space-y-3">
        <textarea
          value={brief}
          onChange={(e) => setBrief(e.target.value)}
          rows={5}
          maxLength={10000}
          placeholder={EXAMPLE}
          className="w-full px-3 py-2 border border-gray-200 rounded-lg text-sm focus:ring-2 focus:ring-indigo-400 outline-none resize-y"
        />
        <div className="flex items-center gap-2">
          <button onClick={generate} disabled={busy} className="px-4 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 inline-flex items-center gap-2">
            {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <Sparkles className="w-4 h-4" />}
            {busy ? 'Writing…' : 'Generate copy'}
          </button>
          <span className="text-xs text-gray-400">Uses your active AI provider (Integrations). Gemini text has a free tier.</span>
        </div>
      </div>

      {result && (
        <div className="grid md:grid-cols-3 gap-4">
          <Block icon={MessageSquare} tone="bg-green-500" title="WhatsApp" onCopy={() => copy(result.whatsApp.broadcast)}>
            <pre className="whitespace-pre-wrap text-sm text-gray-700 font-sans">{result.whatsApp.broadcast}</pre>
            {result.whatsApp.statusText && <p className="mt-3 pt-3 border-t text-xs text-gray-500"><b>Status:</b> {result.whatsApp.statusText}</p>}
          </Block>

          <Block icon={Instagram} tone="bg-pink-500" title="Instagram" onCopy={() => copy(result.instagram.caption)}>
            <pre className="whitespace-pre-wrap text-sm text-gray-700 font-sans">{result.instagram.caption}</pre>
            {result.instagram.reelsHook && <p className="mt-2 text-xs text-gray-500"><b>Reels:</b> {result.instagram.reelsHook}</p>}
            {result.instagram.storyCta && <p className="text-xs text-gray-500"><b>Story:</b> {result.instagram.storyCta}</p>}
          </Block>

          <Block icon={Mail} tone="bg-blue-500" title="Email" onCopy={() => copy(result.email.body)}>
            <p className="text-sm font-semibold text-gray-800">{result.email.subject}</p>
            {result.email.preview && <p className="text-xs text-gray-400 mb-2">{result.email.preview}</p>}
            <div className="text-sm text-gray-700 prose prose-sm max-w-none" dangerouslySetInnerHTML={{ __html: result.email.body }} />
          </Block>

          {result.imagePrompt && (
            <div className="md:col-span-3 bg-indigo-50 border border-indigo-100 rounded-2xl p-4 flex items-start gap-2">
              <ImageIcon className="w-4 h-4 text-indigo-500 mt-0.5" />
              <div className="flex-1">
                <p className="text-xs font-semibold text-indigo-700">Suggested image prompt (paste into Banner Studio)</p>
                <p className="text-sm text-gray-700">{result.imagePrompt}</p>
              </div>
              <button onClick={() => copy(result.imagePrompt)} className="text-indigo-500 hover:text-indigo-700"><Copy className="w-4 h-4" /></button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function Block({ icon: Icon, tone, title, onCopy, children }: { icon: any; tone: string; title: string; onCopy: () => void; children: React.ReactNode }) {
  return (
    <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden flex flex-col">
      <div className={`${tone} text-white px-4 py-2 flex items-center justify-between`}>
        <span className="font-semibold text-sm inline-flex items-center gap-2"><Icon className="w-4 h-4" />{title}</span>
        <button onClick={onCopy} title="Copy" className="hover:opacity-80"><Copy className="w-4 h-4" /></button>
      </div>
      <div className="p-4 overflow-auto max-h-[480px]">{children}</div>
    </div>
  );
}
