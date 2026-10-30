export function isFingerprintedViteWoff2(relativePath) {
  const normalizedPath = relativePath.replaceAll('\\', '/')
  if (!normalizedPath.startsWith('assets/')) return false

  const fileName = normalizedPath.slice('assets/'.length).split('/').at(-1)
  return /[-.][A-Za-z\d_-]{8}\.woff2$/i.test(fileName ?? '')
}
