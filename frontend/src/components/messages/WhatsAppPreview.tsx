import type { ReactNode } from 'react';
import { FileText, Play, Check, ArrowLeft, Phone, Video, Mic } from 'lucide-react';

/**
 * A WhatsApp-style preview of the message the recipient will receive (L1 polish), rendered inside a
 * phone mockup for realism. Purely presentational — no network, no side effects. Shows the attached
 * media (image / video poster / document card) with the message text as the caption, exactly as Meta
 * renders a media message on the recipient's phone. URLs and emails in the caption are clickable.
 */
interface Props {
  message: string;
  mediaUrl?: string | null;
  mediaType?: string | null;   // image | video | document
  mediaFileName?: string | null;
  mediaSizeLabel?: string;      // e.g. "2.3 MB"
  /** Optional sample contact to fill {{placeholders}} for a realistic preview. */
  sample?: { name?: string | null; email?: string | null; phone?: string | null } | null;
}

/** Lightweight placeholder substitution so the caption reads like a real send. */
function fillPlaceholders(text: string, sample?: Props['sample']): string {
  if (!text) return '';
  const name = sample?.name || 'there';
  const email = sample?.email || '';
  const phone = sample?.phone || '';
  return text
    .replace(/\{\{\s*(name|full_name|first_name)\s*\}\}/gi, name)
    .replace(/\{\{\s*email\s*\}\}/gi, email)
    .replace(/\{\{\s*(phone|whatsapp)\s*\}\}/gi, phone)
    .replace(/\{\{\s*sender_name\s*\}\}/gi, 'SAM Digital')
    .replace(/\{\{\s*company_website\s*\}\}/gi, 'samdigital.ae');
}

// Matches http(s) URLs, bare www. links, and email addresses.
const LINK_RE = /(https?:\/\/[^\s]+|www\.[^\s]+|[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})/g;

/** Turn URLs/emails in the caption into clickable links (WhatsApp shows these blue + tappable). */
function linkify(text: string): ReactNode[] {
  const out: ReactNode[] = [];
  let last = 0;
  let key = 0;
  let m: RegExpExecArray | null;
  LINK_RE.lastIndex = 0;
  while ((m = LINK_RE.exec(text)) !== null) {
    const match = m[0];
    if (m.index > last) out.push(text.slice(last, m.index));
    const isEmail = match.includes('@') && !/^https?:\/\//i.test(match) && !/^www\./i.test(match);
    const href = isEmail ? `mailto:${match}` : (/^https?:\/\//i.test(match) ? match : `https://${match}`);
    out.push(
      <a key={key++} href={href} target="_blank" rel="noopener noreferrer"
         onClick={e => e.stopPropagation()}
         className="text-blue-600 underline break-all">{match}</a>
    );
    last = m.index + match.length;
  }
  if (last < text.length) out.push(text.slice(last));
  return out;
}

export default function WhatsAppPreview({ message, mediaUrl, mediaType, mediaFileName, mediaSizeLabel, sample }: Props) {
  const caption = fillPlaceholders(message, sample);
  const hasMedia = !!mediaUrl;
  const contactName = sample?.name || 'New Contact';
  const initial = (contactName.trim()[0] || '?').toUpperCase();

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-5">
      <h3 className="font-semibold text-gray-900 mb-3 flex items-center gap-2">
        <span className="inline-flex w-5 h-5 rounded-full bg-green-500 text-white items-center justify-center text-[11px] font-bold">W</span>
        WhatsApp Preview
      </h3>

      {/* Phone mockup */}
      <div className="mx-auto w-[260px] rounded-[36px] bg-black p-2 shadow-xl">
        <div className="relative rounded-[28px] overflow-hidden bg-white">
          {/* Dynamic island */}
          <div className="absolute top-1.5 left-1/2 -translate-x-1/2 w-16 h-4 bg-black rounded-full z-20" />

          {/* WhatsApp header */}
          <div className="bg-[#075e54] text-white pt-7 pb-2 px-3 flex items-center gap-2">
            <ArrowLeft className="w-4 h-4 opacity-90" />
            <div className="w-7 h-7 rounded-full bg-white/25 flex items-center justify-center text-xs font-semibold">
              {initial}
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-xs font-semibold truncate leading-tight">{contactName}</p>
              <p className="text-[10px] text-white/70 leading-tight">online</p>
            </div>
            <Video className="w-4 h-4 opacity-90" />
            <Phone className="w-3.5 h-3.5 opacity-90" />
          </div>

          {/* Chat area */}
          <div className="px-2.5 py-3 min-h-[280px] flex flex-col justify-end" style={{ backgroundColor: '#e5ddd5' }}>
            <div className="flex justify-end">
              <div className="relative max-w-[88%] rounded-lg rounded-tr-sm shadow-sm overflow-hidden"
                   style={{ backgroundColor: '#d9fdd3' }}>
                {/* Media */}
                {hasMedia && mediaType === 'image' && (
                  <img src={mediaUrl!} alt="media" className="w-full max-h-44 object-cover" />
                )}
                {hasMedia && mediaType === 'video' && (
                  <div className="w-full h-32 bg-black flex items-center justify-center">
                    <div className="w-10 h-10 rounded-full bg-white/30 flex items-center justify-center">
                      <Play className="w-5 h-5 text-white" fill="white" />
                    </div>
                  </div>
                )}
                {hasMedia && mediaType === 'document' && (
                  <div className="m-1.5 flex items-center gap-2 bg-white/70 rounded-md px-2 py-1.5">
                    <div className="w-8 h-8 rounded bg-red-100 text-red-600 flex items-center justify-center flex-shrink-0">
                      <FileText className="w-4 h-4" />
                    </div>
                    <div className="min-w-0">
                      <p className="text-[11px] font-medium text-gray-800 truncate">{mediaFileName || 'document'}</p>
                      <p className="text-[9px] text-gray-500 uppercase">
                        {(mediaFileName?.split('.').pop() || 'file')}{mediaSizeLabel ? ` · ${mediaSizeLabel}` : ''}
                      </p>
                    </div>
                  </div>
                )}

                {/* Caption / text (links + emails clickable) */}
                <div className="px-2 py-1.5">
                  {caption ? (
                    <p className="text-[13px] text-gray-900 whitespace-pre-wrap break-words leading-snug">{linkify(caption)}</p>
                  ) : (
                    <p className="text-[13px] text-gray-400 italic">Your message text appears here…</p>
                  )}
                  <div className="flex items-center justify-end gap-1 mt-0.5">
                    <span className="text-[9px] text-gray-500">12:30</span>
                    <Check className="w-3 h-3 text-blue-500" />
                    <Check className="w-3 h-3 text-blue-500 -ml-2" />
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Input bar */}
          <div className="bg-gray-100 px-2 py-2 flex items-center gap-2">
            <div className="flex-1 bg-white rounded-full px-3 py-1.5 text-[11px] text-gray-400">Type a message</div>
            <div className="w-7 h-7 rounded-full bg-[#075e54] flex items-center justify-center text-white">
              <Mic className="w-3.5 h-3.5" />
            </div>
          </div>
        </div>
      </div>

      <p className="text-xs text-gray-400 mt-3 text-center">
        Approximate — links &amp; emails are tappable on the recipient's device.
      </p>
    </div>
  );
}
