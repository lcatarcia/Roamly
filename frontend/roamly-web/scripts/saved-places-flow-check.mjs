#!/usr/bin/env node

import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import https from 'node:https'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { chromium } from 'playwright'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const VITE_CLI = path.join(ROOT, 'node_modules/vite/bin/vite.js')
const PORT = 4322
const BASE_URL = `https://127.0.0.1:${PORT}`
const BUNKER = 'https://www.google.com/maps/place/Bunker+Soratte/@42.2370291,12.5091909,805m/data=!3m2!1e3!4b1!4m6!3m5!1s0x132f14154ace8c1d:0x881d17f0b2739244!8m2!3d42.2370252!4d12.5117658!16s%2Fg%2F11gbfd5xfy?authuser=0&entry=tts'

async function main() {
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
    const page = await browser.newPage({ ignoreHTTPSErrors: true })
    const remoteRequests = []
    page.on('request', (request) => {
      if (new URL(request.url()).origin !== BASE_URL) remoteRequests.push(request.url())
    })
    const observed = await mockApi(page)
    await page.goto(`${BASE_URL}/places`, { waitUntil: 'networkidle' })

    await page.getByRole('heading', { name: 'Da visitare' }).waitFor()
    await page.getByLabel('Nome').fill('Borgo senza coordinate')
    await page.getByLabel('Categoria').selectOption('10')
    await page.getByLabel('Link Google Maps').fill('https://maps.google.com/?q=Borgo')
    await page.getByLabel('Latitudine').fill('45.1')
    await page.getByRole('button', { name: 'Aggiungi luogo' }).click()
    await page.getByText('Inserisci entrambe le coordinate oppure lasciale entrambe vuote.').waitFor()
    assert.equal(observed.createCount, 0, 'un form con coordinate incomplete non deve essere inviato')
    await page.getByLabel('Latitudine').fill('')
    await page.getByRole('button', { name: 'Aggiungi luogo' }).click()
    await page.getByText("Non e' stato possibile aggiungere il luogo. Verifica i dati e riprova.").waitFor()
    assert.equal(observed.createCount, 1)
    await page.getByRole('button', { name: 'Aggiungi luogo' }).click()
    await page.getByRole('heading', { name: 'Borgo senza coordinate' }).waitFor()

    assert.equal(observed.createCount, 2)
    assert.equal(observed.createRequests[0].id, observed.createRequests[1].id, 'un retry deve riusare l’ID per non duplicare il luogo')
    assert.equal(observed.savedPlaceCount, 1, 'il retry idempotente non deve creare duplicati')
    assert.equal(observed.created.category, 10)
    assert.equal(observed.created.position, undefined)
    assert.equal(observed.created.googleMapsUrl, 'https://maps.google.com/?q=Borgo')
    assert.deepEqual(remoteRequests, [], 'la wishlist manuale non deve contattare servizi esterni')
    await page.getByRole('button', { name: 'Modifica', exact: true }).click()
    const editForm = page.getByRole('form', { name: 'Modifica luogo' })
    await editForm.getByLabel('Nome').fill('Borgo rinominato')
    await editForm.getByLabel('Link Google Maps').fill(BUNKER)
    assert.equal(await editForm.getByLabel('Latitudine').inputValue(), '42.2370252')
    assert.equal(await editForm.getByLabel('Longitudine').inputValue(), '12.5117658')
    await editForm.getByLabel('Link Google Maps').fill('https://maps.google.com/?q=3,4')
    assert.equal(await editForm.getByLabel('Latitudine').inputValue(), '3')
    assert.equal(await editForm.getByLabel('Longitudine').inputValue(), '4')
    await editForm.getByLabel('Link Google Maps').fill('')
    assert.equal(await editForm.getByLabel('Latitudine').inputValue(), '')
    assert.equal(await editForm.getByLabel('Longitudine').inputValue(), '')
    await editForm.getByLabel('Link Google Maps').fill(BUNKER)
    await editForm.getByRole('button', { name: 'Salva modifiche' }).click()
    await page.getByRole('heading', { name: 'Borgo rinominato' }).waitFor()
    assert.equal(observed.updateIfMatch, '"1"', 'la modifica deve inviare l’ETag letto')
    assert.equal(observed.updated.name, 'Borgo rinominato')
    assert.deepEqual(observed.updated.position, { latitude: 42.2370252, longitude: 12.5117658 })
    assert.equal(observed.updated.googleMapsUrl, BUNKER)
    await page.getByRole('button', { name: 'Elimina', exact: true }).click()
    await page.getByRole('button', { name: 'Conferma eliminazione' }).click()
    await page.getByRole('heading', { name: 'Borgo rinominato' }).waitFor({ state: 'detached' })
    assert.equal(observed.deleteIfMatch, '"2"', 'l’eliminazione deve inviare l’ETag più recente')
    assert.equal(observed.savedPlaceCount, 0)

    // The create form was reset: the next link can derive new coordinates.
    await page.getByLabel('Nome').fill('Bunker automatico')
    await page.getByLabel('Categoria').selectOption('8')
    await page.getByLabel('Link Google Maps').fill(BUNKER)
    assert.equal(await page.getByLabel('Latitudine').inputValue(), '42.2370252')
    assert.equal(await page.getByLabel('Longitudine').inputValue(), '12.5117658')
    await page.getByLabel('Link Google Maps').fill('https://maps.google.com/?q=1,2')
    assert.equal(await page.getByLabel('Latitudine').inputValue(), '1')
    await page.getByLabel('Link Google Maps').fill('https://maps.app.goo.gl/example')
    assert.equal(await page.getByLabel('Latitudine').inputValue(), '')
    assert.equal(await page.getByLabel('Longitudine').inputValue(), '')
    await page.getByLabel('Link Google Maps').fill(BUNKER)
    await page.getByRole('button', { name: 'Aggiungi luogo' }).click()
    await page.getByRole('heading', { name: 'Bunker automatico' }).waitFor()
    assert.deepEqual(observed.created.position, { latitude: 42.2370252, longitude: 12.5117658 })
    assert.equal(observed.created.googleMapsUrl, BUNKER)
    await page.getByRole('button', { name: 'Modifica', exact: true }).click()
    await editForm.getByLabel('Link Google Maps').fill('https://maps.google.com/?q=3,4')
    assert.equal(await editForm.getByLabel('Latitudine').inputValue(), '42.2370252', 'saved position is treated as manual')
    await editForm.getByLabel('Latitudine').fill('9')
    await editForm.getByLabel('Longitudine').fill('8')
    await editForm.getByLabel('Link Google Maps').fill(BUNKER)
    await editForm.getByRole('button', { name: 'Salva modifiche' }).click()
    await page.getByRole('heading', { name: 'Bunker automatico' }).waitFor()
    assert.deepEqual(observed.updated.position, { latitude: 9, longitude: 8 })
    assert.deepEqual(remoteRequests, [], 'parsing and the default list never contact Google or Azure')
    await page.setViewportSize({ width: 390, height: 844 })
    assert.ok(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      'la pagina non deve avere overflow orizzontale su mobile',
    )
    console.log('saved-places-flow-check: superato (autofill esatto create/edit, precedenza manuale, link brevi, CRUD If-Match e retry; nessuna chiamata esterna).')
  } finally {
    await browser.close()
    preview.kill()
  }
}

async function mockApi(page) {
  let places = []
  const versions = new Map()
  const observed = { createCount: 0, created: null, createRequests: [], savedPlaceCount: 0, updated: null, updateIfMatch: null, deleteIfMatch: null }

  await page.route('**/api/v1/me', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'owner',
      email: 'owner@example.test',
      displayName: 'Persona',
      reportingCurrency: 'EUR',
      mustChangePassword: false,
    }),
  }))
  await page.route('**/api/v1/csrf-token', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: '{"token":"flow-csrf"}',
  }))
  await page.route('**/api/v1/saved-places/*', async (route) => {
    const request = route.request()
    const id = decodeURIComponent(new URL(request.url()).pathname.split('/').pop())
    const place = places.find((item) => item.id === id)
    if (!place) {
      await route.fulfill({ status: 404 })
      return
    }
    if (request.method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { ETag: `"${versions.get(id)}"` },
        body: JSON.stringify(place),
      })
      return
    }

    const headers = await request.allHeaders()
    assert.equal(headers['x-roamly-request'], '1')
    assert.equal(headers['x-xsrf-token'], 'flow-csrf')
    if (headers['if-match'] !== `"${versions.get(id)}"`) {
      await route.fulfill({ status: 412 })
      return
    }
    if (request.method() === 'PUT') {
      observed.updateIfMatch = headers['if-match']
      observed.updated = request.postDataJSON()
      versions.set(id, versions.get(id) + 1)
      Object.assign(place, observed.updated)
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { ETag: `"${versions.get(id)}"` },
        body: JSON.stringify(place),
      })
      return
    }

    assert.equal(request.method(), 'DELETE')
    observed.deleteIfMatch = headers['if-match']
    places = places.filter((item) => item.id !== id)
    observed.savedPlaceCount = places.length
    await route.fulfill({ status: 204 })
  })
  await page.route('**/api/v1/saved-places*', async (route) => {
    const request = route.request()
    if (request.method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: places, nextCursor: null }),
      })
      return
    }

    assert.equal(request.method(), 'POST')
    const headers = await request.allHeaders()
    assert.equal(headers['x-roamly-request'], '1')
    assert.equal(headers['x-xsrf-token'], 'flow-csrf')
    observed.created = request.postDataJSON()
    observed.createRequests.push(observed.created)
    observed.createCount += 1
    let saved = places.find((place) => place.id === observed.created.id)
    if (!saved) {
      saved = {
        ...observed.created,
        position: observed.created.position ?? null,
        address: null,
        notes: null,
      }
      places = [saved, ...places]
      versions.set(saved.id, 1)
      observed.savedPlaceCount = places.length
    }
    if (observed.createCount === 1) {
      await route.abort('failed')
      return
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      headers: { Location: `/api/v1/saved-places/${saved.id}` },
      body: JSON.stringify(saved),
    })
  })

  return observed
}

async function startBrowser(preview) {
  await waitForServer()
  if (preview.exitCode !== null) throw new Error('vite preview si e’ chiuso prima del test.')
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
        if (Date.now() - startedAt >= timeoutMs) reject(new Error(`Timeout in attesa di ${BASE_URL}`))
        else setTimeout(poll, 300)
      })
    }
    poll()
  })
}

main().catch((error) => {
  console.error(error)
  process.exitCode = 1
})
