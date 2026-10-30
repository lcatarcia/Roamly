#!/usr/bin/env node

import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import https from 'node:https'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { AxeBuilder } from '@axe-core/playwright'
import { chromium } from 'playwright'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const VITE_CLI = path.join(ROOT, 'node_modules/vite/bin/vite.js')
const PORT = 4322
const BASE_URL = `https://127.0.0.1:${PORT}`
const CAMPER_ID = '0199cafe-1000-7000-8000-000000000001'

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
    const context = await browser.newContext({ ignoreHTTPSErrors: true, acceptDownloads: true })
    const page = await context.newPage()
    await page.addInitScript(() => window.localStorage.setItem('roamly.theme', 'light'))
    const state = await mockApi(page)
    await page.goto(BASE_URL, { waitUntil: 'networkidle' })
    await login(page, 'persona@example.test', 'password-normale')
    await page.getByRole('heading', { name: 'Camper riservato' }).waitFor()
    await page.getByText('Non hai ancora aggiunto accessori a questo camper.').waitFor()
    state.normalCamperApiRequests = state.camperApiRequests

    await page.getByRole('link', { name: 'Privacy e account' }).click()
    await page.getByRole('heading', { name: "Richiedi la cancellazione dell'account" }).waitFor()
    await assertAccessible(page, 'privacy normale, tema chiaro')
    await page.getByRole('button', { name: 'Attiva tema scuro' }).click()
    await assertAccessible(page, 'privacy normale, tema scuro')
    await verifyExport(page, state)
    await scheduleDeletion(page, state)
    await page.getByRole('heading', { name: 'Accesso limitato' }).waitFor()
    await page.getByText(/Richiesta di cancellazione programmata/).waitFor()
    assert.equal(await page.getByRole('heading', { name: 'Esporta i dati' }).count(), 0)
    await assertNoCamperAccess(page, state)
    await page.getByRole('button', { name: 'Esci' }).click()
    await page.getByRole('link', { name: 'Vai al login' }).click()
    await page.getByRole('heading', { name: 'Accedi' }).waitFor()

    await login(page, 'persona@example.test', 'limited-pass')
    await page.getByRole('heading', { name: 'Accesso limitato' }).waitFor()
    await page.getByText(/Richiesta di cancellazione programmata/).waitFor()
    await assertNoCamperAccess(page, state)
    await assertAccessible(page, 'sessione limitata, tema scuro')
    await page.getByRole('button', { name: 'Attiva tema chiaro' }).click()
    await assertAccessible(page, 'sessione limitata, tema chiaro')

    state.limitedOnRefresh = true
    await page.goto(`${BASE_URL}/campers/${CAMPER_ID}`, { waitUntil: 'networkidle' })
    await page.getByRole('heading', { name: 'Accesso limitato' }).waitFor()
    await page.reload({ waitUntil: 'networkidle' })
    await page.getByRole('heading', { name: 'Accesso limitato' }).waitFor()
    await assertNoCamperAccess(page, state)

    await page.getByRole('button', { name: 'Revoca la richiesta' }).click()
    await page.getByText(/La richiesta e' stata revocata/).waitFor()
    assert.equal(state.revokeRequest?.headers['x-roamly-request'], '1')
    assert.equal(state.revokeRequest?.headers['x-xsrf-token'], 'privacy-csrf')
    await assertNoCamperAccess(page, state)
    await page.getByRole('button', { name: 'Esci' }).click()
    await page.getByRole('heading', { name: 'Accedi' }).waitFor()
    await page.goto(BASE_URL, { waitUntil: 'networkidle' })
    const campersRefetch = page.waitForRequest((request) =>
      new URL(request.url()).pathname === '/api/v1/campers',
    )
    await login(page, 'persona@example.test', 'password-normale')
    await campersRefetch
    await page.getByRole('heading', { name: 'Camper riservato' }).waitFor()
    assert.equal(state.campersRequests, 2, 'Dopo logout e nuovo login la cache deve ricaricarsi.')
    state.meFailure = true
    await page.goto(`${BASE_URL}/campers/${CAMPER_ID}`, { waitUntil: 'networkidle' })
    await page.getByRole('heading', { name: 'Accedi' }).waitFor()
    assert.equal(await page.getByRole('heading', { name: 'Camper riservato' }).count(), 0)
    assert.equal(state.campersRequests, 2, 'Un errore di autenticazione non deve ricaricare i camper.')
    console.log('privacy-account-check: superato (normale, limitata, purge, revoca, export e axe).')
  } finally {
    preview.kill()
    await browser.close()
  }
}

async function login(page, email, password) {
  const emailField = page.getByLabel('Email')
  await emailField.fill(email)
  await page.getByLabel('Password', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Accedi' }).click()
}

async function scheduleDeletion(page, state) {
  const password = page.getByLabel('Password attuale')
  await password.fill('current-password')
  await page.getByRole('button', { name: 'Richiedi la cancellazione' }).click()
  await page.getByText(/La password non e' corretta/).waitFor()
  assert.equal(await password.inputValue(), '', 'La password deve essere pulita anche dopo un errore.')
  await password.fill('current-password')
  await page.getByText(/periodo di grazia di 30 giorni/).waitFor()
  await page.getByText(/sessione e' limitata/).waitFor()
  await page.getByText(/cancellazione dell'account e' irreversibile/).waitFor()
  await page.getByRole('button', { name: 'Richiedi la cancellazione' }).click()
  await page.getByRole('heading', { name: 'Accesso limitato' }).waitFor()
  await page.getByText(/Data prevista:/).waitFor()
  assert.deepEqual(state.scheduleBody, { password: 'current-password' })
  assert.equal(state.scheduleAttempts, 2)
  assert.equal(state.scheduleHeaders['x-roamly-request'], '1')
  assert.equal(state.scheduleHeaders['x-xsrf-token'], 'privacy-csrf')
  assert.ok((await page.getByText(/Data prevista:/).innerText()).includes('5 novembre 2026'))
  await page.getByText(/cancellazione dell'account e' irreversibile/).waitFor()
}

async function verifyExport(page, state) {
  await page.getByRole('button', { name: "Scarica l'archivio" }).click()
  await page.getByText(/esci e accedi di nuovo con la password/).waitFor()
  assert.equal(state.exportCalls, 1)

  state.exportAllowed = true
  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: "Scarica l'archivio" }).click()
  const download = await downloadPromise
  assert.equal(download.suggestedFilename(), 'roamly-account-export.zip')
  assert.equal(state.exportCalls, 2)
}

async function assertNoCamperAccess(page, state) {
  await page.waitForTimeout(100)
  assert.equal(await page.getByRole('heading', { name: 'Camper riservato' }).count(), 0)
  assert.equal(await page.getByRole('link', { name: 'Profilo tecnico' }).count(), 0)
  assert.equal(state.campersRequests, 1, 'La sessione limitata non deve invocare API camper.')
  assert.equal(
    state.camperApiRequests,
    state.normalCamperApiRequests,
    'La sessione limitata non deve chiamare alcun endpoint camper.',
  )
}

async function assertAccessible(page, label) {
  const results = await new AxeBuilder({ page }).analyze()
  for (const violation of results.violations) {
    console.error(label, violation.id, violation.nodes.map(({ target, failureSummary }) => ({
      target,
      failureSummary,
    })))
  }
  assert.deepEqual(
    results.violations.map(({ id, impact }) => ({ id, impact })),
    [],
    `Violazioni axe nella schermata ${label}.`,
  )
}

async function mockApi(page) {
  const state = {
    loggedIn: false,
    mode: 'normal',
    meFailure: false,
    deletionScheduled: false,
    limitedOnRefresh: false,
    exportAllowed: false,
    exportCalls: 0,
    campersRequests: 0,
    camperApiRequests: 0,
    normalCamperApiRequests: 0,
    scheduleBody: null,
    scheduleHeaders: null,
    scheduleAttempts: 0,
    revokeRequest: null,
  }

  page.on('request', (request) => {
    if (new URL(request.url()).pathname.startsWith('/api/v1/campers')) {
      state.camperApiRequests += 1
    }
  })
  await page.route('**/api/v1/csrf-token', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: '{"token":"privacy-csrf"}',
  }))
  await page.route('**/api/v1/me', (route) => {
    if (state.meFailure) {
      return route.fulfill({
        status: 500,
        contentType: 'application/problem+json',
        body: '{"title":"Errore temporaneo."}',
      })
    }
    if (!state.loggedIn) {
      return route.fulfill({ status: 401, contentType: 'application/problem+json', body: '{"title":"Non autenticato."}' })
    }
    if (state.mode === 'limited' || state.limitedOnRefresh) {
      return route.fulfill({
        status: 403,
        contentType: 'application/problem+json',
        body: JSON.stringify({
          type: 'https://roamly.dev/problems/restricted-session',
          title: 'Sessione limitata.',
        }),
      })
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'owner',
        email: 'persona@example.test',
        displayName: 'Persona',
        reportingCurrency: 'EUR',
      }),
    })
  })
  await page.route('**/api/v1/auth/login', async (route) => {
    const input = route.request().postDataJSON()
    state.loggedIn = true
    state.mode = input.password === 'limited-pass' ? 'limited' : 'normal'
    state.limitedOnRefresh = false
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'owner',
        email: input.email,
        displayName: 'Persona',
        ...(state.mode === 'limited' ? { sessionMode: 'limited' } : {}),
      }),
    })
  })
  await page.route('**/api/v1/auth/logout', async (route) => {
    state.loggedIn = false
    await route.fulfill({ status: 204 })
  })
  await page.route('**/api/v1/me/deletion/preview', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      generatedAtUtc: '2026-10-06T08:00:00Z',
      entityCounts: [{ entityType: 'Camper', count: 1 }, { entityType: 'Equipment', count: 2 }],
      totalCount: 3,
    }),
  }))
  await page.route('**/api/v1/me/deletion', async (route) => {
    if (route.request().method() === 'GET') {
      if (!state.deletionScheduled) return route.fulfill({ status: 204 })
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          status: 'scheduled',
          deletionRequestedAtUtc: '2026-10-06T08:00:00Z',
          deletionScheduledForUtc: '2026-11-05T10:00:00Z',
        }),
      })
    }
    const headers = await route.request().allHeaders()
    if (route.request().method() === 'POST') {
      state.scheduleBody = route.request().postDataJSON()
      state.scheduleHeaders = headers
      state.scheduleAttempts += 1
      if (state.scheduleAttempts === 1) {
        await route.fulfill({
          status: 401,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'Password non corretta.' }),
        })
        return
      }
      state.deletionScheduled = true
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          deletionRequestedAtUtc: '2026-10-06T08:00:00Z',
          deletionScheduledForUtc: '2026-11-05T10:00:00Z',
        }),
      })
      return
    }
    state.revokeRequest = { headers }
    state.deletionScheduled = false
    await route.fulfill({ status: 204 })
  })
  await page.route('**/api/v1/me/export', async (route) => {
    state.exportCalls += 1
    if (!state.exportAllowed) {
      return route.fulfill({
        status: 401,
        contentType: 'application/problem+json',
        body: JSON.stringify({
          type: 'https://roamly.dev/problems/recent-password-required',
          title: 'Password recente richiesta.',
        }),
      })
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/zip',
      body: 'test-zip-content',
    })
  })
  await page.route('**/api/v1/campers?*', async (route) => {
    state.campersRequests += 1
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [{ id: CAMPER_ID, name: 'Camper riservato', vehicleKind: 1 }],
        nextCursor: null,
      }),
    })
  })
  await page.route(`**/api/v1/campers/${CAMPER_ID}/dashboard`, (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      camper: {
        id: CAMPER_ID,
        name: 'Camper riservato',
        brand: null,
        model: null,
        year: null,
        vehicleKind: 1,
      },
      odometer: null,
      maintenance: [],
    }),
  }))
  await page.route(`**/api/v1/campers/${CAMPER_ID}/equipment?*`, (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: '{"items":[],"nextCursor":null}',
  }))

  return state
}

async function startBrowser(preview) {
  await waitForServer()
  if (preview.exitCode !== null) {
    throw new Error('vite preview si e’ chiuso prima di avviare il test.')
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
      else reject(new Error(`${command} ${args.join(' ')} si e’ chiuso con codice ${code}`))
    })
  })
}

main().catch((error) => {
  console.error(error)
  process.exitCode = 1
})
