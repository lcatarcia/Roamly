import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const sourceRoot = path.join(root, 'src')
const publicFontsRoot = path.join(root, 'public', 'fonts')
const distRoot = path.join(root, 'dist')
const fontBudgetBytes = 120 * 1024
const allowedDisplaySelectors = new Set(['.numeral-display', '.page-title'])
const errors = []

function listFiles(directory, predicate = () => true) {
  if (!fs.existsSync(directory)) return []

  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const filePath = path.join(directory, entry.name)
    if (entry.isDirectory()) return listFiles(filePath, predicate)
    return entry.isFile() && predicate(filePath) ? [filePath] : []
  })
}

function relative(filePath) {
  return path.relative(root, filePath).split(path.sep).join('/')
}

function stripCssComments(css) {
  return css.replace(/\/\*[\s\S]*?\*\//g, '')
}

function extractBlocks(source, atRule) {
  const blocks = []
  const matcher = new RegExp(`${atRule}\\s*\\{`, 'gi')
  let match

  while ((match = matcher.exec(source)) !== null) {
    const openingBrace = source.indexOf('{', match.index)
    let depth = 1
    let cursor = openingBrace + 1

    while (cursor < source.length && depth > 0) {
      if (source[cursor] === '{') depth += 1
      if (source[cursor] === '}') depth -= 1
      cursor += 1
    }

    if (depth !== 0) {
      errors.push(`Unclosed ${atRule} block in CSS.`)
      break
    }

    blocks.push(source.slice(openingBrace + 1, cursor - 1))
    matcher.lastIndex = cursor
  }

  return blocks
}

function checkFontBudgetAndSources() {
  const fontFiles = listFiles(publicFontsRoot, (filePath) => filePath.toLowerCase().endsWith('.woff2'))
  const totalBytes = fontFiles.reduce((sum, filePath) => sum + fs.statSync(filePath).size, 0)

  if (totalBytes > fontBudgetBytes) {
    errors.push(
      `R18: public/fonts/**/*.woff2 totals ${totalBytes} bytes; budget is ${fontBudgetBytes} bytes (120 KiB).`,
    )
  }

  if (fontFiles.length === 0) errors.push('R18: no public/fonts/**/*.woff2 files were found.')

  const cssFiles = listFiles(sourceRoot, (filePath) => filePath.endsWith('.css'))
  for (const filePath of cssFiles) {
    const css = stripCssComments(fs.readFileSync(filePath, 'utf8'))

    for (const fontFace of extractBlocks(css, '@font-face')) {
      const source = fontFace.match(/(?:^|;)\s*src\s*:\s*([^;]+);?/i)?.[1]
      if (!source) {
        errors.push(`R18: @font-face has no src declaration in ${relative(filePath)}.`)
        continue
      }

      const urls = [...source.matchAll(/url\(\s*(['"]?)(.*?)\1\s*\)/gi)].map((match) => match[2].trim())
      if (urls.length === 0 || /\blocal\s*\(/i.test(source)) {
        errors.push(`R18: @font-face must use one or more self-hosted URL sources in ${relative(filePath)}.`)
        continue
      }

      for (const url of urls) {
        if (/^(?:[a-z][a-z\d+.-]*:|\/\/)/i.test(url)) {
          errors.push(`R18: external @font-face source "${url}" in ${relative(filePath)}.`)
        }
      }
    }
  }

  console.log(`R18: ${fontFiles.length} WOFF2 file(s), ${totalBytes}/${fontBudgetBytes} bytes; @font-face sources checked in ${cssFiles.length} source CSS file(s).`)
}

function findCssRules(css) {
  const rules = []
  const matcher = /([^{}]+)\{([^{}]*)\}/g
  let match

  while ((match = matcher.exec(css)) !== null) {
    rules.push({ selector: match[1].trim(), body: match[2] })
  }

  return rules
}

function checkDisplayFontScope() {
  const cssFiles = [
    ...listFiles(sourceRoot, (filePath) => filePath.endsWith('.css')),
    ...listFiles(path.join(distRoot, 'assets'), (filePath) => filePath.endsWith('.css')),
  ]

  if (!listFiles(path.join(distRoot, 'assets'), (filePath) => filePath.endsWith('.css')).length) {
    errors.push('R22: built CSS is missing; run npm run build before this guard.')
  }

  for (const filePath of cssFiles) {
    const css = stripCssComments(fs.readFileSync(filePath, 'utf8'))

    for (const { selector, body } of findCssRules(css)) {
      if (!/\bfont-family\s*:[^;]*(?:var\(\s*--font-display\s*\)|fraunces)/i.test(body)) {
        continue
      }

      if (/^@font-face\b/i.test(selector)) continue

      const selectors = selector.split(',').map((item) => item.trim())
      if (selectors.some((item) => !allowedDisplaySelectors.has(item))) {
        errors.push(
          `R22: display font application outside .numeral-display/.page-title in ${relative(filePath)}: ${selector}`,
        )
      }
    }
  }

  const pageFiles = listFiles(path.join(sourceRoot, 'features'), (filePath) => /Page\.(tsx|jsx)$/.test(filePath))
  for (const filePath of pageFiles) {
    const source = fs.readFileSync(filePath, 'utf8')
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/\{\/\*[\s\S]*?\*\/\}/g, '')
    const classAttributes = [...source.matchAll(/\bclassName\s*=\s*(["'`])([^"'`]*?)\1/gs)]
    let titleCount = 0
    let totalDisplayCount = 0

    for (const [, , classNames] of classAttributes) {
      const classes = classNames.split(/\s+/)
      titleCount += classes.filter((name) => name === 'page-title').length
      totalDisplayCount += classes.filter((name) => allowedDisplaySelectors.has(`.${name}`)).length
    }

    if (titleCount > 1) errors.push(`R22: ${relative(filePath)} has ${titleCount} static .page-title uses; maximum is one per screen.`)
    if (totalDisplayCount > 2) {
      errors.push(`R22: ${relative(filePath)} has ${totalDisplayCount} static display-font uses; maximum is two per screen.`)
    }
  }

  if (pageFiles.length === 0) errors.push('R22: no features/**/*Page.tsx screen components were found for the per-screen usage check.')
  console.log(`R22: display font selectors checked in ${cssFiles.length} source/build CSS file(s); static class usage checked in ${pageFiles.length} page component(s).`)
}

function removeComments(content, kind) {
  if (kind === 'css' || kind === 'html') {
    return content.replace(/\/\*[\s\S]*?\*\//g, '').replace(/<!--[\s\S]*?-->/g, '')
  }

  // Preserve quoted strings while removing JavaScript comments, so URL-looking
  // text in comments cannot become an asset finding.
  return content.replace(
    /("(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`)|(\/\*[\s\S]*?\*\/|\/\/[^\r\n]*)/g,
    (match, quoted) => quoted ?? '',
  )
}

function externalUrl(value) {
  return /^(?:[a-z][a-z\d+.-]*:)?\/\//i.test(value.trim())
}

function readJavaScriptString(content, start) {
  let cursor = start
  while (/\s/.test(content[cursor] ?? '')) cursor += 1

  const quote = content[cursor]
  if (quote !== '"' && quote !== "'" && quote !== '`') return null
  cursor += 1
  let value = ''

  while (cursor < content.length) {
    const character = content[cursor]
    if (character === '\\') {
      const escaped = content[cursor + 1]
      if (escaped === undefined) return null
      value += escaped === '/' ? '/' : `\\${escaped}`
      cursor += 2
      continue
    }
    if (character === quote) {
      if (quote === '`' && value.includes('${')) return null
      return { value, end: cursor + 1 }
    }
    value += character
    cursor += 1
  }

  return null
}

function maskJavaScriptStrings(content) {
  const masked = content.split('')
  let quote = null

  for (let cursor = 0; cursor < content.length; cursor += 1) {
    const character = content[cursor]
    if (quote) {
      if (character !== '\n' && character !== '\r') masked[cursor] = ' '
      if (character === '\\') {
        cursor += 1
        if (cursor < content.length && content[cursor] !== '\n' && content[cursor] !== '\r') masked[cursor] = ' '
      } else if (character === quote) {
        quote = null
      }
    } else if (character === '"' || character === "'" || character === '`') {
      quote = character
      masked[cursor] = ' '
    }
  }

  return masked.join('')
}

function reportExternalReferences(filePath, text, kind) {
  const content = removeComments(text, kind)
  const found = new Set()
  const addIfExternal = (value, context) => {
    const url = value.trim().replace(/[),;]+$/, '')
    if (externalUrl(url)) found.add(`${context}: ${url}`)
  }

  if (kind === 'css') {
    for (const match of content.matchAll(/url\(\s*(['"]?)(.*?)\1\s*\)/gi)) addIfExternal(match[2], 'CSS url()')
    for (const match of content.matchAll(/@import\s+(?:url\()?['"]([^'"]+)['"]/gi)) addIfExternal(match[1], 'CSS @import')
  } else if (kind === 'html') {
    for (const tag of content.matchAll(/<(script|link|img|source|video|audio|iframe|object|embed)\b([^>]*)>/gi)) {
      const tagName = tag[1].toLowerCase()
      for (const attribute of tag[2].matchAll(/\b(src|href|poster|data|srcset)\s*=\s*(['"])(.*?)\2/gi)) {
        const name = attribute[1].toLowerCase()
        const appliesToTag = name === 'srcset'
          ? tagName === 'img' || tagName === 'source'
          : name === 'href'
            ? tagName === 'link'
            : name === 'data'
              ? tagName === 'object'
              : name === 'poster'
                ? tagName === 'video'
                : name === 'src' && tagName !== 'link'
        if (!appliesToTag) continue

        if (name === 'srcset') {
          for (const candidate of attribute[3].split(',')) addIfExternal(candidate.trim().split(/\s+/)[0], 'HTML srcset')
        } else {
          addIfExternal(attribute[3], `HTML <${tagName}> ${name}`)
        }
      }
    }
  } else {
    const code = maskJavaScriptStrings(content)

    for (const match of code.matchAll(/\b(?:src|srcSet|href|poster)\s*[:=]/gi)) {
      const value = readJavaScriptString(content, match.index + match[0].length)
      if (value) addIfExternal(value.value, 'JavaScript resource property')
    }

    for (const match of code.matchAll(/\bsetAttribute\s*\(/gi)) {
      const attributeName = readJavaScriptString(content, match.index + match[0].length)
      if (!attributeName || !/^(?:src|href|poster)$/i.test(attributeName.value)) continue
      const comma = code.indexOf(',', attributeName.end)
      const value = comma < 0 ? null : readJavaScriptString(content, comma + 1)
      if (value) addIfExternal(value.value, 'JavaScript setAttribute')
    }

    for (const match of code.matchAll(/\bimport\s*\(/gi)) {
      const value = readJavaScriptString(content, match.index + match[0].length)
      if (value) addIfExternal(value.value, 'JavaScript module reference')
    }
    for (const match of code.matchAll(/\bimport\b/gi)) {
      const value = readJavaScriptString(content, match.index + match[0].length)
      if (value) addIfExternal(value.value, 'JavaScript module reference')
    }
    for (const match of code.matchAll(/\bimport\b[^;\n]*?\bfrom/gi)) {
      const value = readJavaScriptString(content, match.index + match[0].length)
      if (value) addIfExternal(value.value, 'JavaScript module reference')
    }

    for (const match of code.matchAll(/\bnew\s+URL\s*\(\s*/gi)) {
      const value = readJavaScriptString(content, match.index + match[0].length)
      if (value && /\.(?:woff2?|ttf|otf|eot|svg|png|jpe?g|gif|webp|avif|ico|css|m?js)(?:[?#]|$)/i.test(value.value)) {
        addIfExternal(value.value, 'JavaScript asset URL')
      }
    }
  }

  for (const reference of found) errors.push(`R19: external asset reference in ${relative(filePath)} (${reference}).`)
}

function checkBuiltAssets() {
  if (!fs.existsSync(distRoot)) {
    errors.push('R19: built output is missing; run npm run build before this guard.')
    return
  }

  const files = listFiles(distRoot, (filePath) => /\.(?:html|css|js|mjs)$/i.test(filePath))
  if (files.length === 0) errors.push('R19: no HTML/CSS/JavaScript files found in the built output.')

  for (const filePath of files) {
    const extension = path.extname(filePath).toLowerCase()
    const kind = extension === '.html' ? 'html' : extension === '.css' ? 'css' : 'js'
    reportExternalReferences(filePath, fs.readFileSync(filePath, 'utf8'), kind)
  }

  console.log(`R19: checked ${files.length} built HTML/CSS/JavaScript asset file(s) for external resource references.`)
}

checkFontBudgetAndSources()
checkDisplayFontScope()
checkBuiltAssets()

if (errors.length > 0) {
  console.error(`\nDesign font/asset guard failed with ${errors.length} finding(s):`)
  for (const error of errors) console.error(`- ${error}`)
  process.exitCode = 1
} else {
  console.log('Design font/asset guard passed (R18, R19, R22).')
}
