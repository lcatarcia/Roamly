export interface FuelEstimateInput {
  status: number
  estimatedDistanceKm: string | null
  fuelKind: number | null
  averageConsumptionLPer100Km: number | null
  fuelPricePerLiter: { amount: number; currency: string } | null
}

export type FuelEstimate =
  | { kind: 'hidden' | 'missing' | 'unavailable' }
  | { kind: 'estimated'; liters: number; amount: number; currency: string }

export function isFuelEstimateEligible(status: number): boolean
export function calculateFuelEstimate(input: FuelEstimateInput): FuelEstimate
