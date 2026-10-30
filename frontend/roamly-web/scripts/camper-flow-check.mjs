#!/usr/bin/env node

import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import https from 'node:https'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { chromium } from 'playwright'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const VITE_CLI = path.join(ROOT, 'node_modules/vite/bin/vite.js')
const PORT = 4321
const BASE_URL = `https://127.0.0.1:${PORT}`
const CAMPER_ID = '0199cafe-1000-7000-8000-000000000001'
const SECOND_CAMPER_ID = '0199cafe-1000-7000-8000-000000000002'
const EQUIPMENT_ID = '0199cafe-2000-7000-8000-000000000001'

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
    const page = await browser.newPage({ ignoreHTTPSErrors: true })
    await page.setViewportSize({ width: 1440, height: 900 })
    const observed = await mockApi(page)
    await page.goto(BASE_URL, { waitUntil: 'networkidle' })

    await page.getByRole('link', { name: 'Profilo tecnico' }).first().click()
    await page.getByRole('heading', { name: 'Profilo tecnico del camper' }).waitFor()
    const profileWidth = await page.locator('.max-w-6xl').evaluate((element) =>
      element.getBoundingClientRect().width,
    )
    assert.ok(profileWidth > 1000, `Expected a wide desktop profile, got ${profileWidth}px`)
    await page.setViewportSize({ width: 390, height: 844 })
    const profileFitsMobile = await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    )
    assert.ok(profileFitsMobile, 'Profile page should not overflow a mobile viewport')

    await page.setViewportSize({ width: 1440, height: 900 })
    await page.getByLabel('Marca').fill('Laika')
    await page.getByLabel('Modello').fill('Kreos')
    await page.getByRole('button', { name: 'Salva profilo' }).click()
    await page.getByText('Profilo aggiornato.', { exact: true }).waitFor()

    await page.getByRole('link', { name: 'Torna alla dashboard' }).click()
    await page.getByRole('heading', { name: 'Il Grigio' }).waitFor()
    const dashboardWidth = await page.locator('.max-w-7xl.min-w-0').evaluate((element) =>
      element.getBoundingClientRect().width,
    )
    assert.ok(dashboardWidth > 1100, `Expected a wide desktop dashboard, got ${dashboardWidth}px`)
    await page.setViewportSize({ width: 390, height: 844 })
    const dashboardFitsMobile = await page.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    )
    assert.ok(dashboardFitsMobile, 'Dashboard should not overflow a mobile viewport')
    await page.setViewportSize({ width: 1440, height: 900 })

    const equipmentPanel = page.getByRole('region', { name: 'Equipaggiamento' })
    await equipmentPanel.getByLabel('Nome', { exact: true }).fill('Pannello solare')
    await equipmentPanel.getByLabel('Categoria', { exact: true }).selectOption('0')
    await equipmentPanel.getByLabel('Note', { exact: true }).fill('Installato sul tetto')
    await equipmentPanel.getByRole('button', { name: 'Aggiungi accessorio' }).click()
    await page.getByRole('heading', { name: 'Pannello solare' }).waitFor()

    await equipmentPanel.getByRole('button', { name: 'Modifica' }).click()
    await equipmentPanel.getByRole('heading', { name: 'Modifica accessorio' }).waitFor()
    await equipmentPanel.locator('#edit-equipment-notes').fill('Aggiornato')
    await equipmentPanel.getByRole('button', { name: 'Salva modifiche' }).click()
    await page.getByText('Aggiornato', { exact: true }).waitFor()

    await equipmentPanel.getByRole('button', { name: 'Rimuovi Pannello solare' }).click()
    await page.getByText('Non hai ancora aggiunto accessori a questo camper.').waitFor()
    assert.deepEqual(observed, {
      profilePut: true,
      equipmentPost: true,
      equipmentPut: true,
      equipmentDelete: true,
    })
    console.log('camper-flow-check: superato (profilo, If-Match, inventario CRUD e CSRF).')
  } finally {
    await browser.close()
    preview.kill()
  }
}

async function mockApi(page) {
  let profile = {
    id: CAMPER_ID,
    name: 'Il Grigio',
    brand: null,
    model: null,
    year: null,
    plateNumber: null,
    vehicleKind: 1,
    lengthMm: null,
    widthMm: null,
    heightMm: null,
    kerbWeightKg: null,
    maxWeightKg: null,
    freshWaterL: null,
    greyWaterL: null,
    fuelTankL: null,
    gasKg: null,
    fuelKind: null,
    averageConsumptionLPer100Km: null,
  }
  let equipment = null
  let profileEtag = 'W/"profile-v1"'
  let equipmentEtag = 'W/"equipment-v1"'
  const observed = { profilePut: false, equipmentPost: false, equipmentPut: false, equipmentDelete: false }

  await page.route('**/api/v1/me', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ id: 'owner', email: 'owner@example.test', displayName: 'Persona' }),
  }))
  await page.route('**/api/v1/campers?*', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      items: [
        { id: CAMPER_ID, name: 'Il Grigio', vehicleKind: 1 },
        { id: SECOND_CAMPER_ID, name: 'Il Blu', vehicleKind: 0 },
      ],
      nextCursor: null,
    }),
  }))
  await page.route('**/api/v1/csrf-token', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: '{"token":"flow-csrf"}',
  }))
  await page.route(`**/api/v1/campers/${CAMPER_ID}`, async (route) => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { ETag: profileEtag },
        body: JSON.stringify(profile),
      })
      return
    }

    const headers = await route.request().allHeaders()
    assert.equal(headers['x-roamly-request'], '1')
    assert.equal(headers['x-xsrf-token'], 'flow-csrf')
    assert.equal(headers['if-match'], profileEtag)
    profile = { ...profile, ...route.request().postDataJSON() }
    profileEtag = 'W/"profile-v2"'
    observed.profilePut = true
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      headers: { ETag: profileEtag },
      body: JSON.stringify(profile),
    })
  })
  await page.route(`**/api/v1/campers/${CAMPER_ID}/dashboard`, (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      camper: { id: CAMPER_ID, name: profile.name, brand: profile.brand, model: profile.model, year: profile.year, vehicleKind: 1 },
      odometer: null,
      maintenance: [],
    }),
  }))
  await page.route(
    new RegExp(`/api/v1/campers/${CAMPER_ID}/equipment(?:/[^/?]+)?(?:\\?.*)?$`),
    async (route) => {
      const request = route.request()
      const url = new URL(request.url())
      const headers = await request.allHeaders()
      if (url.pathname.endsWith('/equipment') && request.method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: equipment ? [equipment] : [], nextCursor: null }),
        })
        return
      }
      if (url.pathname.endsWith('/equipment') && request.method() === 'POST') {
        assert.equal(headers['x-roamly-request'], '1')
        assert.equal(headers['x-xsrf-token'], 'flow-csrf')
        const input = request.postDataJSON()
        assert.ok(input.id, 'la richiesta di creazione deve conservare l’id client-side')
        equipment = { id: EQUIPMENT_ID, ...input }
        observed.equipmentPost = true
        await route.fulfill({ status: 201, contentType: 'application/json', body: JSON.stringify(equipment) })
        return
      }
      if (request.method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          headers: { ETag: equipmentEtag },
          body: JSON.stringify(equipment),
        })
        return
      }
      if (request.method() === 'PUT') {
        assert.equal(headers['x-roamly-request'], '1')
        assert.equal(headers['x-xsrf-token'], 'flow-csrf')
        assert.equal(headers['if-match'], equipmentEtag)
        equipment = { ...equipment, ...request.postDataJSON() }
        equipmentEtag = 'W/"equipment-v2"'
        observed.equipmentPut = true
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          headers: { ETag: equipmentEtag },
          body: JSON.stringify(equipment),
        })
        return
      }
      if (request.method() === 'DELETE') {
        assert.equal(headers['x-roamly-request'], '1')
        assert.equal(headers['x-xsrf-token'], 'flow-csrf')
        assert.equal(headers['if-match'], equipmentEtag)
        equipment = null
        observed.equipmentDelete = true
        await route.fulfill({ status: 204 })
        return
      }

      await route.fulfill({ status: 405 })
    },
  )

  return observed
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
      else reject(new Error(`${command} ${args.join(' ')} si e' chiuso con codice ${code}`))
    })
  })
}

main().catch((error) => {
  console.error(error)
  process.exitCode = 1
})
