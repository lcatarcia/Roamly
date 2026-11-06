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
    await page.setViewportSize({ width: 390, height: 844 })
    assert.ok(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
      'la pagina non deve avere overflow orizzontale su mobile',
    )
    console.log('saved-places-flow-check: superato (creazione manuale, lista e nessuna chiamata esterna).')
  } finally {
    await browser.close()
    preview.kill()
  }
}

async function mockApi(page) {
  let places = []
  const observed = { createCount: 0, created: null, createRequests: [], savedPlaceCount: 0 }

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
        position: null,
        address: null,
        notes: null,
      }
      places = [saved, ...places]
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
