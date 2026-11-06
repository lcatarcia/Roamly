// Local overlays only: these symbols and names never become provider features.
export const categorySymbols = ['♧', '⛺', 'P', '◉', '🍴', '⛽', '⚒', '🛒', '◆', '▥', '⌂', '▤', '♫', '★']

export function positionedPlaces(places) {
  return places.filter(({ position: p }) => p &&
    Number.isFinite(p.latitude) && Math.abs(p.latitude) <= 90 &&
    Number.isFinite(p.longitude) && Math.abs(p.longitude) <= 180)
}

export function mapCamera(places) {
  const points = positionedPlaces(places).map(p => p.position)
  if (!points.length) return null
  if (points.length === 1) return { center: [points[0].longitude, points[0].latitude], zoom: 12 }
  const longitudes = points.map(p => (p.longitude + 360) % 360).sort((a, b) => a - b)
  let gap = -1
  let start = 0
  for (let i = 0; i < longitudes.length; i++) {
    const next = longitudes[(i + 1) % longitudes.length] + (i === longitudes.length - 1 ? 360 : 0)
    if (next - longitudes[i] > gap) {
      gap = next - longitudes[i]
      start = next % 360
    }
  }
  if (start > 180) start -= 360
  const latitudes = points.map(p => Math.max(-85, Math.min(85, p.latitude)))
  return { bounds: [start, Math.min(...latitudes), start + 360 - gap, Math.max(...latitudes)],
    padding: 60, maxZoom: 12 }
}
