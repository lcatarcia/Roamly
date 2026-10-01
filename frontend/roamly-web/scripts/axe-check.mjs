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
// questo script e' uno SCAFFOLD MINIMO FUNZIONANTE che ottiene lo stesso
// risultato (axe-core sull'app reale, due volte, una per tema) usando
// Playwright + `vite preview` self-contenuto, invece che sui singoli
// componenti isolati. Va migrato a test di componente veri quando Vitest
// sara' configurato (fuori scope di Phase 0). Nel frattempo copre la
// pagina di login (l'unica schermata esistente in Phase 0).

import { spawn } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { chromium } from 'playwright'
import { AxeBuilder } from '@axe-core/playwright'

// Il certificato di "vite preview" (via @vitejs/plugin-basic-ssl) e'
// self-signed: il `fetch` nativo di Node lo rifiuterebbe di default. Questo
// script gira solo in locale/CI come verificatore, mai contro un servizio
// esposto: disabilitare la verifica qui e' un compromesso accettato, non
// una pratica da estendere al resto del codice.
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const ROOT = path.resolve(__dirname, '..')
const VITE_CLI = path.join(ROOT, 'node_modules/vite/bin/vite.js')
const PREVIEW_PORT = 4319
// https, non http: il plugin @vitejs/plugin-basic-ssl si applica anche a
// "vite preview" (stessa config server), quindi anche qui il certificato
// e' self-signed e va accettato esplicitamente (fetch + Playwright sotto).
const PREVIEW_URL = `https://127.0.0.1:${PREVIEW_PORT}`

function waitForServer(url, timeoutMs = 30000) {
  const started = Date.now()
  return new Promise((resolve, reject) => {
    const tryOnce = () => {
      fetch(url, { dispatcher: undefined })
        .then(() => resolve())
        .catch(() => {
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
  console.log('Build di produzione (necessaria per "vite preview")...')
  await runToCompletion('npm', ['run', 'build'], ROOT)

  console.log(`Avvio "vite preview" su porta ${PREVIEW_PORT}...`)
  const preview = spawn(process.execPath, [VITE_CLI, 'preview', '--port', String(PREVIEW_PORT), '--host', '127.0.0.1', '--strictPort'], {
    cwd: ROOT,
    stdio: 'pipe',
  })

  let previewFailed = false
  preview.on('exit', (code) => {
    if (code !== null && code !== 0) previewFailed = true
  })

  try {
    await waitForServer(PREVIEW_URL)

    const browser = await chromium.launch()
    let totalViolations = 0

    for (const theme of ['light', 'dark']) {
      const context = await browser.newContext({ ignoreHTTPSErrors: true })
      const page = await context.newPage()
      await page.goto(PREVIEW_URL, { waitUntil: 'networkidle' })
      await page.evaluate((t) => window.localStorage.setItem('roamly.theme', t), theme)
      await page.reload({ waitUntil: 'networkidle' })

      const results = await new AxeBuilder({ page }).analyze()

      console.log(`\n=== axe-core, tema "${theme}" ===`)
      if (results.violations.length === 0) {
        console.log('  Nessuna violazione.')
      } else {
        for (const violation of results.violations) {
          totalViolations += violation.nodes.length
          console.error(`X [${violation.impact}] ${violation.id}: ${violation.help} (${violation.nodes.length} nodo/i)`)
          for (const node of violation.nodes) {
            console.error(`    - ${node.target.join(' ')}`)
          }
        }
      }

      await context.close()
    }

    await browser.close()

    if (totalViolations > 0) {
      console.error(`\naxe-check: FALLITO (${totalViolations} violazioni totali tra i due temi).`)
      process.exitCode = 1
      return
    }

    console.log('\naxe-check: PASSATO in entrambi i temi.')
  } finally {
    // Vite preview e' avviato direttamente come processo Node; terminare il
    // PID specifico evita wrapper shell e processi residui su Windows.
    if (!preview.killed) preview.kill()
    if (previewFailed) {
      console.error('axe-check: il processo "vite preview" e' + " terminato in modo anomalo.")
      process.exitCode = 1
    }
  }
}

function runToCompletion(cmd, args, cwd) {
  return new Promise((resolve, reject) => {
    const child = spawn(cmd, args, { cwd, shell: true, stdio: 'inherit' })
    child.on('exit', (code) => {
      if (code === 0) resolve()
      else reject(new Error(`${cmd} ${args.join(' ')} uscito con codice ${code}`))
    })
  })
}

main().catch((error) => {
  console.error(error)
  process.exitCode = 1
})
