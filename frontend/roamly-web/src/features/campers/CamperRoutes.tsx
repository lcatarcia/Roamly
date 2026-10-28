import { lazy, Suspense } from 'react'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { Link, Navigate, Route, Routes, useParams } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { PageContainer } from '@/components/layout/PageContainer'
import { t } from '@/i18n/catalog'
import { CreateCamperForm } from '@/features/campers/CreateCamperForm'
import { CamperDashboardManualPanel } from '@/features/campers/CamperManualSection'
import { CamperProfileForm } from '@/features/campers/CamperProfileForm'
import { EquipmentInventory } from '@/features/campers/EquipmentInventory'
import {
  CreateMaintenanceForm,
  MaintenanceLogHistory,
  RecordMaintenanceLogForm,
} from '@/features/campers/MaintenanceForms'
import {
  fetchCamperDashboard,
  fetchCampers,
  type CamperDashboardAxisStatus,
  type CamperDashboardResponse,
} from '@/lib/api'
import { CAMPER_DASHBOARD_QUERY_KEY, CAMPERS_QUERY_KEY } from '@/features/campers/queryKeys'

const TripsPage = lazy(() => import('@/features/trips/TripsPages')
  .then((module) => ({ default: module.TripsPage })))
const CreateTripPage = lazy(() => import('@/features/trips/TripsPages')
  .then((module) => ({ default: module.CreateTripPage })))
const TripJournalPage = lazy(() => import('@/features/trips/TripsPages')
  .then((module) => ({ default: module.TripJournalPage })))

/** Flusso autenticato di selezione, creazione e dashboard, senza schermate Phase 2. */
export function CamperRoutes() {
  return (
    <Routes>
      <Route path="/" element={<CamperSelectionPage />} />
      <Route path="/campers" element={<CamperSelectionPage />} />
      <Route path="/campers/new" element={<CreateCamperPage />} />
      <Route path="/campers/:camperId/profile" element={<CamperProfilePage />} />
      <Route path="/campers/:camperId" element={<CamperDashboardPage />} />
      <Route
        path="/trips"
        element={(
          <Suspense fallback={<FlowMessage message={t('neutral.trips.loading')} />}>
            <TripsPage />
          </Suspense>
        )}
      />
      <Route
        path="/trips/new"
        element={(
          <Suspense fallback={<FlowMessage message={t('neutral.trips.loading')} />}>
            <CreateTripPage />
          </Suspense>
        )}
      />
      <Route
        path="/trips/:tripId"
        element={(
          <Suspense fallback={<FlowMessage message={t('neutral.trips.loading')} />}>
            <TripJournalPage />
          </Suspense>
        )}
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

function CamperSelectionPage() {
  const campersQuery = useInfiniteQuery({
    queryKey: CAMPERS_QUERY_KEY,
    queryFn: ({ pageParam }) => fetchCampers(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    retry: false,
  })
  const pages = campersQuery.data?.pages ?? []
  const campers = pages.flatMap((page) => page.items)
  const hasMore = pages.at(-1)?.nextCursor != null

  if (campersQuery.isPending) {
    return <FlowMessage message={t('neutral.camper.selection.loading')} />
  }

  if (campersQuery.isError) {
    return <FlowError message={t('neutral.camper.selection.error')} />
  }

  if (campers.length === 0) {
    return <Navigate to="/campers/new" replace />
  }

  if (campers.length === 1 && !hasMore) {
    return <Navigate to={`/campers/${campers[0].id}`} replace />
  }

  return (
    <PageContainer className="max-w-3xl justify-start">
      <section className="rounded-xl border border-border bg-card p-6">
        <div className="mb-5">
          <h1 className="page-title">{t('neutral.camper.selection.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('neutral.camper.selection.description')}</p>
        </div>
        <ul className="grid gap-3 sm:grid-cols-2" aria-label={t('neutral.camper.selection.title')}>
          {campers.map((camper) => (
            <li key={camper.id}>
              <div className="rounded-lg border border-border bg-background p-4 transition-colors hover:border-ring">
                <Link
                  className="block rounded-lg focus-visible:outline-2 focus-visible:outline-ring"
                  to={`/campers/${camper.id}`}
                >
                  <span className="block font-semibold">{camper.name}</span>
                  <span className="mt-1 block text-sm text-muted-foreground">
                    {vehicleKindLabel(camper.vehicleKind)}
                  </span>
                </Link>
                <Link
                  className="mt-3 inline-block text-sm text-muted-foreground underline underline-offset-4"
                  to={`/campers/${camper.id}/profile`}
                >
                  {t('neutral.camper.profile.open')}
                </Link>
              </div>
            </li>
          ))}
        </ul>
        <div className="mt-5 flex flex-wrap gap-3">
          <Button render={<Link to="/campers/new" />}>
            {t('neutral.camper.selection.addCamper')}
          </Button>
          {hasMore && (
            <Button
              type="button"
              variant="outline"
              onClick={() => campersQuery.fetchNextPage()}
              disabled={campersQuery.isFetchingNextPage}
            >
              {campersQuery.isFetchingNextPage
                ? t('neutral.camper.selection.loadingMore')
                : t('neutral.camper.selection.loadMore')}
            </Button>
          )}
        </div>
      </section>
    </PageContainer>
  )
}

function CreateCamperPage() {
  return (
    <PageContainer className="max-w-xl justify-start">
      <p>
        <Link className="text-sm text-muted-foreground underline underline-offset-4" to="/campers">
          {t('neutral.camper.dashboard.allCampers')}
        </Link>
      </p>
      <CreateCamperForm />
    </PageContainer>
  )
}

function CamperProfilePage() {
  const { camperId = '' } = useParams()
  return (
    <PageContainer className="max-w-4xl justify-start">
      <BackToCampers />
      <Link
        className="self-start text-sm text-muted-foreground underline underline-offset-4"
        to={`/campers/${camperId}`}
      >
        {t('neutral.camper.profile.backToDashboard')}
      </Link>
      <CamperProfileForm camperId={camperId} />
    </PageContainer>
  )
}

function CamperDashboardPage() {
  const { camperId = '' } = useParams()
  const dashboardQuery = useQuery({
    queryKey: [...CAMPER_DASHBOARD_QUERY_KEY, camperId],
    queryFn: () => fetchCamperDashboard(camperId),
    enabled: camperId.length > 0,
    retry: false,
  })

  if (dashboardQuery.isPending) {
    return <FlowMessage message={t('neutral.camper.dashboard.loading')} />
  }

  if (dashboardQuery.isError) {
    return (
      <PageContainer className="max-w-5xl justify-start">
        <FlowError message={t('neutral.camper.dashboard.error')} />
        <BackToCampers />
      </PageContainer>
    )
  }

  return <CamperDashboard dashboard={dashboardQuery.data} />
}

function CamperDashboard({ dashboard }: { dashboard: CamperDashboardResponse }) {
  const { camper, odometer, maintenance } = dashboard
  const description = [camper.brand, camper.model, camper.year].filter(Boolean).join(' · ')

  return (
    <PageContainer className="max-w-5xl justify-start">
      <BackToCampers />
      <Link
        className="self-start text-sm text-muted-foreground underline underline-offset-4"
        to="/campers/new"
      >
        {t('neutral.camper.selection.addCamper')}
      </Link>
      <header className="rounded-xl border border-border bg-card p-6">
        <p className="text-xs font-semibold uppercase tracking-[0.08em] text-muted-foreground">
          {t('neutral.camper.dashboard.title')}
        </p>
        <h1 className="page-title mt-2">{camper.name}</h1>
        <p className="text-sm text-muted-foreground">
          {[vehicleKindLabel(camper.vehicleKind), description].filter(Boolean).join(' · ')}
        </p>
      </header>
      <div className="flex flex-wrap gap-3">
        <Link
          className="inline-flex h-8 items-center justify-center rounded-lg border border-border px-3 text-sm font-medium hover:bg-muted"
          to={`/campers/${camper.id}/profile`}
        >
          {t('neutral.camper.profile.open')}
        </Link>
      </div>

      <OdometerPanel odometer={odometer} />
      <CamperDashboardManualPanel camperId={camper.id} />
      <EquipmentInventory camperId={camper.id} />

      <section aria-labelledby="maintenance-heading">
        <div className="mb-4 flex items-end justify-between gap-4">
          <h2 id="maintenance-heading" className="text-lg font-semibold">
            {t('neutral.camper.dashboard.maintenance')}
          </h2>
        </div>
        {maintenance.length === 0 ? (
          <p className="rounded-xl border border-border bg-card p-5 text-sm text-muted-foreground">
            {t('neutral.camper.dashboard.noMaintenance')}
          </p>
        ) : (
          <ul className="grid gap-3 lg:grid-cols-2">
            {maintenance.map((item) => (
              <MaintenanceItemCard key={item.id} camperId={camper.id} item={item} />
            ))}
          </ul>
        )}
      </section>
      <CreateMaintenanceForm camperId={camper.id} />
    </PageContainer>
  )
}

function MaintenanceItemCard({
  camperId,
  item,
}: {
  camperId: string
  item: CamperDashboardResponse['maintenance'][number]
}) {
  return (
    <li className="rounded-xl border border-border bg-card p-5">
      <h3 className="font-semibold">{item.name}</h3>
      {item.hasServiceHistory ? (
        <p className="mt-1 text-sm text-muted-foreground">
          {t('neutral.camper.dashboard.lastService')}: {formatDate(item.lastServiceOnUtc)}
        </p>
      ) : (
        <p className="mt-1 text-sm text-muted-foreground">
          <span className="font-medium text-foreground">{t('neutral.camper.dashboard.neverServiced')}</span>{' '}
          {t('neutral.camper.dashboard.noReminderBeforeService')}
        </p>
      )}
      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <AxisPanel
          title={t('neutral.camper.dashboard.timeAxis')}
          status={item.timeAxis.status}
          detail={item.timeAxis.dueOnUtc
            ? `${t('neutral.camper.dashboard.dueOn')}: ${formatDate(item.timeAxis.dueOnUtc)}`
            : null}
        />
        <AxisPanel
          title={t('neutral.camper.dashboard.odometerAxis')}
          status={item.odometerAxis.status}
          detail={odometerDetail(item.odometerAxis.dueAtKm, item.odometerAxis.kmRemaining)}
        />
      </div>
      <RecordMaintenanceLogForm camperId={camperId} maintenanceItemId={item.id} />
      <MaintenanceLogHistory camperId={camperId} maintenanceItemId={item.id} />
    </li>
  )
}

function OdometerPanel({
  odometer,
}: {
  odometer: CamperDashboardResponse['odometer']
}) {
  return (
    <section className="rounded-xl border border-border bg-card p-6" aria-labelledby="odometer-heading">
      <h2 id="odometer-heading" className="text-sm font-semibold">
        {t('neutral.camper.dashboard.odometerTitle')}
      </h2>
      {odometer === null ? (
        <p className="mt-2 text-sm text-muted-foreground">
          {t('neutral.camper.dashboard.noOdometer')}
        </p>
      ) : (
        <>
          <p className="numeral-display mt-2">{formatKilometers(odometer.readingKm)} km</p>
          <p className="text-sm text-muted-foreground">
            {odometer.isStale
              ? t('neutral.camper.dashboard.lastDatedReading')
              : t('neutral.camper.dashboard.recentReading')}
            {' · '}
            {formatDate(odometer.takenOnUtc)}
          </p>
          {odometer.isStale && (
            <p className="mt-3 text-sm text-muted-foreground">
              {t('neutral.camper.dashboard.staleOdometer')}
            </p>
          )}
        </>
      )}
    </section>
  )
}

function AxisPanel({
  title,
  status,
  detail,
}: {
  title: string
  status: CamperDashboardAxisStatus
  detail: string | null
}) {
  return (
    <div className="rounded-lg border border-border bg-background p-3">
      <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{title}</p>
      <p className={`mt-1 text-sm font-medium ${statusTextClass(status)}`}>{axisStatusLabel(status)}</p>
      {detail && <p className="mt-1 text-xs text-muted-foreground">{detail}</p>}
    </div>
  )
}

function FlowMessage({ message }: { message: string }) {
  return (
    <PageContainer>
      <p role="status" className="text-sm text-muted-foreground">{message}</p>
    </PageContainer>
  )
}

function FlowError({ message }: { message: string }) {
  return (
    <p role="alert" className="rounded-xl border border-border bg-background p-5 text-sm text-danger-text">
      {message}
    </p>
  )
}

function BackToCampers() {
  return (
    <Link className="text-sm text-muted-foreground underline underline-offset-4" to="/campers">
      {t('neutral.camper.dashboard.allCampers')}
    </Link>
  )
}

function vehicleKindLabel(vehicleKind: number): string {
  switch (vehicleKind) {
    case 0:
      return t('neutral.camper.create.motorhome')
    case 1:
      return t('neutral.camper.create.van')
    case 2:
      return t('neutral.camper.create.coachbuilt')
    case 3:
      return t('neutral.camper.create.alcove')
    case 4:
      return t('neutral.camper.create.caravan')
    default:
      return t('neutral.camper.dashboard.otherVehicle')
  }
}

function axisStatusLabel(status: CamperDashboardAxisStatus): string {
  switch (status) {
    case 'NotScheduled':
      return t('neutral.camper.dashboard.status.notScheduled')
    case 'NotConfigured':
      return t('neutral.camper.dashboard.status.notConfigured')
    case 'Unknown':
      return t('neutral.camper.dashboard.status.unknown')
    case 'Ok':
      return t('neutral.camper.dashboard.status.ok')
    case 'DueSoon':
      return t('neutral.camper.dashboard.status.dueSoon')
    case 'Overdue':
      return t('neutral.camper.dashboard.status.overdue')
  }
}

function statusTextClass(status: CamperDashboardAxisStatus): string {
  if (status === 'Overdue') {
    return 'text-danger-text'
  }

  return status === 'Unknown' || status === 'NotScheduled' || status === 'NotConfigured'
    ? 'text-muted-foreground'
    : 'text-foreground'
}

function odometerDetail(dueAtKm: number | null, remainingKm: number | null): string | null {
  if (dueAtKm === null) {
    return null
  }

  const target = `${t('neutral.camper.dashboard.dueAt')} ${formatKilometers(dueAtKm)} km`
  if (remainingKm === null) {
    return target
  }

  if (remainingKm <= 0) {
    if (remainingKm === 0) {
      return `${target} · ${t('neutral.camper.dashboard.thresholdReached')}`
    }

    return `${target} · ${t('neutral.camper.dashboard.passedBy')} ${formatKilometers(Math.abs(remainingKm))} km`
  }

  return `${target} · ${t('neutral.camper.dashboard.remaining')} ${formatKilometers(remainingKm)} km`
}

function formatKilometers(value: number): string {
  return new Intl.NumberFormat('it-IT').format(value)
}

function formatDate(value: string | null): string {
  if (value === null) {
    return ''
  }

  return new Intl.DateTimeFormat('it-IT', {
    dateStyle: 'medium',
    timeZone: 'UTC',
  }).format(new Date(`${value}T00:00:00Z`))
}
