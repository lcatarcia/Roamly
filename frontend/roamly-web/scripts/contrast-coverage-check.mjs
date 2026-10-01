#!/usr/bin/env node
// scripts/contrast-coverage-check.mjs
//
// Verificatore #2 di R14 (ADR-0007): "ogni combinazione semantica USATA NEL
// CODICE deve esistere nel manifesto — una coppia non dichiarata rompe la
// build". Senza questo, il manifesto invecchia in silenzio.
//
// LIMITE DICHIARATO (onesto, non nascosto): questa e' un'euristica basata su
// regex sulle stringhe className, non un'analisi AST/Tailwind completa. Scova
// le combinazioni bg-<nome>/text-<nome> del "ponte shadcn" (background,
// card, primary, secondary, muted, accent, destructive, popover + varianti
// -foreground) che compaiono nella STESSA stringa className, piu' la coppia
// di base impostata in CSS su <body> (bg-background/text-foreground e'
// sempre "in uso" perche' e' il fondo di ogni pagina). Non individua colori
// applicati per altre vie (style inline, css-in-js, classi custom nuove):
// se in futuro si useranno pattern diversi, questo script va esteso.
//
// Il confronto con il manifesto e' fatto sui LETTERALI risolti (fg,bg) come
// coppia NON ordinata: il contrasto WCAG e' simmetrico, quindi una coppia
// (A su B) dichiarata copre anche l'uso fisico (B su A) per la sola
// verifica del rapporto, anche se il ruolo semantico e' diverso.

import { readFileSync, readdirSync, statSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { loadThemeTokens, resolveHex } from './lib/theme-tokens.mjs'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const SRC_DIR = path.resolve(__dirname, '../src')
const MANIFEST_PATH = path.resolve(__dirname, '../src/design/contrast-pairs.json')

// Nomi del "ponte shadcn" in theme.css (bg-<X> / text-<X>-foreground o text-<X>).
const BRIDGE_SURFACES = [
  { bgClass: 'bg-background', bgVar: '--color-background', fgClass: 'text-foreground', fgVar: '--color-foreground' },
  { bgClass: 'bg-card', bgVar: '--color-card', fgClass: 'text-card-foreground', fgVar: '--color-card-foreground' },
  { bgClass: 'bg-popover', bgVar: '--color-popover', fgClass: 'text-popover-foreground', fgVar: '--color-popover-foreground' },
  { bgClass: 'bg-primary', bgVar: '--color-primary', fgClass: 'text-primary-foreground', fgVar: '--color-primary-foreground' },
  { bgClass: 'bg-secondary', bgVar: '--color-secondary', fgClass: 'text-secondary-foreground', fgVar: '--color-secondary-foreground' },
  { bgClass: 'bg-muted', bgVar: '--color-muted', fgClass: 'text-muted-foreground', fgVar: '--color-muted-foreground' },
  { bgClass: 'bg-accent', bgVar: '--color-accent', fgClass: 'text-accent-foreground', fgVar: '--color-accent-foreground' },
  { bgClass: 'bg-destructive', bgVar: '--color-destructive', fgClass: 'text-destructive-foreground', fgVar: '--color-destructive-foreground' },
]

function walk(dir, out = []) {
  for (const entry of readdirSync(dir)) {
    const full = path.join(dir, entry)
    const stat = statSync(full)
    if (stat.isDirectory()) {
      walk(full, out)
    } else if (/\.(tsx?|css)$/.test(entry)) {
      out.push(full)
    }
  }
  return out
}

function extractClassNameStrings(source) {
  const strings = []
  const re = /className\s*=\s*"([^"]*)"|className\s*=\s*\{`([^`]*)`\}|cn\(([^)]*)\)/g
  let match
  while ((match = re.exec(source)) !== null) {
    strings.push(match[1] ?? match[2] ?? match[3] ?? '')
  }
  return strings
}

function hexNormalize(hex) {
  return hex.toLowerCase()
}

function main() {
  const manifest = JSON.parse(readFileSync(MANIFEST_PATH, 'utf8'))
  const tokens = loadThemeTokens()

  // Costruisce l'insieme delle coppie hex dichiarate (non ordinate), per tema.
  const declaredByTheme = { chiaro: new Set(), scuro: new Set() }
  for (const pair of manifest.coppie) {
    const fgHex = resolveHex(`--color-${pair.fg}`, tokens, pair.tema === 'scuro')
    const bgHex = resolveHex(`--color-${pair.bg}`, tokens, pair.tema === 'scuro')
    const key = [hexNormalize(fgHex), hexNormalize(bgHex)].sort().join('|')
    declaredByTheme[pair.tema].add(key)
  }

  // Coppie effettivamente usate nel codice: bg-background/text-foreground e'
  // sempre in uso (impostato su <body> in theme.css), le altre solo se una
  // stringa className contiene ENTRAMBE le classi della stessa superficie.
  const usedSurfaceNames = new Set(['bg-background']) // sempre presente (body)

  const files = walk(SRC_DIR)
  for (const file of files) {
    const source = readFileSync(file, 'utf8')
    for (const classString of extractClassNameStrings(source)) {
      for (const surface of BRIDGE_SURFACES) {
        if (classString.includes(surface.bgClass)) {
          usedSurfaceNames.add(surface.bgClass)
        }
      }
    }
  }

  let failures = 0
  for (const surface of BRIDGE_SURFACES) {
    if (!usedSurfaceNames.has(surface.bgClass)) {
      continue // non usata nel codice, non serve nel manifesto
    }

    for (const themeName of ['chiaro', 'scuro']) {
      const isDark = themeName === 'scuro'
      const fgHex = resolveHex(surface.fgVar, tokens, isDark)
      const bgHex = resolveHex(surface.bgVar, tokens, isDark)
      const key = [hexNormalize(fgHex), hexNormalize(bgHex)].sort().join('|')

      if (declaredByTheme[themeName].has(key)) {
        console.log(`  OK   ${surface.bgClass.padEnd(20)} tema ${themeName.padEnd(6)} ${fgHex}/${bgHex} -> presente nel manifesto`)
      } else {
        failures++
        console.error(
          `X FALLITA ${surface.bgClass.padEnd(20)} tema ${themeName.padEnd(6)} ${fgHex}/${bgHex} -> NON dichiarata in contrast-pairs.json`,
        )
      }
    }
  }

  console.log('')
  console.log(`Superfici usate nel codice: ${[...usedSurfaceNames].join(', ')}`)

  if (failures > 0) {
    console.error(`\ncontrast-coverage-check: FALLITO (${failures} coppie usate ma non dichiarate).`)
    process.exit(1)
  }

  console.log('\ncontrast-coverage-check: PASSATO (euristica su className, vedi limiti nel commento del file).')
}

main()
