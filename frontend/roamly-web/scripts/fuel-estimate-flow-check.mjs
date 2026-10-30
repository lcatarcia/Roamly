#!/usr/bin/env node

import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import https from 'node:https'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { chromium } from 'playwright'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const VITE_CLI = path.join(ROOT, 'node_modules/vite/bin/vite.js')
const PORT = 4324
const BASE_URL = `https://127.0.0.1:${PORT}`
const TRIP_ID = '0199cafe-9000-7000-8000-000000000001'
const CAMPER_ID = '0199cafe-9000-7000-8000-000000000002'
const STOP_ID = '0199cafe-9000-7000-8000-000000000003'
const ESTIMATED_COST_LABEL = 'Costo carburante stimato'
const FUEL_PRICE_ERROR =
  'Inserisci un prezzo maggiore di zero, fino a 9.999,9999 per litro, con al massimo 4 decimali e un codice valuta di 3 lettere.'

async function main() {
  await runToCompletion('npm', ['run', 'build'], ROOT)
  const preview = spawn(process.execPath, [
    VITE_CLI,
    'preview',
    '--port',
    String(PORT),
    '--host',
    '127.0.0.1',
    '--strictPort',
  ], { cwd: ROOT, stdio: 'pipe' })
  const browser = await startBrowser(preview)

  try {
    for (const status of [2, 3]) {
      const scenario = await openTrip(browser, { status })
      try {
        await scenario.page.getByRole('heading', { name: 'Weekend test' }).waitFor()
        assert.equal(await scenario.page.getByRole('heading', { name: 'Stima carburante' }).count(), 0)
        assert.equal(scenario.observed.profileRequests, 0, `status ${status} non deve leggere il profilo camper`)
      } finally {
        await scenario.context.close()
      }
    }

    for (const status of [0, 1]) {
      const scenario = await openTrip(browser, { status })
      try {
        await scenario.page.getByRole('heading', { name: 'Stima carburante' }).waitFor()
        await scenario.page.getByText(ESTIMATED_COST_LABEL, { exact: true }).waitFor()
        await scenario.page.getByText('216 EUR', { exact: true }).waitFor()
        assert.equal(scenario.observed.profileRequests, 1)
      } finally {
        await scenario.context.close()
      }
    }

    for (const missing of ['distance', 'consumption', 'price']) {
      const scenario = await openTrip(browser, { missing })
      try {
        await scenario.page.getByText(
          'Completa distanza, consumo medio e prezzo per litro per vedere la stima.',
          { exact: true },
        ).waitFor()
        assert.equal(await scenario.page.getByText(ESTIMATED_COST_LABEL, { exact: true }).count(), 0)
        assert.equal(await scenario.page.getByText(/(?:^|\s)0(?:,0+)?\s*EUR$/).count(), 0)
      } finally {
        await scenario.context.close()
      }
    }

    const electric = await openTrip(browser, { fuelKind: 4 })
    try {
      await electric.page.getByText(
        'Stima non disponibile per un camper elettrico: il calcolo non converte kWh.',
        { exact: true },
      ).waitFor()
      assert.equal(await electric.page.getByText(ESTIMATED_COST_LABEL, { exact: true }).count(), 0)
    } finally {
      await electric.context.close()
    }

    const loading = await openTrip(browser, { profileMode: 'delayed' })
    try {
      await loading.page.getByRole('status').filter({
        hasText: 'Caricamento del profilo Camper...',
      }).waitFor()
      assert.equal(await loading.page.getByText(ESTIMATED_COST_LABEL, { exact: true }).count(), 0)
      loading.releaseProfile()
      await loading.page.getByText(ESTIMATED_COST_LABEL, { exact: true }).waitFor()
    } finally {
      loading.releaseProfile()
      await loading.context.close()
    }

    const profileError = await openTrip(browser, { profileMode: 'error' })
    try {
      await profileError.page.getByRole('alert').filter({
        hasText: "Non e' stato possibile leggere il profilo Camper: la stima non e' disponibile.",
      }).waitFor()
      assert.equal(await profileError.page.getByText(ESTIMATED_COST_LABEL, { exact: true }).count(), 0)
    } finally {
      await profileError.context.close()
    }

    const pricing = await openTrip(browser, { status: 0 })
    try {
      await pricing.page.getByText(ESTIMATED_COST_LABEL, { exact: true }).waitFor()
      await pricing.page.getByRole('button', { name: 'Modifica viaggio' }).click()
      const fuelPrice = pricing.page.getByLabel('Prezzo carburante per litro')
      assert.equal(await fuelPrice.getAttribute('maxlength'), '9')
      await fuelPrice.fill('9999.9999')
      await pricing.page.getByLabel('Valuta del prezzo (codice ISO 4217)').fill('EUR')
      await pricing.page.getByRole('button', { name: 'Salva viaggio' }).click()
      await pricing.page.getByText('Viaggio aggiornato.', { exact: true }).waitFor()
      assert.equal(pricing.observed.tripUpdates.length, 1)
      assert.deepEqual(pricing.observed.tripUpdates[0].fuelPricePerLiter, {
        amount: 9999.9999,
        currency: 'EUR',
      })

      await pricing.page.getByRole('button', { name: 'Modifica viaggio' }).click()
      await pricing.page.getByLabel('Prezzo carburante per litro').fill('10000')
      await pricing.page.getByLabel('Valuta del prezzo (codice ISO 4217)').fill('EUR')
      await pricing.page.getByRole('button', { name: 'Salva viaggio' }).click()
      await pricing.page.getByRole('alert').filter({ hasText: FUEL_PRICE_ERROR }).waitFor({ timeout: 2000 })
      assert.equal(pricing.observed.tripUpdates.length, 1, 'un prezzo sopra il limite non deve essere inviato')
    } finally {
      await pricing.context.close()
    }

    const legacyPrice = await openTrip(browser, { legacyFuelPrice: 10000 })
    try {
      await legacyPrice.page.getByRole('button', { name: 'Modifica viaggio' }).click()
      const warning = legacyPrice.page.getByText(
        /prezzo.*(?:salvat|esistent).*(?:supera|oltre).*(?:limite|supportat).*(?:sostituis|inserisci|modifica).*(?:oppure|o).*(?:svuota|cancella).*(?:prezzo|valuta)/i,
      )
      const warningVisible = await warning.waitFor({ timeout: 2000 }).then(
        () => true,
        (error) => {
          if (error.name !== 'TimeoutError') throw error
          return false
        },
      )

      const name = legacyPrice.page.getByLabel('Nome del viaggio')
      await name.fill('Weekend aggiornato')
      await legacyPrice.page.getByLabel('Prezzo carburante per litro').fill('')
      await legacyPrice.page.getByLabel('Valuta del prezzo (codice ISO 4217)').fill('')
      await legacyPrice.page.getByRole('button', { name: 'Salva viaggio' }).click()
      await legacyPrice.page.getByText('Viaggio aggiornato.', { exact: true }).waitFor()

      assert.equal(legacyPrice.observed.tripUpdates.length, 1)
      assert.equal(legacyPrice.observed.tripUpdates[0].name, 'Weekend aggiornato')
      assert.equal(legacyPrice.observed.tripUpdates[0].fuelPricePerLiter, null)
      assert.ok(
        warningVisible,
        'un prezzo legacy fuori limite deve spiegare come sostituirlo o cancellare prezzo e valuta',
      )
    } finally {
      await legacyPrice.context.close()
    }

    for (const { status, name } of [
      { status: 2, name: 'Weekend completato aggiornato' },
      { status: 3, name: 'Weekend annullato aggiornato' },
    ]) {
      const ineligibleLegacyPrice = await openTrip(browser, { status, legacyFuelPrice: 10000 })
      try {
        await ineligibleLegacyPrice.page.getByRole('button', { name: 'Modifica viaggio' }).click()
        const fuelPrice = ineligibleLegacyPrice.page.getByLabel('Prezzo carburante per litro')
        await fuelPrice.waitFor()
        assert.equal(await fuelPrice.inputValue(), '10000')
        assert.equal(
          await ineligibleLegacyPrice.page.getByLabel('Valuta del prezzo (codice ISO 4217)').inputValue(),
          'EUR',
        )
        await ineligibleLegacyPrice.page.getByText(
          /prezzo.*(?:salvat|esistent).*(?:supera|oltre).*(?:limite|supportat).*(?:sostituis|inserisci|modifica).*(?:oppure|o).*(?:svuota|cancella).*(?:prezzo|valuta)/i,
        ).waitFor({ timeout: 2000 })

        await ineligibleLegacyPrice.page.getByLabel('Nome del viaggio').fill(name)
        await fuelPrice.fill('')
        await ineligibleLegacyPrice.page.getByLabel('Valuta del prezzo (codice ISO 4217)').fill('')
        await ineligibleLegacyPrice.page.getByRole('button', { name: 'Salva viaggio' }).click()
        await ineligibleLegacyPrice.page.getByText('Viaggio aggiornato.', { exact: true }).waitFor()

        assert.equal(ineligibleLegacyPrice.observed.tripUpdates.length, 1)
        assert.equal(ineligibleLegacyPrice.observed.tripUpdates[0].name, name)
        assert.equal(ineligibleLegacyPrice.observed.tripUpdates[0].fuelPricePerLiter, null)
      } finally {
        await ineligibleLegacyPrice.context.close()
      }
    }

    console.log('fuel-estimate-flow-check: superato (stati, calcolo, dati mancanti, elettrico, profilo e recupero prezzi legacy).')
  } finally {
    await browser.close()
    preview.kill()
  }
}

async function openTrip(browser, {
  status = 0,
  missing = null,
  fuelKind = 0,
  profileMode = 'success',
  legacyFuelPrice = null,
} = {}) {
  const context = await browser.newContext({ ignoreHTTPSErrors: true })
  const page = await context.newPage()
  const observed = { profileRequests: 0, tripUpdates: [] }
  let releaseProfile = () => {}
  const profileGate = new Promise((resolve) => {
    releaseProfile = resolve
  })
  const trip = {
    id: TRIP_ID,
    camperId: CAMPER_ID,
    name: 'Weekend test',
    status,
    plannedStartOnUtc: '2026-10-22',
    plannedEndOnUtc: '2026-10-24',
    actualStartOnUtc: null,
    actualEndOnUtc: null,
    estimatedDistanceKm: missing === 'distance' ? null : '1200',
    estimatedDurationMinutes: 720,
    fuelPricePerLiter: missing === 'price'
      ? null
      : { amount: legacyFuelPrice ?? 1.8, currency: 'EUR' },
    stops: [{
      id: STOP_ID,
      sequenceNo: 0,
      name: 'Cortina',
      arrivalOnUtc: null,
      departureOnUtc: null,
      position: null,
      notes: null,
    }],
  }

  await page.route('**/api/v1/**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    const { pathname } = url

    if (pathname === '/api/v1/me') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ id: 'owner', email: 'owner@example.test', displayName: 'Persona' }),
      })
      return
    }
    if (pathname === '/api/v1/csrf-token') {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '{"token":"flow-csrf"}' })
      return
    }
    if (pathname === `/api/v1/trips/${TRIP_ID}` && request.method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { ETag: '"trip-v1"' },
        body: JSON.stringify(trip),
      })
      return
    }
    if (pathname === `/api/v1/trips/${TRIP_ID}` && request.method() === 'PUT') {
      const input = request.postDataJSON()
      observed.tripUpdates.push(input)
      Object.assign(trip, input)
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { ETag: `"trip-v${observed.tripUpdates.length + 1}"` },
        body: JSON.stringify(trip),
      })
      return
    }
    if (pathname === `/api/v1/campers/${CAMPER_ID}` && request.method() === 'GET') {
      observed.profileRequests++
      if (profileMode === 'delayed') await profileGate
      if (profileMode === 'error') {
        await route.fulfill({
          status: 500,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'Internal Server Error', status: 500 }),
        })
        return
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { ETag: '"profile-v1"' },
        body: JSON.stringify({
          id: CAMPER_ID,
          name: 'Il Grigio',
          vehicleKind: 1,
          fuelKind,
          averageConsumptionLPer100Km: missing === 'consumption' ? null : 10,
        }),
      })
      return
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: [], nextCursor: null }),
    })
  })

  await page.goto(`${BASE_URL}/trips/${TRIP_ID}`, { waitUntil: 'domcontentloaded' })
  return { context, page, observed, releaseProfile }
}

async function startBrowser(preview) {
  await waitForServer()
  if (preview.exitCode !== null) {
    throw new Error('vite preview si è chiuso prima di avviare il test.')
  }
  return chromium.launch()
}

function waitForServer(timeoutMs = 30000) {
  const startedAt = Date.now()
  return new Promise((resolve, reject) => {
    const poll = () => {
      const request = https.get(BASE_URL, { rejectUnauthorized: false }, (response) => {
        response.resume()
        resolve()
      })
      request.on('error', () => {
        if (Date.now() - startedAt >= timeoutMs) {
          reject(new Error(`Timeout in attesa di ${BASE_URL}`))
        } else {
          setTimeout(poll, 300)
        }
      })
    }
    poll()
  })
}

function runToCompletion(command, args, cwd) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { cwd, shell: true, stdio: 'inherit' })
    child.on('exit', (code) => {
      if (code === 0) resolve()
      else reject(new Error(`${command} ${args.join(' ')} si è chiuso con codice ${code}`))
    })
  })
}

main().catch((error) => {
  console.error(error)
  process.exitCode = 1
})
