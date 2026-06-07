export type ThemeName = 'gold' | 'midnight';

const KEY = 'mp-theme';

export function getTheme(): ThemeName {
  const t = (localStorage.getItem(KEY) as ThemeName) || 'gold';
  return t === 'midnight' ? 'midnight' : 'gold';
}

export function applyTheme(theme: ThemeName) {
  const el = document.documentElement;
  el.setAttribute('data-theme', theme);
  el.classList.toggle('dark', theme === 'midnight');
  try { localStorage.setItem(KEY, theme); } catch { /* ignore */ }
}

export function toggleTheme(): ThemeName {
  const next: ThemeName = getTheme() === 'midnight' ? 'gold' : 'midnight';
  applyTheme(next);
  return next;
}
