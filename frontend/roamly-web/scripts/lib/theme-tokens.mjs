// scripts/lib/theme-tokens.mjs
//
// Legge i valori REALI da src/styles/theme.css (mai valori copiati a mano):
// e' il prerequisito di R14 ("lo script legge i valori reali da theme.css").
//
// Il file ha due blocchi rilevanti:
//   - `@theme { ... }`            -> letterali (livello 1) + semantici di default (tema chiaro)
//   - `[data-theme='dark'] { ... }` -> override dei soli token semantici (livello 2)
//
// Risolviamo qualunque nome di variabile (con o senza "--") al suo valore
// esadecimale finale, seguendo le catene `var(--altro-nome)` ricorsivamente,
// con il blocco dark che ha priorita' sul blocco chiaro quando si chiede il
// valore "in dark".

import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
export const THEME_CSS_PATH = path.resolve(__dirname, '../../src/styles/theme.css')

/** Estrae il contenuto tra la prima "{" dopo `selector` e la sua "}" corrispondente. */
function extractBlock(css, selector) {
  const startIdx = css.indexOf(selector)
  if (startIdx === -1) {
    throw new Error(`Blocco "${selector}" non trovato in ${THEME_CSS_PATH}`)
  }
  const braceStart = css.indexOf('{', startIdx)
  let depth = 0
  for (let i = braceStart; i < css.length; i++) {
    if (css[i] === '{') depth++
    if (css[i] === '}') {
      depth--
      if (depth === 0) {
        return css.slice(braceStart + 1, i)
      }
    }
  }
  throw new Error(`Blocco "${selector}" non chiuso correttamente in ${THEME_CSS_PATH}`)
}

/** Estrae le dichiarazioni `--nome: valore;` di un blocco in una Map (nome CON "--"). */
function parseDeclarations(blockCss) {
  const map = new Map()
  const re = /(--[a-zA-Z0-9-]+)\s*:\s*([^;]+);/g
  let match
  while ((match = re.exec(blockCss)) !== null) {
    map.set(match[1], match[2].trim())
  }
  return map
}

function normalizeName(name) {
  return name.startsWith('--') ? name : `--${name}`
}

/** Costruisce l'indice { rootMap, darkMap } leggendo theme.css una sola volta. */
export function loadThemeTokens(cssPath = THEME_CSS_PATH) {
  const css = readFileSync(cssPath, 'utf8')
  const rootMap = parseDeclarations(extractBlock(css, '@theme'))
  const darkMap = parseDeclarations(extractBlock(css, "[data-theme='dark']"))
  return { rootMap, darkMap }
}

const VAR_REF_RE = /^var\((--[a-zA-Z0-9-]+)\)$/
const HEX_RE = /^#([0-9A-Fa-f]{6})$/

/**
 * Risolve un nome di variabile al suo valore esadecimale finale.
 * @param {string} name nome della variabile, con o senza "--"
 * @param {{rootMap: Map, darkMap: Map}} tokens indice caricato da loadThemeTokens
 * @param {boolean} dark se true, il blocco dark ha priorita' sul root
 */
export function resolveHex(name, tokens, dark = false) {
  const seen = new Set()
  let current = normalizeName(name)

  for (let hop = 0; hop < 20; hop++) {
    if (seen.has(current)) {
      throw new Error(`Riferimento circolare risolvendo ${name}: ${[...seen, current].join(' -> ')}`)
    }
    seen.add(current)

    const value = (dark && tokens.darkMap.has(current) ? tokens.darkMap.get(current) : tokens.rootMap.get(current))

    if (value === undefined) {
      throw new Error(`Token "${current}" non definito in theme.css (risolvendo "${name}")`)
    }

    const hexMatch = HEX_RE.exec(value)
    if (hexMatch) {
      return `#${hexMatch[1].toLowerCase()}`
    }

    const varMatch = VAR_REF_RE.exec(value)
    if (varMatch) {
      current = varMatch[1]
      continue
    }

    throw new Error(`Valore "${value}" di "${current}" non e' ne' un hex ne' un var(--x) singolo: non risolvibile a colore`)
  }

  throw new Error(`Troppi hop risolvendo ${name} (possibile ciclo non rilevato)`)
}

/** Risolve un nome di token LETTERALE (livello 1, es. "sand-100") al suo hex. Identico in entrambi i temi. */
export function resolveLiteralHex(literalName, tokens) {
  return resolveHex(`--color-${literalName}`, tokens, false)
}
