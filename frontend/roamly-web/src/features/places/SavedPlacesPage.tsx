import { useRef, useState, type FormEvent, type ReactNode } from 'react'
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldGroup, FieldLabel } from '@/components/ui/field'
import { PageContainer } from '@/components/layout/PageContainer'
import { Input } from '@/components/ui/input'
import { SAVED_PLACES_QUERY_KEY } from '@/features/places/queryKeys'
import { SavedPlacesMapPanel } from './SavedPlacesMapPanel'
import { t } from '@/i18n/catalog'
import { createCombGuid } from '@/lib/comb-guid.mjs'
import {
  ApiError,
  createSavedPlace,
  fetchSavedPlaces,
  type SavedPlace,
  type SavedPlaceInput,
} from '@/lib/api'

const CATEGORIES = [
  [0, t('neutral.savedPlaces.category.aire')],
  [1, t('neutral.savedPlaces.category.campsite')],
  [2, t('neutral.savedPlaces.category.parking')],
  [3, t('neutral.savedPlaces.category.viewpoint')],
  [4, t('neutral.savedPlaces.category.restaurant')],
  [5, t('neutral.savedPlaces.category.fuelStation')],
  [6, t('neutral.savedPlaces.category.workshop')],
  [7, t('neutral.savedPlaces.category.supermarket')],
  [8, t('neutral.savedPlaces.category.pointOfInterest')],
  [9, t('neutral.savedPlaces.category.city')],
  [10, t('neutral.savedPlaces.category.town')],
  [11, t('neutral.savedPlaces.category.museum')],
  [12, t('neutral.savedPlaces.category.venue')],
  [13, t('neutral.savedPlaces.category.attraction')],
] as const

export function SavedPlacesPage() {
  const queryClient = useQueryClient()
  const placesQuery = useInfiniteQuery({
    queryKey: SAVED_PLACES_QUERY_KEY,
    queryFn: ({ pageParam }) => fetchSavedPlaces(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (page) => page.nextCursor ?? undefined,
    retry: false,
  })
  const createMutation = useMutation({
    mutationFn: createSavedPlace,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: SAVED_PLACES_QUERY_KEY }),
  })
  const places = placesQuery.data?.pages.flatMap((page) => page.items) ?? []

  return (
    <SavedPlacesLayout>
      <SavedPlaceCreateForm
        onCreate={(input) => createMutation.mutateAsync(input)}
        saving={createMutation.isPending}
        saved={createMutation.isSuccess}
        error={createMutation.error}
      />
      <SavedPlaceCollection
        places={places}
        loading={placesQuery.isPending}
        error={placesQuery.isError}
        hasMore={placesQuery.hasNextPage}
        loadingMore={placesQuery.isFetchingNextPage}
        onRetry={() => void placesQuery.refetch()}
        onLoadMore={() => void placesQuery.fetchNextPage()}
      />
    </SavedPlacesLayout>
  )
}

function SavedPlacesLayout({ children }: { children: ReactNode }) {
  return (
    <PageContainer size="dashboard" className="justify-start gap-6">
      <header className="rounded-3xl border border-border bg-card p-6 shadow-xs sm:p-8">
        <p className="inline-flex rounded-full bg-secondary px-3 py-1 text-xs font-semibold uppercase tracking-[0.08em] text-secondary-foreground">
          {t('narrative.savedPlaces.eyebrow')}
        </p>
        <h1 className="page-title mt-3">{t('narrative.savedPlaces.title')}</h1>
        <p className="mt-2 max-w-2xl text-sm text-muted-foreground">
          {t('narrative.savedPlaces.description')}
        </p>
      </header>
      <div className="grid min-w-0 gap-6 lg:grid-cols-[minmax(18rem,0.8fr)_minmax(0,1.2fr)]">
        {children}
      </div>
    </PageContainer>
  )
}

function SavedPlaceCreateForm({
  onCreate,
  saving,
  saved,
  error,
}: {
  onCreate: (input: SavedPlaceInput) => Promise<SavedPlace>
  saving: boolean
  saved: boolean
  error: unknown
}) {
  const [name, setName] = useState('')
  const [category, setCategory] = useState('')
  const [address, setAddress] = useState('')
  const [notes, setNotes] = useState('')
  const [latitude, setLatitude] = useState('')
  const [longitude, setLongitude] = useState('')
  const [googleMapsUrl, setGoogleMapsUrl] = useState('')
  const [validationError, setValidationError] = useState<'required' | 'coordinates' | null>(null)
  const createIdsByPayload = useRef(new Map<string, string>())

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!name.trim() || !category) {
      setValidationError('required')
      return
    }
    if (Boolean(latitude) !== Boolean(longitude)) {
      setValidationError('coordinates')
      return
    }
    setValidationError(null)
    void submitSavedPlace()
  }

  async function submitSavedPlace() {
    const place = buildSavedPlaceInput(name, category, address, notes, latitude, longitude, googleMapsUrl)
    const fingerprint = JSON.stringify(place)
    let id = createIdsByPayload.current.get(fingerprint)
    if (!id) {
      id = createCombGuid()
      createIdsByPayload.current.set(fingerprint, id)
    }

    try {
      await onCreate({ ...place, id })
      createIdsByPayload.current.clear()
      clearForm(setName, setCategory, setAddress, setNotes, setLatitude, setLongitude, setGoogleMapsUrl)
    } catch {
      // L'errore della mutation resta visibile sotto il pulsante.
    }
  }

  return (
    <section className="min-w-0 rounded-3xl border border-border bg-card p-5 shadow-xs sm:p-6">
      <h2 className="text-xl font-semibold">{t('neutral.savedPlaces.formTitle')}</h2>
      <form className="mt-5 space-y-4" onSubmit={handleSubmit} noValidate>
        <FieldGroup>
          <TextInputField
            id="saved-place-name"
            label={t('neutral.savedPlaces.name')}
            value={name}
            onChange={setName}
            placeholder={t('neutral.savedPlaces.namePlaceholder')}
            required
            maxLength={120}
          />
          <CategoryField value={category} onChange={setCategory} />
          <TextInputField id="saved-place-address" label={t('neutral.savedPlaces.address')} value={address} onChange={setAddress} maxLength={300} />
          <TextInputField id="saved-place-notes" label={t('neutral.savedPlaces.notes')} value={notes} onChange={setNotes} maxLength={1000} />
          <CoordinateFields latitude={latitude} longitude={longitude} setLatitude={setLatitude} setLongitude={setLongitude} />
          <TextInputField
            id="saved-place-google-maps-url"
            label={t('neutral.savedPlaces.googleMapsUrl')}
            value={googleMapsUrl}
            onChange={setGoogleMapsUrl}
            type="url"
            maxLength={2048}
            help={t('neutral.savedPlaces.googleMapsHelp')}
          />
        </FieldGroup>
        {validationError && (
          <p role="alert" className="rounded-lg bg-background p-2 text-sm text-danger-text">
            {validationMessage(validationError)}
          </p>
        )}
        {Boolean(error) && <p role="alert" className="rounded-lg bg-background p-2 text-sm text-danger-text">{savedPlaceError(error)}</p>}
        {saved && <p role="status" className="text-sm text-muted-foreground">{t('neutral.savedPlaces.added')}</p>}
        <Button type="submit" className="w-full" disabled={saving}>
          {saving ? t('neutral.savedPlaces.adding') : t('neutral.savedPlaces.add')}
        </Button>
      </form>
    </section>
  )
}

function TextInputField({
  id,
  label,
  value,
  onChange,
  type = 'text',
  required = false,
  maxLength,
  step,
  placeholder,
  help,
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  type?: string
  required?: boolean
  maxLength?: number
  step?: string
  placeholder?: string
  help?: string
}) {
  return (
    <Field>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <FieldContent>
        <Input id={id} type={type} value={value} onChange={(event) => onChange(event.target.value)} required={required} maxLength={maxLength} step={step} placeholder={placeholder} />
        {help && <p className="text-xs text-muted-foreground">{help}</p>}
      </FieldContent>
    </Field>
  )
}

function CategoryField({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return (
    <Field>
      <FieldLabel htmlFor="saved-place-category">{t('neutral.savedPlaces.category')}</FieldLabel>
      <FieldContent>
        <select
          id="saved-place-category"
          required
          value={value}
          onChange={(event) => onChange(event.target.value)}
          className="h-11 w-full rounded-xl border border-input bg-card px-3.5 text-sm text-foreground outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
        >
          <option value="">{t('neutral.savedPlaces.chooseCategory')}</option>
          {CATEGORIES.map(([category, label]) => (
            <option key={category} value={category}>{label}</option>
          ))}
        </select>
      </FieldContent>
    </Field>
  )
}

function CoordinateFields({
  latitude,
  longitude,
  setLatitude,
  setLongitude,
}: {
  latitude: string
  longitude: string
  setLatitude: (value: string) => void
  setLongitude: (value: string) => void
}) {
  return (
    <fieldset className="grid min-w-0 gap-4 rounded-xl border border-border p-3 sm:grid-cols-2">
      <legend className="px-1 text-xs font-semibold uppercase tracking-[0.05em] text-foreground">
        {t('neutral.savedPlaces.positionLabel')}
      </legend>
      <p className="text-sm text-muted-foreground sm:col-span-2">{t('neutral.savedPlaces.positionHelp')}</p>
      <TextInputField id="saved-place-latitude" label={t('neutral.savedPlaces.latitude')} value={latitude} onChange={setLatitude} type="number" step="0.000001" />
      <TextInputField id="saved-place-longitude" label={t('neutral.savedPlaces.longitude')} value={longitude} onChange={setLongitude} type="number" step="0.000001" />
    </fieldset>
  )
}

function SavedPlaceCollection({
  places,
  loading,
  error,
  hasMore,
  loadingMore,
  onRetry,
  onLoadMore,
}: {
  places: SavedPlace[]
  loading: boolean
  error: boolean
  hasMore: boolean
  loadingMore: boolean
  onRetry: () => void
  onLoadMore: () => void
}) {
  const [view, setView] = useState<'list' | 'map'>('list')
  if (loading) return <CollectionMessage>{t('neutral.savedPlaces.loading')}</CollectionMessage>
  if (error && places.length === 0) return <CollectionError onRetry={onRetry} />

  return (
    <section aria-label={t('narrative.savedPlaces.title')} className="min-w-0 space-y-4">
      <div role="group" aria-label={t('neutral.maps.view')} className="flex gap-2">
        <Button variant={view === 'list' ? 'default' : 'outline'} aria-pressed={view === 'list'} onClick={() => setView('list')}>{t('neutral.maps.list')}</Button>
        <Button variant={view === 'map' ? 'default' : 'outline'} aria-pressed={view === 'map'} onClick={() => setView('map')}>{t('neutral.maps.map')}</Button>
      </div>
      {error && <CollectionError onRetry={onRetry} />}
      {view === 'map' ? <SavedPlacesMapPanel places={places} categoryLabel={categoryLabel} /> : (
        places.length === 0 ? <CollectionMessage>{t('neutral.savedPlaces.empty')}</CollectionMessage> :
          <ul className="grid min-w-0 gap-3 sm:grid-cols-2">
            {places.map((place) => <SavedPlaceCard key={place.id} place={place} />)}
          </ul>
      )}
      {hasMore && (
        <Button variant="outline" onClick={onLoadMore} disabled={loadingMore}>
          {loadingMore ? t('neutral.savedPlaces.loadingMore') : t('neutral.savedPlaces.loadMore')}
        </Button>
      )}
    </section>
  )
}

function SavedPlaceCard({ place }: { place: SavedPlace }) {
  return (
    <li className="min-w-0 rounded-2xl border border-border bg-card p-5 shadow-2xs">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <h2 className="break-words text-lg font-semibold">{place.name}</h2>
        <span className="rounded-full bg-secondary px-3 py-1 text-xs font-semibold text-secondary-foreground">
          {categoryLabel(place.category)}
        </span>
      </div>
      <p className="mt-2 text-sm text-muted-foreground">
        {place.position
          ? `${place.position.latitude}, ${place.position.longitude}`
          : t('neutral.savedPlaces.positionMissing')}
      </p>
      {place.address && <p className="mt-2 break-words text-sm">{place.address}</p>}
      {place.notes && <p className="mt-2 break-words text-sm text-muted-foreground">{place.notes}</p>}
      {place.googleMapsUrl && (
        <a
          className="mt-3 inline-block max-w-full break-all text-sm text-foreground underline underline-offset-4"
          href={place.googleMapsUrl}
          target="_blank"
          rel="noopener noreferrer"
        >
          {t('neutral.savedPlaces.googleMapsUrl')}
        </a>
      )}
    </li>
  )
}

function CollectionMessage({ children }: { children: ReactNode }) {
  return <section className="rounded-3xl border border-border bg-card p-6 text-sm text-muted-foreground">{children}</section>
}

function CollectionError({ onRetry }: { onRetry: () => void }) {
  return (
    <section className="space-y-4 rounded-3xl border border-border bg-card p-6">
      <p role="alert" className="rounded-lg bg-background p-2 text-sm text-danger-text">{t('neutral.savedPlaces.loadError')}</p>
      <Button variant="outline" onClick={onRetry}>{t('neutral.savedPlaces.retry')}</Button>
    </section>
  )
}

function buildSavedPlaceInput(
  name: string,
  category: string,
  address: string,
  notes: string,
  latitude: string,
  longitude: string,
  googleMapsUrl: string,
): Omit<SavedPlaceInput, 'id'> {
  const position = latitude && longitude
    ? { latitude: Number(latitude), longitude: Number(longitude) }
    : undefined
  return {
    name: name.trim(),
    category: Number(category),
    ...(position ? { position } : {}),
    address: optionalText(address),
    notes: optionalText(notes),
    googleMapsUrl: optionalText(googleMapsUrl),
  }
}

function optionalText(value: string): string | null {
  return value.trim() || null
}

function clearForm(
  setName: (value: string) => void,
  setCategory: (value: string) => void,
  setAddress: (value: string) => void,
  setNotes: (value: string) => void,
  setLatitude: (value: string) => void,
  setLongitude: (value: string) => void,
  setGoogleMapsUrl: (value: string) => void,
) {
  setName('')
  setCategory('')
  setAddress('')
  setNotes('')
  setLatitude('')
  setLongitude('')
  setGoogleMapsUrl('')
}

function categoryLabel(category: number): string {
  return CATEGORIES.find(([value]) => value === category)?.[1]
    ?? t('neutral.savedPlaces.category.pointOfInterest')
}

function validationMessage(error: 'required' | 'coordinates'): string {
  return error === 'required'
    ? t('neutral.savedPlaces.required')
    : t('neutral.savedPlaces.coordinatesRequired')
}

function savedPlaceError(error: unknown): string {
  if (error instanceof ApiError && error.status === 409) return t('neutral.savedPlaces.conflict')
  return t('neutral.savedPlaces.error')
}
