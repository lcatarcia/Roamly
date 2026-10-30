#!/usr/bin/env node
// scripts/axe-check.mjs
//
// Verificatore #3 di R14/Dark mode (ADR-0007): "axe-core sui test di
// componente, eseguito due volte, una per tema, tramite un wrapper di test
// che imposta data-theme. Un componente che passa solo in chiaro fallisce
// la suite."
//
// SCOSTAMENTO DICHIARATO dal testo dell'ADR: l'ADR immagina axe-core dentro
// una suite di test di componente (jsdom/Testing Library). In Phase 0 non
// esiste ancora un framework di test configurato (nessun Vitest installato):
// questo script usa Playwright + il server Vite self-contenuto per verificare
// le schermate di autenticazione, i principali passaggi del flusso e axe-core
// nei due temi. Il server dev e' necessario per includere la route /register,
// che l'app disabilita intenzionalmente nelle build di produzione.

import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import https from 'node:https'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { chromium } from 'playwright'
import { AxeBuilder } from '@axe-core/playwright'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const ROOT = path.resolve(__dirname, '..')
const VITE_CLI = path.join(ROOT, 'node_modules/vite/bin/vite.js')
const DEV_SERVER_PORT = 4319
const DEV_SERVER_URL = `https://127.0.0.1:${DEV_SERVER_PORT}`
const AUTH_ROUTES = [
  { path: '/', name: 'login', heading: 'Accedi' },
  { path: '/register', name: 'registrazione', heading: 'Crea il tuo account' },
  { path: '/email/confirm', name: 'conferma email', heading: 'Conferma la tua email' },
]

function waitForServer(url, timeoutMs = 30000) {
  const started = Date.now()
  return new Promise((resolve, reject) => {
    const tryOnce = () => {
      const request = https.get(url, { rejectUnauthorized: false }, (response) => {
        response.resume()
        resolve()
      })
      request.on('error', () => {
        if (Date.now() - started > timeoutMs) {
          reject(new Error(`Timeout in attesa di ${url}`))
        } else {
          setTimeout(tryOnce, 300)
        }
      })
    }
    tryOnce()
  })
}

async function main() {
  console.log(`Avvio del server Vite su porta ${DEV_SERVER_PORT}...`)
  const devServer = spawn(process.execPath, [VITE_CLI, '--host', '127.0.0.1', '--port', String(DEV_SERVER_PORT), '--strictPort'], {
    cwd: ROOT,
    stdio: 'pipe',
  })

  let devServerFailed = false
  devServer.on('exit', (code) => {
    if (code !== null && code !== 0) devServerFailed = true
  })

  let browser
  try {
    await waitForServer(DEV_SERVER_URL)

    browser = await chromium.launch()
    let totalViolations = 0

    for (const theme of ['light', 'dark']) {
      const context = await browser.newContext({ ignoreHTTPSErrors: true })
      const page = await context.newPage()
      await page.goto(DEV_SERVER_URL, { waitUntil: 'networkidle' })
      await page.evaluate((t) => window.localStorage.setItem('roamly.theme', t), theme)
      await page.reload({ waitUntil: 'networkidle' })
      await verifyBrandAssets(page, theme)

      for (const route of AUTH_ROUTES) {
        await page.goto(`${DEV_SERVER_URL}${route.path}`, { waitUntil: 'networkidle' })
        await page.getByRole('heading', { name: route.heading }).waitFor()
        totalViolations += await reportViolations(page, `${route.name}, tema "${theme}"`)
      }

      totalViolations += await verifyRegistrationAndConfirmation(page, theme)
      await context.close()
    }

    if (totalViolations > 0) {
      console.error(`\naxe-check: FALLITO (${totalViolations} violazioni totali tra i due temi).`)
      process.exitCode = 1
      return
    }

    console.log('\naxe-check: PASSATO in entrambi i temi.')
  } finally {
    await browser?.close()
    // Vite e' avviato direttamente come processo Node; terminare il
    // PID specifico evita wrapper shell e processi residui su Windows.
    if (!devServer.killed) devServer.kill()
    if (devServerFailed) {
      console.error('axe-check: il server Vite e' + " terminato in modo anomalo.")
      process.exitCode = 1
    }
  }
}

async function reportViolations(page, label) {
  const results = await new AxeBuilder({ page }).analyze()

  console.log(`\n=== axe-core, ${label} ===`)
  if (results.violations.length === 0) {
    console.log('  Nessuna violazione.')
    return 0
  }

  for (const violation of results.violations) {
    console.error(`X [${violation.impact}] ${violation.id}: ${violation.help} (${violation.nodes.length} nodo/i)`)
    for (const node of violation.nodes) {
      console.error(`    - ${node.target.join(' ')}`)
    }
  }
  return results.violations.reduce((count, violation) => count + violation.nodes.length, 0)
}

async function verifyBrandAssets(page, theme) {
  const wordmarkPath = `/brand/logo-horizontal-${theme}.png`
  const wordmark = page.locator('header').getByRole('img', { name: 'Roamly', exact: true })
  const icon = page.locator('main').getByRole('img', { name: 'Roamly', exact: true })
  await wordmark.waitFor()
  await icon.waitFor()
  assert.equal(await wordmark.getAttribute('src'), wordmarkPath)
  assert.equal(await icon.getAttribute('src'), '/brand/app-icon.png')
  assert.equal(await page.locator('link[rel="icon"]').getAttribute('href'), '/brand/favicon-32.png')
  assert.equal(await page.locator('link[rel="apple-touch-icon"]').getAttribute('href'), '/brand/apple-touch-icon.png')

  for (const [src, width, height] of [
    [wordmarkPath, 1300, 332],
    ['/brand/app-icon.png', 319, 319],
    ['/brand/favicon-32.png', 32, 32],
    ['/brand/apple-touch-icon.png', 180, 180],
  ]) {
    const pixels = await page.evaluate(async (url) => {
      const image = new Image()
      image.src = url
      await image.decode()
      const canvas = document.createElement('canvas')
      canvas.width = image.naturalWidth
      canvas.height = image.naturalHeight
      const context = canvas.getContext('2d')
      context.drawImage(image, 0, 0)
      const { data } = context.getImageData(0, 0, canvas.width, canvas.height)
      let transparent = 0, opaque = 0, partial = 0
      for (let i = 3; i < data.length; i += 4) {
        if (data[i] === 0) transparent++
        else if (data[i] === 255) opaque++
        else partial++
      }
      return { width: canvas.width, height: canvas.height, transparent, opaque, partial }
    }, src)
    assert.equal(pixels.width, width, src)
    assert.equal(pixels.height, height, src)
    assert.ok(pixels.transparent > width * height * 0.05, `${src}: sfondo realmente trasparente`)
    assert.ok(pixels.opaque > width * height * 0.25, `${src}: illustrazione preservata`)
    assert.ok(pixels.partial > 0, `${src}: bordi antialias`)
  }
}

async function verifyRegistrationAndConfirmation(page, theme) {
  let violations = 0
  let registrationRequest = null
  let registrationHeaders = null
  let registrationStatus = 400
  let confirmationRequest = null
  let confirmationHeaders = null
  let confirmationStatus = 400
  let loginStatus = 500
  await page.route('**/api/v1/csrf-token', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: '{"token":"test-csrf"}' }),
  )
  await page.route('**/api/v1/me', (route) =>
    route.fulfill({ status: 401, contentType: 'application/problem+json', body: '{"title":"Non autenticato."}' }),
  )
  await page.route('**/api/v1/auth/login', (route) =>
    route.fulfill({
      status: loginStatus,
      contentType: 'application/problem+json',
      body: JSON.stringify({ title: loginStatus === 401 ? 'Credenziali non valide.' : 'Errore del server.' }),
    }),
  )
  await page.route('**/api/v1/auth/register', async (route) => {
    registrationRequest = route.request().postDataJSON()
    registrationHeaders = await route.request().allHeaders()
    await route.fulfill({
      status: registrationStatus,
      contentType: registrationStatus === 202 ? 'application/json' : 'application/problem+json',
      body: registrationStatus === 202
        ? JSON.stringify({ message: "La richiesta e' stata gestita." })
        : JSON.stringify({ title: 'Dati non validi.', errors: { password: ['Policy password non rispettata.'] } }),
    })
  })
  await page.route('**/api/v1/auth/email/confirm', async (route) => {
    confirmationRequest = route.request().postDataJSON()
    confirmationHeaders = await route.request().allHeaders()
    await route.fulfill({
      status: confirmationStatus,
      contentType: confirmationStatus === 200 ? 'application/json' : 'application/problem+json',
      body: confirmationStatus === 200
        ? '{"message":"Email confermata."}'
        : '{"title":"Link di conferma non valido o scaduto."}',
    })
  })

  await page.goto(DEV_SERVER_URL, { waitUntil: 'networkidle' })
  await page.getByRole('link', { name: 'Registrati' }).click()
  // Le etichette Email/Password esistono anche nel login, ancora visibile durante la transizione.
  await page.getByRole('heading', { name: 'Crea il tuo account' }).waitFor()
  await page.getByLabel('Email').fill('persona@example.test')
  await page.getByLabel('Password', { exact: true }).fill('Test-password-123!')
  await page.getByLabel('Conferma password').fill('password-diversa')
  await page.getByRole('button', { name: 'Crea account' }).click()
  await page.getByText('Le password non corrispondono.').waitFor()
  assert.equal(registrationRequest, null, 'La password non corrispondente non deve inviare la richiesta.')

  await page.getByLabel('Conferma password').fill('Test-password-123!')
  await page.getByLabel('Nome visualizzato (facoltativo)').fill('Persona di prova')
  await page.getByRole('button', { name: 'Crea account' }).click()
  await page.getByText('La password non rispetta i requisiti richiesti oppure i dati non sono validi. Verifica e riprova.').waitFor()
  assert.equal(registrationStatus, 400, 'Il primo tentativo deve esercitare l’errore di validazione server.')
  assert.equal(registrationHeaders['x-roamly-request'], '1')
  assert.equal(registrationHeaders['x-xsrf-token'], 'test-csrf')

  registrationStatus = 202
  await page.getByRole('button', { name: 'Crea account' }).click()
  const acceptedMessage = await page.getByRole('status').innerText()
  assert.ok(acceptedMessage.includes('La richiesta e'))
  assert.ok(!acceptedMessage.includes('persona@example.test'), 'Lo stato accettato non deve ripetere l’email.')
  assert.deepEqual(registrationRequest, {
    email: 'persona@example.test',
    password: 'Test-password-123!',
    displayName: 'Persona di prova',
  })
  assert.equal(registrationHeaders['x-roamly-request'], '1')
  assert.equal(registrationHeaders['x-xsrf-token'], 'test-csrf')
  assert.equal(
    await page.getByText(/In locale non viene inviata/).count(),
    1,
    "la nota operativa locale e' visibile solo nella route dev di registrazione",
  )
  violations += await reportViolations(page, `registrazione accettata, tema "${theme}"`)

  await page.getByRole('link', { name: 'Apri il modulo di conferma email' }).click()
  await page.getByRole('heading', { name: 'Conferma la tua email' }).waitFor()
  await page.getByLabel('UserId').fill('21ec2020-3aea-4069-a2dd-08002b4f0000')
  await page.getByLabel('Token di conferma').fill('token-di-prova/+con-caratteri')
  await page.getByRole('button', { name: 'Conferma email' }).click()
  await page.getByText('I dati di conferma non sono validi o sono scaduti. Verificali e riprova.').waitFor()
  assert.equal(confirmationHeaders['x-roamly-request'], '1')
  assert.equal(confirmationHeaders['x-xsrf-token'], 'test-csrf')

  confirmationStatus = 200
  await page.getByRole('button', { name: 'Conferma email' }).click()
  const confirmationMessage = await page.getByRole('status').innerText()
  assert.equal(confirmationMessage, 'Email confermata. Ora puoi accedere.')
  assert.deepEqual(confirmationRequest, {
    userId: '21ec2020-3aea-4069-a2dd-08002b4f0000',
    token: 'token-di-prova/+con-caratteri',
  })
  assert.ok(!page.url().includes('token-di-prova'), 'Il token non deve comparire nell’URL.')
  violations += await reportViolations(page, `conferma completata, tema "${theme}"`)

  await page.getByRole('link', { name: 'Vai al login' }).click()
  await page.getByRole('heading', { name: 'Accedi', exact: true }).waitFor()
  await page.getByLabel('Email').fill('persona@example.test')
  await page.getByLabel('Password').fill('password-errata')
  await page.getByRole('button', { name: 'Accedi' }).click()
  await page.getByText("Non e' stato possibile accedere al servizio. Riprova.").waitFor()

  loginStatus = 401
  await page.getByRole('button', { name: 'Accedi' }).click()
  await page.getByText('Email o password non corretti.').waitFor()
  return violations
}

main().catch((error) => {
  console.error(error)
  process.exitCode = 1
})
