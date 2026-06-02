import { FileText, Play, Check } from 'lucide-react';

/**
 * A WhatsApp-style chat-bubble preview of the message the recipient will receive (L1 polish).
 * Purely presentational — no network, no side effects. Shows the attached media (image thumbnail,
 * video poster, or document card) with the message text rendered as the caption underneath, exactly
 * as Meta renders a media message on the recipient's phone.
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

export default function WhatsAppPreview({ message, mediaUrl, mediaType, mediaFileName, mediaSizeLabel, sample }: Props) {
  const caption = fillPlaceholders(message, sample);
  const hasMedia = !!mediaUrl;

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-5">
      <h3 className="font-semibold text-gray-900 mb-3 flex items-center gap-2">
        <MessagePreviewIcon /> WhatsApp Preview
      </h3>

      {/* Chat surface — WhatsApp's signature wallpaper tint */}
      <div className="rounded-lg p-3" style={{ backgroundColor: '#e5ddd5' }}>
        <div className="flex justify-end">
          {/* Outgoing bubble */}
          <div className="relative max-w-[85%] rounded-lg rounded-tr-sm shadow-sm overflow-hidden"
               style={{ backgroundColor: '#d9fdd3' }}>
            {/* Media */}
            {hasMedia && mediaType === 'image' && (
              <img src={mediaUrl!} alt="media" className="w-full max-h-56 object-cover" />
            )}
            {hasMedia && mediaType === 'video' && (
              <div className="w-full h-40 bg-black flex items-center justify-center">
                <div className="w-12 h-12 rounded-full bg-white/30 flex items-center justify-center">
                  <Play className="w-6 h-6 text-white" fill="white" />
                </div>
              </div>
            )}
            {hasMedia && mediaType === 'document' && (
              <div className="m-2 flex items-center gap-3 bg-white/70 rounded-md px-3 py-2">
                <div className="w-9 h-9 rounded bg-red-100 text-red-600 flex items-center justify-center flex-shrink-0">
                  <FileText className="w-5 h-5" />
                </div>
                <div className="min-w-0">
                  <p className="text-xs font-medium text-gray-800 truncate">{mediaFileName || 'document'}</p>
                  <p className="text-[10px] text-gray-500 uppercase">
                    {(mediaFileName?.split('.').pop() || 'file')}{mediaSizeLabel ? ` · ${mediaSizeLabel}` : ''}
                  </p>
                </div>
              </div>
            )}

            {/* Caption / text */}
            <div className="px-2.5 py-1.5">
              {caption ? (
                <p className="text-sm text-gray-900 whitespace-pre-wrap break-words">{caption}</p>
              ) : (
                <p className="text-sm text-gray-400 italic">Your message text appears here…</p>
              )}
              <div className="flex items-center justify-end gap-1 mt-0.5">
                <span className="text-[10px] text-gray-500">12:30</span>
                <Check className="w-3 h-3 text-blue-500" />
                <Check className="w-3 h-3 text-blue-500 -ml-2" />
              </div>
            </div>
          </div>
        </div>
      </div>

      <p className="text-xs text-gray-400 mt-2">
        Approximate — exact rendering depends on the recipient's device.
      </p>
    </div>
  );
}

function MessagePreviewIcon() {
  return (
    <span className="inline-flex w-5 h-5 rounded-full bg-green-500 text-white items-center justify-center text-[11px] font-bold">
      W
    </span>
  );
}
