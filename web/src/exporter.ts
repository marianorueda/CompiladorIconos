// Exportación en el navegador: SVG -> <canvas> -> PNG, y paquete de app en ZIP (JSZip).
//
// Tamaños verificados (noviembre 2026):
//  - Expo: icon.png 1024×1024 y adaptive-icon.png 1024×1024 (solo primer plano, fondo transparente).
//  - Android: mipmap-{mdpi,hdpi,xhdpi,xxhdpi,xxxhdpi}/ic_launcher.png = 48/72/96/144/192 px.
//  - iOS (Xcode 14+): AppIcon.appiconset con una sola imagen 1024×1024 opaca + Contents.json.
import JSZip from 'jszip'

export const PNG_SIZES = [16, 32, 64, 128, 256, 512, 1024]

const ANDROID_DENSITIES: [string, number][] = [
  ['mdpi', 48], ['hdpi', 72], ['xhdpi', 96], ['xxhdpi', 144], ['xxxhdpi', 192],
]

/** Rasteriza un SVG a PNG del tamaño pedido. `opaque` pinta un fondo blanco (iOS no admite transparencia). */
export async function svgToPng(svg: string, size: number, opaque = false): Promise<Blob> {
  const url = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml' }))
  try {
    const img = new Image()
    img.decoding = 'async'
    img.src = url
    await img.decode()
    const canvas = document.createElement('canvas')
    canvas.width = canvas.height = size
    const ctx = canvas.getContext('2d')!
    if (opaque) { ctx.fillStyle = '#FFFFFF'; ctx.fillRect(0, 0, size, size) }
    ctx.imageSmoothingQuality = 'high'
    ctx.drawImage(img, 0, 0, size, size)
    return await new Promise<Blob>((ok, fail) =>
      canvas.toBlob(b => (b ? ok(b) : fail(new Error('no se pudo generar el PNG'))), 'image/png'))
  } finally {
    URL.revokeObjectURL(url)
  }
}

function parse(svg: string): Document {
  return new DOMParser().parseFromString(svg, 'image/svg+xml')
}

function serialize(doc: Document): string {
  return new XMLSerializer().serializeToString(doc)
}

/** Solo el primer plano: sin el grupo de fondo y sin el recorte de la plantilla (ícono adaptativo). */
export function foregroundOnly(svg: string): string {
  const doc = parse(svg)
  doc.getElementById('background')?.remove()
  doc.getElementById('foreground')?.removeAttribute('clip-path')
  return serialize(doc)
}

/**
 * Versión "a sangre" para tiendas que aplican su propia máscara (iOS, icon.png de Expo):
 * el fondo ocupa todo el cuadrado y no se recorta el primer plano. Solo tiene sentido
 * para la plantilla app; las otras se exportan tal cual sobre fondo blanco.
 */
export function fullBleed(svg: string, template: string): string {
  if (template !== 'app') return svg
  const doc = parse(svg)
  const bg = doc.getElementById('background')?.querySelector('path')
  if (bg) {
    const rect = doc.createElementNS('http://www.w3.org/2000/svg', 'rect')
    rect.setAttribute('width', '1024')
    rect.setAttribute('height', '1024')
    rect.setAttribute('fill', bg.getAttribute('fill') ?? '#FFFFFF')
    bg.replaceWith(rect)
  }
  doc.getElementById('foreground')?.removeAttribute('clip-path')
  return serialize(doc)
}

export function download(blob: Blob, name: string) {
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = name
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 1000)
}

const IOS_CONTENTS = {
  images: [{ filename: 'icon-1024.png', idiom: 'universal', platform: 'ios', size: '1024x1024' }],
  info: { author: 'iconscript', version: 1 },
}

/** Paquete de app: icon.png, adaptive-icon.png, android/mipmap-*, ios/AppIcon.appiconset. */
export async function appPackage(svg: string, template: string): Promise<Blob> {
  const zip = new JSZip()
  const bleed = fullBleed(svg, template)
  zip.file('icon.png', await svgToPng(bleed, 1024, true))
  zip.file('adaptive-icon.png', await svgToPng(foregroundOnly(svg), 1024))
  for (const [density, size] of ANDROID_DENSITIES)
    zip.file(`android/mipmap-${density}/ic_launcher.png`, await svgToPng(svg, size))
  zip.file('ios/AppIcon.appiconset/icon-1024.png', await svgToPng(bleed, 1024, true))
  zip.file('ios/AppIcon.appiconset/Contents.json', JSON.stringify(IOS_CONTENTS, null, 2))
  return zip.generateAsync({ type: 'blob' })
}

/** Todos los PNG sueltos (16 a 1024) en un ZIP. */
export async function pngSet(svg: string): Promise<Blob> {
  const zip = new JSZip()
  for (const s of PNG_SIZES) zip.file(`icon-${s}.png`, await svgToPng(svg, s))
  return zip.generateAsync({ type: 'blob' })
}
