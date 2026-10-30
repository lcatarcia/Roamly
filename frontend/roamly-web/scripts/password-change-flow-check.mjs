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
const PORT = 4326
const BASE_URL = `https://127.0.0.1:${PORT}`

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
    const context = await browser.newContext({ ignoreHTTPSErrors: true })
    const page = await context.newPage()
    await page.addInitScript(() => window.localStorage.setItem('roamly.theme', 'light'))
    const state = await mockApi(page)
    await page.goto(BASE_URL, { waitUntil: 'networkidle' })
    await login(page)
    await page.getByRole('heading', { name: 'Scegli una nuova password' }).waitFor()
    await assertAccessible(page)
    assert.equal(state.productRequests, 0, 'Le API prodotto non devono essere chiamate durante il blocco.')

    await page.getByLabel('Password attuale').fill('initial-pass')
    await page.getByLabel('Nuova password', { exact: true }).fill('Strong!Password456')
    await page.getByLabel('Conferma la nuova password').fill('Different!Password456')
    await page.getByRole('button', { name: 'Cambia password' }).click()
    await page.getByText('Le password non corrispondono.').waitFor()
    assert.equal(state.changes.length, 0, 'La conferma errata non deve inviare la richiesta.')

    await page.getByLabel('Conferma la nuova password').fill('Strong!Password456')
    await page.getByLabel('Password attuale').fill('wrong-current')
    await page.getByRole('button', { name: 'Cambia password' }).click()
    await page.getByText(/La password attuale non e' corretta/).waitFor()
    assert.equal(state.mustChangePassword, true)

    await page.getByLabel('Password attuale').fill('initial-pass')
    await page.getByLabel('Nuova password', { exact: true }).fill('weak')
    await page.getByLabel('Conferma la nuova password').fill('weak')
    await page.getByRole('button', { name: 'Cambia password' }).click()
    await page.getByText(/non rispetta i requisiti di sicurezza/).waitFor()
    assert.equal(state.mustChangePassword, true)

    await page.reload({ waitUntil: 'networkidle' })
    await page.getByRole('heading', { name: 'Scegli una nuova password' }).waitFor()
    assert.equal(state.productRequests, 0, 'Il blocco deve persistere dopo il reload.')

    await page.getByLabel('Password attuale').fill('initial-pass')
    await page.getByLabel('Nuova password', { exact: true }).fill('Strong!Password456')
    await page.getByLabel('Conferma la nuova password').fill('Strong!Password456')
    await page.getByRole('button', { name: 'Cambia password' }).click()
    await page.getByRole('heading', { name: 'Aggiungi il tuo camper' }).waitFor()
    assert.equal(state.mustChangePassword, false)
    assert.ok(state.productRequests > 0, 'Le API prodotto devono tornare accessibili dopo il cambio.')
    assert.deepEqual(state.changes.at(-1).body, {
      currentPassword: 'initial-pass',
      newPassword: 'Strong!Password456',
    })
    assert.equal(state.changes.at(-1).headers['x-roamly-request'], '1')
    assert.equal(state.changes.at(-1).headers['x-xsrf-token'], 'change-csrf')
    console.log('password-change-flow-check: superato (blocco, errori, reload, CSRF e sblocco).')
  } finally {
    await browser.close()
    preview.kill()
  }
}

async function login(page) {
  await page.getByLabel('Email').fill('alpha@example.test')
  await page.getByLabel('Password', { exact: true }).fill('initial-pass')
  await page.getByRole('button', { name: 'Accedi' }).click()
}

async function mockApi(page) {
  const state = {
    loggedIn: false,
    mustChangePassword: true,
    productRequests: 0,
    changes: [],
  }

  page.on('request', (request) => {
    const pathname = new URL(request.url()).pathname
    if (
      pathname.startsWith('/api/v1/')
      && pathname !== '/api/v1/csrf-token'
      && pathname !== '/api/v1/me'
      && !pathname.startsWith('/api/v1/auth/')
    ) {
      state.productRequests += 1
    }
  })
  await page.route('**/api/v1/csrf-token', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: '{"token":"change-csrf"}',
  }))
  await page.route('**/api/v1/me', (route) => {
    if (!state.loggedIn) {
      return route.fulfill({ status: 401, contentType: 'application/problem+json', body: '{"title":"Non autenticato."}' })
    }
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'owner',
        email: 'alpha@example.test',
        displayName: 'Alpha',
        reportingCurrency: 'EUR',
        mustChangePassword: state.mustChangePassword,
      }),
    })
  })
  await page.route('**/api/v1/auth/login', async (route) => {
    state.loggedIn = true
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: 'owner',
        email: 'alpha@example.test',
        displayName: 'Alpha',
      }),
    })
  })
  await page.route('**/api/v1/auth/password/change', async (route) => {
    const body = route.request().postDataJSON()
    const headers = await route.request().allHeaders()
    state.changes.push({ body, headers })
    if (body.currentPassword === 'wrong-current') {
      return route.fulfill({
        status: 401,
        contentType: 'application/problem+json',
        body: JSON.stringify({ type: 'https://roamly.dev/problems/invalid-reauthentication' }),
      })
    }
    if (body.newPassword === 'weak') {
      return route.fulfill({
        status: 400,
        contentType: 'application/problem+json',
        body: JSON.stringify({ errors: { newPassword: ['Policy rejected.'] } }),
      })
    }
    state.mustChangePassword = false
    await route.fulfill({ status: 204 })
  })
  await page.route('**/api/v1/auth/logout', (route) => {
    state.loggedIn = false
    return route.fulfill({ status: 204 })
  })
  await page.route('**/api/v1/campers?*', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: '{"items":[],"nextCursor":null}',
  }))

  return state
}

async function assertAccessible(page) {
  const results = await new AxeBuilder({ page }).analyze()
  assert.deepEqual(
    results.violations.map(({ id, impact }) => ({ id, impact })),
    [],
    'Violazioni axe nella schermata di cambio password.',
  )
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
