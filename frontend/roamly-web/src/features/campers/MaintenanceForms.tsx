import { useRef, useState, type FormEvent } from 'react'
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { t } from '@/i18n/catalog'
import {
  ApiError,
  createMaintenance,
  deleteMaintenance,
  fetchMaintenanceItem,
  fetchMaintenanceLogs,
  updateMaintenance,
  type CreateMaintenanceInput,
  type MaintenanceDetail,
  type UpdateMaintenanceInput,
} from '@/lib/api'
import { createCombGuid } from '@/lib/comb-guid.mjs'
import {
  CAMPER_DASHBOARD_QUERY_KEY,
  maintenanceLogsQueryKey,
} from '@/features/campers/queryKeys'

interface MaintenanceFormValues {
  name: string
  category: string
  intervalKm: string
  intervalMonths: string
  performedOnUtc: string
  odometerKm: string
  amount: string
  currency: string
  workshop: string
  notes: string
  isUnderWarranty: boolean
}

const EMPTY_FORM: MaintenanceFormValues = {
  name: '',
  category: '',
  intervalKm: '',
  intervalMonths: '',
  performedOnUtc: '',
  odometerKm: '',
  amount: '',
  currency: '',
  workshop: '',
  notes: '',
  isUnderWarranty: false,
}

const CATEGORIES = [
  '0',
  '1',
  '2',
  '3',
  '4',
  '5',
  '6',
  '7',
  '8',
] as const

/** Form completo per aggiungere una manutenzione ricorrente o un intervento non periodico. */
export function CreateMaintenanceForm({ camperId }: { camperId: string }) {
  const client = useQueryClient()
  const [isOpen, setIsOpen] = useState(false)
  const [values, setValues] = useState<MaintenanceFormValues>(EMPTY_FORM)
  const [submitted, setSubmitted] = useState(false)
  const submissionRef = useRef<{ fingerprint: string; id: string } | null>(null)
  const mutation = useMutation({
    mutationFn: (input: CreateMaintenanceInput) => createMaintenance(camperId, input),
    onSuccess: async () => {
      submissionRef.current = null
      setValues(EMPTY_FORM)
      setSubmitted(false)
      setIsOpen(false)
      await client.invalidateQueries({ queryKey: CAMPER_DASHBOARD_QUERY_KEY })
    },
  })

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    mutation.reset()
    const draft = toMaintenancePayload(values)
    if (!draft) {
      return
    }
    const fingerprint = JSON.stringify(draft)
    const previous = submissionRef.current
    const id = previous?.fingerprint === fingerprint ? previous.id : createCombGuid()
    submissionRef.current = { fingerprint, id }
    mutation.mutate({ ...draft, id })
  }

  return (
    <section className="rounded-2xl border border-border bg-card p-5 shadow-2xs" aria-labelledby="maintenance-create-heading">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h3 id="maintenance-create-heading" className="text-base font-semibold">
            {t('neutral.camper.maintenance.createTitle')}
          </h3>
          <p className="text-xs text-muted-foreground">
            Imposta una scadenza chilometrica o temporale, oppure registra un intervento non periodico o richiamo indicando la data.
          </p>
        </div>
        <Button
          type="button"
          variant={isOpen ? 'outline' : 'default'}
          size="sm"
          aria-expanded={isOpen}
          onClick={() => setIsOpen((current) => !current)}
        >
          {isOpen ? 'Chiudi modulo' : `+ ${t('neutral.camper.maintenance.create')}`}
        </Button>
      </div>
      {mutation.isSuccess && (
        <p role="status" className="mt-3 text-sm text-foreground">
          {t('neutral.camper.maintenance.created')}
        </p>
      )}
      {isOpen && (
        <form className="mt-4 border-t border-border pt-4" onSubmit={submit} noValidate>
          <MaintenanceFields
            idPrefix="maintenance-create"
            values={values}
            onChange={setValues}
            submitted={submitted}
            pending={mutation.isPending}
            submitLabel={mutation.isPending ? t('neutral.camper.maintenance.creating') : t('neutral.camper.maintenance.create')}
            error={mutation.isError ? maintenanceError(mutation.error) : null}
          />
        </form>
      )}
    </section>
  )
}

/** Azioni di modifica ed eliminazione di una singola manutenzione o intervento. */
export function MaintenanceItemActions({
  camperId,
  maintenanceItemId,
  maintenanceName,
}: {
  camperId: string
  maintenanceItemId: string
  maintenanceName: string
}) {
  const client = useQueryClient()
  const [editingEtag, setEditingEtag] = useState<string | null>(null)
  const [values, setValues] = useState<MaintenanceFormValues>(EMPTY_FORM)
  const [submitted, setSubmitted] = useState(false)
  const [loadingDetail, setLoadingDetail] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  const updateMutation = useMutation({
    mutationFn: ({ input, etag }: { input: UpdateMaintenanceInput; etag: string }) =>
      updateMaintenance(camperId, maintenanceItemId, input, etag),
    onSuccess: async () => {
      setEditingEtag(null)
      setSubmitted(false)
      setActionError(null)
      await Promise.all([
        client.invalidateQueries({ queryKey: CAMPER_DASHBOARD_QUERY_KEY }),
        client.invalidateQueries({ queryKey: maintenanceLogsQueryKey(camperId, maintenanceItemId) }),
      ])
    },
    onError: async (error) => {
      if (error instanceof ApiError && error.status === 412) {
        setEditingEtag(null)
      }
      await client.invalidateQueries({ queryKey: CAMPER_DASHBOARD_QUERY_KEY })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (etag: string) => deleteMaintenance(camperId, maintenanceItemId, etag),
    onSuccess: async () => {
      setActionError(null)
      await client.invalidateQueries({ queryKey: CAMPER_DASHBOARD_QUERY_KEY })
    },
    onError: async (error) => {
      setActionError(maintenanceError(error))
      await client.invalidateQueries({ queryKey: CAMPER_DASHBOARD_QUERY_KEY })
    },
  })

  async function openEdit() {
    if (editingEtag !== null) {
      setEditingEtag(null)
      setSubmitted(false)
      return
    }
    setActionError(null)
    setLoadingDetail(true)
    try {
      const detail = await fetchMaintenanceItem(camperId, maintenanceItemId)
      setValues(toFormValues(detail.maintenance))
      setSubmitted(false)
      setEditingEtag(detail.etag)
    } catch (error) {
      setActionError(maintenanceError(error))
    } finally {
      setLoadingDetail(false)
    }
  }

  async function remove() {
    setActionError(null)
    setLoadingDetail(true)
    try {
      const detail = await fetchMaintenanceItem(camperId, maintenanceItemId)
      deleteMutation.mutate(detail.etag)
    } catch (error) {
      setActionError(maintenanceError(error))
    } finally {
      setLoadingDetail(false)
    }
  }

  function submitEdit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    updateMutation.reset()
    if (!editingEtag) {
      return
    }
    const draft = toMaintenancePayload(values)
    if (!draft) {
      return
    }
    updateMutation.mutate({ input: draft, etag: editingEtag })
  }

  const busy = loadingDetail || updateMutation.isPending || deleteMutation.isPending

  return (
    <div className="w-full">
      <div className="flex flex-wrap items-center gap-2">
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={busy}
          aria-expanded={editingEtag !== null}
          onClick={() => void openEdit()}
        >
          {editingEtag !== null ? t('neutral.camper.maintenance.cancel') : t('neutral.camper.maintenance.edit')}
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          disabled={busy}
          aria-label={`${t('neutral.camper.maintenance.remove')} ${maintenanceName}`}
          onClick={() => void remove()}
        >
          {deleteMutation.isPending ? t('neutral.camper.maintenance.removing') : t('neutral.camper.maintenance.remove')}
        </Button>
        {updateMutation.isSuccess && (
          <p role="status" className="text-xs font-medium text-foreground">
            {t('neutral.camper.maintenance.updated')}
          </p>
        )}
      </div>

      {actionError && editingEtag === null && (
        <p role="alert" className="mt-2 rounded-lg bg-background px-2 py-1 text-sm text-danger-text">
          {actionError}
        </p>
      )}

      {editingEtag !== null && (
        <form className="mt-3 rounded-xl border border-border bg-background p-4" onSubmit={submitEdit} noValidate>
          <h4 className="mb-3 text-sm font-semibold">{t('neutral.camper.maintenance.editTitle')}</h4>
          <MaintenanceFields
            idPrefix={`maintenance-edit-${maintenanceItemId}`}
            values={values}
            onChange={setValues}
            submitted={submitted}
            pending={updateMutation.isPending}
            submitLabel={updateMutation.isPending ? t('neutral.camper.maintenance.saving') : t('neutral.camper.maintenance.save')}
            onCancel={() => {
              setEditingEtag(null)
              setSubmitted(false)
            }}
            error={updateMutation.isError ? maintenanceError(updateMutation.error) : null}
          />
        </form>
      )}
    </div>
  )
}

function MaintenanceFields({
  idPrefix,
  values,
  onChange,
  submitted,
  pending,
  submitLabel,
  onCancel,
  error,
}: {
  idPrefix: string
  values: MaintenanceFormValues
  onChange: (next: MaintenanceFormValues) => void
  submitted: boolean
  pending: boolean
  submitLabel: string
  onCancel?: () => void
  error: string | null
}) {
  const recurrenceInvalid =
    values.intervalKm.trim() === ''
    && values.intervalMonths.trim() === ''
    && values.performedOnUtc.trim() === ''
  const costIncomplete = (values.amount.trim() === '') !== (values.currency.trim() === '')
  const detailsWithoutDate =
    values.performedOnUtc.trim() === ''
    && (values.odometerKm.trim() !== ''
      || values.amount.trim() !== ''
      || values.currency.trim() !== ''
      || values.workshop.trim() !== ''
      || values.notes.trim() !== ''
      || values.isUnderWarranty)

  return (
    <FieldGroup className="grid gap-4 sm:grid-cols-2">
      <Field>
        <FieldLabel htmlFor={`${idPrefix}-name`}>{t('neutral.camper.maintenance.name')}</FieldLabel>
        <FieldContent>
          <Input
            id={`${idPrefix}-name`}
            maxLength={80}
            required
            disabled={pending}
            value={values.name}
            onChange={(event) => onChange({ ...values, name: event.target.value })}
          />
          {submitted && !values.name.trim() && <FieldError>{t('neutral.camper.maintenance.nameRequired')}</FieldError>}
        </FieldContent>
      </Field>
      <Field>
        <FieldLabel htmlFor={`${idPrefix}-category`}>{t('neutral.camper.maintenance.categoryLabel')}</FieldLabel>
        <FieldContent>
          <select
            id={`${idPrefix}-category`}
            className="h-11 w-full cursor-pointer rounded-xl border border-input bg-card px-3.5 text-sm text-foreground shadow-2xs focus-visible:border-ring focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
            disabled={pending}
            value={values.category}
            onChange={(event) => onChange({ ...values, category: event.target.value })}
            required
          >
            <option value="">—</option>
            {CATEGORIES.map((value) => (
              <option key={value} value={value}>{maintenanceCategoryLabel(value)}</option>
            ))}
          </select>
          {submitted && !values.category && <FieldError>{t('neutral.camper.maintenance.categoryRequired')}</FieldError>}
        </FieldContent>
      </Field>
      <Field>
        <FieldLabel htmlFor={`${idPrefix}-interval-km`}>{t('neutral.camper.maintenance.intervalKm')}</FieldLabel>
        <FieldContent>
          <Input
            id={`${idPrefix}-interval-km`}
            type="number"
            min="1"
            step="1"
            disabled={pending}
            value={values.intervalKm}
            onChange={(event) => onChange({ ...values, intervalKm: event.target.value })}
          />
        </FieldContent>
      </Field>
      <Field>
        <FieldLabel htmlFor={`${idPrefix}-interval-months`}>{t('neutral.camper.maintenance.intervalMonths')}</FieldLabel>
        <FieldContent>
          <Input
            id={`${idPrefix}-interval-months`}
            type="number"
            min="1"
            max="32767"
            step="1"
            disabled={pending}
            value={values.intervalMonths}
            onChange={(event) => onChange({ ...values, intervalMonths: event.target.value })}
          />
        </FieldContent>
      </Field>
      <Field>
        <FieldLabel htmlFor={`${idPrefix}-performed-on`}>{t('neutral.camper.maintenance.performedOn')}</FieldLabel>
        <FieldContent>
          <Input
            id={`${idPrefix}-performed-on`}
            type="date"
            disabled={pending}
            value={values.performedOnUtc}
            onChange={(event) => onChange({ ...values, performedOnUtc: event.target.value })}
          />
          {submitted && detailsWithoutDate && (
            <FieldError>{t('neutral.camper.maintenance.dateRequired')}</FieldError>
          )}
        </FieldContent>
      </Field>
      <Field>
        <FieldLabel htmlFor={`${idPrefix}-odometer-km`}>{t('neutral.camper.maintenance.odometerKm')}</FieldLabel>
        <FieldContent>
          <Input
            id={`${idPrefix}-odometer-km`}
            type="number"
            min="0"
            step="1"
            disabled={pending}
            value={values.odometerKm}
            onChange={(event) => onChange({ ...values, odometerKm: event.target.value })}
          />
        </FieldContent>
      </Field>
      <Field>
        <FieldLabel htmlFor={`${idPrefix}-cost-amount`}>{t('neutral.camper.maintenance.costAmount')}</FieldLabel>
        <FieldContent>
          <Input
            id={`${idPrefix}-cost-amount`}
            type="number"
            step="0.0001"
            disabled={pending}
            value={values.amount}
            onChange={(event) => onChange({ ...values, amount: event.target.value })}
          />
        </FieldContent>
      </Field>
      <Field>
        <FieldLabel htmlFor={`${idPrefix}-cost-currency`}>{t('neutral.camper.maintenance.costCurrency')}</FieldLabel>
        <FieldContent>
          <Input
            id={`${idPrefix}-cost-currency`}
            maxLength={3}
            autoCapitalize="characters"
            disabled={pending}
            value={values.currency}
            onChange={(event) => onChange({ ...values, currency: event.target.value.toUpperCase() })}
          />
        </FieldContent>
      </Field>
      {submitted && costIncomplete && (
        <p className="sm:col-span-2 rounded-lg bg-background px-2 py-1 text-sm text-danger-text">
          {t('neutral.camper.maintenance.costPairRequired')}
        </p>
      )}
      <Field className="sm:col-span-2">
        <FieldLabel htmlFor={`${idPrefix}-workshop`}>{t('neutral.camper.maintenance.workshop')}</FieldLabel>
        <FieldContent>
          <Input
            id={`${idPrefix}-workshop`}
            maxLength={80}
            disabled={pending}
            value={values.workshop}
            onChange={(event) => onChange({ ...values, workshop: event.target.value })}
          />
        </FieldContent>
      </Field>
      <Field className="sm:col-span-2">
        <FieldLabel htmlFor={`${idPrefix}-notes`}>{t('neutral.camper.maintenance.notes')}</FieldLabel>
        <FieldContent>
          <textarea
            id={`${idPrefix}-notes`}
            maxLength={1000}
            className="min-h-20 w-full rounded-xl border border-input bg-card px-3.5 py-2 text-sm text-foreground shadow-2xs focus-visible:border-ring focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
            disabled={pending}
            value={values.notes}
            onChange={(event) => onChange({ ...values, notes: event.target.value })}
          />
        </FieldContent>
      </Field>
      <Field className="sm:col-span-2">
        <label
          htmlFor={`${idPrefix}-under-warranty`}
          className="flex min-h-11 cursor-pointer items-center gap-2.5 rounded-xl border border-input bg-card px-3.5 text-sm font-medium text-foreground shadow-2xs"
        >
          <input
            id={`${idPrefix}-under-warranty`}
            type="checkbox"
            className="h-4 w-4 accent-primary"
            disabled={pending}
            checked={values.isUnderWarranty}
            onChange={(event) => onChange({ ...values, isUnderWarranty: event.target.checked })}
          />
          <span>{t('neutral.camper.maintenance.isUnderWarranty')}</span>
        </label>
      </Field>
      <div className="sm:col-span-2 flex flex-wrap items-center gap-3">
        {submitted && recurrenceInvalid && (
          <p className="w-full rounded-lg bg-background px-2 py-1 text-sm text-danger-text">
            {t('neutral.camper.maintenance.recurrenceRequired')}
          </p>
        )}
        <Button type="submit" disabled={pending}>
          {submitLabel}
        </Button>
        {onCancel && (
          <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>
            {t('neutral.camper.maintenance.cancel')}
          </Button>
        )}
        {error && (
          <p role="alert" className="rounded-lg bg-background px-2 py-1 text-sm text-danger-text">
            {error}
          </p>
        )}
      </div>
    </FieldGroup>
  )
}

function toFormValues(detail: MaintenanceDetail): MaintenanceFormValues {
  return {
    name: detail.name,
    category: String(detail.category),
    intervalKm: detail.intervalKm !== null ? String(detail.intervalKm) : '',
    intervalMonths: detail.intervalMonths !== null ? String(detail.intervalMonths) : '',
    performedOnUtc: detail.performedOnUtc ?? '',
    odometerKm: detail.odometerKm !== null ? String(detail.odometerKm) : '',
    amount: detail.cost !== null ? String(detail.cost.amount) : '',
    currency: detail.cost !== null ? detail.cost.currency : '',
    workshop: detail.workshop ?? '',
    notes: detail.notes ?? '',
    isUnderWarranty: detail.isUnderWarranty,
  }
}

function toMaintenancePayload(values: MaintenanceFormValues): UpdateMaintenanceInput | null {
  const name = values.name.trim()
  const category = values.category.trim()
  const intervalKm = values.intervalKm.trim()
  const intervalMonths = values.intervalMonths.trim()
  const performedOnUtc = values.performedOnUtc.trim()
  const odometerKm = values.odometerKm.trim()
  const amount = values.amount.trim()
  const currency = values.currency.trim().toUpperCase()
  const workshop = values.workshop.trim()
  const notes = values.notes.trim()

  const recurrenceInvalid = intervalKm === '' && intervalMonths === '' && performedOnUtc === ''
  const costIncomplete = (amount === '') !== (currency === '')
  const detailsWithoutDate =
    performedOnUtc === ''
    && (odometerKm !== ''
      || amount !== ''
      || currency !== ''
      || workshop !== ''
      || notes !== ''
      || values.isUnderWarranty)

  if (!name || !category || recurrenceInvalid || costIncomplete || detailsWithoutDate) {
    return null
  }

  return {
    name,
    category: Number(category),
    ...(intervalKm ? { intervalKm: Number(intervalKm) } : {}),
    ...(intervalMonths ? { intervalMonths: Number(intervalMonths) } : {}),
    ...(performedOnUtc
      ? {
          performedOnUtc,
          ...(odometerKm ? { odometerKm: Number(odometerKm) } : {}),
          ...(amount && currency ? { cost: { amount: Number(amount), currency } } : {}),
          ...(workshop ? { workshop } : {}),
          ...(notes ? { notes } : {}),
          isUnderWarranty: values.isUnderWarranty,
        }
      : {}),
  }
}

/** Storico espandibile, caricato solo quando richiesto dall'utente. */
export function MaintenanceLogHistory({
  camperId,
  maintenanceItemId,
}: {
  camperId: string
  maintenanceItemId: string
}) {
  const [expanded, setExpanded] = useState(false)
  const query = useInfiniteQuery({
    queryKey: maintenanceLogsQueryKey(camperId, maintenanceItemId),
    queryFn: ({ pageParam }) => fetchMaintenanceLogs(camperId, maintenanceItemId, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (page) => page.nextCursor ?? undefined,
    enabled: expanded,
    retry: false,
  })
  const logs = query.data?.pages.flatMap((page) => page.items) ?? []

  return (
    <div className="w-full">
      <Button type="button" variant="ghost" size="sm" onClick={() => setExpanded(!expanded)} aria-expanded={expanded}>
        {expanded ? t('neutral.camper.maintenance.hideHistory') : t('neutral.camper.maintenance.showHistory')}
      </Button>
      {expanded && query.isPending && <p role="status" className="mt-2 text-sm text-muted-foreground">{t('neutral.camper.maintenance.historyLoading')}</p>}
      {expanded && query.isError && <p role="alert" className="mt-2 rounded-lg bg-background px-2 py-1 text-sm text-danger-text">{t('neutral.camper.maintenance.historyError')}</p>}
      {expanded && !query.isPending && logs.length === 0 && <p className="mt-2 text-sm text-muted-foreground">{t('neutral.camper.maintenance.emptyHistory')}</p>}
      {expanded && logs.length > 0 && (
        <ol className="mt-2 grid gap-2 rounded-xl border border-border bg-background p-3">
          {logs.map((log) => (
            <li key={log.id} className="text-sm">
              <p className="font-medium tabular-nums">
                {new Intl.DateTimeFormat('it-IT', { dateStyle: 'medium', timeZone: 'UTC' })
                  .format(new Date(`${log.performedOnUtc}T00:00:00Z`))}
                {log.odometerKm !== null && ` · ${new Intl.NumberFormat('it-IT').format(log.odometerKm)} km`}
                {log.cost !== null && ` · ${new Intl.NumberFormat('it-IT', { maximumFractionDigits: 4 }).format(log.cost.amount)} ${log.cost.currency}`}
                {log.isUnderWarranty && ` · ${t('neutral.camper.maintenance.underWarrantyBadge')}`}
              </p>
              {(log.workshop || log.notes) && <p className="text-muted-foreground">{[log.workshop, log.notes].filter(Boolean).join(' · ')}</p>}
            </li>
          ))}
        </ol>
      )}
      {expanded && query.hasNextPage && (
        <Button className="mt-3" type="button" variant="outline" size="sm" disabled={query.isFetchingNextPage} onClick={() => query.fetchNextPage()}>
          {query.isFetchingNextPage ? t('neutral.camper.maintenance.historyLoading') : t('neutral.camper.maintenance.loadMoreHistory')}
        </Button>
      )}
    </div>
  )
}

function maintenanceError(error: unknown): string {
  return error instanceof ApiError && error.status === 409
    ? t('neutral.camper.maintenance.conflict')
    : t('neutral.camper.maintenance.error')
}

function maintenanceCategoryLabel(category: (typeof CATEGORIES)[number]): string {
  switch (category) {
    case '0':
      return t('neutral.camper.maintenance.category.engine')
    case '1':
      return t('neutral.camper.maintenance.category.tyres')
    case '2':
      return t('neutral.camper.maintenance.category.brakes')
    case '3':
      return t('neutral.camper.maintenance.category.gas')
    case '4':
      return t('neutral.camper.maintenance.category.water')
    case '5':
      return t('neutral.camper.maintenance.category.electrical')
    case '6':
      return t('neutral.camper.maintenance.category.bodywork')
    case '7':
      return t('neutral.camper.maintenance.category.inspection')
    case '8':
      return t('neutral.camper.maintenance.category.other')
  }
}
