/**
 * Client HTTP minimale verso le API Roamly.
 *
 * Flusso CSRF (SECURITY.md §1, ADR-0003): ogni richiesta non-GET porta
 * l'header custom `X-Roamly-Request: 1` (prima barriera) E il token
 * antiforgery ottenuto da `GET /api/v1/csrf-token`, rimandato come header
 * `X-XSRF-TOKEN` (seconda barriera). I token sono legati all'identita' della
 * richiesta (anonimo vs autenticato): vanno rifatturati dopo ogni login e
 * ogni logout, prima della chiamata non-GET successiva. Per questo NON si
 * mette in cache il token oltre la singola operazione: `fetchCsrfToken()`
 * viene chiamato appena prima di ogni mutazione.
 */

const JSON_HEADERS = { 'Content-Type': 'application/json' } as const
const ROAMLY_HEADER = { 'X-Roamly-Request': '1' } as const

export class ApiError extends Error {
  readonly status: number
  readonly problem: unknown

  constructor(status: number, message: string, problem: unknown) {
    super(message)
    this.status = status
    this.problem = problem
  }
}

/** GET /api/v1/csrf-token — va richiamato prima di OGNI richiesta non-GET. */
export async function fetchCsrfToken(): Promise<string> {
  const response = await fetch('/api/v1/csrf-token', {
    method: 'GET',
    credentials: 'include',
  })

  if (!response.ok) {
    throw new ApiError(response.status, 'Impossibile ottenere il token di sicurezza.', null)
  }

  const body = (await response.json()) as { token: string }
  return body.token
}

interface MutationOptions {
  method: 'POST' | 'PUT' | 'DELETE' | 'PATCH'
  body?: unknown
  etag?: string
}

/** Esegue una richiesta non-GET con CSRF e precondizione opzionale gia' applicati. */
async function sendMutation(path: string, options: MutationOptions): Promise<Response> {
  const csrfToken = await fetchCsrfToken()

  const response = await fetch(path, {
    method: options.method,
    credentials: 'include',
    headers: {
      ...JSON_HEADERS,
      ...ROAMLY_HEADER,
      'X-XSRF-TOKEN': csrfToken,
      ...(options.etag ? { 'If-Match': options.etag } : {}),
    },
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  })

  if (!response.ok) {
    throw await toApiError(response, 'Si e\' verificato un errore imprevisto.')
  }

  return response
}

/** Esegue una richiesta non-GET e deserializza il corpo quando presente. */
async function mutate<T>(path: string, options: MutationOptions): Promise<T> {
  const response = await sendMutation(path, options)
  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

/** Variante che conserva anche l'ETag restituito dopo una lettura o modifica. */
async function mutateVersioned<T>(
  path: string,
  options: MutationOptions,
): Promise<{ value: T; etag: string }> {
  const response = await sendMutation(path, options)
  const etag = response.headers.get('ETag')
  if (!etag) {
    throw new ApiError(response.status, 'La risposta non contiene il token di aggiornamento.', null)
  }

  return {
    value: response.status === 204 ? undefined as T : (await response.json()) as T,
    etag,
  }
}

export interface LoginProfile {
  id: string
  email: string
  displayName: string | null
  sessionMode?: 'limited'
}

export interface MeResponse {
  id: string
  email: string
  displayName: string | null
  reportingCurrency: string
  mustChangePassword: boolean
}

export interface NormalAccountSession {
  mode: 'normal'
  id: string
  email: string
  displayName: string | null
  reportingCurrency?: string
  mustChangePassword: boolean
}

export interface LimitedAccountSession {
  mode: 'limited'
}

export type AccountSession = NormalAccountSession | LimitedAccountSession

export interface AccountDeletionStatus {
  status: 'scheduled'
  deletionRequestedAtUtc: string
  deletionScheduledForUtc: string
}

export interface AccountDeletionPreview {
  generatedAtUtc: string
  entityCounts: { entityType: string; count: number }[]
  totalCount: number
}

export interface AccountDeletionSchedule {
  deletionRequestedAtUtc: string
  deletionScheduledForUtc: string
}

export interface AuthenticationMessageResponse {
  message: string
}

/** POST /api/v1/auth/login — credenziali non valide restituiscono un messaggio
 * generico (nessun oracolo su quale campo sia sbagliato). */
export function login(email: string, password: string): Promise<LoginProfile> {
  return mutate<LoginProfile>('/api/v1/auth/login', {
    method: 'POST',
    body: { email, password },
  })
}

/** POST /api/v1/auth/password/change — richiede password attuale e nuova password. */
export function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  return mutate<void>('/api/v1/auth/password/change', {
    method: 'POST',
    body: { currentPassword, newPassword },
  })
}

/** POST /api/v1/auth/register — mutazione CSRF protetta, risposta generica. */
export function register(
  email: string,
  password: string,
  displayName?: string,
): Promise<AuthenticationMessageResponse> {
  return mutate<AuthenticationMessageResponse>('/api/v1/auth/register', {
    method: 'POST',
    body: { email, password, displayName },
  })
}

/** POST /api/v1/auth/email/confirm — conferma email con ID e token Identity. */
export function confirmEmail(
  userId: string,
  token: string,
): Promise<AuthenticationMessageResponse> {
  return mutate<AuthenticationMessageResponse>('/api/v1/auth/email/confirm', {
    method: 'POST',
    body: { userId, token },
  })
}

/** POST /api/v1/auth/email/resend-confirmation — reinvio locale con risposta generica. */
export function resendEmailConfirmation(email: string): Promise<AuthenticationMessageResponse> {
  return mutate<AuthenticationMessageResponse>('/api/v1/auth/email/resend-confirmation', {
    method: 'POST',
    body: { email },
  })
}

export interface CreateCamperResponse {
  id: string
  name: string
  vehicleKind: number
}

export type CamperManualView = 'side' | 'front' | 'top'

export interface CamperManualViewImages {
  side?: string | null
  front?: string | null
  top?: string | null
}

export interface CamperManualMeasurement {
  id: string
  name: string
  value: string
  zone: string
  view: CamperManualView
  positionX: number
  positionY: number
  description?: string | null
  page?: number | null
}

export interface CamperManualParseRequest {
  sourceName?: string | null
  sourceUrl?: string | null
  pdfBase64?: string | null
  rawText?: string | null
}

export interface CamperManualParseResponse {
  sourceName: string
  sourceUrl: string | null
  brand: string | null
  model: string | null
  year: number | null
  vehicleKind: number | null
  fuelKind: number | null
  lengthMm: number | null
  widthMm: number | null
  heightMm: number | null
  kerbWeightKg: number | null
  maxWeightKg: number | null
  freshWaterL: number | null
  greyWaterL: number | null
  fuelTankL: number | null
  gasKg: number | null
  averageConsumptionLPer100Km: number | null
  measurements: CamperManualMeasurement[]
  warnings: string[]
}

export interface CamperProfile {
  id: string
  name: string
  brand: string | null
  model: string | null
  year: number | null
  plateNumber: string | null
  vehicleKind: number
  lengthMm: number | null
  widthMm: number | null
  heightMm: number | null
  kerbWeightKg: number | null
  maxWeightKg: number | null
  freshWaterL: number | null
  greyWaterL: number | null
  fuelTankL: number | null
  gasKg: number | null
  fuelKind: number | null
  averageConsumptionLPer100Km: number | null
  manualSourceName?: string | null
  manualSourceUrl?: string | null
  manualMeasurements?: CamperManualMeasurement[] | null
  manualViewImages?: CamperManualViewImages | null
}

export type CamperProfileInput = Omit<CamperProfile, 'id'>

export interface EquipmentItem {
  id: string
  name: string
  category: number
  notes: string | null
  installedOnUtc: string | null
}

export interface EquipmentInput {
  name: string
  category: number
  notes: string | null
  installedOnUtc: string | null
}

export interface CamperListItem {
  id: string
  name: string
  vehicleKind: number
}

export interface PageResponse<T> {
  items: T[]
  nextCursor: string | null
}

export type CamperDashboardAxisStatus =
  | 'NotScheduled'
  | 'NotConfigured'
  | 'Unknown'
  | 'Ok'
  | 'DueSoon'
  | 'Overdue'

export interface CamperDashboardResponse {
  camper: {
    id: string
    name: string
    brand: string | null
    model: string | null
    year: number | null
    vehicleKind: number
  }
  odometer: {
    readingKm: number
    takenOnUtc: string
    isStale: boolean
  } | null
  maintenance: {
    id: string
    name: string
    hasServiceHistory: boolean
    lastServiceOnUtc: string | null
    timeAxis: {
      status: CamperDashboardAxisStatus
      dueOnUtc: string | null
    }
    odometerAxis: {
      status: CamperDashboardAxisStatus
      dueAtKm: number | null
      kmRemaining: number | null
    }
  }[]
}

export interface MaintenanceLog {
  id: string
  performedOnUtc: string
  odometerKm: number | null
  cost: { amount: number; currency: string } | null
  workshop: string | null
  notes: string | null
}

export interface MaintenanceLogInput {
  id: string
  performedOnUtc: string
  odometerKm?: number
  cost?: { amount: number; currency: string }
  workshop?: string
  notes?: string
}

export interface Money {
  amount: number
  currency: string
}

export interface CreateMaintenanceInput {
  id: string
  name: string
  category: number
  intervalKm?: number
  intervalMonths?: number
}

export interface TripSummary {
  id: string
  camperId: string
  name: string
  status: number
}

export interface TripDetails extends TripSummary {
  plannedStartOnUtc: string | null
  plannedEndOnUtc: string | null
  actualStartOnUtc: string | null
  actualEndOnUtc: string | null
  estimatedDistanceKm: string | null
  estimatedDurationMinutes: number | null
  fuelPricePerLiter: Money | null
  stops: TripStop[]
}

export interface TripStop {
  id: string
  sequenceNo: number
  name: string
  arrivalOnUtc: string | null
  departureOnUtc: string | null
  position: { latitude: number; longitude: number } | null
  notes: string | null
}

export interface TripUpdateInput {
  name: string
  plannedStartOnUtc: string | null
  plannedEndOnUtc: string | null
  estimatedDistanceKm: string | null
  estimatedDurationMinutes: number | null
  fuelPricePerLiter: Money | null
  stops: {
    id?: string | null
    name: string
    arrivalOnUtc: string | null
    departureOnUtc: string | null
    position: { latitude: number; longitude: number } | null
    notes: string | null
  }[]
}

export interface JournalEntry {
  id: string
  tripId: string
  tripStopId: string | null
  entryOnUtc: string
  title: string | null
  body: string
  createdAtUtc: string
  updatedAtUtc: string | null
  attachments: JournalAttachment[]
}

export interface ChecklistDetails {
  id: string
  tripId: string
  name: string
  kind: number
  items: ChecklistItemDetails[]
}

export interface ChecklistItemDetails {
  id: string
  text: string
  isDone: boolean
  sortOrder: number
}

export interface ChecklistInput {
  name: string
  kind: number
}

export interface ChecklistSeedPreview {
  name: string
  kind: number
  catalogVersion: string
  items: ChecklistSeedPreviewItem[]
}

export interface ChecklistSeedPreviewItem {
  templateKey: string
  text: string
}

export interface ChecklistSeedConfirmationInput {
  id: string
  kind: number
  items: { id: string; templateKey: string }[]
}

export interface ChecklistItemInput {
  text: string
  isDone: boolean
}

export interface JournalAttachment {
  id: string
  contentType: string
  sizeBytes: number
  createdAtUtc: string
}

export interface JournalEntryInput {
  entryOnUtc: string
  title: string | null
  body: string
}

export interface JournalAttachmentDownload {
  url: string
  expiresAtUtc: string
}

export interface ExpenseDetails {
  id: string
  camperId: string
  tripId: string | null
  amount: { amount: number; currency: string }
  category: number
  incurredOnUtc: string
  description: string | null
  odometerKm: number | null
}

export interface ExpenseInput {
  amount: { amount: number; currency: string }
  category: number
  incurredOnUtc: string
  description: string | null
  odometerKm: number | null
  tripId: string
}

const PAGE_SIZE = 50

export function fetchTrips(after: string | null = null): Promise<PageResponse<TripSummary>> {
  return getJson(
    pagePath('/api/v1/trips', after),
    'Non e’ stato possibile leggere i viaggi.',
  )
}

export function createTrip(
  camperId: string,
  input: { id: string; name: string; stops: { name: string }[] },
): Promise<TripDetails> {
  return mutate(`/api/v1/campers/${encodeURIComponent(camperId)}/trips`, {
    method: 'POST',
    body: input,
  })
}

export async function fetchTrip(tripId: string): Promise<{ trip: TripDetails; etag: string }> {
  const response = await fetch(`/api/v1/trips/${encodeURIComponent(tripId)}`, {
    method: 'GET',
    credentials: 'include',
  })
  if (!response.ok) {
    throw await toApiError(response, 'Non e’ stato possibile leggere il viaggio.')
  }

  const etag = response.headers.get('ETag')
  if (!etag) {
    throw new ApiError(response.status, 'Il viaggio non contiene il token di aggiornamento.', null)
  }

  return { trip: (await response.json()) as TripDetails, etag }
}

/** PUT /api/v1/trips/{id} — sostituisce il viaggio e le tappe con If-Match. */
export function updateTrip(
  tripId: string,
  input: TripUpdateInput,
  etag: string,
): Promise<{ value: TripDetails; etag: string }> {
  return mutateVersioned(`/api/v1/trips/${encodeURIComponent(tripId)}`, {
    method: 'PUT',
    body: input,
    etag,
  })
}

/** POST /api/v1/trips/{id}/transitions — applica una transizione esplicita. */
export function transitionTrip(
  tripId: string,
  status: number,
  etag: string,
): Promise<{ value: TripDetails; etag: string }> {
  return mutateVersioned(`/api/v1/trips/${encodeURIComponent(tripId)}/transitions`, {
    method: 'POST',
    body: { status },
    etag,
  })
}

/** Legge una pagina di checklist e conserva l'ETag del viaggio che la protegge. */
export async function fetchTripChecklists(
  tripId: string,
  after: string | null = null,
): Promise<{ page: PageResponse<ChecklistDetails>; etag: string }> {
  const response = await fetch(
    pagePath(`/api/v1/trips/${encodeURIComponent(tripId)}/checklists`, after),
    { method: 'GET', credentials: 'include' },
  )
  if (!response.ok) {
    throw await toApiError(response, 'Non e’ stato possibile leggere le checklist del viaggio.')
  }

  const etag = response.headers.get('ETag')
  if (!etag) {
    throw new ApiError(response.status, 'Le checklist non contengono il token di aggiornamento.', null)
  }

  return { page: (await response.json()) as PageResponse<ChecklistDetails>, etag }
}

/** Mostra il catalogo attuale senza modificare il viaggio o creare checklist. */
export async function fetchChecklistSeedPreview(
  tripId: string,
  kind: number,
): Promise<{ value: ChecklistSeedPreview; etag: string }> {
  const response = await fetch(
    `/api/v1/trips/${encodeURIComponent(tripId)}/checklists/seed-preview?kind=${kind}`,
    { method: 'GET', credentials: 'include' },
  )
  if (!response.ok) {
    throw await toApiError(response, 'Non e’ stato possibile preparare l’anteprima checklist.')
  }

  const etag = response.headers.get('ETag')
  if (!etag) {
    throw new ApiError(response.status, 'L’anteprima non contiene il token di aggiornamento.', null)
  }

  return { value: (await response.json()) as ChecklistSeedPreview, etag }
}

/** Conferma la proposta attuale; il server ricostruisce ogni testo dal catalogo. */
export function confirmChecklistSeed(
  tripId: string,
  input: ChecklistSeedConfirmationInput,
  etag: string,
): Promise<{ value: ChecklistDetails; etag: string }> {
  return mutateVersioned(`/api/v1/trips/${encodeURIComponent(tripId)}/checklists/seed`, {
    method: 'POST',
    body: input,
    etag,
  })
}

/** Crea una checklist manuale con un ID COMB generato dal client. */
export function createTripChecklist(
  tripId: string,
  input: ChecklistInput & { id: string },
  etag: string,
): Promise<{ value: ChecklistDetails; etag: string }> {
  return mutateVersioned(`/api/v1/trips/${encodeURIComponent(tripId)}/checklists`, {
    method: 'POST',
    body: input,
    etag,
  })
}

/** Sostituisce nome e momento della checklist con la precondizione del Trip. */
export function updateTripChecklist(
  tripId: string,
  checklistId: string,
  input: ChecklistInput,
  etag: string,
): Promise<{ value: ChecklistDetails; etag: string }> {
  return mutateVersioned(
    `/api/v1/trips/${encodeURIComponent(tripId)}/checklists/${encodeURIComponent(checklistId)}`,
    { method: 'PUT', body: input, etag },
  )
}

/** Elimina una checklist; l'API restituisce 204 e il nuovo ETag del Trip. */
export function deleteTripChecklist(
  tripId: string,
  checklistId: string,
  etag: string,
): Promise<{ etag: string }> {
  return mutateVersioned(
    `/api/v1/trips/${encodeURIComponent(tripId)}/checklists/${encodeURIComponent(checklistId)}`,
    { method: 'DELETE', etag },
  )
}

/** Aggiunge una voce con un ID COMB generato dal client. */
export function createChecklistItem(
  tripId: string,
  checklistId: string,
  input: { id: string; text: string },
  etag: string,
): Promise<{ value: ChecklistDetails; etag: string }> {
  return mutateVersioned(
    `/api/v1/trips/${encodeURIComponent(tripId)}/checklists/${encodeURIComponent(checklistId)}/items`,
    { method: 'POST', body: input, etag },
  )
}

/** Sostituisce testo e stato di completamento della voce. */
export function updateChecklistItem(
  tripId: string,
  checklistId: string,
  itemId: string,
  input: ChecklistItemInput,
  etag: string,
): Promise<{ value: ChecklistDetails; etag: string }> {
  return mutateVersioned(
    `/api/v1/trips/${encodeURIComponent(tripId)}/checklists/${encodeURIComponent(checklistId)}/items/${encodeURIComponent(itemId)}`,
    { method: 'PUT', body: input, etag },
  )
}

/** Elimina una voce e restituisce la checklist aggiornata. */
export function deleteChecklistItem(
  tripId: string,
  checklistId: string,
  itemId: string,
  etag: string,
): Promise<{ value: ChecklistDetails; etag: string }> {
  return mutateVersioned(
    `/api/v1/trips/${encodeURIComponent(tripId)}/checklists/${encodeURIComponent(checklistId)}/items/${encodeURIComponent(itemId)}`,
    { method: 'DELETE', etag },
  )
}

/** Sostituisce l'ordine completo delle voci della checklist. */
export function reorderChecklistItems(
  tripId: string,
  checklistId: string,
  itemIds: string[],
  etag: string,
): Promise<{ value: ChecklistDetails; etag: string }> {
  return mutateVersioned(
    `/api/v1/trips/${encodeURIComponent(tripId)}/checklists/${encodeURIComponent(checklistId)}/items/order`,
    { method: 'PUT', body: { itemIds }, etag },
  )
}

/** Legge le spese di un camper e viaggio, mantenendo il cursore keyset. */
export function fetchTripExpenses(
  camperId: string,
  tripId: string,
  after: string | null = null,
): Promise<PageResponse<ExpenseDetails>> {
  const path = pagePath(
    `/api/v1/campers/${encodeURIComponent(camperId)}/expenses`,
    after,
  )
  const separator = path.includes('?') ? '&' : '?'
  return getJson(
    `${path}${separator}tripId=${encodeURIComponent(tripId)}`,
    'Non e’ stato possibile leggere le spese del viaggio.',
  )
}

/** GET /api/v1/expenses/{id} — restituisce anche l'ETag corrente. */
export async function fetchExpense(expenseId: string): Promise<{ expense: ExpenseDetails; etag: string }> {
  const response = await fetch(`/api/v1/expenses/${encodeURIComponent(expenseId)}`, {
    method: 'GET',
    credentials: 'include',
  })
  if (!response.ok) {
    throw await toApiError(response, 'Non e’ stato possibile leggere la spesa.')
  }

  const etag = response.headers.get('ETag')
  if (!etag) {
    throw new ApiError(response.status, 'La spesa non contiene il token di aggiornamento.', null)
  }

  return { expense: (await response.json()) as ExpenseDetails, etag }
}

/** Crea una spesa nella raccolta del camper; ogni spesa ha il proprio ETag. */
export function createExpense(
  camperId: string,
  input: ExpenseInput & { id: string },
): Promise<{ value: ExpenseDetails; etag: string }> {
  return mutateVersioned(`/api/v1/campers/${encodeURIComponent(camperId)}/expenses`, {
    method: 'POST',
    body: input,
  })
}

/** Sostituisce una spesa usando il relativo token If-Match. */
export function updateExpense(
  expenseId: string,
  input: ExpenseInput,
  etag: string,
): Promise<{ value: ExpenseDetails; etag: string }> {
  return mutateVersioned(`/api/v1/expenses/${encodeURIComponent(expenseId)}`, {
    method: 'PUT',
    body: input,
    etag,
  })
}

/** Elimina una spesa usando il relativo token If-Match. */
export function deleteExpense(
  expenseId: string,
  etag: string,
): Promise<void> {
  return mutate<void>(
    `/api/v1/expenses/${encodeURIComponent(expenseId)}`,
    { method: 'DELETE', etag },
  )
}

export function fetchJournalEntries(
  tripId: string,
  after: string | null = null,
): Promise<PageResponse<JournalEntry>> {
  return getJson(
    pagePath(`/api/v1/trips/${encodeURIComponent(tripId)}/journal-entries`, after),
    'Non e’ stato possibile leggere il Journal.',
  )
}

export function createJournalEntry(
  tripId: string,
  input: JournalEntryInput & { id: string },
  etag: string,
): Promise<{ value: JournalEntry; etag: string }> {
  return mutateVersioned(`/api/v1/trips/${encodeURIComponent(tripId)}/journal-entries`, {
    method: 'POST',
    body: input,
    etag,
  })
}

export function updateJournalEntry(
  tripId: string,
  entryId: string,
  input: JournalEntryInput,
  etag: string,
): Promise<{ value: JournalEntry; etag: string }> {
  return mutateVersioned(
    `/api/v1/trips/${encodeURIComponent(tripId)}/journal-entries/${encodeURIComponent(entryId)}`,
    { method: 'PUT', body: input, etag },
  )
}

export function deleteJournalEntry(
  tripId: string,
  entryId: string,
  etag: string,
): Promise<{ etag: string }> {
  return mutateVersioned(
    `/api/v1/trips/${encodeURIComponent(tripId)}/journal-entries/${encodeURIComponent(entryId)}`,
    { method: 'DELETE', etag },
  )
}

export async function uploadJournalAttachment(
  tripId: string,
  entryId: string,
  file: File,
  etag: string,
): Promise<{ value: JournalAttachment; etag: string }> {
  const csrfToken = await fetchCsrfToken()
  const path = journalAttachmentPath(tripId, entryId)
  const response = await fetch(path, {
    method: 'POST',
    credentials: 'include',
    headers: {
      ...ROAMLY_HEADER,
      'X-XSRF-TOKEN': csrfToken,
      'If-Match': etag,
    },
    body: createAttachmentFormData(file),
  })

  if (!response.ok) {
    throw await toApiError(response, 'Non e’ stato possibile caricare la foto.')
  }

  const responseEtag = response.headers.get('ETag')
  if (!responseEtag) {
    throw new ApiError(response.status, 'La risposta non contiene il token di aggiornamento.', null)
  }

  return { value: (await response.json()) as JournalAttachment, etag: responseEtag }
}

export async function fetchJournalAttachmentDownload(
  tripId: string,
  entryId: string,
  attachmentId: string,
): Promise<JournalAttachmentDownload> {
  const response = await fetch(
    `${journalAttachmentPath(tripId, entryId)}/${encodeURIComponent(attachmentId)}/download`,
    { method: 'GET', credentials: 'include', cache: 'no-store' },
  )
  if (!response.ok) {
    throw await toApiError(response, 'Non e’ stato possibile aprire la foto.')
  }

  return (await response.json()) as JournalAttachmentDownload
}

export function deleteJournalAttachment(
  tripId: string,
  entryId: string,
  attachmentId: string,
  etag: string,
): Promise<{ etag: string }> {
  return mutateVersioned(
    `${journalAttachmentPath(tripId, entryId)}/${encodeURIComponent(attachmentId)}`,
    { method: 'DELETE', etag },
  )
}

/** GET /api/v1/campers — restituisce una pagina keyset owner-scoped (R52). */
export function fetchCampers(after: string | null = null): Promise<PageResponse<CamperListItem>> {
  const query = new URLSearchParams({ limit: '50' })
  if (after) {
    query.set('after', after)
  }

  return getJson(`/api/v1/campers?${query.toString()}`, 'Impossibile leggere i camper.')
}

/** GET /api/v1/campers/{id}/dashboard — read model dedicato, senza widget Phase 2. */
export function fetchCamperDashboard(camperId: string): Promise<CamperDashboardResponse> {
  return getJson(
    `/api/v1/campers/${encodeURIComponent(camperId)}/dashboard`,
    'Impossibile leggere la dashboard del camper.',
  )
}

/** GET /api/v1/campers/{id} — profilo completo e ETag per il successivo PUT. */
export async function fetchCamperProfile(
  camperId: string,
): Promise<{ profile: CamperProfile; etag: string }> {
  const response = await fetch(`/api/v1/campers/${encodeURIComponent(camperId)}`, {
    method: 'GET',
    credentials: 'include',
  })

  if (!response.ok) {
    throw await toApiError(response, 'Impossibile leggere il profilo tecnico del camper.')
  }

  const etag = response.headers.get('ETag')
  if (!etag) {
    throw new ApiError(response.status, 'Il profilo non contiene il token di aggiornamento.', null)
  }

  return { profile: (await response.json()) as CamperProfile, etag }
}

/** PUT /api/v1/campers/{id} — aggiornamento completo protetto da If-Match e CSRF. */
export function updateCamperProfile(
  camperId: string,
  profile: CamperProfileInput,
  etag: string,
): Promise<{ value: CamperProfile; etag: string }> {
  return mutateVersioned(`/api/v1/campers/${encodeURIComponent(camperId)}`, {
    method: 'PUT',
    body: profile,
    etag,
  })
}

/** POST /api/v1/campers/{id}/manual/parse — estrae bozza tecnica e misure da PDF o URL esterno. */
export function parseCamperManual(
  camperId: string,
  request: CamperManualParseRequest,
): Promise<CamperManualParseResponse> {
  return mutate<CamperManualParseResponse>(
    `/api/v1/campers/${encodeURIComponent(camperId)}/manual/parse`,
    {
      method: 'POST',
      body: request,
    },
  )
}

/** GET .../maintenance/{id}/logs — storico keyset di una manutenzione. */
export function fetchMaintenanceLogs(
  camperId: string,
  maintenanceItemId: string,
  after: string | null = null,
): Promise<PageResponse<MaintenanceLog>> {
  const query = new URLSearchParams({ limit: '50' })
  if (after) {
    query.set('after', after)
  }
  return getJson(
    `/api/v1/campers/${encodeURIComponent(camperId)}/maintenance/${encodeURIComponent(maintenanceItemId)}/logs?${query.toString()}`,
    'Impossibile leggere lo storico degli interventi.',
  )
}

/** GET /equipment — pagina keyset owner-scoped. */
export function fetchEquipment(
  camperId: string,
  after: string | null = null,
): Promise<PageResponse<EquipmentItem>> {
  const query = new URLSearchParams({ limit: '50' })
  if (after) {
    query.set('after', after)
  }

  return getJson(
    `/api/v1/campers/${encodeURIComponent(camperId)}/equipment?${query.toString()}`,
    'Impossibile leggere l’equipaggiamento.',
  )
}

/** GET /equipment/{id} — dettaglio e ETag per la modifica o rimozione. */
export async function fetchEquipmentItem(
  camperId: string,
  equipmentId: string,
): Promise<{ equipment: EquipmentItem; etag: string }> {
  const response = await fetch(
    `/api/v1/campers/${encodeURIComponent(camperId)}/equipment/${encodeURIComponent(equipmentId)}`,
    { method: 'GET', credentials: 'include' },
  )
  if (!response.ok) {
    throw await toApiError(response, 'Impossibile leggere questo elemento di equipaggiamento.')
  }

  const etag = response.headers.get('ETag')
  if (!etag) {
    throw new ApiError(response.status, 'L’elemento non contiene il token di aggiornamento.', null)
  }

  return { equipment: (await response.json()) as EquipmentItem, etag }
}

/** POST /equipment — crea l'elemento con idempotenza IdentityBased e protezione CSRF. */
export function createEquipment(
  camperId: string,
  id: string,
  input: EquipmentInput,
): Promise<EquipmentItem> {
  return mutate(
    `/api/v1/campers/${encodeURIComponent(camperId)}/equipment`,
    { method: 'POST', body: { id, ...input } },
  )
}

/** PUT /equipment/{id} — modifica con If-Match obbligatorio. */
export function updateEquipment(
  camperId: string,
  equipmentId: string,
  input: EquipmentInput,
  etag: string,
): Promise<{ value: EquipmentItem; etag: string }> {
  return mutateVersioned(
    `/api/v1/campers/${encodeURIComponent(camperId)}/equipment/${encodeURIComponent(equipmentId)}`,
    { method: 'PUT', body: input, etag },
  )
}

/** DELETE /equipment/{id} — rimozione fisica con If-Match obbligatorio. */
export function deleteEquipment(camperId: string, equipmentId: string, etag: string): Promise<void> {
  return mutate(
    `/api/v1/campers/${encodeURIComponent(camperId)}/equipment/${encodeURIComponent(equipmentId)}`,
    { method: 'DELETE', etag },
  )
}

/** POST .../maintenance — crea una ricorrenza con ID COMB generato dal browser. */
export function createMaintenance(
  camperId: string,
  input: CreateMaintenanceInput,
): Promise<{ id: string; name: string }> {
  return mutate(`/api/v1/campers/${encodeURIComponent(camperId)}/maintenance`, {
    method: 'POST',
    body: input,
  })
}

/** POST .../maintenance/{id}/logs — registra un intervento immutabile. */
export function recordMaintenanceLog(
  camperId: string,
  maintenanceItemId: string,
  input: MaintenanceLogInput,
): Promise<MaintenanceLog> {
  return mutate(
    `/api/v1/campers/${encodeURIComponent(camperId)}/maintenance/${encodeURIComponent(maintenanceItemId)}/logs`,
    { method: 'POST', body: input },
  )
}

/** POST /api/v1/campers — l'identificativo COMB e' generato dal browser (ADR-0008). */
export function createCamper(
  id: string,
  name: string,
  vehicleKind: number,
): Promise<CreateCamperResponse> {
  return mutate<CreateCamperResponse>('/api/v1/campers', {
    method: 'POST',
    body: { id, name, vehicleKind },
  })
}

/** POST /api/v1/auth/logout — idempotente per costruzione. */
export function logout(): Promise<void> {
  return mutate<void>('/api/v1/auth/logout', { method: 'POST' })
}

/** GET /api/v1/me — riconosce solo il problema esatto della sessione limitata. */
export async function fetchMe(): Promise<AccountSession | null> {
  const response = await fetch('/api/v1/me', {
    method: 'GET',
    credentials: 'include',
  })

  if (response.status === 401) {
    return null
  }

  if (!response.ok) {
    const error = await toApiError(response, 'Impossibile leggere il profilo.')
    if (hasProblemType(error, 'https://roamly.dev/problems/restricted-session')) {
      return { mode: 'limited' }
    }
    throw error
  }

  return { ...(await response.json()) as MeResponse, mode: 'normal' }
}

/** GET /api/v1/me/deletion — null indica una risposta 204 senza richiesta attiva. */
export async function fetchAccountDeletionStatus(): Promise<AccountDeletionStatus | null> {
  const response = await fetch('/api/v1/me/deletion', {
    method: 'GET',
    credentials: 'include',
  })

  if (response.status === 204) {
    return null
  }

  if (!response.ok) {
    throw await toApiError(response, 'Impossibile leggere lo stato della richiesta.')
  }

  return (await response.json()) as AccountDeletionStatus
}

/** GET /api/v1/me/deletion/preview — conteggi informativi per la conferma. */
export function fetchAccountDeletionPreview(): Promise<AccountDeletionPreview> {
  return getJson(
    '/api/v1/me/deletion/preview',
    'Non e’ stato possibile preparare l’anteprima.',
  )
}

/** POST /api/v1/me/deletion — richiede la password corrente e la protezione CSRF. */
export function scheduleAccountDeletion(password: string): Promise<AccountDeletionSchedule> {
  return mutate('/api/v1/me/deletion', { method: 'POST', body: { password } })
}

/** DELETE /api/v1/me/deletion — revoca la richiesta senza promuovere la sessione. */
export function revokeAccountDeletion(): Promise<void> {
  return mutate<void>('/api/v1/me/deletion', { method: 'DELETE' })
}

/** GET /api/v1/me/export — il server verifica la freshness password. */
export async function fetchAccountExport(): Promise<Blob> {
  const response = await fetch('/api/v1/me/export', {
    method: 'GET',
    credentials: 'include',
  })

  if (!response.ok) {
    throw await toApiError(response, 'Non e’ stato possibile esportare i dati.')
  }

  return response.blob()
}

async function getJson<T>(path: string, fallbackMessage: string): Promise<T> {
  const response = await fetch(path, {
    method: 'GET',
    credentials: 'include',
  })

  if (!response.ok) {
    throw await toApiError(response, fallbackMessage)
  }

  return (await response.json()) as T
}

function pagePath(path: string, after: string | null): string {
  const query = new URLSearchParams({ limit: String(PAGE_SIZE) })
  if (after) {
    query.set('after', after)
  }

  return `${path}?${query.toString()}`
}

function journalAttachmentPath(tripId: string, entryId: string): string {
  return `/api/v1/trips/${encodeURIComponent(tripId)}/journal-entries/${encodeURIComponent(entryId)}/attachments`
}

function createAttachmentFormData(file: File): FormData {
  const body = new FormData()
  body.append('file', file)
  return body
}

async function toApiError(response: Response, fallbackMessage: string): Promise<ApiError> {
  let problem: unknown = null
  try {
    problem = await response.json()
  } catch {
    // La UI usa il messaggio neutro se la risposta non contiene ProblemDetails.
  }

  const title =
    problem && typeof problem === 'object' && 'title' in problem && typeof (problem as { title?: unknown }).title === 'string'
      ? (problem as { title: string }).title
      : fallbackMessage
  return new ApiError(response.status, title, problem)
}

function hasProblemType(error: ApiError, expectedType: string): boolean {
  return error.problem !== null
    && typeof error.problem === 'object'
    && 'type' in error.problem
    && error.problem.type === expectedType
}
