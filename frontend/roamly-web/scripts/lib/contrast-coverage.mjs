import ts from 'typescript'

export function validateManifestCounts(manifest) {
  const pairs = manifest.coppie
  const expected = {
    totale: pairs.length,
    chiare: pairs.filter((pair) => pair.tema === 'chiaro').length,
    scure: pairs.filter((pair) => pair.tema === 'scuro').length,
    conSoglia: pairs.filter((pair) => !pair.declaredNegative).length,
    conEsitoNegativoDichiarato: pairs.filter((pair) => pair.declaredNegative).length,
  }
  const errors = []

  for (const [name, actual] of Object.entries(expected)) {
    if (manifest[name] !== actual) {
      errors.push(`manifest.${name}=${manifest[name]} ma il contenuto richiede ${actual}`)
    }
  }

  return errors
}

function getStaticStrings(node) {
  const strings = []

  function visit(current) {
    if (ts.isStringLiteralLike(current)) {
      strings.push(current.text)
      return
    }

    if (ts.isTemplateExpression(current)) {
      strings.push(current.head.text, ...current.templateSpans.map((span) => span.literal.text))
      return
    }

    ts.forEachChild(current, visit)
  }

  visit(node)
  return strings
}

const UNKNOWN_VALUE = Symbol('unknown')

function getPropertyName(name) {
  if (ts.isIdentifier(name) || ts.isStringLiteralLike(name) || ts.isNumericLiteral(name)) {
    return name.text
  }
  if (name.kind === ts.SyntaxKind.TrueKeyword) return 'true'
  if (name.kind === ts.SyntaxKind.FalseKeyword) return 'false'
  if (name.kind === ts.SyntaxKind.NullKeyword) return 'null'

  return null
}

function unsupportedCvaSyntax(context, fileName) {
  throw new Error(`Unsupported non-static CVA ${context} in ${fileName}; use literal object and class values`)
}

function getStaticClassStrings(node, context, fileName) {
  if (ts.isStringLiteralLike(node)) return [node.text]
  if (ts.isArrayLiteralExpression(node)) {
    return node.elements.flatMap((element) => getStaticClassStrings(element, context, fileName))
  }
  if (
    node.kind === ts.SyntaxKind.NullKeyword ||
    node.kind === ts.SyntaxKind.TrueKeyword ||
    node.kind === ts.SyntaxKind.FalseKeyword
  ) {
    return []
  }

  unsupportedCvaSyntax(context, fileName)
}

function readVariantKey(property) {
  if (ts.isIdentifier(property.nameNode) && property.nameNode.text === 'true') return true
  if (ts.isIdentifier(property.nameNode) && property.nameNode.text === 'false') return false
  if (property.nameNode.kind === ts.SyntaxKind.TrueKeyword) return true
  if (property.nameNode.kind === ts.SyntaxKind.FalseKeyword) return false
  if (property.nameNode.kind === ts.SyntaxKind.NullKeyword) return null
  if (ts.isNumericLiteral(property.nameNode)) return Number(property.nameNode.text)

  return property.name
}

function readStaticValue(node) {
  if (ts.isStringLiteralLike(node)) return node.text
  if (ts.isNumericLiteral(node)) return Number(node.text)
  if (node.kind === ts.SyntaxKind.TrueKeyword) return true
  if (node.kind === ts.SyntaxKind.FalseKeyword) return false
  if (node.kind === ts.SyntaxKind.NullKeyword) return null
  if (ts.isArrayLiteralExpression(node)) {
    const values = node.elements.map(readStaticValue)
    return values.some((value) => value === UNKNOWN_VALUE) ? UNKNOWN_VALUE : values
  }

  return UNKNOWN_VALUE
}

function getStaticObjectProperties(node, context, fileName) {
  if (!node || !ts.isObjectLiteralExpression(node)) {
    unsupportedCvaSyntax(context, fileName)
  }

  return node.properties.map((property) => {
    if (!ts.isPropertyAssignment(property)) unsupportedCvaSyntax(context, fileName)

    const name = getPropertyName(property.name)
    if (name === null) unsupportedCvaSyntax(context, fileName)

    return { name, nameNode: property.name, value: property.initializer }
  })
}

function parseColorClasses(classString, colorNames) {
  const tokens = []

  for (const className of classString.split(/\s+/).filter(Boolean)) {
    const parts = className.split(':')
    const utility = parts.pop()
    const match = /^(bg|text)-([a-z][a-z0-9-]*)(?:\/[a-z0-9.[\]-]+)?$/i.exec(utility)
    if (!match || !colorNames.has(match[2])) continue

    const opacity = utility.slice(match[0].indexOf('/'))
    tokens.push({
      kind: match[1],
      variable: `--color-${match[2]}`,
      variant: parts.join(':'),
      translucent: opacity.startsWith('/'),
    })
  }

  return tokens
}

export function collectUsedColorPairs(source, fileName, colorNames) {
  const pairs = new Map()
  let sourceFile

  function addPair(background, foreground, node) {
    const key = `${background}|${foreground}`
    if (pairs.has(key)) return
    pairs.set(key, {
      background,
      foreground,
      line: node ? sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile)).line + 1 : 1,
    })
  }

  function applyClassGroups(groups, inherited, node) {
    const tokens = groups.flatMap((group) => parseColorClasses(group, colorNames))
    const backgrounds = tokens.filter((token) => token.kind === 'bg' && !token.translucent)
    const foregrounds = tokens.filter((token) => token.kind === 'text')
    const normalBackgrounds = backgrounds.filter((token) => token.variant === '')
    const normalForegrounds = foregrounds.filter((token) => token.variant === '')
    const background = normalBackgrounds.at(-1)?.variable ?? inherited.background
    const foreground = normalForegrounds.at(-1)?.variable ?? inherited.foreground

    for (const bg of backgrounds) {
      for (const text of foregrounds) addPair(bg.variable, text.variable, node)
    }

    for (const text of foregrounds) {
      if (backgrounds.length > 0) {
        for (const bg of backgrounds) addPair(bg.variable, text.variable, node)
      } else {
        addPair(background, text.variable, node)
      }
    }

    return { background, foreground }
  }

  function classGroupsFromAttribute(attribute) {
    const initializer = attribute.initializer
    if (!initializer) return []
    if (ts.isStringLiteralLike(initializer)) return [initializer.text]
    if (ts.isJsxExpression(initializer) && initializer.expression) {
      const strings = getStaticStrings(initializer.expression)
      return strings.length > 0 ? [strings.join(' ')] : []
    }
    return []
  }

  function visitJsx(node, inherited) {
    if (ts.isJsxText(node)) {
      if (node.text.trim().length > 0) addPair(inherited.background, inherited.foreground, node)
      return
    }

    if (ts.isJsxExpression(node)) {
      if (node.expression) {
        addPair(inherited.background, inherited.foreground, node)
        visitJsx(node.expression, inherited)
      }
      return
    }

    if (ts.isJsxElement(node) || ts.isJsxSelfClosingElement(node)) {
      const attributes = node.openingElement?.attributes ?? node.attributes
      const classGroups = []

      for (const attribute of attributes.properties) {
        if (
          ts.isJsxAttribute(attribute) &&
          ts.isIdentifier(attribute.name) &&
          attribute.name.text === 'className'
        ) {
          classGroups.push(...classGroupsFromAttribute(attribute))
        }
      }

      const effective = applyClassGroups(classGroups, inherited, node)

      if (ts.isJsxElement(node)) {
        for (const child of node.children) visitJsx(child, effective)
      }
      return
    }

    if (ts.isJsxFragment(node)) {
      for (const child of node.children) visitJsx(child, inherited)
      return
    }

    ts.forEachChild(node, (child) => visitJsx(child, inherited))
  }

  function visitCva(node) {
    if (
      !ts.isCallExpression(node) ||
      !ts.isIdentifier(node.expression) ||
      node.expression.text !== 'cva'
    ) {
      ts.forEachChild(node, visitCva)
      return
    }

    const [base, config] = node.arguments
    const baseClasses = base ? getStaticClassStrings(base, 'base classes', fileName).join(' ') : ''
    const configProperties = config
      ? getStaticObjectProperties(config, 'configuration', fileName)
      : []
    const variants = configProperties.find(({ name }) => name === 'variants')
      ? getStaticObjectProperties(
        configProperties.find(({ name }) => name === 'variants').value,
        'variants',
        fileName,
      )
      : []
    const dimensions = variants.map(({ name, value }) => ({
      name,
      choices: getStaticObjectProperties(value, `values for variant "${name}"`, fileName).map((choice) => ({
        value: readVariantKey(choice),
        classes: getStaticClassStrings(choice.value, `classes for variant "${name}"`, fileName).join(' '),
      })),
    }))
    const compoundVariants = configProperties.find(({ name }) => name === 'compoundVariants')?.value
    if (compoundVariants && !ts.isArrayLiteralExpression(compoundVariants)) {
      unsupportedCvaSyntax('compound variants', fileName)
    }
    const compounds = compoundVariants
      ? compoundVariants.elements.map((compound) => {
        const properties = getStaticObjectProperties(compound, 'compound variant', fileName)
        const conditions = properties.filter(({ name }) => name !== 'class' && name !== 'className')
        const classNames = properties
          .filter(({ name }) => name === 'class' || name === 'className')
          .flatMap(({ value }) => getStaticClassStrings(value, 'compound variant classes', fileName))

        return {
          conditions: conditions.map(({ name, value }) => ({
            name,
            expected: readStaticValue(value),
          })),
          classNames,
        }
      })
      : []
    const variantNames = new Set(dimensions.map(({ name }) => name))

    for (const compound of compounds) {
      for (const { name, expected } of compound.conditions) {
        if (expected === UNKNOWN_VALUE || !variantNames.has(name)) {
          unsupportedCvaSyntax(`compound condition "${name}"`, fileName)
        }
      }
    }

    function addCombinationClasses(index, selectedValues, classGroups) {
      if (index === dimensions.length) {
        const matchingCompoundClasses = compounds
          .filter(({ conditions }) => conditions.every(({ name, expected }) => {
            const selected = selectedValues.get(name)
            return Array.isArray(expected) ? expected.includes(selected) : expected === selected
          }))
          .flatMap(({ classNames }) => classNames)

        applyClassGroups([baseClasses, ...classGroups, ...matchingCompoundClasses], initial, node)
        return
      }

      const dimension = dimensions[index]
      if (dimension.choices.length === 0) {
        addCombinationClasses(index + 1, selectedValues, classGroups)
        return
      }

      for (const choice of dimension.choices) {
        selectedValues.set(dimension.name, choice.value)
        addCombinationClasses(index + 1, selectedValues, [...classGroups, choice.classes])
      }
      selectedValues.delete(dimension.name)
    }

    addCombinationClasses(0, new Map(), [])
    ts.forEachChild(node, visitCva)
  }

  const extension = fileName.toLowerCase().split('.').at(-1)
  const initial = { background: '--color-background', foreground: '--color-foreground' }

  if (extension === 'css') {
    for (const match of source.matchAll(/@apply\s+([^;]+);/g)) {
      applyClassGroups([match[1]], initial, null)
    }
  } else {
    const scriptKind = extension === 'tsx' ? ts.ScriptKind.TSX : ts.ScriptKind.TS
    sourceFile = ts.createSourceFile(fileName, source, ts.ScriptTarget.Latest, true, scriptKind)
    visitJsx(sourceFile, initial)
    visitCva(sourceFile)
  }

  return [...pairs.values()]
}
