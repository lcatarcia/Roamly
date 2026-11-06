import { useEffect } from 'react'
import { ArrowLeft, ArrowRight } from 'lucide-react'
import { useSearchParams } from 'react-router-dom'

export type WishlistVariant = 'A' | 'B' | 'C'
const variants: WishlistVariant[] = ['A', 'B', 'C']
const labels = { A: 'Split + dettaglio', B: 'Pannello sulla mappa', C: 'Mappa sopra, lista sotto' }

export function PrototypeSwitcher({ current }: { current: WishlistVariant }) {
  const [params, setParams] = useSearchParams()

  function cycle(direction: number) {
    const next = new URLSearchParams(params)
    next.set('variant', variants[(variants.indexOf(current) + direction + variants.length) % variants.length])
    setParams(next, { replace: true })
  }

  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if (event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) return
      const target = event.target
      if (target instanceof Element && target.closest(
        'input, textarea, select, button, a, summary, [contenteditable]:not([contenteditable="false"]), [role], [tabindex]',
      )) return
      if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
        event.preventDefault()
        cycle(event.key === 'ArrowLeft' ? -1 : 1)
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  })

  if (!import.meta.env.DEV) return null
  return (
    <nav className="pw-switcher" aria-label="Varianti del prototipo">
      <button type="button" aria-label="Variante precedente" onClick={() => cycle(-1)}>
        <ArrowLeft size={18} aria-hidden="true" />
      </button>
      <span aria-live="polite"><small>PROTOTIPO · {variants.indexOf(current) + 1}/3</small>{current} — {labels[current]}</span>
      <button type="button" aria-label="Variante successiva" onClick={() => cycle(1)}>
        <ArrowRight size={18} aria-hidden="true" />
      </button>
    </nav>
  )
}
