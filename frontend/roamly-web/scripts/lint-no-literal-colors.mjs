#!/usr/bin/env node
// scripts/lint-no-literal-colors.mjs
//
// Lint bloccante per R12 e R13 (ADR-0007):
//   R12 - nessun colore letterale (hex, rgb()/hsl(), classi Tailwind
//         arbitrarie bg-[#...]) fuori da src/styles/theme.css.
//   R13 - i token LETTERALI (livello 1, es. --color-sand-100) sono
//         referenziabili SOLO da src/styles/theme.css; altrove si usano
//         ESCLUSIVAMENTE i nomi semantici (livello 2).
//
// Regex-based (non un AST CSS/TS completo): scelta deliberata per restare
// uno script che "gira in meno di un secondo" (stesso principio di R14).
// Falsi negativi possibili su offuscamenti estremi (concatenazione di
// stringhe per costruire un hex); non e' un sostituto di una review umana
// su PR che toccano colori, ma intercetta il caso comune.

import { readFileSync, readdirSync, statSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const SRC_DIR = path.resolve(__dirname, '../src')
const EXEMPT_FILES = new Set([path.resolve(__dirname, '../src/styles/theme.css')])

const SCAN_EXTENSIONS = /\.(tsx?|css)$/

// Nomi dei token letterali (livello 1) definiti in theme.css: qualunque
// riferimento a `--color-<uno-di-questi>` fuori da theme.css viola R13.
const LITERAL_COLOR_NAMES = [
  'white', 'sand-100', 'sand-200',
  'night-950', 'night-900', 'night-800',
  'forest-950', 'forest-900', 'forest-700',
  'moss-700', 'moss-500', 'sage-400', 'sage-300',
  'clay-700', 'clay-600', 'clay-500', 'clay-400',
  'sun-700', 'sun-400', 'dune-300',
  'bark-500', 'bark-400',
  'danger-700', 'danger-600', 'danger-500', 'danger-400',
  'ink',
]

const HEX_COLOR_RE = /#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})\b/g
const COLOR_FN_RE = /\b(?:rgba?|hsla?|oklch|oklab|lab|lch)\s*\(/g
const ARBITRARY_TAILWIND_COLOR_RE = /\b(?:bg|text|border|from|via|to|fill|stroke|ring|outline|decoration|divide|shadow|caret|accent)-\[(#|rgb|hsl|oklch)/g
const LITERAL_VAR_RE = new RegExp(`var\\(\\s*--color-(${LITERAL_COLOR_NAMES.join('|')})\\s*\\)`, 'g')

function walk(dir, out = []) {
  for (const entry of readdirSync(dir)) {
    const full = path.join(dir, entry)
    const stat = statSync(full)
    if (stat.isDirectory()) {
      walk(full, out)
    } else if (SCAN_EXTENSIONS.test(entry)) {
      out.push(full)
    }
  }
  return out
}

function lineOf(source, index) {
  return source.slice(0, index).split('\n').length
}

function main() {
  const files = walk(SRC_DIR).filter((f) => !EXEMPT_FILES.has(f))
  let violations = 0

  for (const file of files) {
    const source = readFileSync(file, 'utf8')
    const relative = path.relative(path.resolve(__dirname, '..'), file)

    for (const re of [HEX_COLOR_RE, COLOR_FN_RE, ARBITRARY_TAILWIND_COLOR_RE]) {
      re.lastIndex = 0
      let match
      while ((match = re.exec(source)) !== null) {
        violations++
        console.error(`X R12  ${relative}:${lineOf(source, match.index)}  colore letterale: "${match[0]}"`)
      }
    }

    LITERAL_VAR_RE.lastIndex = 0
    let match
    while ((match = LITERAL_VAR_RE.exec(source)) !== null) {
      violations++
      console.error(`X R13  ${relative}:${lineOf(source, match.index)}  token letterale fuori da theme.css: "${match[0]}"`)
    }
  }

  console.log('')
  console.log(`File esaminati: ${files.length} (esclusi: ${[...EXEMPT_FILES].map((f) => path.relative(path.resolve(__dirname, '..'), f)).join(', ')})`)

  if (violations > 0) {
    console.error(`\nlint-no-literal-colors: FALLITO (${violations} violazioni R12/R13).`)
    process.exit(1)
  }

  console.log('\nlint-no-literal-colors: PASSATO (nessun colore letterale o token di livello 1 fuori da theme.css).')
}

main()
