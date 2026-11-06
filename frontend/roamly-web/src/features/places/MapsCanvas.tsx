import { useEffect, useRef, useState } from 'react'
import * as atlas from 'azure-maps-control'
import 'azure-maps-control/dist/atlas.min.css'
import './maps.css'
import { issueMapsToken, type MapsConfiguration, type SavedPlace } from '@/lib/api'
import { t } from '@/i18n/catalog'
import { categorySymbols, mapCamera, positionedPlaces } from './map-model.mjs'

export default function MapsCanvas({ places, configuration, selectedId, onSelect }: {
  places: SavedPlace[]
  configuration: MapsConfiguration
  selectedId: string | null
  onSelect: (id: string) => void
}) {
  const container = useRef<HTMLDivElement>(null)
  const select = useRef(onSelect)
  const [readyMap, setReadyMap] = useState<atlas.Map | null>(null)
  const [failed, setFailed] = useState(false)
  const { accountClientId, domain } = configuration
  useEffect(() => { select.current = onSelect }, [onSelect])

  useEffect(() => {
    if (!container.current || !accountClientId) return
    const controller = new AbortController()
    let map: atlas.Map | undefined
    let timeout: ReturnType<typeof setTimeout> | undefined
    const fail = () => {
      if (controller.signal.aborted) return
      controller.abort()
      clearTimeout(timeout)
      setReadyMap(null)
      setFailed(true)
      // Avoid disposing inside the SDK's own event dispatch stack.
      queueMicrotask(() => { map?.dispose(); map = undefined })
    }
    try {
      map = new atlas.Map(container.current, {
        domain, disableTelemetry: true, enableAccessibilityLocationFallback: false,
        showLogo: true, showFeedbackLink: true, language: 'it-IT',
        center: [12, 42], zoom: 4,
        authOptions: {
          authType: atlas.AuthenticationType.anonymous, clientId: accountClientId,
          getToken: (resolve, reject) => {
            if (controller.signal.aborted) { reject(); return }
            void issueMapsToken(controller.signal).then(result => {
              if (!controller.signal.aborted) resolve(result.accessToken)
              else reject()
            }).catch(() => { reject(); fail() })
          },
        },
      })
      const instance = map
      timeout = setTimeout(fail, 30_000)
      map.events.add('ready', () => {
        clearTimeout(timeout)
        if (!controller.signal.aborted) setReadyMap(instance)
      })
      map.events.add('error', fail)
    } catch { fail() }
    return () => {
      controller.abort()
      clearTimeout(timeout)
      map?.dispose()
      map = undefined
    }
  }, [accountClientId, domain])

  useEffect(() => {
    if (!readyMap) return
    const markers = positionedPlaces(places).map(place => {
      const button = document.createElement('button')
      button.type = 'button'
      button.className = 'saved-place-map-pin'
      button.dataset.placeId = place.id
      const icon = document.createElement('span')
      icon.setAttribute('aria-hidden', 'true')
      icon.textContent = categorySymbols[place.category] ?? '◆'
      const name = document.createElement('span')
      name.textContent = place.name
      button.append(icon, name)
      button.addEventListener('click', () => select.current(place.id))
      return new atlas.HtmlMarker({ htmlContent: button,
        position: [place.position!.longitude, place.position!.latitude], anchor: 'bottom' })
    })
    readyMap.markers.add(markers)
    const camera = mapCamera(places)
    if (camera) readyMap.setCamera(camera)
    return () => { if (readyMap.getMapContainer()) readyMap.markers.remove(markers) }
  }, [readyMap, places])

  useEffect(() => {
    if (!readyMap || !selectedId) return
    for (const button of container.current?.querySelectorAll<HTMLButtonElement>('.saved-place-map-pin') ?? []) {
      button.setAttribute('aria-pressed', String(button.dataset.placeId === selectedId))
    }
    const place = positionedPlaces(places).find(item => item.id === selectedId)
    if (place) readyMap.setCamera({ center: [place.position!.longitude, place.position!.latitude] })
  }, [readyMap, selectedId, places])

  return (
    <>
      {failed && <p role="alert" className="rounded-lg bg-background p-2 text-sm text-danger-text">{t('neutral.maps.renderError')}</p>}
      <div ref={container} role="region" className="saved-places-map" aria-label={t('neutral.maps.mapLabel')} />
    </>
  )
}
