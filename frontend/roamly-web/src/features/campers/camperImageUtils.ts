export async function resizeImageFileToDataUrl(file: File): Promise<string | null> {
  const rawDataUrl = await new Promise<string | null>((resolve) => {
    const reader = new FileReader()
    reader.onload = () => resolve(typeof reader.result === 'string' ? reader.result : null)
    reader.onerror = () => resolve(null)
    reader.readAsDataURL(file)
  })

  if (!rawDataUrl) {
    return null
  }

  return await new Promise<string>((resolve) => {
    const img = new Image()
    img.onload = () => {
      try {
        const maxWidth = 1100
        const maxHeight = 580
        const scale = Math.min(1, maxWidth / (img.width || 1), maxHeight / (img.height || 1))
        const targetWidth = Math.max(1, Math.round(img.width * scale))
        const targetHeight = Math.max(1, Math.round(img.height * scale))
        const canvas = document.createElement('canvas')
        canvas.width = targetWidth
        canvas.height = targetHeight
        const ctx = canvas.getContext('2d')
        if (!ctx) {
          resolve(rawDataUrl)
          return
        }
        ctx.drawImage(img, 0, 0, targetWidth, targetHeight)
        const compressed = canvas.toDataURL('image/webp', 0.86)
        resolve(compressed.startsWith('data:image/') ? compressed : rawDataUrl)
      } catch {
        resolve(rawDataUrl)
      }
    }
    img.onerror = () => resolve(rawDataUrl)
    img.src = rawDataUrl
  })
}
