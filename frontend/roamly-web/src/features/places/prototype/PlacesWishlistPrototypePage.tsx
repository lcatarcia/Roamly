import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { Bookmark, ChevronDown, MapPin, Plus, Search, X } from 'lucide-react'
import { useSearchParams } from 'react-router-dom'

import { PrototypeSwitcher, type WishlistVariant } from './PrototypeSwitcher'
import './places-wishlist-prototype.css'

// PROTOTYPE — quale composizione mappa + elenco facilita la wishlist?
// Tre varianti throwaway, dati inventati e posizioni su canvas NON geografiche.
const categories = ['Città', 'Paese', 'Museo', 'Locale', 'Attrattiva'] as const
type Category = typeof categories[number]
type Place = {
  id: string
  name: string
  category: Category
  notes: string
  source: string
  link?: string
  point?: { x: number; y: number }
}

const demoPlaces: Place[] = [
  { id: 'demo-1', name: 'Città delle Vele', category: 'Città', notes: 'Immaginare una passeggiata tra portici e botteghe. Tenere una giornata libera, senza un itinerario rigido.', source: 'Taccuino inventato · demo', point: { x: 43, y: 29 } },
  { id: 'demo-2', name: 'Borgo di Carta', category: 'Paese', notes: 'Un piccolo borgo immaginario da esplorare con calma. Cercare una piazza tranquilla per una pausa.', source: 'Racconto inventato · demo', point: { x: 74, y: 23 } },
  { id: 'demo-3', name: 'Museo delle Nuvole', category: 'Museo', notes: 'Collezione immaginaria di cieli e strumenti del vento. Verifiche di orari e biglietti non disponibili nel prototipo.', source: 'Idea inventata · demo', point: { x: 57, y: 47 } },
  { id: 'demo-4', name: 'Caffè del Sentiero', category: 'Locale', notes: 'Una sosta immaginaria per leggere e disegnare. Nessuna recensione o informazione reale.', source: 'Appunto inventato · demo', point: { x: 82, y: 62 } },
  { id: 'demo-5', name: 'Giardino degli Echi', category: 'Attrattiva', notes: 'Percorso botanico di fantasia, da tenere tra le idee per un pomeriggio lento.', source: 'Taccuino inventato · demo', point: { x: 40, y: 71 } },
  { id: 'demo-6', name: 'Officina delle Lanterne', category: 'Museo', notes: 'Spunto ancora da collocare. Questo elemento dimostra come resta consultabile un luogo senza posizione.', source: 'Inserimento manuale inventato · demo' },
]

type LayoutProps = { map: ReactNode; panel: ReactNode; detail: ReactNode }

export function VariantA({ map, panel, detail }: LayoutProps) {
  return <div className="pw-layout pw-layout-a">{panel}<div className="pw-map-column">{map}{detail}</div></div>
}

export function VariantB({ map, panel, detail }: LayoutProps) {
  return <div className="pw-layout pw-layout-b">{map}<div className="pw-floating-panel">{panel}{detail}</div></div>
}

export function VariantC({ map, panel, detail }: LayoutProps) {
  return <div className="pw-layout pw-layout-c">{map}<div className="pw-bottom-panel">{panel}{detail}</div></div>
}

function IllustrativeMap({ places, selectedId, onSelect, numberOf }: {
  places: Place[]
  selectedId?: string
  onSelect: (id: string) => void
  numberOf: (id: string) => number
}) {
  const positioned = places.filter(place => place.point)
  return (
    <section className="pw-map" aria-label="Mappa illustrativa con marker selezionabili">
      <div className="pw-map-caption">
        <span><MapPin size={16} aria-hidden="true" /> Canvas demo · non geografico</span>
        <small>{positioned.length} marker · nessuna posizione reale</small>
      </div>
      <svg className="pw-map-art" viewBox="0 0 1000 700" preserveAspectRatio="none" aria-hidden="true">
        <path className="pw-land" d="M0 0H1000V700H0Z" />
        <g className="pw-contours">
          <path d="M-50 150C120-50 350 70 320 230S60 500 210 760M-20 180C140 0 310 90 280 230S10 510 165 760M20 200C170 50 265 100 240 230S-20 550 120 760" />
          <path d="M630-40C530 120 760 170 710 350S510 570 650 750M680-40C580 110 810 180 760 350S570 570 700 750M730-40C630 110 860 180 810 350S620 570 750 750" />
          <path d="M850 0C780 140 1020 230 1040 440M880-20C820 120 1050 200 1070 440" />
        </g>
        <path className="pw-river" d="M420-40C590 170 320 270 490 410S620 590 490 740" />
        <path className="pw-trail" d="M250 570Q400 400 570 330T870 150" />
      </svg>
      <div className="pw-marker-layer">
        {positioned.map(place => (
          <button
            type="button"
            className="pw-marker"
            key={place.id}
            style={{ left: `${place.point!.x}%`, top: `${place.point!.y}%` }}
            aria-label={`Seleziona ${place.name} (demo), ${place.category}, marker ${numberOf(place.id)}`}
            aria-pressed={selectedId === place.id}
            onClick={() => onSelect(place.id)}
          >{numberOf(place.id)}</button>
        ))}
      </div>
      <p className="pw-map-footnote">Disegno di fantasia · nessuna distanza, strada o direzione reale.</p>
    </section>
  )
}

function PlaceDetail({ place, number }: { place?: Place; number: number }) {
  return (
    <section className="pw-detail" aria-label="Dettaglio luogo">
      {place ? <>
        <p className="pw-eyebrow">SELEZIONATO · {String(number).padStart(2, '0')} / DEMO</p>
        <h2>{place.name}</h2>
        <p className="pw-meta">{place.category} · {place.point ? 'Posizione sintetica sul canvas' : 'Senza posizione · solo in elenco'}</p>
        <h3>Note</h3>
        <p className="pw-notes">{place.notes || 'Nessuna nota in questa sessione demo.'}</p>
        <h3>Sorgente</h3>
        <p>{place.source}</p>
        {place.link && <p className="pw-source-link">Link fornito (non risolto né aperto): <span>{place.link}</span></p>}
      </> : <p>Nessun luogo corrisponde ai filtri. Modifica ricerca o categoria.</p>}
    </section>
  )
}

function AddDemoPlace({ onAdd, onClose }: { onAdd: (place: Place) => void; onClose: () => void }) {
  const [error, setError] = useState('')

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const name = String(data.get('name') ?? '').trim()
    const link = String(data.get('link') ?? '').trim()
    if (!name) {
      setError('Inserisci un nome demo, non solo spazi.')
      return
    }
    if (link) {
      try {
        const url = new URL(link)
        const googleHost = ['google.com', 'www.google.com', 'google.it', 'www.google.it'].includes(url.hostname)
        const mapsLink = (googleHost && (url.pathname === '/maps' || url.pathname.startsWith('/maps/')))
          || url.hostname === 'maps.google.com'
          || url.hostname === 'maps.app.goo.gl'
          || (url.hostname === 'goo.gl' && url.pathname.startsWith('/maps/'))
        if (url.protocol !== 'https:' || !mapsLink || url.username || url.password) throw new Error('Invalid demo link')
      } catch {
        setError('Usa un link HTTPS Google Maps oppure lascia il campo vuoto. Non verrà aperto o risolto.')
        return
      }
    }
    onAdd({
      id: `session-${crypto.randomUUID()}`,
      name,
      category: String(data.get('category')) as Category,
      notes: String(data.get('notes') ?? '').trim(),
      source: 'Inserimento manuale · solo sessione demo',
      link: link || undefined,
    })
  }

  return (
    <section className="pw-add" aria-labelledby="pw-add-title">
      <div className="pw-section-heading">
        <h2 id="pw-add-title">Aggiungi un luogo demo</h2>
        <button type="button" className="pw-icon-button" aria-label="Chiudi form" onClick={onClose}><X size={20} aria-hidden="true" /></button>
      </div>
      <p id="pw-add-help">Usa dati inventati. Solo in memoria fino al ricaricamento, sempre senza posizione. Nessun servizio viene contattato.</p>
      <form onSubmit={submit} aria-describedby="pw-add-help">
        <label>Nome demo<input name="name" required maxLength={100} autoFocus /></label>
        <label>Categoria<select name="category">{categories.map(category => <option key={category}>{category}</option>)}</select></label>
        <label className="pw-form-wide">Note demo<textarea name="notes" rows={3} maxLength={1000} /></label>
        <label className="pw-form-wide">Link Google Maps (facoltativo)<input name="link" type="url" maxLength={2000} aria-describedby="pw-link-help" /></label>
        <p id="pw-link-help" className="pw-form-wide pw-meta">Trattato solo come testo: non geocodificato, non verificato, non aperto.</p>
        {error && <p className="pw-form-wide pw-error" role="alert">{error}</p>}
        <div className="pw-form-wide pw-form-actions">
          <button type="button" className="pw-button" onClick={onClose}>Annulla</button>
          <button type="submit" className="pw-button pw-primary">Aggiungi alla sessione demo</button>
        </div>
      </form>
    </section>
  )
}

export default function PlacesWishlistPrototypePage() {
  const [params] = useSearchParams()
  const candidate = params.get('variant')
  const variant: WishlistVariant = candidate === 'B' || candidate === 'C' ? candidate : 'A'
  const [places, setPlaces] = useState(demoPlaces)
  const [selectedId, setSelectedId] = useState(demoPlaces[0].id)
  const [query, setQuery] = useState('')
  const [category, setCategory] = useState('Tutte')
  const [adding, setAdding] = useState(false)
  const [expanded, setExpanded] = useState(true)
  const [announcement, setAnnouncement] = useState('')
  const listRef = useRef<HTMLUListElement>(null)
  const addButtonRef = useRef<HTMLButtonElement>(null)
  const filtered = places.filter(place =>
    (category === 'Tutte' || place.category === category)
    && `${place.name} ${place.notes}`.toLocaleLowerCase('it').includes(query.trim().toLocaleLowerCase('it')),
  )
  const selected = filtered.find(place => place.id === selectedId) ?? filtered[0]
  const numberOf = (id: string) => places.findIndex(place => place.id === id) + 1

  // Keep the selected row visible within its own scroll area, without moving the page/focus.
  useEffect(() => {
    const list = listRef.current
    const row = list?.querySelector<HTMLButtonElement>('[aria-pressed="true"]')
    if (!list || !row) return
    const listBounds = list.getBoundingClientRect()
    const rowBounds = row.getBoundingClientRect()
    if (rowBounds.top < listBounds.top) list.scrollTop -= listBounds.top - rowBounds.top
    else if (rowBounds.bottom > listBounds.bottom) list.scrollTop += rowBounds.bottom - listBounds.bottom
  }, [selected?.id, variant, expanded])

  function select(id: string) {
    setSelectedId(id)
    setExpanded(true)
  }

  function closeForm() {
    setAdding(false)
    addButtonRef.current?.focus()
  }

  const panel = (
    <section className="pw-panel" data-expanded={expanded} aria-label="Elenco wishlist">
      <div className="pw-section-heading">
        <h2>Da visitare <span className="pw-count">{filtered.length}</span></h2>
        <button type="button" className="pw-mobile-toggle pw-button" aria-expanded={expanded} aria-controls="pw-panel-body" onClick={() => setExpanded(!expanded)}>
          {expanded ? 'Riduci' : 'Espandi'} <ChevronDown size={16} aria-hidden="true" />
        </button>
      </div>
      <div id="pw-panel-body" className="pw-panel-body">
        <div className="pw-filters">
          <label><span><Search size={16} aria-hidden="true" /> Cerca nei luoghi demo</span><input type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder="Nome o nota…" /></label>
          <label>Categoria<select value={category} onChange={event => setCategory(event.target.value)}><option>Tutte</option>{categories.map(item => <option key={item}>{item}</option>)}</select></label>
        </div>
        <p className="pw-results" role="status">{filtered.length} di {places.length} luoghi · {filtered.filter(place => !place.point).length} senza posizione</p>
        <ul className="pw-list" ref={listRef}>
          {filtered.map(place => <li key={place.id}>
            <button type="button" className="pw-place-row" aria-pressed={selected?.id === place.id} onClick={() => select(place.id)}>
              <span className="pw-row-number">{numberOf(place.id)}</span>
              <span><strong>{place.name}</strong><small>{place.category} · demo</small><small>{place.point ? 'Sul canvas illustrativo' : 'Senza posizione · solo elenco'}</small></span>
              {selected?.id === place.id && <span className="pw-selected-label">Selezionato</span>}
            </button>
          </li>)}
        </ul>
        {!filtered.length && <div className="pw-empty"><p>Nessun luogo trovato.</p><button type="button" className="pw-button" onClick={() => { setQuery(''); setCategory('Tutte') }}>Azzera filtri</button></div>}
      </div>
    </section>
  )
  const layoutProps = {
    panel,
    map: <IllustrativeMap places={filtered} selectedId={selected?.id} onSelect={select} numberOf={numberOf} />,
    detail: <PlaceDetail place={selected} number={selected ? numberOf(selected.id) : 0} />,
  }

  return (
    <div className="pw-prototype">
      <header className="pw-page-header">
        <div><p className="pw-eyebrow"><Bookmark size={14} aria-hidden="true" /> LABORATORIO LUOGHI · SOLO DEV</p><h1 className="page-title">Le prossime possibilità.</h1><p>Una wishlist, due modi di esplorarla: sulla mappa e tra gli appunti.</p></div>
        <button type="button" ref={addButtonRef} className="pw-button pw-primary" aria-expanded={adding} aria-controls="pw-add-region" onClick={() => setAdding(!adding)}><Plus size={18} aria-hidden="true" /> Aggiungi luogo demo</button>
      </header>
      <p className="pw-notice" role="note">Prototipo throwaway · tutti i luoghi sono inventati. Mappa illustrativa non geografica, nessun provider o API. Modifiche solo in memoria: ricaricando si perdono.</p>
      <div id="pw-add-region">{adding && <AddDemoPlace onClose={closeForm} onAdd={place => {
        setPlaces(current => [...current, place])
        setQuery('')
        setCategory('Tutte')
        select(place.id)
        setAnnouncement(`${place.name} aggiunto solo alla sessione demo, senza posizione.`)
        closeForm()
      }} />}</div>
      <p className="pw-session-state" role="status">{announcement || 'Seleziona una voce o un marker: elenco e dettaglio restano sincronizzati.'}</p>
      {variant === 'A' ? <VariantA {...layoutProps} /> : variant === 'B' ? <VariantB {...layoutProps} /> : <VariantC {...layoutProps} />}
      <PrototypeSwitcher current={variant} />
    </div>
  )
}
