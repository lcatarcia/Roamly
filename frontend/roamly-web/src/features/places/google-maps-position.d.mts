export type PositionFields = { latitude: string; longitude: string }
export function googleMapsPosition(value: string | null | undefined): { latitude: number; longitude: number } | null
export function googleMapsPositionFields(value: string | null | undefined): PositionFields | null
export function linkPositionFields(current: PositionFields, previous: PositionFields | null, url: string,
  manual?: { latitude?: boolean; longitude?: boolean }): PositionFields
