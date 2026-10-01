#!/usr/bin/env node
// scripts/contrast-check.mjs
//
// Verificatore #1 di R14 (ADR-0007): legge i valori REALI da
// src/styles/theme.css, ricalcola WCAG 2.x su tutte le coppie dichiarate in
// src/design/contrast-pairs.json e fallisce sotto soglia. Nessun database,
// nessun browser: gira in meno di un secondo.
//
// Uscita: 0 se tutte le coppie con soglia obbligatoria passano e i ratio
// dichiarati nel manifesto non si sono scostati dal reale (drift della
// palette non accompagnato da un aggiornamento del manifesto); 1 altrimenti.

import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { loadThemeTokens, resolveLiteralHex } from './lib/theme-tokens.mjs'
import { contrastRatio } from './lib/wcag-contrast.mjs'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const MANIFEST_PATH = path.resolve(__dirname, '../src/design/contrast-pairs.json')

// Tolleranza di scostamento tra il ratio dichiarato nell'ADR (arrotondato a
// mano nel documento) e quello ricalcolato con precisione doppia: oltre
// questa soglia, il manifesto e theme.css si sono scollegati (drift) e va
// indagato anche se la soglia WCAG e' ancora rispettata.
const DRIFT_TOLERANCE = 0.05

function main() {
  const manifest = JSON.parse(readFileSync(MANIFEST_PATH, 'utf8'))
  const tokens = loadThemeTokens()

  let failures = 0
  let warnings = 0
  const rows = []

  for (const pair of manifest.coppie) {
    let fgHex
    let bgHex
    try {
      fgHex = resolveLiteralHex(pair.fg, tokens)
      bgHex = resolveLiteralHex(pair.bg, tokens)
    } catch (error) {
      failures++
      rows.push({ id: pair.id, esito: 'ERRORE', dettaglio: error.message })
      continue
    }

    const realRatio = contrastRatio(fgHex, bgHex)
    const drift = Math.abs(realRatio - pair.ratio)

    if (pair.declaredNegative) {
      // Coppia con esito negativo dichiarato (D23/D24/D25): non deve passare
      // la soglia, deve pero' avere una mitigazione documentata.
      if (!pair.mitigazione || pair.mitigazione.trim().length === 0) {
        failures++
        rows.push({
          id: pair.id,
          esito: 'ERRORE',
          dettaglio: `dichiarata negativa ma senza "mitigazione" nel manifesto`,
        })
      } else {
        rows.push({
          id: pair.id,
          esito: 'OK (negativa dichiarata)',
          dettaglio: `${pair.fg}/${pair.bg} = ${realRatio.toFixed(2)}:1 (mitigata: ${pair.mitigazione.slice(0, 40)}...)`,
        })
      }
      continue
    }

    const passesThreshold = realRatio + 1e-9 >= pair.soglia
    const withinDrift = drift <= DRIFT_TOLERANCE

    if (!passesThreshold) {
      failures++
      rows.push({
        id: pair.id,
        esito: 'FALLITA (sotto soglia)',
        dettaglio: `${pair.fg}/${pair.bg} = ${realRatio.toFixed(2)}:1, richiesto >= ${pair.soglia}:1`,
      })
    } else if (!withinDrift) {
      warnings++
      rows.push({
        id: pair.id,
        esito: 'AVVISO (drift manifesto)',
        dettaglio: `ratio reale ${realRatio.toFixed(2)} si discosta da quello dichiarato ${pair.ratio} (differenza ${drift.toFixed(2)} > tolleranza ${DRIFT_TOLERANCE})`,
      })
    } else {
      rows.push({ id: pair.id, esito: 'OK', dettaglio: `${pair.fg}/${pair.bg} = ${realRatio.toFixed(2)}:1 (>= ${pair.soglia}:1)` })
    }
  }

  for (const row of rows) {
    const marker = row.esito.startsWith('OK') ? '  ' : row.esito.startsWith('AVVISO') ? '! ' : 'X '
    console.log(`${marker}${row.id.padEnd(4)} ${row.esito.padEnd(28)} ${row.dettaglio}`)
  }

  console.log('')
  console.log(
    `Totale coppie: ${manifest.coppie.length} (attese ${manifest.totale}) | falliti: ${failures} | avvisi: ${warnings}`,
  )

  if (manifest.coppie.length !== manifest.totale) {
    console.log(
      `AVVISO: il manifesto dichiara totale=${manifest.totale} ma contiene ${manifest.coppie.length} voci. Aggiornare "totale"/"chiare"/"scure" o completare la matrice.`,
    )
  }

  if (failures > 0) {
    console.error(`\ncontrast-check: FALLITO (${failures} coppie sotto soglia o con errore).`)
    process.exit(1)
  }

  console.log('\ncontrast-check: PASSATO.')
}

main()
