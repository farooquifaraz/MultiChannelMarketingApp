import { useState } from 'react';
import { PenLine, Image as ImageIcon, Sparkles } from 'lucide-react';
import ContentStudioPage from './ContentStudioPage';
import BannerStudioPage from './BannerStudioPage';

type StudioTab = 'copy' | 'banner';

/**
 * Creative Studio — single page, two tabs:
 *   • AI Copywriter  (multi-channel copy + a matching hero-image prompt)
 *   • Banner Studio  (text-to-image generation)
 *
 * Both tabs stay mounted (toggled with `hidden`) so state survives tab switches and the
 * "Generate this image →" button can hand its prompt straight to the Banner tab.
 */
export default function CreativeStudioPage({ initialTab = 'copy' }: { initialTab?: StudioTab } = {}) {
  const [tab, setTab] = useState<StudioTab>(initialTab);
  const [seedPrompt, setSeedPrompt] = useState('');
  const [seedNonce, setSeedNonce] = useState(0);

  // Copywriter → Banner handoff: fill the Banner composer + jump to that tab.
  const sendToBanner = (p: string) => {
    setSeedPrompt(p);
    setSeedNonce((n) => n + 1);
    setTab('banner');
  };

  const tabs: { id: StudioTab; label: string; icon: any }[] = [
    { id: 'copy', label: 'AI Copywriter', icon: PenLine },
    { id: 'banner', label: 'Banner Studio', icon: ImageIcon },
  ];

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex items-center gap-3">
        <div className="p-2.5 bg-gradient-to-br from-primary-500 to-purple-600 rounded-xl">
          <Sparkles className="w-6 h-6 text-white" />
        </div>
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Creative Studio</h1>
          <p className="text-gray-500 text-sm">Write multi-channel copy and generate matching visuals — all in one place.</p>
        </div>
      </div>

      {/* Tab switcher */}
      <div className="inline-flex p-1 bg-gray-100 rounded-xl">
        {tabs.map((t) => {
          const active = tab === t.id;
          return (
            <button
              key={t.id}
              onClick={() => setTab(t.id)}
              className={`inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all ${
                active ? 'bg-white text-primary-700 shadow-sm' : 'text-gray-500 hover:text-gray-800'
              }`}
            >
              <t.icon className="w-4 h-4" />
              {t.label}
            </button>
          );
        })}
      </div>

      {/* Both tabs mounted; toggled so state persists across switches */}
      <div className={tab === 'copy' ? '' : 'hidden'}>
        <ContentStudioPage embedded onSendToBanner={sendToBanner} />
      </div>
      <div className={tab === 'banner' ? '' : 'hidden'}>
        <BannerStudioPage embedded seedPrompt={seedPrompt} seedNonce={seedNonce} />
      </div>
    </div>
  );
}
