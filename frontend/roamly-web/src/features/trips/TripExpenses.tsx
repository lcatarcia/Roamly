import { useState, type FormEvent } from 'react'
import { AlertDialog } from '@base-ui/react/alert-dialog'
import {
  useInfiniteQuery,
  useMutation,
  useQueryClient,
  type InfiniteData,
} from '@tanstack/react-query'

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
  createExpense,
  deleteExpense,
  fetchExpense,
  fetchTripExpenses,
  updateExpense,
  type ExpenseDetails,
  type ExpenseInput,
  type PageResponse,
} from '@/lib/api'
import { tripExpensesQueryKey } from '@/features/trips/queryKeys'

const EXPENSE_CATEGORY = {
  fuel: 0,
  toll: 1,
  campsite: 2,
  insurance: 3,
  gas: 4,
  parking: 5,
  other: 6,
} as const

interface TripExpensesProps {
  camperId: string
  tripId: string
  tripCancelled: boolean
}

export function TripExpenses({ camperId, tripId, tripCancelled }: TripExpensesProps) {
  const queryClient = useQueryClient()
  const [creating, setCreating] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<ExpenseDetails | null>(null)
  const [deleteErrorId, setDeleteErrorId] = useState<string | null>(null)
  const [etags, setEtags] = useState<Record<string, string>>({})
  const [versionError, setVersionError] = useState<unknown>(null)
  const queryKey = tripExpensesQueryKey(camperId, tripId)
  const expensesQuery = useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam }) => fetchTripExpenses(camperId, tripId, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (page) => page.nextCursor ?? undefined,
    retry: false,
  })
  const expenses = expensesQuery.data?.pages.flatMap((page) => page.items) ?? []

  const saveMutation = useMutation({
    mutationFn: async ({ expenseId, input }: { expenseId: string | null; input: ExpenseInput }) => {
      if (expenseId === null) {
        return createExpense(camperId, { ...input, id: createCombGuid() })
      }
      const etag = etags[expenseId] ?? (await fetchExpense(expenseId)).etag
      return updateExpense(expenseId, input, etag)
    },
    onSuccess: ({ etag }, variables) => {
      if (variables.expenseId) setEtags((current) => ({ ...current, [variables.expenseId!]: etag }))
      setCreating(false)
      setEditingId(null)
      setVersionError(null)
      void queryClient.invalidateQueries({ queryKey })
    },
  })
  const deleteMutation = useMutation({
    mutationFn: async (expense: ExpenseDetails) => {
      const etag = etags[expense.id] ?? (await fetchExpense(expense.id)).etag
      return deleteExpense(expense.id, etag)
    },
    onSuccess: () => {
      setDeleteTarget(null)
      setDeleteErrorId(null)
      setVersionError(null)
      void queryClient.invalidateQueries({ queryKey })
    },
    onError: (_error, expense) => {
      setDeleteTarget(null)
      setDeleteErrorId(expense.id)
    },
  })

  function save(input: ExpenseInput) {
    if (tripCancelled && editingId === null) {
      setCreating(false)
      return
    }
    saveMutation.mutate({ expenseId: editingId, input })
  }

  async function reloadEditedExpense(expenseId: string) {
    setVersionError(null)
    try {
      const current = await fetchExpense(expenseId)
      queryClient.setQueryData<InfiniteData<PageResponse<ExpenseDetails>, string | null>>(
        queryKey,
        (cached) => {
        if (!cached) return cached
        return {
          ...cached,
          pages: cached.pages.map((page) => ({
            ...page,
            items: page.items.map((item) => item.id === expenseId ? current.expense : item),
          })),
        }
        },
      )
      setEtags((existing) => ({ ...existing, [expenseId]: current.etag }))
      saveMutation.reset()
      setEditingId(null)
    } catch (error) {
      setVersionError(error)
    }
  }

  async function reloadVersion(expenseId: string) {
    setVersionError(null)
    try {
      const current = await fetchExpense(expenseId)
      setEtags((existing) => ({ ...existing, [expenseId]: current.etag }))
    } catch (error) {
      setVersionError(error)
    }
  }

  return (
    <section aria-labelledby="expenses-heading" className="space-y-5 border-b border-border py-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h2 id="expenses-heading" className="text-xl font-semibold">{t('neutral.expenses.title')}</h2>
          <p className="mt-1 text-sm text-muted-foreground">{t('neutral.expenses.description')}</p>
        </div>
        {!tripCancelled && !creating && editingId === null && (
          <Button onClick={() => setCreating(true)}>{t('neutral.expenses.add')}</Button>
        )}
      </div>
      {tripCancelled && (
        <p className="text-sm text-muted-foreground">{t('neutral.expenses.cancelledTrip')}</p>
      )}
      {expensesQuery.isPending ? (
        <p role="status" className="text-sm text-muted-foreground">{t('neutral.expenses.loading')}</p>
      ) : expensesQuery.isError ? (
        <ExpensesError message={t('neutral.expenses.loadError')} onRetry={() => void expensesQuery.refetch()} />
      ) : (
        <>
          {creating && !tripCancelled && (
            <ExpenseForm
              pending={saveMutation.isPending}
              error={saveMutation.error}
              versionError={versionError}
              onReload={undefined}
              tripId={tripId}
              onSave={save}
              onCancel={() => setCreating(false)}
            />
          )}
          {expenses.length === 0 ? (
            <p className="max-w-xl text-sm text-muted-foreground">{t('neutral.expenses.empty')}</p>
          ) : (
            <ol className="divide-y divide-border">
              {expenses.map((expense) => (
                <li key={expense.id} className="py-5 first:pt-0">
                  {editingId === expense.id ? (
                    <ExpenseForm
                      expense={expense}
                      pending={saveMutation.isPending}
                      error={saveMutation.error}
                      versionError={versionError}
                      onReload={() => void reloadEditedExpense(expense.id)}
                      tripId={tripId}
                      onSave={save}
                      onCancel={() => setEditingId(null)}
                    />
                  ) : (
                    <ExpenseItem
                      expense={expense}
                      onEdit={() => {
                        saveMutation.reset()
                        setVersionError(null)
                        setEditingId(expense.id)
                      }}
                      onDelete={() => {
                        deleteMutation.reset()
                        setDeleteErrorId(null)
                        setVersionError(null)
                        setDeleteTarget(expense)
                      }}
                    />
                  )}
                  {deleteMutation.error && deleteErrorId === expense.id && (
                    <ExpenseMutationError
                      error={deleteMutation.error}
                      versionError={versionError}
                      onReload={() => void reloadVersion(expense.id)}
                    />
                  )}
                </li>
              ))}
            </ol>
          )}
          {expensesQuery.hasNextPage && (
            <Button
              variant="outline"
              onClick={() => void expensesQuery.fetchNextPage()}
              disabled={expensesQuery.isFetchingNextPage}
            >
              {expensesQuery.isFetchingNextPage
                ? t('neutral.expenses.loadingMore')
                : t('neutral.expenses.loadMore')}
            </Button>
          )}
        </>
      )}
      <AlertDialog.Root
        open={deleteTarget !== null}
        onOpenChange={(open) => {
          if (!open && !deleteMutation.isPending) setDeleteTarget(null)
        }}
      >
        <DialogContent showCloseButton={false}>
          <DialogHeader>
            <DialogTitle>{t('neutral.expenses.deleteTitle')}</DialogTitle>
            <DialogDescription>{t('neutral.expenses.deleteWarning')}</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-2 sm:flex-row sm:justify-end">
            <AlertDialog.Close render={<Button variant="outline" disabled={deleteMutation.isPending} />}>
              {t('neutral.expenses.keep')}
            </AlertDialog.Close>
            <Button
              variant="destructive"
              disabled={deleteMutation.isPending}
              onClick={() => {
                if (deleteTarget) deleteMutation.mutate(deleteTarget)
              }}
            >
              {deleteMutation.isPending
                ? t('neutral.expenses.deleting')
                : t('neutral.expenses.confirmDelete')}
            </Button>
          </div>
        </DialogContent>
      </AlertDialog.Root>
    </section>
  )
}

function ExpenseForm({
  expense,
  pending,
  error,
  versionError,
  onReload,
  tripId,
  onSave,
  onCancel,
}: {
  expense?: ExpenseDetails
  pending: boolean
  error: unknown
  versionError: unknown
  onReload?: () => void
  tripId: string
  onSave: (input: ExpenseInput) => void
  onCancel: () => void
}) {
  const [amount, setAmount] = useState(expense ? String(expense.amount.amount) : '')
  const [currency, setCurrency] = useState(expense?.amount.currency ?? 'EUR')
  const [category, setCategory] = useState(expense?.category ?? EXPENSE_CATEGORY.fuel)
  const [date, setDate] = useState(expense?.incurredOnUtc ?? '')
  const [description, setDescription] = useState(expense?.description ?? '')
  const [odometer, setOdometer] = useState(expense?.odometerKm?.toString() ?? '')
  const [validationError, setValidationError] = useState(false)

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsedAmount = parseExpenseAmount(amount)
    const normalizedCurrency = currency.trim().toUpperCase()
    const parsedOdometer = odometer === '' ? null : Number(odometer)
    const valid = parsedAmount !== null
      && /^[A-Z]{3}$/.test(normalizedCurrency)
      && isCalendarDate(date)
      && Number.isInteger(category)
      && Object.values(EXPENSE_CATEGORY).some((value) => value === category)
      && (parsedOdometer === null || (Number.isInteger(parsedOdometer) && parsedOdometer >= 0))
      && description.trim().length <= 200
    if (!valid || pending) {
      setValidationError(true)
      return
    }
    setValidationError(false)
    onSave({
      amount: { amount: parsedAmount, currency: normalizedCurrency },
      category,
      incurredOnUtc: date,
      description: description.trim() || null,
      odometerKm: parsedOdometer,
      tripId,
    })
  }

  const conflict = error instanceof ApiError && error.status === 412
  return (
    <form onSubmit={submit} noValidate className="space-y-5 rounded-xl border border-border bg-background p-5">
      <h3 className="font-semibold">
        {expense ? t('neutral.expenses.editTitle') : t('neutral.expenses.newTitle')}
      </h3>
      {conflict && (
        <div className="space-y-2">
          <p role="alert" className="text-sm text-danger-text">{t('neutral.expenses.stale')}</p>
          {onReload && (
            <Button type="button" variant="outline" disabled={pending} onClick={onReload}>
              {t('neutral.expenses.reloadDiscard')}
            </Button>
          )}
        </div>
      )}
      {Boolean(versionError) && (
        <p role="alert" className="text-sm text-danger-text">{t('neutral.expenses.reloadError')}</p>
      )}
      {Boolean(error) && !conflict && (
        <p role="alert" className="text-sm text-danger-text">{t('neutral.expenses.mutationError')}</p>
      )}
      {validationError && (
        <p role="alert" className="text-sm text-danger-text">{t('neutral.expenses.validation')}</p>
      )}
      <FieldGroup>
        <Field>
          <FieldLabel htmlFor={expense ? `expense-amount-${expense.id}` : 'expense-amount'}>
            {t('neutral.expenses.amount')}
          </FieldLabel>
          <FieldContent>
            <Input
              id={expense ? `expense-amount-${expense.id}` : 'expense-amount'}
              inputMode="decimal"
              required
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
            />
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={expense ? `expense-currency-${expense.id}` : 'expense-currency'}>
            {t('neutral.expenses.currency')}
          </FieldLabel>
          <FieldContent>
            <Input
              id={expense ? `expense-currency-${expense.id}` : 'expense-currency'}
              maxLength={3}
              required
              value={currency}
              onChange={(event) => setCurrency(event.target.value.toUpperCase())}
            />
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={expense ? `expense-category-${expense.id}` : 'expense-category'}>
            {t('neutral.expenses.category')}
          </FieldLabel>
          <FieldContent>
            <select
              id={expense ? `expense-category-${expense.id}` : 'expense-category'}
              className="h-11 w-full rounded-lg border border-input bg-background px-3 text-sm"
              value={category}
              onChange={(event) => setCategory(Number(event.target.value))}
            >
              {expenseCategories().map(({ value, label }) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={expense ? `expense-date-${expense.id}` : 'expense-date'}>
            {t('neutral.expenses.date')}
          </FieldLabel>
          <FieldContent>
            <Input
              id={expense ? `expense-date-${expense.id}` : 'expense-date'}
              type="date"
              required
              value={date}
              onChange={(event) => setDate(event.target.value)}
            />
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={expense ? `expense-description-${expense.id}` : 'expense-description'}>
            {t('neutral.expenses.descriptionField')}
          </FieldLabel>
          <FieldContent>
            <Input
              id={expense ? `expense-description-${expense.id}` : 'expense-description'}
              maxLength={200}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
            />
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={expense ? `expense-odometer-${expense.id}` : 'expense-odometer'}>
            {t('neutral.expenses.odometer')}
          </FieldLabel>
          <FieldContent>
            <Input
              id={expense ? `expense-odometer-${expense.id}` : 'expense-odometer'}
              type="number"
              min={0}
              step={1}
              value={odometer}
              onChange={(event) => setOdometer(event.target.value)}
            />
          </FieldContent>
        </Field>
      </FieldGroup>
      <div className="flex flex-wrap gap-2">
        <Button type="submit" disabled={pending}>
          {pending
            ? t('neutral.expenses.saving')
            : expense ? t('neutral.expenses.saveChanges') : t('neutral.expenses.save')}
        </Button>
        <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>
          {t('neutral.expenses.cancel')}
        </Button>
      </div>
    </form>
  )
}

function ExpenseItem({
  expense,
  onEdit,
  onDelete,
}: {
  expense: ExpenseDetails
  onEdit: () => void
  onDelete: () => void
}) {
  return (
    <article className="space-y-3">
      <header>
        <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          <time dateTime={expense.incurredOnUtc}>{formatExpenseDate(expense.incurredOnUtc)}</time>
          {' · '}{expenseCategoryLabel(expense.category)}
        </p>
        <p className="mt-1 text-lg font-semibold tabular-nums">{formatExpenseAmount(expense.amount)}</p>
        {expense.description && <p className="mt-1 text-sm">{expense.description}</p>}
        {expense.odometerKm !== null && (
          <p className="mt-1 text-sm text-muted-foreground">
            {t('neutral.expenses.odometerValue')} {new Intl.NumberFormat('it-IT').format(expense.odometerKm)} km
          </p>
        )}
      </header>
      <div className="flex flex-wrap gap-2">
        <Button variant="outline" onClick={onEdit}>{t('neutral.expenses.edit')}</Button>
        <Button variant="destructive" onClick={onDelete}>{t('neutral.expenses.delete')}</Button>
      </div>
    </article>
  )
}

function ExpenseMutationError({
  error,
  versionError,
  onReload,
}: {
  error: unknown
  versionError: unknown
  onReload: () => void
}) {
  const conflict = error instanceof ApiError && error.status === 412
  return (
    <div role="alert" className="mt-3 space-y-2 rounded-lg border border-border bg-background p-4">
      <p className="text-sm text-danger-text">
        {versionError
          ? t('neutral.expenses.reloadError')
          : conflict ? t('neutral.expenses.stale') : t('neutral.expenses.mutationError')}
      </p>
      {conflict && !versionError && (
        <Button variant="outline" onClick={onReload}>{t('neutral.expenses.reload')}</Button>
      )}
    </div>
  )
}

function ExpensesError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div role="alert" className="space-y-3 rounded-lg border border-border bg-background p-4">
      <p className="text-sm text-danger-text">{message}</p>
      <Button variant="outline" onClick={onRetry}>{t('neutral.expenses.retry')}</Button>
    </div>
  )
}

function expenseCategories() {
  return [
    { value: EXPENSE_CATEGORY.fuel, label: t('neutral.expenses.category.fuel') },
    { value: EXPENSE_CATEGORY.toll, label: t('neutral.expenses.category.toll') },
    { value: EXPENSE_CATEGORY.campsite, label: t('neutral.expenses.category.campsite') },
    { value: EXPENSE_CATEGORY.insurance, label: t('neutral.expenses.category.insurance') },
    { value: EXPENSE_CATEGORY.gas, label: t('neutral.expenses.category.gas') },
    { value: EXPENSE_CATEGORY.parking, label: t('neutral.expenses.category.parking') },
    { value: EXPENSE_CATEGORY.other, label: t('neutral.expenses.category.other') },
  ]
}

function expenseCategoryLabel(category: number): string {
  return expenseCategories().find((item) => item.value === category)?.label
    ?? t('neutral.expenses.category.other')
}

function formatExpenseAmount(amount: ExpenseDetails['amount']): string {
  const formatted = new Intl.NumberFormat('it-IT', { maximumFractionDigits: 4 }).format(amount.amount)
  return `${formatted} ${amount.currency}`
}

function formatExpenseDate(value: string): string {
  return new Intl.DateTimeFormat('it-IT', { dateStyle: 'medium', timeZone: 'UTC' })
    .format(new Date(`${value}T00:00:00Z`))
}

function parseExpenseAmount(value: string): number | null {
  const normalized = value.trim().replace(',', '.')
  if (!/^-?\d+(?:\.\d{1,4})?$/.test(normalized)) return null
  const amount = Number(normalized)
  return Number.isFinite(amount) && amount !== 0 ? amount : null
}

function isCalendarDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false
  const parsed = new Date(`${value}T00:00:00Z`)
  return !Number.isNaN(parsed.valueOf()) && parsed.toISOString().slice(0, 10) === value
}
