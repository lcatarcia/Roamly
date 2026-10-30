import assert from 'node:assert/strict'
import test from 'node:test'

import { collectUsedColorPairs, validateManifestCounts } from '../lib/contrast-coverage.mjs'

const colorNames = new Set([
  'background',
  'foreground',
  'card',
  'card-foreground',
  'primary',
  'primary-foreground',
  'muted',
  'muted-foreground',
  'destructive',
])

function pairNames(pairs) {
  return pairs.map(({ background, foreground }) => `${background}|${foreground}`)
}

test('collects the foreground and surface classes applied to the same element', () => {
  const pairs = collectUsedColorPairs(
    '<button className="bg-primary text-primary-foreground">Continua</button>',
    'fixture.tsx',
    colorNames,
  )

  assert.ok(pairNames(pairs).includes('--color-primary|--color-primary-foreground'))
})

test('carries a parent surface to nested text, without crossing into siblings', () => {
  const nestedPairs = collectUsedColorPairs(
    '<div className="bg-card"><p className="text-muted-foreground">Testo</p></div>',
    'fixture.tsx',
    colorNames,
  )
  const siblingPairs = collectUsedColorPairs(
    '<><div className="bg-card" /><p className="text-muted-foreground">Testo</p></>',
    'fixture.tsx',
    colorNames,
  )

  assert.ok(pairNames(nestedPairs).includes('--color-card|--color-muted-foreground'))
  assert.ok(!pairNames(siblingPairs).includes('--color-card|--color-muted-foreground'))
})

test('combines static cva base and variant classes', () => {
  const pairs = collectUsedColorPairs(
    'const variants = cva("bg-primary", { variants: { color: { primary: "text-primary-foreground" } } })',
    'fixture.ts',
    colorNames,
  )

  assert.ok(pairNames(pairs).includes('--color-primary|--color-primary-foreground'))
})

test('does not combine mutually exclusive values of the same cva variant', () => {
  const pairs = collectUsedColorPairs(
    'const variants = cva("", { variants: { appearance: { filled: "bg-primary", quiet: "text-primary-foreground" } } })',
    'fixture.ts',
    colorNames,
  )

  assert.ok(!pairNames(pairs).includes('--color-primary|--color-primary-foreground'))
})

test('enumerates combinations across independent cva variants', () => {
  const pairs = collectUsedColorPairs(
    'const variants = cva("", { variants: { appearance: { filled: "bg-primary", quiet: "" }, tone: { strong: "text-primary-foreground", soft: "" } } })',
    'fixture.ts',
    colorNames,
  )

  assert.ok(pairNames(pairs).includes('--color-primary|--color-primary-foreground'))
})

test('includes matching cva compound variant classes', () => {
  const pairs = collectUsedColorPairs(
    'const variants = cva("bg-primary", { variants: { appearance: { filled: "", quiet: "" } }, compoundVariants: [{ appearance: "filled", class: "text-primary-foreground" }] })',
    'fixture.ts',
    colorNames,
  )

  assert.ok(pairNames(pairs).includes('--color-primary|--color-primary-foreground'))
})

test('matches boolean cva compound variant conditions', () => {
  const pairs = collectUsedColorPairs(
    'const variants = cva("bg-primary", { variants: { enabled: { true: "", false: "" } }, compoundVariants: [{ enabled: true, class: "text-primary-foreground" }] })',
    'fixture.ts',
    colorNames,
  )

  assert.ok(pairNames(pairs).includes('--color-primary|--color-primary-foreground'))
})

test('matches boolean values inside cva compound variant arrays', () => {
  const pairs = collectUsedColorPairs(
    'const variants = cva("bg-primary", { variants: { enabled: { true: "", false: "" } }, compoundVariants: [{ enabled: [true], class: "text-primary-foreground" }] })',
    'fixture.ts',
    colorNames,
  )

  assert.ok(pairNames(pairs).includes('--color-primary|--color-primary-foreground'))
})

test('does not apply a cva compound class when its condition is false', () => {
  const pairs = collectUsedColorPairs(
    'const variants = cva("", { variants: { appearance: { filled: "bg-primary", quiet: "" } }, compoundVariants: [{ appearance: "quiet", class: "text-primary-foreground" }] })',
    'fixture.ts',
    colorNames,
  )

  assert.ok(!pairNames(pairs).includes('--color-primary|--color-primary-foreground'))
})

test('fails explicitly for dynamic cva compound conditions', () => {
  assert.throws(
    () => collectUsedColorPairs(
      'const variants = cva("bg-primary", { variants: { enabled: { true: "", false: "" } }, compoundVariants: [{ enabled: isEnabled, class: "text-primary-foreground" }] })',
      'fixture.ts',
      colorNames,
    ),
    /Unsupported non-static CVA compound condition/,
  )
})

test('fails explicitly for shorthand, spread, and computed cva compound properties', () => {
  const sources = [
    'const variants = cva("bg-primary", { variants: { enabled: { true: "", false: "" } }, compoundVariants: [{ enabled, class: "text-primary-foreground" }] })',
    'const variants = cva("bg-primary", { variants: { enabled: { true: "", false: "" } }, compoundVariants: [{ ...shared, class: "text-primary-foreground" }] })',
    'const variants = cva("bg-primary", { variants: { enabled: { true: "", false: "" } }, compoundVariants: [{ [condition]: true, class: "text-primary-foreground" }] })',
  ]

  for (const source of sources) {
    assert.throws(
      () => collectUsedColorPairs(source, 'fixture.ts', colorNames),
      /Unsupported non-static CVA/,
    )
  }
})

test('treats translucent surfaces as overlays on the inherited surface', () => {
  const pairs = collectUsedColorPairs(
    '<button className="bg-destructive/10 text-destructive">Elimina</button>',
    'fixture.tsx',
    colorNames,
  )

  assert.ok(pairNames(pairs).includes('--color-background|--color-destructive'))
  assert.ok(!pairNames(pairs).includes('--color-destructive|--color-destructive'))
})

test('extracts utility combinations from CSS @apply rules', () => {
  const pairs = collectUsedColorPairs(
    '.notice { @apply bg-primary text-primary-foreground; }',
    'fixture.css',
    colorNames,
  )

  assert.ok(pairNames(pairs).includes('--color-primary|--color-primary-foreground'))
})

test('requires every declared manifest count to match its contents', () => {
  const manifest = {
    totale: 2,
    chiare: 1,
    scure: 1,
    conSoglia: 1,
    conEsitoNegativoDichiarato: 1,
    coppie: [
      { tema: 'chiaro', soglia: 4.5 },
      { tema: 'scuro', declaredNegative: true },
    ],
  }

  assert.deepEqual(validateManifestCounts(manifest), [])
  assert.match(validateManifestCounts({ ...manifest, totale: 3 })[0], /totale/)
  assert.match(validateManifestCounts({ ...manifest, conSoglia: 2 })[0], /conSoglia/)
})
