/**
 * Gestione del tema (chiaro/scuro) pilotata dall'attributo `data-theme` su
 * <html>, coerente con `[data-theme="dark"]` in styles/theme.css.
 *
 * Al primo avvio (nessuna preferenza salvata) si rispetta
 * `prefers-color-scheme` del sistema operativo; da quel momento la scelta
 * esplicita dell'utente e' persistita in localStorage e ha priorita' sul
 * sistema, finche' non viene cancellata.
 */

export type Theme = 'light' | 'dark'

const STORAGE_KEY = 'roamly.theme'

function systemPrefersDark(): boolean {
  return window.matchMedia('(prefers-color-scheme: dark)').matches
}

/** Legge la preferenza persistita, o ricade su quella di sistema. */
export function getInitialTheme(): Theme {
  const stored = window.localStorage.getItem(STORAGE_KEY)
  if (stored === 'light' || stored === 'dark') {
    return stored
  }

  return systemPrefersDark() ? 'dark' : 'light'
}

/** Applica il tema al documento (attributo `data-theme` su <html>). */
export function applyTheme(theme: Theme): void {
  document.documentElement.setAttribute('data-theme', theme)
}

/** Persiste la scelta esplicita dell'utente e la applica subito. */
export function setTheme(theme: Theme): void {
  window.localStorage.setItem(STORAGE_KEY, theme)
  applyTheme(theme)
}
