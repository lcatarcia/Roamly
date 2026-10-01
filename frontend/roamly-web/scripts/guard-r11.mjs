#!/usr/bin/env node
// scripts/guard-r11.mjs
//
// Verifica i "cinque correttivi anti-look" (R11, ADR-0007) restino in vigore
// nel tempo, non solo al momento dello scaffold. Controlla:
//   1. la palette forest/sand (non neutral/zinc di default shadcn) e'
//      ancora quella definita in theme.css (spot-check su alcuni letterali);
//   2. la scala di radius e' differenziata: almeno 4 valori --radius-*
//      distinti, e nessuno e' il default shadcn "0.5rem"/"0.625rem";
//   3. components.json non e' tornato al preset di colori/radius di default
//      (nessun "cssVariables": false con oklch, nessun --radius uniforme);
//   4. nessuna dipendenza da font Geist (il default shadcn/Tailwind v4),
//      R19 self-hosted.

import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const ROOT = path.resolve(__dirname, '..')
const THEME_CSS = readFileSync(path.join(ROOT, 'src/styles/theme.css'), 'utf8')
const COMPONENTS_JSON = readFileSync(path.join(ROOT, 'components.json'), 'utf8')
const PACKAGE_JSON = JSON.parse(readFileSync(path.join(ROOT, 'package.json'), 'utf8'))

let failures = 0

function check(label, condition, detail) {
  if (condition) {
    console.log(`  OK   ${label}`)
  } else {
    failures++
    console.error(`X FALLITA ${label}${detail ? ` — ${detail}` : ''}`)
  }
}

// (1) palette forest/sand presente (spot-check).
check(
  'Palette forest/sand presente in theme.css',
  /--color-sand-100:\s*#F3EBDD/i.test(THEME_CSS) && /--color-forest-950:\s*#10231C/i.test(THEME_CSS),
  'atteso --color-sand-100:#F3EBDD e --color-forest-950:#10231C',
)

// (2) radius differenziato: almeno 4 valori --radius-* distinti, nessun default shadcn.
const radiusMatches = [...THEME_CSS.matchAll(/--radius-[a-z]+:\s*([^;]+);/g)].map((m) => m[1].trim())
const distinctRadii = new Set(radiusMatches)
check(
  `Radius differenziato (>= 4 valori distinti, trovati ${distinctRadii.size})`,
  distinctRadii.size >= 4,
  [...distinctRadii].join(', '),
)
check(
  'Nessun radius uniforme "default shadcn" (0.5rem / 0.625rem)',
  ![...distinctRadii].some((v) => v.includes('0.5rem') || v.includes('0.625rem')),
  [...distinctRadii].join(', '),
)

// (3) components.json non contiene il preset oklch neutral di default.
check(
  'components.json non contiene un preset oklch (dovrebbe risolvere via theme.css)',
  !/oklch/i.test(COMPONENTS_JSON),
)
check(
  '"cssVariables" e\' attivo in components.json (token, non classi statiche)',
  /"cssVariables"\s*:\s*true/.test(COMPONENTS_JSON),
)

// (4) nessuna dipendenza sul font Geist (default shadcn/Tailwind v4), R19 self-hosted.
const allDeps = { ...PACKAGE_JSON.dependencies, ...PACKAGE_JSON.devDependencies }
check(
  'Nessuna dipendenza da font Geist (@fontsource*/geist) — R19 self-hosted',
  !Object.keys(allDeps).some((name) => /geist/i.test(name)),
  Object.keys(allDeps).filter((n) => /geist/i.test(n)).join(', '),
)

console.log('')
if (failures > 0) {
  console.error(`guard-r11: FALLITO (${failures} controlli falliti).`)
  process.exit(1)
}
console.log('guard-r11: PASSATO (i cinque correttivi anti-look sono ancora in vigore).')
