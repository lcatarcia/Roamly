// scripts/lib/wcag-contrast.mjs
//
// Calcolo del contrast ratio WCAG 2.x (luminanza relativa, sRGB) — R14:
// "Tutti i contrast ratio di questo documento sono calcolati con la formula
// WCAG 2.x, non stimati ne' letti a occhio".

/** #RRGGBB -> [r,g,b] 0-255 */
function hexToRgb(hex) {
  const clean = hex.replace('#', '')
  return [
    parseInt(clean.slice(0, 2), 16),
    parseInt(clean.slice(2, 4), 16),
    parseInt(clean.slice(4, 6), 16),
  ]
}

function channelToLinear(c) {
  const cs = c / 255
  return cs <= 0.03928 ? cs / 12.92 : Math.pow((cs + 0.055) / 1.055, 2.4)
}

/** Luminanza relativa WCAG (0..1). */
export function relativeLuminance(hex) {
  const [r, g, b] = hexToRgb(hex).map(channelToLinear)
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

/** Contrast ratio WCAG tra due colori hex (simmetrico, sempre >= 1). */
export function contrastRatio(hexA, hexB) {
  const lA = relativeLuminance(hexA)
  const lB = relativeLuminance(hexB)
  const lighter = Math.max(lA, lB)
  const darker = Math.min(lA, lB)
  return (lighter + 0.05) / (darker + 0.05)
}
