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
  fetchCamperProfile,
  fetchCampers,
  type CamperDashboardAxisStatus,
  type CamperDashboardResponse,
} from '@/lib/api'
import {
  CAMPER_DASHBOARD_QUERY_KEY,
  CAMPERS_QUERY_KEY,
  camperProfileQueryKey,
} from '@/features/campers/queryKeys'

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
    <PageContainer size="wide" className="justify-start">
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
    <PageContainer size="form" className="justify-start">
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
    <PageContainer size="wide" className="justify-start">
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
      <PageContainer size="wide" className="justify-start">
        <FlowError message={t('neutral.camper.dashboard.error')} />
        <BackToCampers />
      </PageContainer>
    )
  }

  return <CamperDashboard dashboard={dashboardQuery.data} />
}

function CamperDashboard({ dashboard }: { dashboard: CamperDashboardResponse }) {
  const { camper, odometer, maintenance } = dashboard
  const profileQuery = useQuery({
    queryKey: camperProfileQueryKey(camper.id),
    queryFn: () => fetchCamperProfile(camper.id),
    enabled: camper.id.length > 0,
    retry: false,
  })
  const profile = profileQuery.data?.profile
  const description = [camper.brand, camper.model, camper.year].filter(Boolean).join(' · ')

  return (
    <PageContainer size="dashboard" className="justify-start gap-8">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <BackToCampers />
          <span className="text-muted-foreground">·</span>
          <Link
            className="text-sm font-medium text-muted-foreground underline underline-offset-4 hover:text-foreground"
            to="/campers/new"
          >
            {t('neutral.camper.selection.addCamper')}
          </Link>
        </div>
        <div className="flex flex-wrap items-center gap-2.5">
          <Link
            className="inline-flex h-10 items-center justify-center rounded-full border border-border-strong bg-secondary px-4 text-sm font-semibold text-secondary-foreground shadow-2xs transition-colors hover:bg-muted"
            to={`/campers/${camper.id}/profile`}
          >
            {t('neutral.camper.profile.open')}
          </Link>
          <Link
            className="inline-flex h-10 items-center justify-center rounded-full bg-primary px-5 text-sm font-semibold text-primary-foreground shadow-xs transition-colors hover:bg-primary/90"
            to="/trips/new"
          >
            + Nuovo viaggio
          </Link>
        </div>
      </div>

      <header className="overflow-hidden rounded-3xl border border-border bg-card shadow-xs">
        <div className="grid gap-6 p-6 sm:p-8 lg:grid-cols-[minmax(0,1.2fr)_minmax(18rem,0.8fr)] lg:items-center">
          <div className="space-y-5">
            <div className="flex flex-wrap items-center gap-2.5">
              <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary px-3 py-1 text-xs font-semibold text-secondary-foreground">
                <span className="size-2 rounded-full bg-primary" aria-hidden="true" />
                {t('neutral.camper.dashboard.title')} · {vehicleKindLabel(camper.vehicleKind)}
              </span>
              {profile?.plateNumber && (
                <span className="inline-flex items-center rounded-lg border border-border-strong bg-background px-2.5 py-0.5 font-mono text-xs font-bold tracking-wider text-foreground">
                  {profile.plateNumber}
                </span>
              )}
            </div>

            <div>
              <h1 className="page-title">{camper.name}</h1>
              {description && (
                <p className="mt-1 text-base font-medium text-muted-foreground">
                  {description}
                </p>
              )}
            </div>

            <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <div className="rounded-2xl border border-border bg-background p-3.5">
                <dt className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Lunghezza
                </dt>
                <dd className="mt-1 text-base font-bold text-foreground">
                  {profile?.lengthMm ? `${formatKilometers(profile.lengthMm)} mm` : '—'}
                </dd>
                <dd className="mt-0.5 text-xs text-muted-foreground">
                  {profile?.widthMm ? `Largh. ${formatKilometers(profile.widthMm)} mm` : 'Ingombro'}
                </dd>
              </div>
              <div className="rounded-2xl border border-border bg-background p-3.5">
                <dt className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Altezza
                </dt>
                <dd className="mt-1 text-base font-bold text-foreground">
                  {profile?.heightMm ? `${formatKilometers(profile.heightMm)} mm` : '—'}
                </dd>
                <dd className="mt-0.5 text-xs text-muted-foreground">
                  Fuori tutto
                </dd>
              </div>
              <div className="rounded-2xl border border-border bg-background p-3.5">
                <dt className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Acqua chiara
                </dt>
                <dd className="mt-1 text-base font-bold text-foreground">
                  {profile?.freshWaterL ? `${profile.freshWaterL} L` : '—'}
                </dd>
                <dd className="mt-0.5 text-xs text-muted-foreground">
                  {profile?.greyWaterL ? `Grigie ${profile.greyWaterL} L` : 'Serbatoi'}
                </dd>
              </div>
              <div className="rounded-2xl border border-border bg-background p-3.5">
                <dt className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Autonomia
                </dt>
                <dd className="mt-1 text-base font-bold text-foreground">
                  {profile?.fuelTankL ? `${profile.fuelTankL} L` : '—'}
                </dd>
                <dd className="mt-0.5 text-xs text-muted-foreground">
                  {profile?.gasKg ? `Gas ${profile.gasKg} kg` : ' Impianto'}
                </dd>
              </div>
            </dl>
          </div>

          <div className="space-y-4">
            <CamperHeroIllustration />
            <OdometerPanel odometer={odometer} />
          </div>
        </div>
      </header>

      <CamperDashboardManualPanel camperId={camper.id} />
      <EquipmentInventory camperId={camper.id} />

      <section aria-labelledby="maintenance-heading" className="space-y-4">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Cura del veicolo e scadenze
            </p>
            <h2 id="maintenance-heading" className="text-xl font-semibold">
              {t('neutral.camper.dashboard.maintenance')}
            </h2>
          </div>
        </div>
        {maintenance.length === 0 ? (
          <p className="rounded-2xl border border-border bg-card p-6 text-sm text-muted-foreground">
            {t('neutral.camper.dashboard.noMaintenance')}
          </p>
        ) : (
          <ul className="grid min-w-0 gap-4 lg:grid-cols-2">
            {maintenance.map((item) => (
              <MaintenanceItemCard key={item.id} camperId={camper.id} item={item} />
            ))}
          </ul>
        )}
        <CreateMaintenanceForm camperId={camper.id} />
      </section>
    </PageContainer>
  )
}

function CamperHeroIllustration() {
  return (
    <div
      aria-hidden="true"
      className="overflow-hidden rounded-2xl border border-border bg-muted p-4"
    >
      <svg viewBox="0 0 420 175" className="h-auto w-full">
        {/* Sole caldo terracotta */}
        <circle cx="342" cy="46" r="24" fill="var(--color-primary)" opacity="0.85" />
        {/* Montagne e colline sullo sfondo */}
        <path
          d="M0 138 L78 72 L152 126 L236 58 L328 132 L420 88 L420 175 L0 175 Z"
          fill="var(--color-surface-raised-2)"
        />
        <path
          d="M0 148 Q110 112 220 138 T420 130 L420 175 L0 175 Z"
          fill="var(--color-surface-raised)"
        />
        {/* Strada panoramica */}
        <line
          x1="24"
          y1="152"
          x2="396"
          y2="152"
          stroke="var(--color-border-strong)"
          strokeWidth="2.5"
          strokeDasharray="6 6"
        />
        {/* Silhouette editoriale del camper van */}
        <g transform="translate(88, 56)">
          <path
            d="M14 82 V30 C14 16 25 6 40 6 H166 C182 6 196 14 206 28 L228 56 L244 62 C250 64 254 70 254 76 V82 Z"
            fill="var(--color-card)"
            stroke="var(--color-foreground)"
            strokeWidth="3"
            strokeLinejoin="round"
          />
          {/* Fascia terracotta laterale */}
          <path d="M16 62 H252 V71 H16 Z" fill="var(--color-primary)" />
          {/* Finestrini */}
          <rect
            x="34"
            y="20"
            width="54"
            height="28"
            rx="6"
            fill="var(--color-surface-raised)"
            stroke="var(--color-foreground)"
            strokeWidth="2.5"
          />
          <rect
            x="100"
            y="20"
            width="48"
            height="28"
            rx="6"
            fill="var(--color-surface-raised)"
            stroke="var(--color-foreground)"
            strokeWidth="2.5"
          />
          <path
            d="M162 20 H194 L214 48 H162 Z"
            fill="var(--color-surface-raised)"
            stroke="var(--color-foreground)"
            strokeWidth="2.5"
            strokeLinejoin="round"
          />
          {/* Ruote */}
          <circle
            cx="64"
            cy="84"
            r="16"
            fill="var(--color-surface-raised-2)"
            stroke="var(--color-foreground)"
            strokeWidth="3"
          />
          <circle cx="64" cy="84" r="6" fill="var(--color-card)" />
          <circle
            cx="202"
            cy="84"
            r="16"
            fill="var(--color-surface-raised-2)"
            stroke="var(--color-foreground)"
            strokeWidth="3"
          />
          <circle cx="202" cy="84" r="6" fill="var(--color-card)" />
        </g>
      </svg>
    </div>
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
    <li className="min-w-0 rounded-2xl border border-border bg-card p-5 shadow-2xs">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <h3 className="text-base font-semibold">{item.name}</h3>
        {item.hasServiceHistory ? (
          <span className="rounded-full bg-secondary px-3 py-0.5 text-xs font-medium text-secondary-foreground">
            {t('neutral.camper.dashboard.lastService')}: {formatDate(item.lastServiceOnUtc)}
          </span>
        ) : (
          <span className="rounded-full bg-secondary px-3 py-0.5 text-xs font-medium text-secondary-foreground">
            {t('neutral.camper.dashboard.neverServiced')}
          </span>
        )}
      </div>
      {!item.hasServiceHistory && (
        <p className="mt-1 text-xs text-muted-foreground">
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
      <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-border pt-3">
        <RecordMaintenanceLogForm camperId={camperId} maintenanceItemId={item.id} />
        <MaintenanceLogHistory camperId={camperId} maintenanceItemId={item.id} />
      </div>
    </li>
  )
}

function OdometerPanel({
  odometer,
}: {
  odometer: CamperDashboardResponse['odometer']
}) {
  return (
    <section
      className="rounded-2xl border border-border bg-background p-4"
      aria-labelledby="odometer-heading"
    >
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="odometer-heading" className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          {t('neutral.camper.dashboard.odometerTitle')}
        </h2>
        {odometer !== null && (
          <span className="text-xs text-muted-foreground">
            {odometer.isStale
              ? t('neutral.camper.dashboard.lastDatedReading')
              : t('neutral.camper.dashboard.recentReading')}
            {' · '}
            {formatDate(odometer.takenOnUtc)}
          </span>
        )}
      </div>
      {odometer === null ? (
        <p className="mt-1.5 text-sm text-muted-foreground">
          {t('neutral.camper.dashboard.noOdometer')}
        </p>
      ) : (
        <>
          <p className="numeral-display mt-1">{formatKilometers(odometer.readingKm)} km</p>
          {odometer.isStale && (
            <p className="mt-1.5 text-xs text-muted-foreground">
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
