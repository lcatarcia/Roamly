#!/usr/bin/env node
// Verifica che ogni coppia foreground/surface semantica usata nel codice sia nel manifesto R14.

import { readFileSync, readdirSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { collectUsedColorPairs } from './lib/contrast-coverage.mjs'
import { loadThemeTokens, resolveHex } from './lib/theme-tokens.mjs'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const SRC_DIR = path.resolve(__dirname, '../src')
const MANIFEST_PATH = path.resolve(__dirname, '../src/design/contrast-pairs.json')

function walk(directory, files = []) {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const file = path.join(directory, entry.name)
    if (entry.isDirectory()) walk(file, files)
    else if (/\.(tsx?|css)$/.test(entry.name)) files.push(file)
  }
  return files
}

function hexNormalize(hex) {
  return hex.toLowerCase()
}

function main() {
  const manifest = JSON.parse(readFileSync(MANIFEST_PATH, 'utf8'))
  const tokens = loadThemeTokens()
  const colorNames = new Set(
    [...tokens.rootMap.keys()]
      .filter((name) => name.startsWith('--color-'))
      .map((name) => name.slice('--color-'.length)),
  )

  const declaredByTheme = { chiaro: new Set(), scuro: new Set() }
  for (const pair of manifest.coppie) {
    const fgHex = resolveHex(`--color-${pair.fg}`, tokens, pair.tema === 'scuro')
    const bgHex = resolveHex(`--color-${pair.bg}`, tokens, pair.tema === 'scuro')
    declaredByTheme[pair.tema].add([hexNormalize(fgHex), hexNormalize(bgHex)].sort().join('|'))
  }

  const files = walk(SRC_DIR)
  const usedPairs = new Map()
  for (const file of files) {
    const source = readFileSync(file, 'utf8')
    for (const pair of collectUsedColorPairs(source, file, colorNames)) {
      const key = `${pair.background}|${pair.foreground}`
      if (!usedPairs.has(key)) {
        usedPairs.set(key, {
          ...pair,
          file: path.relative(SRC_DIR, file).split(path.sep).join('/'),
        })
      }
    }
  }

  let failures = 0
  for (const pair of usedPairs.values()) {
    for (const themeName of ['chiaro', 'scuro']) {
      const isDark = themeName === 'scuro'
      const fgHex = resolveHex(pair.foreground, tokens, isDark)
      const bgHex = resolveHex(pair.background, tokens, isDark)
      const key = [hexNormalize(fgHex), hexNormalize(bgHex)].sort().join('|')
      const details = `${pair.foreground} su ${pair.background} (${pair.file}:${pair.line}) ${themeName}: ${fgHex}/${bgHex}`

      if (declaredByTheme[themeName].has(key)) {
        console.log(`  OK   ${details}`)
      } else {
        failures++
        console.error(`X FALLITA ${details} -> NON dichiarata in contrast-pairs.json`)
      }
    }
  }

  console.log(`\nCoppie semantiche trovate: ${usedPairs.size}; file analizzati: ${files.length}.`)

  if (failures > 0) {
    console.error(`\ncontrast-coverage-check: FALLITO (${failures} combinazioni usate ma non dichiarate).`)
    process.exit(1)
  }

  console.log('\ncontrast-coverage-check: PASSATO.')
}

main()
