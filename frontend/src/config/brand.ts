/**
 * Brand configuration — single source of truth for platform name, logo, colors.
 *
 * Priority order at runtime:
 *   1. Backend SystemSettings (GET /public/brand) — admin-managed, runtime-editable
 *   2. Vite env vars (VITE_BRAND_NAME, VITE_BRAND_LOGO, VITE_PRIMARY_COLOR) — build-time
 *   3. Hardcoded defaults below — last-resort fallback
 *
 * The login/register screens fetch the public brand info before render so they always
 * show the correct branding even before the user logs in.
 */

export interface BrandInfo {
  name: string;
  logoUrl: string | null;
  primaryColor: string;
}

export const brandDefaults: BrandInfo = {
  name: (import.meta as any).env?.VITE_BRAND_NAME || 'MarketPro',
  logoUrl: (import.meta as any).env?.VITE_BRAND_LOGO || null,
  primaryColor: (import.meta as any).env?.VITE_PRIMARY_COLOR || '#4f46e5',
};

// In-memory cache populated by useBrand() on first call
let cachedBrand: BrandInfo = brandDefaults;
let inflight: Promise<BrandInfo> | null = null;

/**
 * Build a deterministic avatar URL using the admin-configured pattern OR the default.
 * Placeholders in pattern: {name}, {size}, {bg}, {fg}.
 *
 * Pattern itself can be passed (when caller has access to live SystemSettings) — otherwise
 * fall back to ui-avatars.com default. Centralising this prevents the same URL pattern
 * from being hardcoded across the codebase.
 */
export function buildAvatarUrl(
  fullName: string,
  pattern?: string | null,
  size = 128,
  bg = '6366f1',
  fg = 'fff'
): string {
  const tpl = pattern && pattern.includes('{name}')
    ? pattern
    : 'https://ui-avatars.com/api/?name={name}&size={size}&background={bg}&color={fg}&bold=true&rounded=true';
  return tpl
    .replace('{name}', encodeURIComponent(fullName))
    .replace('{size}', String(size))
    .replace('{bg}', bg)
    .replace('{fg}', fg);
}

export function getCachedBrand(): BrandInfo {
  return cachedBrand;
}

/**
 * Fetch brand from backend (anonymous endpoint) — cached after the first successful call.
 * Safe to call from anywhere; returns the defaults synchronously if not yet fetched.
 */
export async function fetchBrand(apiBaseUrl: string): Promise<BrandInfo> {
  if (inflight) return inflight;
  inflight = (async () => {
    try {
      const url = `${apiBaseUrl.replace(/\/$/, '')}/public/brand`;
      const res = await fetch(url);
      if (!res.ok) return cachedBrand;
      const json = await res.json();
      if (json?.success && json?.data) {
        cachedBrand = {
          name: json.data.platformName || brandDefaults.name,
          logoUrl: json.data.logoUrl ?? brandDefaults.logoUrl,
          primaryColor: json.data.primaryColor || brandDefaults.primaryColor,
        };
      }
    } catch { /* keep defaults */ }
    finally { inflight = null; }
    return cachedBrand;
  })();
  return inflight;
}
