import { useState, type FormEvent, type HTMLAttributes } from 'react'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { AlertDialog } from '@base-ui/react/alert-dialog'

import { PageContainer } from '@/components/layout/PageContainer'
import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import {
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { t } from '@/i18n/catalog'
import { createCombGuid } from '@/lib/comb-guid.mjs'
import {
  ApiError,
  createTrip,
  fetchCampers,
  fetchCamperProfile,
  fetchTrip,
  fetchTrips,
  transitionTrip,
  updateTrip,
  type CamperProfile,
  type Money,
  type TripDetails,
  type TripUpdateInput,
} from '@/lib/api'
import { CAMPERS_QUERY_KEY, camperProfileQueryKey } from '@/features/campers/queryKeys'
import { TRIPS_QUERY_KEY, tripDetailsQueryKey } from '@/features/trips/queryKeys'
import {
  calculateFuelEstimate,
  isFuelEstimateEligible,
} from '@/features/trips/fuel-estimate.mjs'
import { JournalEntries } from '@/features/trips/JournalEntries'
import { TripChecklists } from '@/features/trips/TripChecklists'
import { TripExpenses } from '@/features/trips/TripExpenses'

const TRIP_STATUS = {
  planned: 0,
  active: 1,
  completed: 2,
  cancelled: 3,
} as const
const MAX_UI_FUEL_PRICE = 9999.9999

export function TripsPage() {
  const tripsQuery = useInfiniteQuery({
    queryKey: TRIPS_QUERY_KEY,
    queryFn: ({ pageParam }) => fetchTrips(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (page) => page.nextCursor ?? undefined,
    retry: false,
  })
  const trips = tripsQuery.data?.pages.flatMap((page) => page.items) ?? []

  return (
    <PageContainer size="dashboard" className="justify-start gap-6">
      <header className="flex flex-wrap items-end justify-between gap-4 rounded-3xl border border-border bg-card p-6 shadow-xs sm:p-8">
        <div>
          <p className="inline-flex items-center gap-2 rounded-full bg-secondary px-3 py-1 text-xs font-semibold uppercase tracking-[0.08em] text-secondary-foreground">
            {t('narrative.trips.eyebrow')}
          </p>
          <h1 className="page-title mt-3">{t('narrative.trips.title')}</h1>
          <p className="mt-2 max-w-2xl text-sm text-muted-foreground">{t('narrative.trips.description')}</p>
        </div>
        {trips.length > 0 && (
          <Button render={<Link to="/trips/new" />}>{t('neutral.trips.create')}</Button>
        )}
      </header>
      {tripsQuery.isPending ? (
        <FlowMessage message={t('neutral.trips.loading')} />
      ) : tripsQuery.isError ? (
        <FlowError message={t('neutral.trips.loadError')} onRetry={() => void tripsQuery.refetch()} />
      ) : trips.length === 0 ? (
        <section className="space-y-4 rounded-3xl border border-border bg-card p-6 shadow-xs">
          <p className="max-w-xl text-sm text-muted-foreground">{t('narrative.trips.empty')}</p>
          <Button render={<Link to="/trips/new" />}>{t('neutral.trips.create')}</Button>
        </section>
      ) : (
        <>
          <ul aria-label={t('narrative.trips.title')} className="grid gap-4 sm:grid-cols-2">
            {trips.map((trip) => (
              <li key={trip.id}>
                <Link
                  className="flex min-h-20 items-center justify-between gap-4 rounded-2xl border border-border bg-card p-5 shadow-2xs transition-colors hover:border-ring hover:bg-muted/40 focus-visible:outline-2 focus-visible:outline-ring"
                  to={`/trips/${trip.id}`}
                >
                  <span className="text-lg font-semibold text-foreground">{trip.name}</span>
                  <span className="shrink-0 rounded-full bg-secondary px-3 py-1 text-xs font-semibold text-secondary-foreground">
                    {tripStatus(trip.status)}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
          {tripsQuery.hasNextPage && (
            <Button
              variant="outline"
              onClick={() => void tripsQuery.fetchNextPage()}
              disabled={tripsQuery.isFetchingNextPage}
            >
              {tripsQuery.isFetchingNextPage
                ? t('neutral.trips.loadingMore')
                : t('neutral.trips.loadMore')}
            </Button>
          )}
        </>
      )}
    </PageContainer>
  )
}

export function CreateTripPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const campersQuery = useInfiniteQuery({
    queryKey: CAMPERS_QUERY_KEY,
    queryFn: ({ pageParam }) => fetchCampers(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (page) => page.nextCursor ?? undefined,
    retry: false,
  })
  const campers = campersQuery.data?.pages.flatMap((page) => page.items) ?? []
  const [camperId, setCamperId] = useState('')
  const [name, setName] = useState('')
  const [firstStop, setFirstStop] = useState('')
  const [validationError, setValidationError] = useState('')
  const createMutation = useMutation({
    mutationFn: () => createTrip(camperId, {
      id: createCombGuid(),
      name: name.trim(),
      stops: [{ name: firstStop.trim() }],
    }),
    onSuccess: (trip) => {
      void queryClient.invalidateQueries({ queryKey: TRIPS_QUERY_KEY })
      navigate(`/trips/${trip.id}`)
    },
  })

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setValidationError('')
    if (!name.trim() || name.trim().length > 80 || !firstStop.trim() || firstStop.trim().length > 80) {
      setValidationError(t('neutral.trips.validation'))
      return
    }
    if (!camperId) {
      setValidationError(t('neutral.trips.chooseCamper'))
      return
    }
    createMutation.mutate()
  }

  if (campersQuery.isPending) {
    return <FlowMessage message={t('neutral.trips.campersLoading')} />
  }
  if (campersQuery.isError) {
    return (
      <PageContainer size="form" className="justify-start">
        <BackToTrips />
        <FlowError
          message={t('neutral.trips.campersError')}
          onRetry={() => void campersQuery.refetch()}
        />
      </PageContainer>
    )
  }

  return (
    <PageContainer size="form" className="justify-start">
      <BackToTrips />
      <div className="rounded-3xl border border-border bg-card p-6 shadow-xs sm:p-8">
        <header className="mb-6">
          <h1 className="page-title">{t('neutral.trips.createTitle')}</h1>
          <p className="mt-2 text-sm text-muted-foreground">{t('narrative.trips.createDescription')}</p>
        </header>
        {campers.length === 0 ? (
          <section className="space-y-3">
            <p className="text-sm text-muted-foreground">{t('neutral.trips.noCampers')}</p>
            <Button render={<Link to="/campers/new" />}>{t('neutral.camper.selection.addCamper')}</Button>
          </section>
        ) : (
          <form onSubmit={handleSubmit}>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="trip-camper">{t('neutral.trips.camper')}</FieldLabel>
                <FieldContent>
                  <select
                    id="trip-camper"
                    className="h-11 w-full cursor-pointer rounded-xl border border-input bg-card px-3.5 text-sm text-foreground shadow-2xs"
                    required
                    value={camperId}
                    onChange={(event) => setCamperId(event.target.value)}
                  >
                    <option value="">{t('neutral.trips.chooseCamper')}</option>
                    {campers.map((camper) => (
                      <option key={camper.id} value={camper.id}>{camper.name}</option>
                    ))}
                  </select>
                </FieldContent>
              </Field>
              <Field>
                <FieldLabel htmlFor="trip-name">{t('neutral.trips.name')}</FieldLabel>
                <FieldContent>
                  <Input
                    id="trip-name"
                    maxLength={80}
                    required
                    value={name}
                    onChange={(event) => setName(event.target.value)}
                  />
                </FieldContent>
              </Field>
              <Field>
                <FieldLabel htmlFor="trip-first-stop">{t('neutral.trips.firstStop')}</FieldLabel>
                <FieldContent>
                  <Input
                    id="trip-first-stop"
                    maxLength={80}
                    required
                    value={firstStop}
                    onChange={(event) => setFirstStop(event.target.value)}
                  />
                </FieldContent>
              </Field>
              {campersQuery.hasNextPage && (
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => void campersQuery.fetchNextPage()}
                  disabled={campersQuery.isFetchingNextPage}
                >
                  {t('neutral.trips.loadMoreCampers')}
                </Button>
              )}
              {validationError && <p role="alert" className="rounded-lg bg-background p-2 text-sm text-danger-text">{validationError}</p>}
              {createMutation.isError && (
                <p role="alert" className="rounded-lg bg-background p-2 text-sm text-danger-text">{t('neutral.trips.createError')}</p>
              )}
              <Button type="submit" disabled={createMutation.isPending}>
                {createMutation.isPending
                  ? t('neutral.trips.creating')
                  : t('neutral.trips.save')}
              </Button>
            </FieldGroup>
          </form>
        )}
      </div>
    </PageContainer>
  )
}

export function TripJournalPage() {
  const { tripId = '' } = useParams()
  const queryClient = useQueryClient()
  const [editingTrip, setEditingTrip] = useState(false)
  const [feedback, setFeedback] = useState('')
  const tripQuery = useQuery({
    queryKey: tripDetailsQueryKey(tripId),
    queryFn: () => fetchTrip(tripId),
    enabled: Boolean(tripId),
    retry: false,
  })
  const saveTripMutation = useMutation({
    mutationFn: ({ targetTripId, input, etag }: {
      targetTripId: string
      input: TripUpdateInput
      etag: string
    }) => updateTrip(targetTripId, input, etag),
    onSuccess: ({ value, etag: newEtag }, variables) => {
      queryClient.setQueryData(tripDetailsQueryKey(variables.targetTripId), {
        trip: value,
        etag: newEtag,
      })
      void queryClient.invalidateQueries({ queryKey: TRIPS_QUERY_KEY })
      setEditingTrip(false)
      setFeedback(t('neutral.trips.saved'))
    },
  })
  const transitionMutation = useMutation({
    mutationFn: ({ targetTripId, status, etag }: {
      targetTripId: string
      status: number
      etag: string
    }) => transitionTrip(targetTripId, status, etag),
    onSuccess: ({ value, etag: newEtag }, variables) => {
      queryClient.setQueryData(tripDetailsQueryKey(variables.targetTripId), {
        trip: value,
        etag: newEtag,
      })
      void queryClient.invalidateQueries({ queryKey: TRIPS_QUERY_KEY })
      setFeedback(t('neutral.trips.statusSaved'))
    },
  })

  if (tripQuery.isPending) {
    return <FlowMessage message={t('neutral.trips.detailLoading')} />
  }
  if (tripQuery.isError) {
    return (
      <PageContainer size="dashboard" className="justify-start">
        <BackToTrips />
        <FlowError
          message={t('neutral.trips.detailError')}
          onRetry={() => void tripQuery.refetch()}
        />
      </PageContainer>
    )
  }

  const { trip, etag } = tripQuery.data
  return (
    <PageContainer size="dashboard" className="justify-start">
      <BackToTrips />
      <TripHeader trip={trip} />
      {feedback && <p role="status" className="text-sm font-medium">{feedback}</p>}
      <TripExpenses
        camperId={trip.camperId}
        tripId={trip.id}
        tripCancelled={trip.status === TRIP_STATUS.cancelled}
      />
      <TripChecklists
        tripId={trip.id}
        etag={etag}
        onEtagChanged={(newEtag) => {
          queryClient.setQueryData(tripDetailsQueryKey(trip.id), { trip, etag: newEtag })
        }}
      />
      {editingTrip ? (
        <TripEditor
          trip={trip}
          pending={saveTripMutation.isPending}
          error={saveTripMutation.error}
          onSave={(input) => {
            saveTripMutation.reset()
            setFeedback('')
            saveTripMutation.mutate({ targetTripId: trip.id, input, etag })
          }}
          onCancel={() => setEditingTrip(false)}
          onReload={async () => {
            const result = await tripQuery.refetch()
            if (!result.isError) {
              setEditingTrip(false)
              setFeedback(t('neutral.trips.reloaded'))
            }
          }}
        />
      ) : (
        <TripActions
          trip={trip}
          pending={transitionMutation.isPending}
          error={transitionMutation.error}
          onReload={() => {
            void tripQuery.refetch().then((result) => {
              if (!result.isError) transitionMutation.reset()
            })
          }}
          onEdit={() => {
            saveTripMutation.reset()
            setEditingTrip(true)
            setFeedback('')
          }}
          onTransition={(status) => {
            transitionMutation.reset()
            setFeedback('')
            transitionMutation.mutate({ targetTripId: trip.id, status, etag })
          }}
        />
      )}
      <JournalEntries
        tripId={trip.id}
        etag={etag}
        onEtagChanged={(newEtag) => {
          queryClient.setQueryData(tripDetailsQueryKey(trip.id), { trip, etag: newEtag })
        }}
        onReload={() => {
          void queryClient.invalidateQueries({ queryKey: tripDetailsQueryKey(trip.id) })
        }}
      />
    </PageContainer>
  )
}

function TripHeader({ trip }: { trip: TripDetails }) {
  return (
    <header className="space-y-5 rounded-3xl border border-border bg-card p-6 shadow-xs sm:p-8">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="inline-flex items-center rounded-full bg-secondary px-3 py-1 text-xs font-semibold uppercase tracking-[0.08em] text-secondary-foreground">
            {t('narrative.trips.journalEyebrow')}
          </p>
          <h1 className="page-title mt-3">{trip.name}</h1>
        </div>
        <span className="rounded-full bg-primary px-4 py-1.5 text-xs font-semibold text-primary-foreground shadow-2xs">
          {tripStatus(trip.status)}
        </span>
      </div>
      {(trip.estimatedDistanceKm !== null || trip.estimatedDurationMinutes !== null) && (
        <section aria-labelledby="trip-estimate-heading" className="rounded-2xl border border-border bg-background p-4">
          <h2 id="trip-estimate-heading" className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            {t('neutral.trips.manualEstimate')}
          </h2>
          <dl className="mt-2 flex flex-wrap gap-x-8 gap-y-2 text-sm">
            {trip.estimatedDistanceKm !== null && (
              <div>
                <dt className="text-xs text-muted-foreground">{t('neutral.trips.totalDistance')}</dt>
                <dd className="text-base font-bold tabular-nums">{formatEstimatedDistance(trip.estimatedDistanceKm)}</dd>
              </div>
            )}
            {trip.estimatedDurationMinutes !== null && (
              <div>
                <dt className="text-xs text-muted-foreground">{t('neutral.trips.totalDuration')}</dt>
                <dd className="text-base font-bold tabular-nums">{formatEstimatedDuration(trip.estimatedDurationMinutes)}</dd>
              </div>
            )}
          </dl>
        </section>
      )}
      <TripFuelEstimate trip={trip} />
      <section aria-labelledby="trip-stops-heading" className="rounded-2xl border border-border bg-background p-4">
        <h2 id="trip-stops-heading" className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          {t('neutral.trips.stops')}
        </h2>
        <ol className="mt-3 flex flex-wrap items-center gap-3">
          {trip.stops.map((stop) => (
            <li key={stop.id} className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-3.5 py-1.5 text-sm font-medium text-foreground shadow-2xs">
              <span className="inline-flex size-6 items-center justify-center rounded-full bg-primary text-xs font-bold tabular-nums text-primary-foreground">
                {stop.sequenceNo + 1}.
              </span>{' '}
              <span>{stop.name}</span>
            </li>
          ))}
        </ol>
      </section>
    </header>
  )
}

function TripFuelEstimate({ trip }: { trip: TripDetails }) {
  const eligible = isFuelEstimateEligible(trip.status)
  const profileQuery = useQuery({
    queryKey: camperProfileQueryKey(trip.camperId),
    queryFn: () => fetchCamperProfile(trip.camperId),
    enabled: eligible,
    retry: false,
  })

  if (!eligible) return null

  return (
    <section aria-labelledby="trip-fuel-estimate-heading">
      <h2 id="trip-fuel-estimate-heading" className="text-sm font-semibold">
        {t('neutral.trips.fuelEstimate')}
      </h2>
      {profileQuery.isPending ? (
        <p role="status" className="mt-2 text-sm text-muted-foreground">
          {t('neutral.trips.fuelProfileLoading')}
        </p>
      ) : profileQuery.isError || !profileQuery.data ? (
        <p role="alert" className="mt-2 text-sm text-danger-text">
          {t('neutral.trips.fuelProfileError')}
        </p>
      ) : (
        <FuelEstimateResult trip={trip} profile={profileQuery.data.profile} />
      )}
    </section>
  )
}

function FuelEstimateResult({ trip, profile }: { trip: TripDetails; profile: CamperProfile }) {
  const estimate = calculateFuelEstimate({
    status: trip.status,
    estimatedDistanceKm: trip.estimatedDistanceKm,
    fuelKind: profile.fuelKind,
    averageConsumptionLPer100Km: profile.averageConsumptionLPer100Km,
    fuelPricePerLiter: trip.fuelPricePerLiter,
  })

  if (estimate.kind === 'hidden') return null
  if (estimate.kind === 'unavailable') {
    return <p className="mt-2 text-sm text-muted-foreground">{t('neutral.trips.electricFuelUnavailable')}</p>
  }
  if (estimate.kind !== 'estimated') {
    return <p className="mt-2 text-sm text-muted-foreground">{t('neutral.trips.fuelEstimateNeedsData')}</p>
  }

  const amount = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 4 }).format(estimate.amount)
  return (
    <dl className="mt-2 text-sm">
      <dt className="text-muted-foreground">{t('neutral.trips.estimatedFuelCost')}</dt>
      <dd className="tabular-nums">{amount} {estimate.currency}</dd>
    </dl>
  )
}

function TripActions({
  trip,
  pending,
  error,
  onEdit,
  onTransition,
  onReload,
}: {
  trip: TripDetails
  pending: boolean
  error: unknown
  onEdit: () => void
  onTransition: (status: number) => void
  onReload: () => void
}) {
  const [confirmCancel, setConfirmCancel] = useState(false)
  const targets = trip.status === TRIP_STATUS.planned
    ? [
      { status: TRIP_STATUS.active, label: t('neutral.trips.start') },
      { status: TRIP_STATUS.cancelled, label: t('neutral.trips.cancel') },
    ]
    : trip.status === TRIP_STATUS.active
      ? [
        { status: TRIP_STATUS.completed, label: t('neutral.trips.complete') },
        { status: TRIP_STATUS.cancelled, label: t('neutral.trips.cancel') },
      ]
      : trip.status === TRIP_STATUS.completed
        ? [{ status: TRIP_STATUS.active, label: t('neutral.trips.reopen') }]
        : []

  return (
    <AlertDialog.Root open={confirmCancel} onOpenChange={setConfirmCancel}>
      <section aria-label={t('neutral.trips.actions')} className="space-y-3 py-4">
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={onEdit}>{t('neutral.trips.edit')}</Button>
          {targets.map(({ status, label }) => status === TRIP_STATUS.cancelled ? (
            <AlertDialog.Trigger
              key={status}
              render={<Button variant="outline" disabled={pending} />}
            >
              {label}
            </AlertDialog.Trigger>
          ) : (
            <Button
              key={status}
              disabled={pending}
              onClick={() => onTransition(status)}
            >
              {label}
            </Button>
          ))}
        </div>
        <DialogContent showCloseButton={false}>
          <DialogHeader>
            <DialogTitle>{t('neutral.trips.cancelTitle')}</DialogTitle>
            <DialogDescription>{t('neutral.trips.cancelWarning')}</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-2 sm:flex-row sm:justify-end">
            <AlertDialog.Close render={<Button variant="outline" disabled={pending} />}>
              {t('neutral.trips.keepTrip')}
            </AlertDialog.Close>
            <Button
              disabled={pending}
              onClick={() => {
                setConfirmCancel(false)
                onTransition(TRIP_STATUS.cancelled)
              }}
            >
              {t('neutral.trips.confirmCancel')}
            </Button>
          </div>
        </DialogContent>
        {Boolean(error) && (
          <div className="space-y-2">
            <p role="alert" className="text-sm text-danger-text">
              {error instanceof ApiError && error.status === 412
                ? t('neutral.trips.statusConflict')
                : t('neutral.trips.statusError')}
            </p>
            {error instanceof ApiError && error.status === 412 && (
              <Button variant="outline" onClick={onReload}>{t('neutral.trips.reloadStatus')}</Button>
            )}
          </div>
        )}
      </section>
    </AlertDialog.Root>
  )
}

interface TripStopDraft {
  id?: string
  name: string
  arrivalOnUtc: string
  departureOnUtc: string
  position: TripDetails['stops'][number]['position']
  notes: string
}

type ManualEstimateInput = Pick<
  TripUpdateInput,
  'estimatedDistanceKm' | 'estimatedDurationMinutes'
>
type TripEstimateInput = ManualEstimateInput & Pick<TripUpdateInput, 'fuelPricePerLiter'>

function TripEditor({
  trip,
  pending,
  error,
  onSave,
  onCancel,
  onReload,
}: {
  trip: TripDetails
  pending: boolean
  error: unknown
  onSave: (input: TripUpdateInput) => void
  onCancel: () => void
  onReload: () => void
}) {
  const [name, setName] = useState(trip.name)
  const [plannedStartOnUtc, setPlannedStartOnUtc] = useState(dateInputValue(trip.plannedStartOnUtc))
  const [plannedEndOnUtc, setPlannedEndOnUtc] = useState(dateInputValue(trip.plannedEndOnUtc))
  const [estimatedDistanceKm, setEstimatedDistanceKm] = useState(
    trip.estimatedDistanceKm?.toString() ?? '',
  )
  const [estimatedDurationMinutes, setEstimatedDurationMinutes] = useState(
    trip.estimatedDurationMinutes?.toString() ?? '',
  )
  const [fuelPriceAmount, setFuelPriceAmount] = useState(
    trip.fuelPricePerLiter?.amount.toString() ?? '',
  )
  const [fuelPriceCurrency, setFuelPriceCurrency] = useState(
    trip.fuelPricePerLiter?.currency ?? '',
  )
  const [validationError, setValidationError] = useState(false)
  const [estimateValidationError, setEstimateValidationError] = useState(false)
  const [fuelPriceValidationError, setFuelPriceValidationError] = useState(false)
  const [stops, setStops] = useState<TripStopDraft[]>(() => trip.stops.map((stop) => ({
    id: stop.id,
    name: stop.name,
    arrivalOnUtc: dateInputValue(stop.arrivalOnUtc),
    departureOnUtc: dateInputValue(stop.departureOnUtc),
    position: stop.position,
    notes: stop.notes ?? '',
  })))
  const savedFuelPrice = trip.fuelPricePerLiter ?? null
  const hasUnsupportedSavedFuelPrice = savedFuelPrice !== null
    && savedFuelPrice.amount > MAX_UI_FUEL_PRICE
  const canEditFuelPrice = isFuelEstimateEligible(trip.status) || hasUnsupportedSavedFuelPrice
  const unchangedUnsupportedFuelPrice = savedFuelPrice !== null
    && savedFuelPrice.amount > MAX_UI_FUEL_PRICE
    && fuelPriceAmount === savedFuelPrice.amount.toString()
    && fuelPriceCurrency === savedFuelPrice.currency

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (pending) return
    if (!name.trim() || stops.some((stop) => !stop.name.trim())) {
      setValidationError(true)
      return
    }
    const estimates = parseManualEstimates(estimatedDistanceKm, estimatedDurationMinutes)
    if (estimates === null) {
      setEstimateValidationError(true)
      return
    }
    const fuelPrice = canEditFuelPrice
      ? parseFuelPrice(fuelPriceAmount, fuelPriceCurrency)
      : trip.fuelPricePerLiter
    if (fuelPrice === undefined) {
      setFuelPriceValidationError(true)
      return
    }
    setValidationError(false)
    setEstimateValidationError(false)
    setFuelPriceValidationError(false)
    onSave(createTripUpdateInput({
      name,
      plannedStartOnUtc: plannedStartOnUtc || null,
      plannedEndOnUtc: plannedEndOnUtc || null,
      estimates: { ...estimates, fuelPricePerLiter: fuelPrice },
      stops,
    }))
  }

  function updateStop(index: number, changes: Partial<TripStopDraft>) {
    setStops((current) => current.map((stop, stopIndex) => (
      stopIndex === index ? { ...stop, ...changes } : stop
    )))
  }

  function moveStop(index: number, direction: -1 | 1) {
    setStops((current) => {
      const reordered = [...current]
      const target = index + direction
      ;[reordered[index], reordered[target]] = [reordered[target], reordered[index]]
      return reordered
    })
  }

  const isConflict = error instanceof ApiError && error.status === 412
  const isReferencedStop = error instanceof ApiError
    && error.status === 409
    && error.problem !== null
    && typeof error.problem === 'object'
    && 'type' in error.problem
    && error.problem.type === 'https://roamly.dev/problems/trip-stop-referenced'

  return (
    <form onSubmit={submit} className="space-y-5 rounded-xl border border-border p-5">
      <h2 className="text-lg font-semibold">{t('neutral.trips.editTitle')}</h2>
      {isConflict && (
        <div className="space-y-2">
          <p role="alert" className="text-sm text-danger-text">{t('neutral.trips.editConflict')}</p>
          <Button type="button" variant="outline" disabled={pending} onClick={onReload}>
            {t('neutral.trips.reloadDiscard')}
          </Button>
        </div>
      )}
      {isReferencedStop && (
        <p role="alert" className="text-sm text-danger-text">{t('neutral.trips.referencedStop')}</p>
      )}
      {Boolean(error) && !isConflict && !isReferencedStop && (
        <p role="alert" className="text-sm text-danger-text">{t('neutral.trips.editError')}</p>
      )}
      {validationError && (
        <p role="alert" className="text-sm text-danger-text">
          {t('neutral.trips.editValidation')}
        </p>
      )}
      {estimateValidationError && (
        <p role="alert" className="text-sm text-danger-text">
          {t('neutral.trips.estimateValidation')}
        </p>
      )}
      <FieldGroup>
        <Field>
          <FieldLabel htmlFor="trip-edit-name">{t('neutral.trips.name')}</FieldLabel>
          <FieldContent>
            <Input
              id="trip-edit-name"
              maxLength={80}
              required
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor="trip-planned-start">{t('neutral.trips.plannedStart')}</FieldLabel>
          <FieldContent>
            <Input
              id="trip-planned-start"
              type="date"
              value={plannedStartOnUtc}
              onChange={(event) => setPlannedStartOnUtc(event.target.value)}
            />
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor="trip-planned-end">{t('neutral.trips.plannedEnd')}</FieldLabel>
          <FieldContent>
            <Input
              id="trip-planned-end"
              type="date"
              value={plannedEndOnUtc}
              onChange={(event) => setPlannedEndOnUtc(event.target.value)}
            />
          </FieldContent>
        </Field>
        <section aria-labelledby="trip-estimate-edit-heading" className="space-y-3">
          <div>
            <h3 id="trip-estimate-edit-heading" className="text-sm font-semibold">
              {t('neutral.trips.manualEstimate')}
            </h3>
            <p className="text-sm text-muted-foreground">
              {t('neutral.trips.estimateHelp')}
            </p>
          </div>
          <LabeledTripInput
            id="trip-estimated-distance"
            label={t('neutral.trips.estimatedDistance')}
            value={estimatedDistanceKm}
            onChange={(value) => {
              setEstimatedDistanceKm(value)
              setEstimateValidationError(false)
            }}
            inputMode="decimal"
          />
          <LabeledTripInput
            id="trip-estimated-duration"
            label={t('neutral.trips.estimatedDuration')}
            value={estimatedDurationMinutes}
            onChange={(value) => {
              setEstimatedDurationMinutes(value)
              setEstimateValidationError(false)
            }}
            inputMode="numeric"
          />
          {canEditFuelPrice && (
            <>
              <LabeledTripInput
                id="trip-fuel-price"
                label={t('neutral.trips.fuelPricePerLiter')}
                value={fuelPriceAmount}
                onChange={(value) => {
                  setFuelPriceAmount(value)
                  setFuelPriceValidationError(false)
                }}
                inputMode="decimal"
                maxLength={9}
              />
              <LabeledTripInput
                id="trip-fuel-currency"
                label={t('neutral.trips.fuelCurrency')}
                value={fuelPriceCurrency}
                onChange={(value) => {
                  setFuelPriceCurrency(value.toUpperCase())
                  setFuelPriceValidationError(false)
                }}
                maxLength={3}
              />
              {unchangedUnsupportedFuelPrice && (
                <p role="status" className="text-sm text-danger-text">
                  {t('neutral.trips.fuelPriceUnsupportedSaved')}
                </p>
              )}
              {fuelPriceValidationError && (
                <p role="alert" className="text-sm text-danger-text">
                  {t('neutral.trips.fuelPriceValidation')}
                </p>
              )}
            </>
          )}
        </section>
        {stops.map((stop, index) => (
          <fieldset key={stop.id ?? `new-${index}`} className="space-y-3 rounded-lg border border-border p-4">
            <legend className="px-1 text-sm font-semibold">
              {t('neutral.trips.stopGroup')} {index + 1}
            </legend>
            <LabeledTripInput
              id={`trip-stop-name-${index}`}
              label={`${t('neutral.trips.stopName')} ${index + 1}`}
              value={stop.name}
              onChange={(value) => updateStop(index, { name: value })}
              required
              maxLength={80}
            />
            <LabeledTripInput
              id={`trip-stop-arrival-${index}`}
              label={`${t('neutral.trips.stopArrival')} ${index + 1}`}
              value={stop.arrivalOnUtc}
              onChange={(value) => updateStop(index, { arrivalOnUtc: value })}
              type="date"
            />
            <LabeledTripInput
              id={`trip-stop-departure-${index}`}
              label={`${t('neutral.trips.stopDeparture')} ${index + 1}`}
              value={stop.departureOnUtc}
              onChange={(value) => updateStop(index, { departureOnUtc: value })}
              type="date"
            />
            <LabeledTripInput
              id={`trip-stop-notes-${index}`}
              label={`${t('neutral.trips.stopNotes')} ${index + 1}`}
              value={stop.notes}
              onChange={(value) => updateStop(index, { notes: value })}
              maxLength={1000}
            />
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                variant="outline"
                disabled={pending || index === 0}
                onClick={() => moveStop(index, -1)}
              >
                {t('neutral.trips.moveUp')} {index + 1}
              </Button>
              <Button
                type="button"
                variant="outline"
                disabled={pending || index === stops.length - 1}
                onClick={() => moveStop(index, 1)}
              >
                {t('neutral.trips.moveDown')} {index + 1}
              </Button>
              <Button
                type="button"
                variant="outline"
                disabled={pending || stops.length === 1}
                onClick={() => setStops((current) => current.filter((_, stopIndex) => stopIndex !== index))}
              >
                {t('neutral.trips.removeStop')} {index + 1}
              </Button>
            </div>
          </fieldset>
        ))}
      </FieldGroup>
      <div className="flex flex-wrap gap-2">
        {trip.status !== TRIP_STATUS.cancelled && (
          <Button
            type="button"
            variant="outline"
            disabled={pending}
            onClick={() => setStops((current) => [...current, {
              name: '',
              arrivalOnUtc: '',
              departureOnUtc: '',
              position: null,
              notes: '',
            }])}
          >
            {t('neutral.trips.addStop')}
          </Button>
        )}
        <Button type="submit" disabled={pending}>
          {pending ? t('neutral.trips.saving') : t('neutral.trips.save')}
        </Button>
        <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>
          {t('neutral.trips.discard')}
        </Button>
      </div>
    </form>
  )
}

function LabeledTripInput({
  id,
  label,
  value,
  onChange,
  type = 'text',
  inputMode,
  maxLength,
  required = false,
}: {
  id: string
  label: string
  value: string
  onChange: (value: string) => void
  type?: string
  inputMode?: HTMLAttributes<HTMLInputElement>['inputMode']
  maxLength?: number
  required?: boolean
}) {
  return (
    <Field>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <FieldContent>
        <Input
          id={id}
          type={type}
          inputMode={inputMode}
          maxLength={maxLength}
          required={required}
          value={value}
          onChange={(event) => onChange(event.target.value)}
        />
      </FieldContent>
    </Field>
  )
}

function dateInputValue(value: string | null) {
  return value?.slice(0, 10) ?? ''
}

function parseManualEstimates(
  distanceInput: string,
  durationInput: string,
): ManualEstimateInput | null {
  const distance = parseEstimatedDistance(distanceInput)
  const duration = parseEstimatedDuration(durationInput)
  if (distance === undefined || duration === undefined) return null
  return { estimatedDistanceKm: distance, estimatedDurationMinutes: duration }
}

function parseFuelPrice(amountInput: string, currencyInput: string): Money | null | undefined {
  const amountText = amountInput.trim().replace(',', '.')
  const currency = currencyInput.trim().toUpperCase()
  if (!amountText && !currency) return null
  if (!/^\d{1,4}(?:\.\d{1,4})?$/.test(amountText) || !/^[A-Z]{3}$/.test(currency)) {
    return undefined
  }

  const amount = Number(amountText)
  return Number.isFinite(amount) && amount > 0 && amount <= MAX_UI_FUEL_PRICE
    ? { amount, currency }
    : undefined
}

function parseEstimatedDistance(value: string): string | null | undefined {
  const trimmed = value.trim()
  if (!trimmed) return null
  if (!/^\d{1,16}(?:[.,]\d{1,2})?$/.test(trimmed)) return undefined
  return trimmed.replace(',', '.')
}

function parseEstimatedDuration(value: string): number | null | undefined {
  const trimmed = value.trim()
  if (!trimmed) return null
  if (!/^\d+$/.test(trimmed)) return undefined
  const duration = Number(trimmed)
  return Number.isFinite(duration)
    && duration >= 0
    && Number.isInteger(duration)
    && duration <= 2_147_483_647
    ? duration
    : undefined
}

function formatEstimatedDistance(distanceKm: string): string {
  return `${distanceKm.replace('.', ',')} km`
}

function formatEstimatedDuration(durationMinutes: number): string {
  return `${durationMinutes} min`
}

function createTripUpdateInput(draft: {
  name: string
  plannedStartOnUtc: string | null
  plannedEndOnUtc: string | null
  estimates: TripEstimateInput
  stops: TripStopDraft[]
}): TripUpdateInput {
  return {
    name: draft.name.trim(),
    plannedStartOnUtc: draft.plannedStartOnUtc,
    plannedEndOnUtc: draft.plannedEndOnUtc,
    ...draft.estimates,
    stops: draft.stops.map((stop) => ({
      ...(stop.id ? { id: stop.id } : {}),
      name: stop.name.trim(),
      arrivalOnUtc: stop.arrivalOnUtc || null,
      departureOnUtc: stop.departureOnUtc || null,
      position: stop.position,
      notes: stop.notes.trim() || null,
    })),
  }
}

function BackToTrips() {
  return (
    <Link className="text-sm text-muted-foreground underline underline-offset-4" to="/trips">
      {t('neutral.trips.back')}
    </Link>
  )
}

function FlowMessage({ message }: { message: string }) {
  return (
    <PageContainer>
      <p role="status" className="text-sm text-muted-foreground">{message}</p>
    </PageContainer>
  )
}

function FlowError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div role="alert" className="space-y-3 rounded-xl border border-border bg-background p-5">
      <p className="text-sm text-danger-text">{message}</p>
      <Button variant="outline" onClick={onRetry}>{t('neutral.trips.retry')}</Button>
    </div>
  )
}

function tripStatus(status: number): string {
  switch (status) {
    case TRIP_STATUS.active:
      return t('neutral.trips.status.active')
    case TRIP_STATUS.completed:
      return t('neutral.trips.status.completed')
    case TRIP_STATUS.cancelled:
      return t('neutral.trips.status.cancelled')
    default:
      return t('neutral.trips.status.planned')
  }
}
