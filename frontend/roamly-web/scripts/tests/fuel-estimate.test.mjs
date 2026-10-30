import assert from 'node:assert/strict'
import test from 'node:test'

import {
  calculateFuelEstimate,
  isFuelEstimateEligible,
} from '../../src/features/trips/fuel-estimate.mjs'

const base = {
  status: 0,
  estimatedDistanceKm: '123.45',
  fuelKind: 0,
  averageConsumptionLPer100Km: 12.5,
  fuelPricePerLiter: { amount: 1.879, currency: 'EUR' },
}

test('calculates the total fuel cost from manual distance, camper consumption, and unit price', () => {
  assert.deepEqual(calculateFuelEstimate(base), {
    kind: 'estimated',
    liters: 15.43125,
    amount: 28.99531875,
    currency: 'EUR',
  })
})

test('does not show a fuel estimate for completed or cancelled trips', () => {
  for (const status of [2, 3]) {
    assert.equal(isFuelEstimateEligible(status), false)
    assert.deepEqual(calculateFuelEstimate({ ...base, status }), { kind: 'hidden' })
  }

  assert.equal(isFuelEstimateEligible(0), true)
  assert.equal(isFuelEstimateEligible(1), true)
})

test('does not produce a partial or zero estimate when required inputs are missing', () => {
  const incomplete = [
    { estimatedDistanceKm: null },
    { fuelKind: null },
    { averageConsumptionLPer100Km: null },
    { averageConsumptionLPer100Km: 0 },
    { fuelPricePerLiter: null },
  ]

  for (const changes of incomplete) {
    assert.deepEqual(calculateFuelEstimate({ ...base, ...changes }), { kind: 'missing' })
  }
})

test('reports electric fuel as unavailable instead of converting kWh', () => {
  assert.deepEqual(calculateFuelEstimate({ ...base, fuelKind: 4 }), { kind: 'unavailable' })
})

test('supports liquid-fuel categories including hybrid consumption measured in liters', () => {
  for (const fuelKind of [0, 1, 2, 3, 5]) {
    assert.equal(calculateFuelEstimate({ ...base, fuelKind }).kind, 'estimated')
  }
})
