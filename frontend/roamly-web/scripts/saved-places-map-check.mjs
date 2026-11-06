#!/usr/bin/env node
// Real React map component, local SDK double. No Azure credentials or external requests.
import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import https from 'node:https'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from 'playwright'
import AxeBuilder from '@axe-core/playwright'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const origin = 'https://127.0.0.1:4323'
const maliciousName = '<img src=x onerror=alert(1)> Posto salvato'
const sdk = `
export const AuthenticationType = { anonymous: 'anonymous' };
const state = window.__maps = { created: 0, disposed: 0, live: 0, options: [], maps: [] };
export class HtmlMarker {
  constructor(options) { this.options = options; }
}
export class Map {
  constructor(container, options) {
    this.container = container; this.options = options; this.handlers = {};
    state.created++; state.live++; state.options.push(options); state.maps.push(this);
    this.events = { add: (event, fn) => { this.handlers[event] = fn; } };
    this.markers = {
      add: markers => markers.forEach(m => container.appendChild(m.options.htmlContent)),
      remove: markers => markers.forEach(m => m.options.htmlContent.remove())
    };
    const attribution = document.createElement('span');
    attribution.textContent = '© Microsoft Azure Maps (test double)';
    container.appendChild(attribution);
    options.authOptions.getToken(() => {
      if (!this.disposed) this.handlers.ready?.();
    }, () => {});
  }
  getMapContainer() { return this.disposed ? null : this.container; }
  setCamera(camera) { this.camera = camera; }
  dispose() {
    if (this.disposed) throw new Error('double dispose');
    this.disposed = true; state.disposed++; state.live--; this.container.replaceChildren();
  }
}`

async function main() {
  const server = spawn(process.execPath, [path.join(root, 'node_modules/vite/bin/vite.js'),
    '--host', '127.0.0.1', '--port', '4323', '--strictPort'], { cwd: root, stdio: 'pipe' })
  let browser
  let serverOutput = ''
  server.stdout.on('data', data => { serverOutput += data })
  server.stderr.on('data', data => { serverOutput += data })
  try {
    await waitForServer()
    browser = await chromium.launch()
    const context = await browser.newContext({ ignoreHTTPSErrors: true })
    const page = await context.newPage()
    const errors = []
    const external = []
    page.on('pageerror', error => errors.push(error.message))
    let granted = false
    let revision = 0
    let tokenCount = 0
    let sdkLoads = 0
    let cssLoads = 0
    let configured = true
    let failToken = false
    let delayToken = null
    const versions = new Map()
    const mutations = []
    const places = Array.from({ length: 14 }, (_, category) => ({
      id: `place-${category}`, name: category === 0 ? maliciousName : `Categoria ${category}`,
      category, position: { latitude: category, longitude: category }, notes: 'Nota locale', address: null,
    }))
    await page.route('**/*', async route => {
      const request = route.request()
      const url = new URL(request.url())
      if (url.origin !== origin) { external.push(url.href); await route.abort(); return }
      if (/azure-maps-control.*\.js/.test(url.pathname)) {
        sdkLoads++
        return route.fulfill({ contentType: 'text/javascript', body: sdk })
      }
      if (/atlas.*\.css/.test(url.pathname)) cssLoads++
      const json = (body, status = 200) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
      if (url.pathname === '/api/v1/me') return json({
        id: 'owner', email: 'owner@example.test', displayName: 'Persona', mustChangePassword: false,
      })
      if (url.pathname === '/api/v1/csrf-token') return json({ token: 'csrf' })
      if (url.pathname === '/api/v1/maps/config') return json({
        isConfigured: configured, accountClientId: configured ? 'maps-account-id' : null, domain: 'eu.atlas.microsoft.com',
      })
      if (url.pathname.startsWith('/api/v1/me/consents/')) {
        if (request.method() === 'POST') {
          const input = request.postDataJSON()
          assert.equal(input.version, 'v1')
          assert.equal(input.textReference, 'roamly:azure-maps-render:v1')
          granted = url.pathname.endsWith('/grants')
          revision++
          return json({ revision }, 201)
        }
        return json({ purpose: 'azure-maps-render', version: 'v1',
          textReference: 'roamly:azure-maps-render:v1', isActive: true, isGranted: granted, revision })
      }
      if (url.pathname === '/api/v1/maps/token') {
        assert.equal(granted, true, 'token only after explicit consent')
        assert.equal(request.headers()['x-xsrf-token'], 'csrf')
        tokenCount++
        if (delayToken) await delayToken
        return json(failToken ? { title: 'Azure unavailable' } : { accessToken: 'mock', expiresAtUtc: '2099-01-01' }, failToken ? 503 : 200)
      }
      if (url.pathname.startsWith('/api/v1/saved-places/')) {
        const id = decodeURIComponent(url.pathname.split('/').pop())
        const place = places.find(item => item.id === id)
        if (!place) return json({}, 404)
        const version = versions.get(id) ?? 1
        if (request.method() === 'GET') return route.fulfill({
          status: 200, contentType: 'application/json',
          headers: { ETag: `"${version}"` }, body: JSON.stringify(place),
        })
        assert.equal(request.headers()['if-match'], `"${version}"`)
        assert.equal(request.headers()['x-xsrf-token'], 'csrf')
        mutations.push({ id, method: request.method(), version })
        if (request.method() === 'PUT') {
          Object.assign(place, request.postDataJSON())
          versions.set(id, version + 1)
          return route.fulfill({
            status: 200, contentType: 'application/json',
            headers: { ETag: `"${version + 1}"` }, body: JSON.stringify(place),
          })
        }
        assert.equal(request.method(), 'DELETE')
        places.splice(places.indexOf(place), 1)
        return route.fulfill({ status: 204 })
      }
      if (url.pathname === '/api/v1/saved-places') return json({
        items: url.searchParams.has('after') ? [{ id: 'no-coordinates', name: 'Senza coordinate', category: 10, position: null }] : places,
        nextCursor: url.searchParams.has('after') ? null : 'next',
      })
      return route.continue()
    })
    await page.goto(`${origin}/places`, { waitUntil: 'networkidle' })
    assert.equal(sdkLoads, 0)
    assert.equal(cssLoads, 0)
    await page.getByRole('button', { name: 'Mappa', exact: true }).click()
    await page.getByRole('button', { name: 'Consenti e apri mappa' }).waitFor()
    assert.equal(sdkLoads, 0, 'SDK is not loaded while asking consent')
    assert.equal(tokenCount, 0)
    await page.getByRole('button', { name: 'Consenti e apri mappa' }).click()
    const pins = page.locator('.saved-place-map-pin')
    await pins.first().waitFor()
    assert.equal(await pins.count(), 14)
    assert.ok(sdkLoads > 0)
    assert.equal(await pins.first().textContent(), '♧' + maliciousName)
    assert.equal(await page.locator('.saved-places-map img').count(), 0, 'labels use textContent, never HTML')
    assert.equal(new Set(await pins.locator('span:first-child').allTextContents()).size, 14)
    assert.equal(await page.evaluate(() => window.__maps.live), 1, 'StrictMode leaves one live map')
    const creations = await page.evaluate(() => window.__maps.created)
    await pins.first().click()
    await page.getByRole('region', { name: 'Dettagli luogo selezionato' }).getByText(maliciousName).waitFor()
    await page.getByRole('button', { name: 'Carica altri' }).click()
    await page.getByRole('button', { name: /Senza coordinate/ }).waitFor()
    assert.equal(await pins.count(), 14)
    assert.equal(await page.evaluate(() => window.__maps.created), creations, 'paging/selection do not recreate map')
    await page.getByRole('button', { name: /Senza coordinate/ }).focus()
    await page.keyboard.press('Enter')
    await page.getByRole('region', { name: 'Dettagli luogo selezionato' }).getByText('Senza coordinate').waitFor()
    const options = await page.evaluate(() => window.__maps.options[0])
    assert.equal(options.domain, 'eu.atlas.microsoft.com')
    assert.equal(options.disableTelemetry, true)
    assert.equal(options.enableAccessibilityLocationFallback, false)
    assert.equal(options.showLogo, true)
    const accessibility = await new AxeBuilder({ page }).include('.saved-places-map')
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa']).analyze()
    assert.deepEqual(accessibility.violations.map(item => item.id), [], 'map pins have accessible names and contrast')

    // Maps selection survives list CRUD; details and pins use the refreshed resource.
    await pins.first().click()
    await page.getByRole('button', { name: 'Lista', exact: true }).click()
    const firstCard = page.locator('li').filter({ has: page.getByRole('heading', { name: maliciousName, exact: true }) })
    await firstCard.getByRole('button', { name: 'Modifica', exact: true }).click()
    const edit = page.getByRole('form', { name: 'Modifica luogo' })
    await edit.getByLabel('Note').fill('Nota aggiornata con If-Match')
    await edit.getByRole('button', { name: 'Salva modifiche' }).click()
    await firstCard.getByText('Nota aggiornata con If-Match').waitFor()
    await page.getByRole('button', { name: 'Mappa', exact: true }).click()
    const details = page.getByRole('region', { name: 'Dettagli luogo selezionato' })
    await details.getByText(maliciousName).waitFor()
    await details.getByText('Nota aggiornata con If-Match').waitFor()
    await page.getByRole('button', { name: 'Consenti e apri mappa' }).click()
    await pins.last().waitFor()
    await pins.last().click()
    await details.getByText('Categoria 13', { exact: true }).waitFor()
    await page.getByRole('button', { name: 'Lista', exact: true }).click()
    const lastCard = page.locator('li').filter({ has: page.getByRole('heading', { name: 'Categoria 13', exact: true }) })
    await lastCard.getByRole('button', { name: 'Elimina', exact: true }).click()
    await lastCard.getByRole('button', { name: 'Conferma eliminazione' }).click()
    await lastCard.waitFor({ state: 'detached' })
    await page.getByRole('button', { name: 'Mappa', exact: true }).click()
    assert.equal(await details.count(), 0, 'deleted selection cannot leave stale details')
    await page.getByRole('button', { name: 'Consenti e apri mappa' }).click()
    await pins.first().waitFor()
    assert.equal(await pins.count(), 13, 'deleted place has no pin')
    assert.deepEqual(mutations, [
      { id: 'place-0', method: 'PUT', version: 1 },
      { id: 'place-13', method: 'DELETE', version: 1 },
    ])

    await page.getByRole('button', { name: 'Revoca consenso e chiudi mappa' }).click()
    await page.getByRole('button', { name: 'Consenti e apri mappa' }).waitFor()
    assert.equal(await page.evaluate(() => window.__maps.live), 0)
    const tokensAfterRevoke = tokenCount
    await page.evaluate(() => window.__maps.maps.at(-1).options.authOptions.getToken(() => {}, () => {}))
    assert.equal(tokenCount, tokensAfterRevoke, 'disposed callback cannot renew')

    failToken = true
    await page.getByRole('button', { name: 'Consenti e apri mappa' }).click()
    await page.getByRole('alert').filter({ hasText: "Non e' stato possibile caricare la mappa" }).waitFor()
    await page.getByRole('button', { name: 'Lista', exact: true }).click()
    await page.getByRole('heading', { name: maliciousName, exact: true }).waitFor()

    failToken = false
    // Late token completion after changing view cannot revive the map.
    let release
    delayToken = new Promise(resolve => { release = resolve })
    await page.getByRole('button', { name: 'Mappa', exact: true }).click()
    await page.getByRole('button', { name: 'Consenti e apri mappa' }).click()
    await page.waitForRequest('**/api/v1/maps/token')
    await page.getByRole('button', { name: 'Lista', exact: true }).click()
    release()
    delayToken = null
    await page.waitForLoadState('networkidle')
    assert.equal(await page.evaluate(() => window.__maps.live), 0)
    configured = false
    await page.getByRole('button', { name: 'Mappa', exact: true }).click()
    await page.getByText("Azure Maps non e' configurato su questo host. La lista rimane disponibile.").waitFor()
    assert.equal(await page.getByRole('button', { name: 'Consenti e apri mappa' }).count(), 0)
    configured = true
    places.splice(0)
    await page.reload({ waitUntil: 'networkidle' })
    const sdkBeforeEmpty = sdkLoads
    const tokensBeforeEmpty = tokenCount
    await page.getByRole('button', { name: 'Mappa', exact: true }).click()
    await page.getByRole('button', { name: 'Consenti e apri mappa' }).click()
    await page.getByText('Nessun luogo caricato ha coordinate valide. Aggiungile manualmente per vedere i pin.').waitFor()
    assert.equal(sdkLoads, sdkBeforeEmpty, 'empty map never loads SDK')
    assert.equal(tokenCount, tokensBeforeEmpty, 'empty map never acquires token')
    await page.setViewportSize({ width: 390, height: 844 })
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth))
    assert.deepEqual(errors, [])
    assert.deepEqual(external, [], 'all external requests must remain absent')
    console.log('saved-places-map-check: PASS (lazy consent, safe category pins, selection, pagination, revoke, errors, late response, StrictMode; no Azure).')
  } catch (error) {
    console.error(serverOutput.slice(-2000))
    throw error
  } finally {
    await browser?.close()
    server.kill()
  }
}

function waitForServer() {
  const started = Date.now()
  return new Promise((resolve, reject) => {
    const poll = () => {
      const request = https.get(origin, { rejectUnauthorized: false }, response => { response.resume(); resolve() })
      request.on('error', () => Date.now() - started > 30000 ? reject(new Error('Vite timeout')) : setTimeout(poll, 300))
    }
    poll()
  })
}
main().catch(error => { console.error(error); process.exitCode = 1 })
