import { useMutation, useQueryClient } from '@tanstack/react-query'
import { t } from '@/i18n/catalog'
import { ApiError, fetchSavedPlaceVersion, updateSavedPlace, type SavedPlace } from '@/lib/api'
import { SAVED_PLACES_QUERY_KEY } from './queryKeys'

export function SavedPlaceVisitControl({ place, disabled }: { place: SavedPlace; disabled?: boolean }) {
  const queryClient = useQueryClient()
  const refresh = () => queryClient.invalidateQueries({ queryKey: SAVED_PLACES_QUERY_KEY })
  const mutation = useMutation({
    mutationFn: async (isVisited: boolean) => {
      const { value, etag } = await fetchSavedPlaceVersion(place.id)
      const { name, category, position, address, notes, googleMapsUrl } = value
      return updateSavedPlace(place.id, {
        name, category, position: position ?? undefined, address, notes, googleMapsUrl, isVisited,
      }, etag)
    },
    onSuccess: refresh,
    onError: (error) => {
      if (error instanceof ApiError && error.status === 412) return refresh()
    },
  })
  const unavailable = disabled || mutation.isPending

  return (
    <div className="space-y-2">
      <label className="inline-flex min-h-9 items-center gap-2 text-sm font-medium">
        <input
          type="checkbox"
          className="size-4 shrink-0 accent-primary"
          aria-label={`${t('neutral.savedPlaces.visited')}: ${place.name}`}
          checked={place.isVisited === true}
          aria-disabled={unavailable}
          onClick={event => { if (unavailable) event.preventDefault() }}
          onChange={event => { if (!unavailable) mutation.mutate(event.currentTarget.checked) }}
        />
        <span>{t('neutral.savedPlaces.visited')}</span>
      </label>
      {mutation.isError && (
        <p role="alert" className="rounded-lg bg-surface-raised px-3 py-2 text-sm text-danger-text">
          {mutation.error instanceof ApiError && mutation.error.status === 412
            ? t('neutral.savedPlaces.stale')
            : t('neutral.savedPlaces.visitSaveError')}
        </p>
      )}
    </div>
  )
}
