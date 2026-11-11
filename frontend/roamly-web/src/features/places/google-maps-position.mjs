// Exact allowlist, not a suffix match. No URL fetch or short-link expansion.
const domains = new Set(['com', 'it', 'fr', 'de', 'es', 'co.uk', 'nl', 'pt', 'ch', 'at', 'be', 'ca', 'com.au', 'co.jp']
  .flatMap(suffix => [`google.${suffix}`, `www.google.${suffix}`, `maps.google.${suffix}`]))
const number = '-?(?:\\d+(?:\\.\\d+)?|\\.\\d+)'
const pair = new RegExp(`^(${number}),(${number})$`)
const placePair = new RegExp(`!3d(${number})!4d(${number})(?=!|$)`, 'g')

export function googleMapsPosition(value) {
  if (!value || value.length > 2048 ||
    [...value.trim()].some(character => character <= ' ' || character === '\\')) return null
  try {
    const url = readMapsUrl(value)
    if (!url) return null
    // Decode each URL component once only. Never scan arbitrary URL text.
    const path = decodeURIComponent(url.pathname)
    if (path === '/maps/dir' || path.startsWith('/maps/dir/')) return null
    return readPosition(path, url.searchParams)
  } catch { return null }
}

function readMapsUrl(value) {
  const authority = value.trim().match(/^https:\/\/([^/?#]+)/i)?.[1]?.toLowerCase().replace(/:443$/, '')
  if (!domains.has(authority) || /%(?![0-9a-f]{2})/i.test(value)) return null
  const url = new URL(value.trim())
  return url.protocol === 'https:' && !url.username && !url.password && !url.port &&
    domains.has(url.hostname) && (url.pathname === '/maps' || url.pathname.startsWith('/maps/') ||
      (url.hostname.startsWith('maps.') && url.pathname === '/')) ? url : null
}

function readPosition(path, params) {
  const queries = [...params.getAll('q'), ...params.getAll('query')]
  const matches = [...path.matchAll(placePair)]
  const markers = path.match(/![34]d/g) ?? []
  if (matches.length > 1 || markers.length !== matches.length * 2 || queries.length > 1 ||
    (matches.length && queries.length)) return null
  const coordinates = matches[0] ??
    (queries.length && (!params.has('query') ||
      (params.getAll('api').length === 1 && params.get('api') === '1')) ? queries[0].match(pair) : null)
  if (!coordinates) return null
  const latitude = Number(coordinates[1]), longitude = Number(coordinates[2])
  return Number.isFinite(latitude) && Math.abs(latitude) <= 90 &&
    Number.isFinite(longitude) && Math.abs(longitude) <= 180 ? { latitude, longitude } : null
}

export function googleMapsPositionFields(url) {
  const position = googleMapsPosition(url)
  return position ? {
    latitude: String(Number(position.latitude.toFixed(3))),
    longitude: String(Number(position.longitude.toFixed(3))),
  } : null
}

// Empty fields can be filled; a nonempty field changes only while still equal to
// this editing session's last automatic value. Existing saved positions are manual.
export function linkPositionFields(current, previous, url, manual = {}) {
  const position = googleMapsPositionFields(url)
  return Object.fromEntries(['latitude', 'longitude'].map(key => [
    key, !manual[key] && (current[key] === '' || current[key] === previous?.[key])
      ? position ? position[key] : '' : current[key],
  ]))
}
