import { useRef, useState, type FormEvent } from 'react'
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { t } from '@/i18n/catalog'
import {
  ApiError,
  createMaintenance,
  fetchMaintenanceLogs,
  recordMaintenanceLog,
  type CreateMaintenanceInput,
  type MaintenanceLogInput,
} from '@/lib/api'
import { createCombGuid } from '@/lib/comb-guid.mjs'
import {
  CAMPER_DASHBOARD_QUERY_KEY,
  maintenanceLogsQueryKey,
} from '@/features/campers/queryKeys'

type RecurrenceDraft = Omit<CreateMaintenanceInput, 'id'>
type LogDraft = Omit<MaintenanceLogInput, 'id'>

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

/** Form essenziale per aggiungere una ricorrenza senza inventare uno storico. */
export function CreateMaintenanceForm({ camperId }: { camperId: string }) {
  const client = useQueryClient()
  const [name, setName] = useState('')
  const [category, setCategory] = useState('')
  const [intervalKm, setIntervalKm] = useState('')
  const [intervalMonths, setIntervalMonths] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const submissionRef = useRef<{ fingerprint: string; id: string } | null>(null)
  const mutation = useMutation({
    mutationFn: (input: CreateMaintenanceInput) => createMaintenance(camperId, input),
    onSuccess: async () => {
      submissionRef.current = null
      setName('')
      setCategory('')
      setIntervalKm('')
      setIntervalMonths('')
      setSubmitted(false)
      await client.invalidateQueries({ queryKey: CAMPER_DASHBOARD_QUERY_KEY })
    },
  })
  const recurrenceInvalid = intervalKm.trim() === '' && intervalMonths.trim() === ''

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    mutation.reset()
    if (!name.trim() || !category || recurrenceInvalid) {
      return
    }
    const draft: RecurrenceDraft = {
      name: name.trim(),
      category: Number(category),
      ...(intervalKm.trim() ? { intervalKm: Number(intervalKm) } : {}),
      ...(intervalMonths.trim() ? { intervalMonths: Number(intervalMonths) } : {}),
    }
    const fingerprint = JSON.stringify(draft)
    const previous = submissionRef.current
    const id = previous?.fingerprint === fingerprint ? previous.id : createCombGuid()
    submissionRef.current = { fingerprint, id }
    mutation.mutate({ ...draft, id })
  }

  return (
    <section className="rounded-xl border border-border bg-card p-5" aria-labelledby="maintenance-create-heading">
      <h3 id="maintenance-create-heading" className="font-semibold">
        {t('neutral.camper.maintenance.createTitle')}
      </h3>
      <form className="mt-4" onSubmit={submit} noValidate>
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="maintenance-name">{t('neutral.camper.maintenance.name')}</FieldLabel>
            <FieldContent>
              <Input id="maintenance-name" maxLength={80} required disabled={mutation.isPending} value={name} onChange={(event) => setName(event.target.value)} />
              {submitted && !name.trim() && <FieldError>{t('neutral.camper.maintenance.nameRequired')}</FieldError>}
            </FieldContent>
          </Field>
          <Field>
            <FieldLabel htmlFor="maintenance-category">{t('neutral.camper.maintenance.categoryLabel')}</FieldLabel>
            <FieldContent>
              <select
                id="maintenance-category"
                className="h-9 w-full rounded-lg border border-input bg-background px-2.5 text-sm text-foreground focus-visible:border-ring focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
                disabled={mutation.isPending}
                value={category}
                onChange={(event) => setCategory(event.target.value)}
                required
              >
                <option value="">—</option>
                {CATEGORIES.map((value) => (
                  <option key={value} value={value}>{maintenanceCategoryLabel(value)}</option>
                ))}
              </select>
              {submitted && !category && <FieldError>{t('neutral.camper.maintenance.categoryRequired')}</FieldError>}
            </FieldContent>
          </Field>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field>
              <FieldLabel htmlFor="maintenance-interval-km">{t('neutral.camper.maintenance.intervalKm')}</FieldLabel>
              <FieldContent>
                <Input id="maintenance-interval-km" type="number" min="1" step="1" disabled={mutation.isPending} value={intervalKm} onChange={(event) => setIntervalKm(event.target.value)} />
              </FieldContent>
            </Field>
            <Field>
              <FieldLabel htmlFor="maintenance-interval-months">{t('neutral.camper.maintenance.intervalMonths')}</FieldLabel>
              <FieldContent>
                <Input id="maintenance-interval-months" type="number" min="1" max="32767" step="1" disabled={mutation.isPending} value={intervalMonths} onChange={(event) => setIntervalMonths(event.target.value)} />
              </FieldContent>
            </Field>
          </div>
          {submitted && recurrenceInvalid && <p className="rounded-lg bg-background px-2 py-1 text-sm text-danger-text">{t('neutral.camper.maintenance.recurrenceRequired')}</p>}
          <Button type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? t('neutral.camper.maintenance.creating') : t('neutral.camper.maintenance.create')}
          </Button>
          {mutation.isError && <p role="alert" className="rounded-lg bg-background px-2 py-1 text-sm text-danger-text">{maintenanceError(mutation.error)}</p>}
          {mutation.isSuccess && <p role="status" className="text-sm">{t('neutral.camper.maintenance.created')}</p>}
        </FieldGroup>
      </form>
    </section>
  )
}

/** Form di registrazione di un fatto storico immutabile. */
export function RecordMaintenanceLogForm({
  camperId,
  maintenanceItemId,
}: {
  camperId: string
  maintenanceItemId: string
}) {
  const client = useQueryClient()
  const [performedOnUtc, setPerformedOnUtc] = useState('')
  const [odometerKm, setOdometerKm] = useState('')
  const [amount, setAmount] = useState('')
  const [currency, setCurrency] = useState('')
  const [workshop, setWorkshop] = useState('')
  const [notes, setNotes] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const submissionRef = useRef<{ fingerprint: string; id: string } | null>(null)
  const mutation = useMutation({
    mutationFn: (input: MaintenanceLogInput) => recordMaintenanceLog(camperId, maintenanceItemId, input),
    onSuccess: async () => {
      submissionRef.current = null
      setPerformedOnUtc('')
      setOdometerKm('')
      setAmount('')
      setCurrency('')
      setWorkshop('')
      setNotes('')
      setSubmitted(false)
      await Promise.all([
        client.invalidateQueries({ queryKey: CAMPER_DASHBOARD_QUERY_KEY }),
        client.invalidateQueries({ queryKey: maintenanceLogsQueryKey(camperId, maintenanceItemId) }),
      ])
    },
  })
  const costIncomplete = (amount.trim() === '') !== (currency.trim() === '')

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    mutation.reset()
    if (!performedOnUtc || costIncomplete) {
      return
    }
    const draft: LogDraft = {
      performedOnUtc,
      ...(odometerKm.trim() ? { odometerKm: Number(odometerKm) } : {}),
      ...(amount.trim() && currency.trim()
        ? { cost: { amount: Number(amount), currency: currency.trim().toUpperCase() } }
        : {}),
      ...(workshop.trim() ? { workshop: workshop.trim() } : {}),
      ...(notes.trim() ? { notes: notes.trim() } : {}),
    }
    const fingerprint = JSON.stringify(draft)
    const previous = submissionRef.current
    const id = previous?.fingerprint === fingerprint ? previous.id : createCombGuid()
    submissionRef.current = { fingerprint, id }
    mutation.mutate({ ...draft, id })
  }

  return (
    <form className="mt-4 border-t border-border pt-4" onSubmit={submit} noValidate>
      <h4 className="mb-3 text-sm font-semibold">{t('neutral.camper.maintenance.recordTitle')}</h4>
      <FieldGroup>
        <Field>
          <FieldLabel htmlFor={`service-date-${maintenanceItemId}`}>{t('neutral.camper.maintenance.performedOn')}</FieldLabel>
          <FieldContent>
            <Input
              id={`service-date-${maintenanceItemId}`}
              type="date"
              required
              disabled={mutation.isPending}
              value={performedOnUtc}
              onChange={(event) => setPerformedOnUtc(event.target.value)}
            />
            {submitted && !performedOnUtc && <FieldError>{t('neutral.camper.maintenance.dateRequired')}</FieldError>}
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={`service-km-${maintenanceItemId}`}>{t('neutral.camper.maintenance.odometerKm')}</FieldLabel>
          <FieldContent>
            <Input id={`service-km-${maintenanceItemId}`} type="number" min="0" step="1" disabled={mutation.isPending} value={odometerKm} onChange={(event) => setOdometerKm(event.target.value)} />
          </FieldContent>
        </Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field>
            <FieldLabel htmlFor={`service-cost-${maintenanceItemId}`}>{t('neutral.camper.maintenance.costAmount')}</FieldLabel>
            <FieldContent>
              <Input id={`service-cost-${maintenanceItemId}`} type="number" step="0.0001" disabled={mutation.isPending} value={amount} onChange={(event) => setAmount(event.target.value)} />
            </FieldContent>
          </Field>
          <Field>
            <FieldLabel htmlFor={`service-currency-${maintenanceItemId}`}>{t('neutral.camper.maintenance.costCurrency')}</FieldLabel>
            <FieldContent>
              <Input id={`service-currency-${maintenanceItemId}`} maxLength={3} autoCapitalize="characters" disabled={mutation.isPending} value={currency} onChange={(event) => setCurrency(event.target.value.toUpperCase())} />
            </FieldContent>
          </Field>
        </div>
        {submitted && costIncomplete && <p className="rounded-lg bg-background px-2 py-1 text-sm text-danger-text">{t('neutral.camper.maintenance.costPairRequired')}</p>}
        <Field>
          <FieldLabel htmlFor={`service-workshop-${maintenanceItemId}`}>{t('neutral.camper.maintenance.workshop')}</FieldLabel>
          <FieldContent>
            <Input id={`service-workshop-${maintenanceItemId}`} maxLength={80} disabled={mutation.isPending} value={workshop} onChange={(event) => setWorkshop(event.target.value)} />
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={`service-notes-${maintenanceItemId}`}>{t('neutral.camper.maintenance.notes')}</FieldLabel>
          <FieldContent>
            <textarea
              id={`service-notes-${maintenanceItemId}`}
              maxLength={1000}
              className="min-h-20 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground focus-visible:border-ring focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
              disabled={mutation.isPending}
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
            />
          </FieldContent>
        </Field>
        <Button type="submit" variant="outline" disabled={mutation.isPending}>
          {mutation.isPending ? t('neutral.camper.maintenance.recording') : t('neutral.camper.maintenance.record')}
        </Button>
        {mutation.isError && <p role="alert" className="rounded-lg bg-background px-2 py-1 text-sm text-danger-text">{maintenanceError(mutation.error)}</p>}
        {mutation.isSuccess && <p role="status" className="text-sm">{t('neutral.camper.maintenance.recorded')}</p>}
      </FieldGroup>
    </form>
  )
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
    <div className="mt-4 border-t border-border pt-3">
      <Button type="button" variant="ghost" onClick={() => setExpanded(!expanded)} aria-expanded={expanded}>
        {expanded ? t('neutral.camper.maintenance.hideHistory') : t('neutral.camper.maintenance.showHistory')}
      </Button>
      {expanded && query.isPending && <p role="status" className="text-sm text-muted-foreground">{t('neutral.camper.maintenance.historyLoading')}</p>}
      {expanded && query.isError && <p role="alert" className="rounded-lg bg-background px-2 py-1 text-sm text-danger-text">{t('neutral.camper.maintenance.historyError')}</p>}
      {expanded && !query.isPending && logs.length === 0 && <p className="text-sm text-muted-foreground">{t('neutral.camper.maintenance.emptyHistory')}</p>}
      {expanded && logs.length > 0 && (
        <ol className="mt-2 grid gap-3">
          {logs.map((log) => (
            <li key={log.id} className="text-sm">
              <p className="font-medium tabular-nums">
                {new Intl.DateTimeFormat('it-IT', { dateStyle: 'medium', timeZone: 'UTC' })
                  .format(new Date(`${log.performedOnUtc}T00:00:00Z`))}
                {log.odometerKm !== null && ` · ${new Intl.NumberFormat('it-IT').format(log.odometerKm)} km`}
                {log.cost !== null && ` · ${new Intl.NumberFormat('it-IT', { maximumFractionDigits: 4 }).format(log.cost.amount)} ${log.cost.currency}`}
              </p>
              {(log.workshop || log.notes) && <p className="text-muted-foreground">{[log.workshop, log.notes].filter(Boolean).join(' · ')}</p>}
            </li>
          ))}
        </ol>
      )}
      {expanded && query.hasNextPage && (
        <Button className="mt-3" type="button" variant="outline" disabled={query.isFetchingNextPage} onClick={() => query.fetchNextPage()}>
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
