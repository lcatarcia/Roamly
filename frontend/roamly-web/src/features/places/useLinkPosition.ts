import { useRef, useState } from 'react'
import { googleMapsPosition, linkPositionFields, type PositionFields } from './google-maps-position.mjs'
import type { SavedPlace } from '@/lib/api'

// Persisted positions are deliberately treated as manual: no provenance is
// inferred from equality with a URL. Editing either field locks that field.
export function useLinkPosition(place?: SavedPlace) {
  const initial = place?.position ?? googleMapsPosition(place?.googleMapsUrl)
  const [fields, setFields] = useState<PositionFields>({
    latitude: initial ? String(initial.latitude) : '',
    longitude: initial ? String(initial.longitude) : '',
  })
  const [googleMapsUrl, setUrl] = useState(place?.googleMapsUrl ?? '')
  const previous = useRef<PositionFields | null>(!place?.position && initial
    ? { latitude: String(initial.latitude), longitude: String(initial.longitude) } : null)
  const manual = useRef({ latitude: !!place?.position, longitude: !!place?.position })
  function edit(key: keyof PositionFields, value: string) {
    manual.current[key] = true
    setFields(current => ({ ...current, [key]: value }))
  }
  function setGoogleMapsUrl(url: string) {
    setUrl(url)
    const last = previous.current
    const locked = { ...manual.current }
    setFields(current => linkPositionFields(current, last, url, locked))
    const position = googleMapsPosition(url)
    previous.current = position ? { latitude: String(position.latitude), longitude: String(position.longitude) } : null
  }
  function reset() {
    setFields({ latitude: '', longitude: '' })
    setUrl('')
    previous.current = null
    manual.current = { latitude: false, longitude: false }
  }
  return { ...fields, googleMapsUrl, setGoogleMapsUrl, reset,
    setLatitude: (value: string) => edit('latitude', value),
    setLongitude: (value: string) => edit('longitude', value) }
}
