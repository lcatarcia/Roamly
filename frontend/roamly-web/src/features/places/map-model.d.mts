import type { SavedPlace } from '@/lib/api'
export const categorySymbols: readonly string[]
export function positionedPlaces<T extends Pick<SavedPlace, 'position'>>(places: T[]): T[]
export function mapCamera(places: Pick<SavedPlace, 'position'>[]):
  { center: [number, number]; zoom: number; bounds?: never; padding?: never; maxZoom?: never } |
  { bounds: [number, number, number, number]; padding: number; maxZoom: number; center?: never; zoom?: never } | null
