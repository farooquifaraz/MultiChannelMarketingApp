import { useState } from 'react';

export type ChannelId = 'whatsapp' | 'instagram' | 'facebook' | 'email' | 'sms';

export const CHANNEL_META: Record<string, { label: string; slug: string; color: string; head: string }> = {
  whatsapp:  { label: 'WhatsApp',  slug: 'whatsapp',  color: '25D366', head: 'from-green-500 to-emerald-600' },
  instagram: { label: 'Instagram', slug: 'instagram', color: 'E4405F', head: 'from-pink-500 to-purple-600' },
  facebook:  { label: 'Facebook',  slug: 'facebook',  color: '1877F2', head: 'from-blue-500 to-blue-700' },
  email:     { label: 'Email',     slug: 'gmail',     color: 'EA4335', head: 'from-indigo-500 to-indigo-700' },
  sms:       { label: 'SMS',       slug: 'minutemailer', color: '6366F1', head: 'from-slate-500 to-slate-700' },
};

/** Brand logo with monogram fallback (simple-icons CDN). */
export function ChannelLogo({ channel, size = 20, invert = false }: { channel: string; size?: number; invert?: boolean }) {
  const [err, setErr] = useState(false);
  const m = CHANNEL_META[channel] || CHANNEL_META.email;
  if (err) return <span style={{ width: size, height: size, fontSize: size * 0.55 }} className="inline-grid place-items-center rounded font-bold text-current">{m.label[0]}</span>;
  return (
    <img
      src={`https://cdn.simpleicons.org/${m.slug}/${m.color}`}
      alt={m.label} width={size} height={size}
      style={invert ? { filter: 'brightness(0) invert(1)' } : undefined}
      onError={() => setErr(true)}
    />
  );
}

function HeroImage({ img, h }: { img?: string | null; h: number }) {
  if (img) return <div style={{ height: h, backgroundImage: `url(${img})`, backgroundSize: 'cover', backgroundPosition: 'center' }} className="w-full" />;
  return <div style={{ height: h }} className="w-full grid place-items-center text-[11px] text-gray-400 border border-dashed border-gray-300">🖼️ No image</div>;
}

export interface PreviewFields {
  text: string;       // primary copy / email HTML body
  subject?: string;   // email
  isHtml?: boolean;   // render text as HTML (email)
  handle?: string;    // brand handle
  sender?: string;    // email sender
}

/**
 * Renders a copy + image preview the way it would appear on the target platform.
 * `img` null → the image block shows a placeholder (or is omitted entirely when omitImageWhenEmpty).
 */
export function ChannelPreview({ channel, fields, img, omitImageWhenEmpty = false, brand }:
  { channel: string; fields: PreviewFields; img?: string | null; omitImageWhenEmpty?: boolean; brand?: string }) {
  const showImg = img || !omitImageWhenEmpty;
  const cleanBrand = (brand || '').trim();
  const sender = fields.sender || cleanBrand || 'Your Brand';
  const handle = fields.handle || (cleanBrand ? cleanBrand.toLowerCase().replace(/[^a-z0-9]+/g, '.').replace(/^\.|\.$/g, '') : 'your.brand');

  if (channel === 'whatsapp' || channel === 'sms') {
    return (
      <div className="w-[290px] bg-[#0b141a] rounded-2xl p-2.5">
        <div className="flex items-center gap-2 text-gray-200 text-[13px] px-1 pb-2"><span className="w-6 h-6 rounded-full bg-gradient-to-br from-green-500 to-emerald-600" /> {sender}</div>
        <div className="bg-[#005c4b] text-gray-100 rounded-lg rounded-tr-sm p-1.5 ml-auto max-w-[240px] text-[12.5px] leading-snug">
          {showImg && <div className="rounded mb-1.5 overflow-hidden"><HeroImage img={img} h={150} /></div>}
          <span className="whitespace-pre-wrap">{fields.text.slice(0, 360)}</span>
          <span className="block text-right text-[10px] text-gray-400 mt-1">11:24 ✓✓</span>
        </div>
      </div>
    );
  }
  if (channel === 'instagram') {
    return (
      <div className="w-[300px] bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="flex items-center gap-2 p-2.5"><span className="w-7 h-7 rounded-full bg-gradient-to-br from-amber-400 via-pink-500 to-purple-600" /><b className="text-[13px]">{handle}</b><span className="ml-auto text-gray-400">•••</span></div>
        {showImg && <HeroImage img={img} h={280} />}
        <div className="flex gap-3 px-3 py-2 text-lg">❤️ 💬 ✈️ <span className="ml-auto">🔖</span></div>
        <div className="px-3 pb-3 text-[12.5px] leading-snug"><b>{handle}</b> {fields.text.slice(0, 200)}</div>
      </div>
    );
  }
  if (channel === 'facebook') {
    return (
      <div className="w-[310px] bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="flex items-center gap-2 p-2.5"><span className="w-9 h-9 rounded-full bg-gradient-to-br from-blue-500 to-blue-700" /><div><b className="text-[13px] block leading-tight">{sender}</b><small className="text-[11px] text-gray-500">Sponsored · 🌐</small></div></div>
        <div className="px-3 pb-2 text-[13px] leading-snug whitespace-pre-wrap">{fields.text.slice(0, 240)}</div>
        {showImg && <HeroImage img={img} h={210} />}
        <div className="flex gap-4 px-3 py-2 text-[13px] text-gray-500 border-t">👍❤️ 128 · 14 comments · 9 shares</div>
      </div>
    );
  }
  // email
  return (
    <div className="w-[340px] bg-white border border-gray-200 rounded-xl overflow-hidden">
      <div className="px-3 py-2.5 border-b"><div className="font-bold text-[13.5px]">{fields.subject || 'Subject'}</div><div className="text-[11px] text-gray-500 mt-0.5">{sender} · to you</div></div>
      {showImg && <HeroImage img={img} h={130} />}
      {fields.isHtml
        ? <div className="px-3 py-3 text-[12.5px] leading-snug text-gray-700 max-h-[200px] overflow-auto prose prose-sm" dangerouslySetInnerHTML={{ __html: fields.text }} />
        : <div className="px-3 py-3 text-[12.5px] leading-snug text-gray-700 max-h-[200px] overflow-auto whitespace-pre-wrap">{fields.text}</div>}
    </div>
  );
}
