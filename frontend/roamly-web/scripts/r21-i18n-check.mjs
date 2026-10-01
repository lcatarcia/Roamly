#!/usr/bin/env node
// Verificatore R21 (ADR-0007), deliberatamente basato su regex e senza nuovo
// tooling AST: controlla catalogo, riferimenti t('namespace.key') e JSX grezzo
// nelle superfici Phase 0 nominate qui sotto.

import { readFileSync, readdirSync, statSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const src = path.join(root, 'src')
const catalogPath = path.join(src, 'i18n/catalog.ts')

const jsxSurfaces = [
  'components/layout/AppShell.tsx',
  'components/layout/ThemeToggle.tsx',
  'components/ui/toast.tsx',
  'features/auth/LoginPage.tsx',
  'features/design-system/prototype/Ribbon.tsx',
  'features/design-system/prototype/RibbonPrototypePage.tsx',
]

const neutralOnlySurfaces = [
  'components/layout/AppShell.tsx',
  'components/layout/ThemeToggle.tsx',
  'components/ui/toast.tsx',
  'features/auth/LoginPage.tsx',
]

function lineOf(source, index) {
  return source.slice(0, index).split('\n').length
}

function walk(directory, files = []) {
  for (const entry of readdirSync(directory)) {
    const fullPath = path.join(directory, entry)
    if (statSync(fullPath).isDirectory()) {
      walk(fullPath, files)
    } else if (/\.(tsx?|jsx?)$/.test(entry)) {
      files.push(fullPath)
    }
  }
  return files
}

function main() {
  let violations = 0
  const catalogSource = readFileSync(catalogPath, 'utf8')
  const catalogEntries = new Map()
  const entryPattern = /^\s*'([^']+)'\s*:\s*"([^"]*)"/gm
  let entryMatch

  while ((entryMatch = entryPattern.exec(catalogSource)) !== null) {
    const [, key, value] = entryMatch
    if (!/^(?:neutral|narrative)\.[\w.-]+$/.test(key)) {
      violations++
      console.error(`X R21 ${path.relative(root, catalogPath)}:${lineOf(catalogSource, entryMatch.index)} namespace non valido: ${key} (attesi neutral.* o narrative.*)`)
      continue
    }
    if (catalogEntries.has(key)) {
      violations++
      console.error(`X R21 ${path.relative(root, catalogPath)}:${lineOf(catalogSource, entryMatch.index)} chiave duplicata: ${key}`)
    } else {
      catalogEntries.set(key, value)
    }
    if (value.trim().length === 0) {
      violations++
      console.error(`X R21 ${path.relative(root, catalogPath)}:${lineOf(catalogSource, entryMatch.index)} traduzione vuota: ${key}`)
    }
  }

  for (const namespace of ['neutral', 'narrative']) {
    if (![...catalogEntries.keys()].some((key) => key.startsWith(`${namespace}.`))) {
      violations++
      console.error(`X R21 catalogo italiano: manca il namespace ${namespace}.*`)
    }
  }

  // La forma esplicita chiave: valore permette di verificare i namespace e
  // impedisce chiavi non registrate senza interpretare TypeScript.
  const allSourceFiles = walk(src)
  const usedKeys = new Set()
  const referencePattern = /\bt\(\s*(['"])((?:neutral|narrative)\.[\w.-]+)\1\s*\)/g
  for (const file of allSourceFiles) {
    const source = readFileSync(file, 'utf8')
    let referenceMatch
    while ((referenceMatch = referencePattern.exec(source)) !== null) {
      const key = referenceMatch[2]
      usedKeys.add(key)
      if (!catalogEntries.has(key)) {
        violations++
        console.error(`X R21 ${path.relative(root, file)}:${lineOf(source, referenceMatch.index)} chiave non presente nel catalogo: ${key}`)
      }
    }
  }

  for (const key of catalogEntries.keys()) {
    if (!usedKeys.has(key)) {
      violations++
      console.error(`X R21 ${path.relative(root, catalogPath)} chiave non usata: ${key}`)
    }
  }

  for (const relativePath of neutralOnlySurfaces) {
    const file = path.join(src, relativePath)
    const source = readFileSync(file, 'utf8')
    const narrativeReference = /\bt\(\s*(['"])narrative\.[\w.-]+\1\s*\)/g
    let match
    while ((match = narrativeReference.exec(source)) !== null) {
      violations++
      console.error(`X R21 ${relativePath}:${lineOf(source, match.index)} questa superficie funzionale puo' usare solo chiavi neutral.*`)
    }
  }

  for (const relativePath of jsxSurfaces) {
    const file = path.join(src, relativePath)
    const source = readFileSync(file, 'utf8')

    // Testo JSX diretto: scarta espressioni, frammenti vuoti e righe che
    // contengono sintassi di codice (limite dichiarato della verifica regex).
    const textPattern = />([^<]+)</g
    let textMatch
    while ((textMatch = textPattern.exec(source)) !== null) {
      const text = textMatch[1].trim()
      if (
        text.length > 0 &&
        /[\p{L}\p{N}]/u.test(text) &&
        !text.startsWith('{') &&
        !text.includes('{') &&
        !text.includes('}') &&
        !/[=();]/.test(text)
      ) {
        violations++
        console.error(`X R21 ${relativePath}:${lineOf(source, textMatch.index)} testo JSX inline: "${text}"`)
      }
    }

    const expressionTextPattern = />\s*\{\s*(['"])([^'"]+)\1\s*\}\s*</g
    let expressionTextMatch
    while ((expressionTextMatch = expressionTextPattern.exec(source)) !== null) {
      violations++
      console.error(`X R21 ${relativePath}:${lineOf(source, expressionTextMatch.index)} stringa JSX inline: "${expressionTextMatch[2]}"`)
    }

    // Gli attributi testuali accessibili sono una superficie di testo tanto
    // quanto i nodi JSX; gli attributi tecnici HTML/SVG restano fuori scope.
    const attributePattern = /\b(?:aria-label|aria-description|placeholder|title|alt)\s*=\s*(["'])(.*?)\1/g
    let attributeMatch
    while ((attributeMatch = attributePattern.exec(source)) !== null) {
      violations++
      console.error(`X R21 ${relativePath}:${lineOf(source, attributeMatch.index)} attributo testuale inline: "${attributeMatch[2]}"`)
    }

    const expressionAttributePattern =
      /\b(?:aria-label|aria-description|placeholder|title|alt)\s*=\s*\{\s*(['"])(.*?)\1\s*\}/g
    let expressionAttributeMatch
    while ((expressionAttributeMatch = expressionAttributePattern.exec(source)) !== null) {
      violations++
      console.error(`X R21 ${relativePath}:${lineOf(source, expressionAttributeMatch.index)} attributo testuale inline: "${expressionAttributeMatch[2]}"`)
    }
  }

  if (violations > 0) {
    console.error(`\nr21-i18n-check: FALLITO (${violations} violazioni). Usa t('neutral.*') o t('narrative.*') nelle superfici previste.`)
    process.exit(1)
  }

  console.log(`Catalogo: ${catalogEntries.size} chiavi, namespaces neutral.* e narrative.* presenti.`)
  console.log(`Uso: ${usedKeys.size} chiavi tutte registrate e utilizzate; ${jsxSurfaces.length} superfici JSX senza testo letterale rilevato.`)
  console.log('r21-i18n-check: PASSATO (regex mirate; nessuna analisi AST).')
}

main()
