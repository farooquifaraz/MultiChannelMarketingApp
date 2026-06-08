import { useEffect, useRef, useState } from 'react';
import { Bold, Italic, Underline, Link2, Image as ImageIcon, MousePointerClick, Eye, Edit3, Code2, List } from 'lucide-react';

interface Props {
  value: string;
  onChange: (html: string) => void;
  /** Optional rendered HTML (merge-tags substituted) for the Preview tab. Falls back to value. */
  previewHtml?: string;
}

// Inline style for CTA buttons — emails ignore <style>/external CSS, so the look MUST be inline.
const CTA_STYLE =
  'display:inline-block;padding:12px 24px;background:#c8a04f;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:600;font-family:Arial,sans-serif;';

const normalizeUrl = (raw: string) => (/^(https?:|mailto:|tel:)/i.test(raw) ? raw : `https://${raw}`);
const escapeHtml = (s: string) =>
  s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/**
 * End-user-friendly email body editor. Three tabs:
 *  - Visual: WYSIWYG (bold/italic/lists, links, CTA buttons, images) — no HTML knowledge needed.
 *  - Preview: sandboxed render of exactly what the recipient sees.
 *  - HTML: raw markup for power users.
 * Buttons/links are first-class: insert with a couple of prompts, and click into an existing
 * button then hit "Button" again to edit its text + URL (fields come pre-filled).
 */
export default function EmailRichEditor({ value, onChange, previewHtml }: Props) {
  const [mode, setMode] = useState<'visual' | 'preview' | 'html'>('visual');
  const ref = useRef<HTMLDivElement | null>(null);

  // Load current value into the contentEditable ONLY when (re)entering Visual mode — never on every
  // keystroke, which would reset the caret to the start. While typing we push changes out via sync().
  useEffect(() => {
    if (mode === 'visual' && ref.current) {
      if (ref.current.innerHTML !== value) ref.current.innerHTML = value || '';
      normalizeImages(ref.current);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode]);

  const sync = () => { if (ref.current) onChange(ref.current.innerHTML); };
  const exec = (cmd: string, arg?: string) => { ref.current?.focus(); document.execCommand(cmd, false, arg); sync(); };

  const normalizeImages = (el: HTMLElement | null) => {
    if (!el) return;
    el.querySelectorAll('img').forEach((img) => {
      img.style.maxWidth = '100%';
      img.style.height = 'auto';
      img.removeAttribute('width');
      img.removeAttribute('height');
    });
  };

  // Walk up from the caret to find an enclosing <a> (a link or a CTA button).
  const selectionAnchor = (): HTMLAnchorElement | null => {
    const sel = window.getSelection();
    if (!sel || sel.rangeCount === 0) return null;
    let node: Node | null = sel.anchorNode;
    while (node && node !== ref.current) {
      if (node instanceof HTMLAnchorElement) return node;
      node = node.parentNode;
    }
    return null;
  };

  const insertLink = () => {
    ref.current?.focus();
    const existing = selectionAnchor();
    const sel = window.getSelection();
    const hasSelection = sel && !sel.isCollapsed;
    const url = window.prompt('Link URL (e.g. https://example.com)', existing?.getAttribute('href') || 'https://');
    if (!url || !url.trim()) return;
    const href = normalizeUrl(url.trim());
    if (existing) {
      existing.setAttribute('href', href);
    } else if (hasSelection) {
      document.execCommand('createLink', false, href);
    } else {
      // No text selected — insert the URL itself as the link text.
      document.execCommand('insertHTML', false,
        `<a href="${href}" target="_blank" rel="noopener noreferrer">${escapeHtml(url.trim())}</a>`);
    }
    ref.current?.querySelectorAll('a:not(.cta-btn)').forEach((a) => {
      a.setAttribute('target', '_blank');
      a.setAttribute('rel', 'noopener noreferrer');
    });
    sync();
  };

  // Insert a CTA button OR edit the one the caret is in (text + URL pre-filled).
  const insertOrEditButton = () => {
    ref.current?.focus();
    const existing = selectionAnchor();
    const defaultText = existing?.textContent || window.getSelection()?.toString() || 'Click here';
    const text = window.prompt('Button text', defaultText);
    if (text === null) return;
    const url = window.prompt('Button link URL (e.g. https://example.com)', existing?.getAttribute('href') || 'https://');
    if (url === null || !url.trim()) return;
    const href = normalizeUrl(url.trim());
    if (existing) {
      existing.textContent = text || 'Click here';
      existing.setAttribute('href', href);
      existing.setAttribute('class', 'cta-btn');
      existing.setAttribute('style', CTA_STYLE);
      existing.setAttribute('target', '_blank');
      existing.setAttribute('rel', 'noopener noreferrer');
    } else {
      document.execCommand('insertHTML', false,
        `<a href="${href}" class="cta-btn" target="_blank" rel="noopener noreferrer" style="${CTA_STYLE}">${escapeHtml(text || 'Click here')}</a>&nbsp;`);
    }
    sync();
  };

  const insertImage = () => {
    ref.current?.focus();
    const url = window.prompt('Image URL (e.g. https://example.com/banner.png)');
    if (!url || !url.trim()) return;
    document.execCommand('insertHTML', false,
      `<img src="${normalizeUrl(url.trim())}" alt="" style="max-width:100%;height:auto;display:block;margin:8px 0;" />`);
    normalizeImages(ref.current);
    sync();
  };

  const Tab = ({ id, icon: Icon, label }: { id: typeof mode; icon: any; label: string }) => (
    <button
      type="button"
      onClick={() => setMode(id)}
      className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium transition-all ${
        mode === id ? 'bg-white shadow-sm text-primary-700' : 'text-gray-500 hover:text-gray-700'
      }`}
    >
      <Icon className="w-3.5 h-3.5" /> {label}
    </button>
  );

  const ToolBtn = ({ onClick, icon: Icon, label }: { onClick: () => void; icon: any; label: string }) => (
    <button
      type="button"
      onMouseDown={(e) => e.preventDefault() /* keep selection while clicking toolbar */}
      onClick={onClick}
      className="inline-flex items-center gap-1 px-2 py-1.5 text-xs font-medium text-gray-600 bg-gray-100 hover:bg-gray-200 rounded-md"
      title={label}
    >
      <Icon className="w-3.5 h-3.5" /> {label}
    </button>
  );

  return (
    <div className="border border-gray-200 rounded-xl overflow-hidden bg-white">
      {/* Mode tabs */}
      <div className="flex items-center justify-between px-3 py-2 bg-gray-50 border-b border-gray-200">
        <div className="inline-flex bg-gray-100 rounded-lg p-0.5">
          <Tab id="visual" icon={Edit3} label="Visual" />
          <Tab id="preview" icon={Eye} label="Preview" />
          <Tab id="html" icon={Code2} label="HTML" />
        </div>
        <span className="text-[11px] text-gray-400">Personalize with {'{{first_name}}'}, {'{{email}}'}…</span>
      </div>

      {/* Visual toolbar */}
      {mode === 'visual' && (
        <div className="flex flex-wrap items-center gap-1.5 px-3 py-2 border-b border-gray-100 bg-white">
          <ToolBtn onClick={() => exec('bold')} icon={Bold} label="Bold" />
          <ToolBtn onClick={() => exec('italic')} icon={Italic} label="Italic" />
          <ToolBtn onClick={() => exec('underline')} icon={Underline} label="Underline" />
          <ToolBtn onClick={() => exec('insertUnorderedList')} icon={List} label="List" />
          <span className="w-px h-5 bg-gray-200 mx-0.5" />
          <ToolBtn onClick={insertLink} icon={Link2} label="Link" />
          <ToolBtn onClick={insertOrEditButton} icon={MousePointerClick} label="Button" />
          <ToolBtn onClick={insertImage} icon={ImageIcon} label="Image" />
        </div>
      )}

      {/* Editor surfaces */}
      {mode === 'visual' && (
        <div
          ref={ref}
          contentEditable
          suppressContentEditableWarning
          onInput={(e) => { normalizeImages(e.currentTarget as HTMLDivElement); sync(); }}
          onPaste={(e) => {
            const html = e.clipboardData.getData('text/html');
            if (html) { e.preventDefault(); document.execCommand('insertHTML', false, html); }
            setTimeout(() => { normalizeImages(ref.current); sync(); }, 0);
          }}
          className="min-h-[280px] max-h-[460px] overflow-y-auto px-4 py-3 focus:outline-none prose prose-sm max-w-none"
          data-placeholder="Write your email… use the toolbar to add links, buttons and images."
        />
      )}

      {mode === 'preview' && (
        <iframe
          title="Email preview"
          srcDoc={previewHtml || value || '<p style="color:#9ca3af;padding:16px">Nothing to preview yet.</p>'}
          sandbox=""
          className="w-full min-h-[420px] bg-white"
          style={{ height: '460px', border: 'none' }}
        />
      )}

      {mode === 'html' && (
        <textarea
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="w-full min-h-[420px] px-4 py-3 font-mono text-xs leading-relaxed outline-none resize-y"
          placeholder={'<h2>Hello {{first_name}},</h2>\n<p>Your message…</p>'}
        />
      )}
    </div>
  );
}
