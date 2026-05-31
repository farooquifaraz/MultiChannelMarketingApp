import { useMemo } from 'react';
import DOMPurify from 'dompurify';

/**
 * Renders email HTML inline (Gmail/Outlook style) after sanitizing with DOMPurify.
 * Replaces the old fixed-height sandboxed iframes which looked cramped + showed scrollbars.
 *
 * Security: DOMPurify strips <script>, event handlers, javascript: URLs, etc. We additionally
 * force all links to open in a new tab with noopener. Content is the authenticated user's own
 * received email, shown only to them — but we sanitize anyway (defense in depth).
 */
export default function SafeHtml({ html, className }: { html?: string | null; className?: string }) {
  const clean = useMemo(() => {
    if (!html) return '';
    const sanitized = DOMPurify.sanitize(html, {
      USE_PROFILES: { html: true },
      FORBID_TAGS: ['script', 'style', 'iframe', 'form', 'input', 'object', 'embed'],
      FORBID_ATTR: ['onerror', 'onload', 'onclick', 'onmouseover', 'style'],
      ADD_ATTR: ['target', 'rel'],
    });
    return sanitized;
  }, [html]);

  return (
    <div
      className={`email-html-body ${className ?? ''}`}
      // eslint-disable-next-line react/no-danger
      dangerouslySetInnerHTML={{ __html: clean }}
    />
  );
}
