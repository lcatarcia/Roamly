import { lazy, Suspense, type ChangeEvent } from 'react'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, Navigate, Route, Routes, useParams } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { PageContainer } from '@/components/layout/PageContainer'
import { t } from '@/i18n/catalog'
import { CreateCamperForm } from '@/features/campers/CreateCamperForm'
import { CamperDashboardManualPanel } from '@/features/campers/CamperManualSection'
import { resizeImageFileToDataUrl } from '@/features/campers/camperImageUtils'
import { CamperProfileForm } from '@/features/campers/CamperProfileForm'
import { EquipmentInventory } from '@/features/campers/EquipmentInventory'
import {
  CreateMaintenanceForm,
  MaintenanceItemActions,
  MaintenanceLogHistory,
} from '@/features/campers/MaintenanceForms'
import {
  fetchCamperDashboard,
  fetchCamperProfile,
  fetchCampers,
  updateCamperProfile,
  type CamperDashboardAxisStatus,
  type CamperDashboardResponse,
  type CamperProfile,
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
const SavedPlacesPage = lazy(() => import('@/features/places/SavedPlacesPage')
  .then((module) => ({ default: module.SavedPlacesPage })))

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
      <Route
        path="/places"
        element={(
          <Suspense fallback={<FlowMessage message={t('neutral.savedPlaces.loading')} />}>
            <SavedPlacesPage />
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
  const queryClient = useQueryClient()
  const profileQuery = useQuery({
    queryKey: camperProfileQueryKey(camper.id),
    queryFn: () => fetchCamperProfile(camper.id),
    enabled: camper.id.length > 0,
    retry: false,
  })
  const profile = profileQuery.data?.profile
  const profileEtag = profileQuery.data?.etag
  const description = [camper.brand, camper.model, camper.year].filter(Boolean).join(' · ')

  const heroPhotoMutation = useMutation({
    mutationFn: async (nextHeroImage: string | null) => {
      if (!profile || !profileEtag) {
        throw new Error('Profilo non ancora pronto.')
      }

      const nextViewImages = {
        side: profile.manualViewImages?.side ?? null,
        front: profile.manualViewImages?.front ?? null,
        top: profile.manualViewImages?.top ?? null,
        hero: nextHeroImage,
      }

      return updateCamperProfile(
        camper.id,
        {
          name: profile.name,
          brand: profile.brand,
          model: profile.model,
          year: profile.year,
          plateNumber: profile.plateNumber,
          vehicleKind: profile.vehicleKind,
          lengthMm: profile.lengthMm,
          widthMm: profile.widthMm,
          heightMm: profile.heightMm,
          kerbWeightKg: profile.kerbWeightKg,
          maxWeightKg: profile.maxWeightKg,
          freshWaterL: profile.freshWaterL,
          greyWaterL: profile.greyWaterL,
          fuelTankL: profile.fuelTankL,
          gasKg: profile.gasKg,
          fuelKind: profile.fuelKind,
          averageConsumptionLPer100Km: profile.averageConsumptionLPer100Km,
          manualSourceName: profile.manualSourceName ?? null,
          manualSourceUrl: profile.manualSourceUrl ?? null,
          manualMeasurements: profile.manualMeasurements ?? [],
          manualViewImages: nextViewImages,
        },
        profileEtag,
      )
    },
    onSuccess: async (result) => {
      queryClient.setQueryData(camperProfileQueryKey(camper.id), {
        profile: result.value,
        etag: result.etag,
      })
      await queryClient.invalidateQueries({ queryKey: CAMPER_DASHBOARD_QUERY_KEY })
    },
  })

  async function handleHeroFileSelected(file: File) {
    const dataUrl = await resizeImageFileToDataUrl(file)
    if (!dataUrl) {
      return
    }
    heroPhotoMutation.mutate(dataUrl)
  }

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
            <CamperHeroMedia
              camperName={camper.name}
              vehicleKind={camper.vehicleKind}
              profile={profile}
              canEditPhoto={Boolean(profile && profileEtag)}
              isSavingPhoto={heroPhotoMutation.isPending}
              onUploadPhoto={(file) => {
                void handleHeroFileSelected(file)
              }}
              onResetPhoto={() => heroPhotoMutation.mutate(null)}
            />
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

function CamperHeroMedia({
  camperName,
  vehicleKind,
  profile,
  canEditPhoto,
  isSavingPhoto,
  onUploadPhoto,
  onResetPhoto,
}: {
  camperName: string
  vehicleKind: number
  profile?: CamperProfile
  canEditPhoto: boolean
  isSavingPhoto: boolean
  onUploadPhoto: (file: File) => void
  onResetPhoto: () => void
}) {
  const customHeroUrl =
    profile?.manualViewImages?.hero ?? profile?.manualViewImages?.side ?? null
  const hasExplicitHero = Boolean(profile?.manualViewImages?.hero)
  const fileInputId = `camper-hero-photo-${profile?.id ?? 'main'}`

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) {
      return
    }
    onUploadPhoto(file)
  }

  return (
    <div className="space-y-2.5 overflow-hidden rounded-2xl border border-border bg-muted p-3.5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          {customHeroUrl ? 'Foto del tuo camper' : `Profilo tecnico · ${vehicleKindLabel(vehicleKind)}`}
        </span>
        {canEditPhoto && (
          <div className="flex flex-wrap items-center gap-1.5">
            <label
              htmlFor={fileInputId}
              className="inline-flex h-7 cursor-pointer items-center justify-center rounded-full border border-border-strong bg-secondary px-3 text-xs font-semibold text-secondary-foreground shadow-2xs transition-colors hover:bg-muted"
            >
              {isSavingPhoto
                ? 'Salvataggio…'
                : customHeroUrl
                  ? 'Cambia foto'
                  : 'Carica foto'}
            </label>
            <input
              id={fileInputId}
              type="file"
              accept="image/png,image/jpeg,image/webp"
              className="sr-only"
              disabled={isSavingPhoto}
              aria-label="Carica foto del camper"
              onChange={handleFileChange}
            />
            {hasExplicitHero && (
              <Button
                type="button"
                variant="outline"
                size="xs"
                disabled={isSavingPhoto}
                onClick={onResetPhoto}
              >
                Rimuovi foto
              </Button>
            )}
          </div>
        )}
      </div>

      {customHeroUrl ? (
        <div className="overflow-hidden rounded-xl border border-border bg-background">
          <img
            src={customHeroUrl}
            alt={`Foto di ${camperName}`}
            className="h-44 w-full object-cover sm:h-48"
          />
        </div>
      ) : (
        <div
          aria-hidden="true"
          className="overflow-hidden rounded-xl border border-border bg-background px-3 py-2"
        >
          <svg viewBox="0 0 540 205" className="h-auto w-full text-foreground">
            {/* Griglia di riferimento leggera */}
            <line x1="28" y1="36" x2="512" y2="36" stroke="currentColor" strokeWidth="1" strokeDasharray="3 5" opacity="0.18" />
            <line x1="28" y1="168" x2="512" y2="168" stroke="currentColor" strokeWidth="1.5" strokeDasharray="6 6" opacity="0.35" />

            {/* Quota superiore lunghezza */}
            <g className="text-muted-foreground" stroke="currentColor" fill="currentColor" strokeWidth="1.2">
              <line x1="48" y1="24" x2="492" y2="24" />
              <line x1="48" y1="18" x2="48" y2="30" />
              <line x1="492" y1="18" x2="492" y2="30" />
              <text x="270" y="18" textAnchor="middle" fontSize="11" fontWeight="600" stroke="none">
                {profile?.lengthMm
                  ? `L ${formatKilometers(profile.lengthMm)} mm`
                  : 'Vista laterale tecnica'}
                {profile?.heightMm ? ` · H ${formatKilometers(profile.heightMm)} mm` : ''}
              </text>
            </g>

            {/* Disegno tecnico dettagliato del camper */}
            <g
              transform="translate(0, -42)"
              stroke="currentColor"
              strokeWidth="2.2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              {/* Oblò sul tetto */}
              <path d="M110 82h44l5 8h-54l5-8Z" fill="var(--color-surface-raised)" />
              <path d="M262 82h52l5 8h-62l5-8Z" fill="var(--color-surface-raised)" />
              {/* Scocca principale */}
              <path
                d="M48 202V104c0-12 9-21 21-21h270c26 0 47 10 64 30l42 47 41 11c10 3 17 11 17 21v10H48Z"
                fill="var(--color-card)"
              />
              {/* Fascia inferiore e paraurti */}
              <path d="M44 191h463v12c0 3-2 5-5 5H49c-3 0-5-2-5-5v-12Z" fill="var(--color-surface-raised)" />
              {/* Tendalino esterno */}
              <rect x="162" y="96" width="172" height="8" rx="4" fill="var(--color-surface-raised-2)" />
              {/* Finestrino cabina e parabrezza */}
              <path d="M354 100v60h81l-36-41c-11-12-26-19-45-19Z" fill="var(--color-surface-raised)" />
              <line x1="387" y1="102" x2="387" y2="160" strokeWidth="1.8" />
              {/* Porta scorrevole e finestra dinette */}
              <rect x="238" y="106" width="98" height="95" rx="5" fill="none" strokeWidth="1.8" />
              <rect x="250" y="114" width="74" height="40" rx="5" fill="var(--color-surface-raised)" />
              <rect x="244" y="162" width="13" height="4" rx="2" fill="var(--color-primary)" />
              {/* Finestra posteriore zona notte */}
              <rect x="82" y="116" width="108" height="38" rx="5" fill="var(--color-surface-raised)" />
              {/* Griglie e sportelli tecnici */}
              <rect x="74" y="166" width="40" height="20" rx="3" fill="var(--color-surface-raised)" />
              <rect x="126" y="168" width="28" height="18" rx="3" fill="var(--color-surface-raised)" />
              {/* Fanali */}
              <path d="M484 174h16v12h-16a5 5 0 0 1-5-5v-2a5 5 0 0 1 5-5Z" fill="var(--color-primary)" />
              <rect x="48" y="150" width="6" height="28" rx="3" fill="var(--color-primary)" />
              {/* Ruote */}
              <circle cx="144" cy="205" r="22" fill="var(--color-surface-raised-2)" />
              <circle cx="144" cy="205" r="10" fill="var(--color-card)" />
              <circle cx="414" cy="205" r="22" fill="var(--color-surface-raised-2)" />
              <circle cx="414" cy="205" r="10" fill="var(--color-card)" />
            </g>
          </svg>
        </div>
      )}
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
  const isRecurring = item.isRecurring ?? true
  return (
    <li className="min-w-0 rounded-2xl border border-border bg-card p-5 shadow-2xs">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <h3 className="text-base font-semibold">{item.name}</h3>
        <div className="flex flex-wrap items-center gap-1.5">
          {!isRecurring && (
            <span className="rounded-full bg-secondary px-3 py-0.5 text-xs font-medium text-secondary-foreground">
              {t('neutral.camper.dashboard.nonPeriodicBadge')}
            </span>
          )}
          {item.lastServiceIsUnderWarranty === true && (
            <span className="rounded-full bg-secondary px-3 py-0.5 text-xs font-medium text-secondary-foreground">
              {t('neutral.camper.maintenance.underWarrantyBadge')}
            </span>
          )}
          {!isRecurring && item.lastServiceIsUnderWarranty === false && (
            <span className="rounded-full bg-secondary px-3 py-0.5 text-xs font-medium text-secondary-foreground">
              {t('neutral.camper.maintenance.outOfWarrantyBadge')}
            </span>
          )}
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
      </div>
      {!item.hasServiceHistory && (
        <p className="mt-1 text-xs text-muted-foreground">
          {t('neutral.camper.dashboard.noReminderBeforeService')}
        </p>
      )}
      {isRecurring && (
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
      )}
      <div className="mt-4 flex flex-wrap items-center gap-2 border-t border-border pt-3">
        <MaintenanceItemActions
          camperId={camperId}
          maintenanceItemId={item.id}
          maintenanceName={item.name}
        />
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
