import ts from 'typescript'

const displayFontClasses = new Set(['numeral-display', 'page-title'])

function getComponentName(statement) {
  if (ts.isFunctionDeclaration(statement) && statement.name && /Page$/.test(statement.name.text)) {
    return statement.name.text
  }

  if (!ts.isVariableStatement(statement)) return null

  for (const declaration of statement.declarationList.declarations) {
    if (
      ts.isIdentifier(declaration.name)
      && /Page$/.test(declaration.name.text)
      && declaration.initializer
      && (ts.isArrowFunction(declaration.initializer) || ts.isFunctionExpression(declaration.initializer))
    ) {
      return declaration.name.text
    }
  }

  return null
}

function hasComponentBody(statement) {
  return (ts.isFunctionDeclaration(statement) && statement.body)
    || (ts.isVariableStatement(statement)
      && statement.declarationList.declarations.some((declaration) =>
        declaration.initializer
        && (ts.isArrowFunction(declaration.initializer) || ts.isFunctionExpression(declaration.initializer)),
      ))
}

function countDisplayClasses(node) {
  const usage = { pageTitles: 0, total: 0 }

  function visit(current) {
    if (ts.isJsxAttribute(current) && current.name.getText() === 'className') {
      const initializer = current.initializer
      const classNames = initializer && ts.isStringLiteralLike(initializer)
        ? initializer.text
        : null

      for (const className of classNames?.split(/\s+/) ?? []) {
        if (className === 'page-title') usage.pageTitles += 1
        if (displayFontClasses.has(className)) usage.total += 1
      }
    }

    ts.forEachChild(current, visit)
  }

  visit(node)
  return usage
}

export function collectDisplayFontUsageByComponent(fileName, source) {
  const scriptKind = fileName.endsWith('.tsx') ? ts.ScriptKind.TSX : ts.ScriptKind.JSX
  const sourceFile = ts.createSourceFile(fileName, source, ts.ScriptTarget.Latest, true, scriptKind)
  const usages = []

  for (const statement of sourceFile.statements) {
    const name = getComponentName(statement)
    if (!name || !hasComponentBody(statement)) continue

    const body = ts.isFunctionDeclaration(statement)
      ? statement.body
      : statement.declarationList.declarations.find((declaration) =>
        declaration.initializer
        && (ts.isArrowFunction(declaration.initializer) || ts.isFunctionExpression(declaration.initializer)),
      )?.initializer

    if (body) usages.push({ name, ...countDisplayClasses(body) })
  }

  return usages
}
