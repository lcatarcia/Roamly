const PLANNED = 0
const ACTIVE = 1
const ELECTRIC = 4
const LIQUID_FUEL_KINDS = new Set([0, 1, 2, 3, 5])

export function isFuelEstimateEligible(status) {
  return status === PLANNED || status === ACTIVE
}

export function calculateFuelEstimate({
  status,
  estimatedDistanceKm,
  fuelKind,
  averageConsumptionLPer100Km,
  fuelPricePerLiter,
}) {
  if (!isFuelEstimateEligible(status)) return { kind: 'hidden' }
  if (fuelKind === ELECTRIC) return { kind: 'unavailable' }

  if (
    !LIQUID_FUEL_KINDS.has(fuelKind)
    || estimatedDistanceKm === null
    || averageConsumptionLPer100Km === null
    || averageConsumptionLPer100Km <= 0
    || fuelPricePerLiter === null
    || fuelPricePerLiter.amount <= 0
  ) {
    return { kind: 'missing' }
  }

  const distance = Number(estimatedDistanceKm)
  if (distance < 0) return { kind: 'missing' }
  const liters = (distance * averageConsumptionLPer100Km) / 100
  const amount = liters * fuelPricePerLiter.amount
  if (!Number.isFinite(distance) || !Number.isFinite(liters) || !Number.isFinite(amount)) {
    return { kind: 'missing' }
  }

  return { kind: 'estimated', liters, amount, currency: fuelPricePerLiter.currency }
}
