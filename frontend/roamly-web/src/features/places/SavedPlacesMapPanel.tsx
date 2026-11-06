import { lazy, Suspense, useEffect, useRef, useState } from 'react'
import { Button } from '@/components/ui/button'
import { t } from '@/i18n/catalog'
import {
  fetchMapsConfiguration, fetchMapsConsent, recordMapsConsent,
  type MapsConfiguration, type MapsConsent, type SavedPlace,
} from '@/lib/api'
import { categorySymbols, positionedPlaces } from './map-model.mjs'

const MapsCanvas = lazy(() => import('./MapsCanvas').catch(() => ({
  default: () => <p role="alert" className="rounded-lg bg-background p-2 text-sm text-danger-text">{t('neutral.maps.renderError')}</p>,
})))

export function SavedPlacesMapPanel({ places, categoryLabel }: {
  places: SavedPlace[]
  categoryLabel: (category: number) => string
}) {
  const [configuration, setConfiguration] = useState<MapsConfiguration | null>(null)
  const [consent, setConsent] = useState<MapsConsent | null>(null)
  const [opened, setOpened] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(false)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const lifetime = useRef<AbortController | null>(null)
  const selected = places.find(place => place.id === selectedId)
  const located = positionedPlaces(places)

  useEffect(() => {
    const controller = new AbortController()
    lifetime.current = controller
    void fetchMapsConfiguration(controller.signal).then(async config => {
      if (controller.signal.aborted) return
      setConfiguration(config)
      if (config.isConfigured) {
        const state = await fetchMapsConsent(controller.signal)
        if (!controller.signal.aborted) setConsent(state)
      }
    }).catch(() => { if (!controller.signal.aborted) setError(true) })
    return () => controller.abort()
  }, [])

  async function changeConsent(grant: boolean) {
    const signal = lifetime.current?.signal
    if (!consent || !signal || signal.aborted) return
    if (!grant) setOpened(false) // Dispose first; do not wait for the registry request.
    setBusy(true)
    setError(false)
    try {
      if (!grant || !consent.isGranted) await recordMapsConsent(consent, grant, signal)
      const current = await fetchMapsConsent(signal)
      if (signal.aborted) return
      setConsent(current)
      setOpened(grant && current.isGranted)
    } catch {
      if (!signal.aborted) setError(true)
    } finally {
      if (!signal.aborted) setBusy(false)
    }
  }

  return (
    <div className="space-y-4 rounded-2xl border border-border bg-card p-4">
      <p className="text-sm text-muted-foreground">{t('neutral.maps.explanation')}</p>
      <p className="text-xs text-muted-foreground">{t('neutral.maps.costNotice')}</p>
      {error && <p role="alert" className="rounded-lg bg-background p-2 text-sm text-danger-text">{t('neutral.maps.error')}</p>}
      {!configuration && !error && <p role="status">{t('neutral.maps.loading')}</p>}
      {configuration && !configuration.isConfigured && <p role="status">{t('neutral.maps.notConfigured')}</p>}
      {configuration?.isConfigured && consent?.isActive && !opened && (
        <Button onClick={() => void changeConsent(true)} disabled={busy}>
          {t('neutral.maps.consentOpen')}
        </Button>
      )}
      {configuration?.isConfigured && consent && !consent.isActive && <p role="status">{t('neutral.maps.notConfigured')}</p>}
      {opened && configuration?.accountClientId && (
        <>
          <Button variant="outline" disabled={busy} onClick={() => void changeConsent(false)}>
            {t('neutral.maps.revoke')}
          </Button>
          <p className="text-xs text-muted-foreground">{t('neutral.maps.revokeNotice')}</p>
          {located.length > 0 ? (
            <Suspense fallback={<p role="status">{t('neutral.maps.loading')}</p>}>
              <MapsCanvas places={places} configuration={configuration} selectedId={selectedId} onSelect={setSelectedId} />
            </Suspense>
          ) : <p role="status">{t('neutral.maps.noCoordinates')}</p>}
        </>
      )}
      <p role="status" className="text-sm">{t('neutral.maps.loaded')}: {places.length} · {t('neutral.maps.located')}: {located.length}</p>
      <details open>
        <summary className="cursor-pointer text-sm font-semibold">{t('neutral.maps.legend')}</summary>
        <ul className="mt-2 grid grid-cols-2 gap-2 text-sm">
          {categorySymbols.map((symbol, category) => (
            <li key={category}><span aria-hidden="true">{symbol} </span>{categoryLabel(category)}</li>
          ))}
        </ul>
      </details>
      <ul aria-label={t('neutral.maps.accessiblePlaces')} className="flex flex-wrap gap-2">
        {places.map(place => (
          <li key={place.id}>
            <Button variant="outline" aria-pressed={selectedId === place.id} onClick={() => setSelectedId(place.id)}>
              <span aria-hidden="true">{categorySymbols[place.category] ?? '◆'}</span>
              <span className="max-w-56 truncate">{place.name}</span>
            </Button>
          </li>
        ))}
      </ul>
      {selected && (
        <section aria-label={t('neutral.maps.details')} className="space-y-2 rounded-xl border border-border p-4">
          <h3 className="break-words font-semibold">{selected.name}</h3>
          <p>{categoryLabel(selected.category)}</p>
          {selected.address && <p className="break-words">{selected.address}</p>}
          {selected.notes && <p className="break-words">{selected.notes}</p>}
          <p>{selected.position ? `${selected.position.latitude}, ${selected.position.longitude}` : t('neutral.savedPlaces.positionMissing')}</p>
        </section>
      )}
    </div>
  )
}
