// The vendor stylesheet includes @font-face URLs to the global atlas host.
// Keep vendor layout/attribution, but use the app/system fonts instead (R19, EU rendering).
export function localMapsStyles(css) {
  return css.replace(/@font-face\s*\{[^}]*\}/g, '')
}
