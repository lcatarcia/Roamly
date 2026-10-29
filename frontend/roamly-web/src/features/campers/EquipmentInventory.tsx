import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useRef, useState, type FormEvent } from 'react'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { t } from '@/i18n/catalog'
import {
  ApiError,
  createEquipment,
  deleteEquipment,
  fetchEquipment,
  fetchEquipmentItem,
  updateEquipment,
  type EquipmentInput,
  type EquipmentItem,
} from '@/lib/api'
import { createCombGuid } from '@/lib/comb-guid.mjs'
import { equipmentQueryKey } from '@/features/campers/queryKeys'

interface EquipmentFormValues {
  name: string
  category: string
  notes: string
  installedOnUtc: string
}

interface EditableEquipment {
  id: string
  etag: string
}

const EMPTY_FORM: EquipmentFormValues = {
  name: '',
  category: '',
  notes: '',
  installedOnUtc: '',
}

/** Elenco paginato e moduli CRUD dell'inventario di un camper. */
export function EquipmentInventory({ camperId }: { camperId: string }) {
  const queryClient = useQueryClient()
  const [newEquipment, setNewEquipment] = useState(EMPTY_FORM)
  const [editing, setEditing] = useState<EditableEquipment | null>(null)
  const [editValues, setEditValues] = useState<EquipmentFormValues>(EMPTY_FORM)
  const [actionError, setActionError] = useState<string | null>(null)
  const createSubmissionRef = useRef<{ signature: string; id: string } | null>(null)

  const equipmentQuery = useInfiniteQuery({
    queryKey: equipmentQueryKey(camperId),
    queryFn: ({ pageParam }) => fetchEquipment(camperId, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    retry: false,
  })
  const equipment = equipmentQuery.data?.pages.flatMap((page) => page.items) ?? []

  const createMutation = useMutation({
    mutationFn: ({ id, input }: { id: string; input: EquipmentInput }) =>
      createEquipment(camperId, id, input),
    onSuccess: async () => {
      createSubmissionRef.current = null
      setNewEquipment(EMPTY_FORM)
      await queryClient.invalidateQueries({ queryKey: equipmentQueryKey(camperId) })
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ input, item }: { input: EquipmentInput; item: EditableEquipment }) =>
      updateEquipment(camperId, item.id, input, item.etag),
    onSuccess: async () => {
      setEditing(null)
      setEditValues(EMPTY_FORM)
      await queryClient.invalidateQueries({ queryKey: equipmentQueryKey(camperId) })
    },
    onError: async (error) => {
      if (error instanceof ApiError && error.status === 412) {
        setEditing(null)
      }
      await queryClient.invalidateQueries({ queryKey: equipmentQueryKey(camperId) })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: ({ id, etag }: { id: string; etag: string }) => deleteEquipment(camperId, id, etag),
    onSuccess: async () => {
      setActionError(null)
      await queryClient.invalidateQueries({ queryKey: equipmentQueryKey(camperId) })
    },
    onError: async () => {
      await queryClient.invalidateQueries({ queryKey: equipmentQueryKey(camperId) })
    },
  })

  function submitCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!isValidForm(newEquipment)) {
      return
    }

    setActionError(null)
    const input = toEquipmentInput(newEquipment)
    const signature = JSON.stringify(input)
    const previous = createSubmissionRef.current
    const submission = previous?.signature === signature
      ? previous
      : { signature, id: createCombGuid() }
    createSubmissionRef.current = submission
    createMutation.mutate({ id: submission.id, input })
  }

  function submitEdit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!editing || !isValidForm(editValues)) {
      return
    }

    setActionError(null)
    updateMutation.mutate({ input: toEquipmentInput(editValues), item: editing })
  }

  async function beginEdit(item: EquipmentItem) {
    setActionError(null)
    try {
      const details = await fetchEquipmentItem(camperId, item.id)
      setEditing({ id: item.id, etag: details.etag })
      setEditValues(toFormValues(details.equipment))
    } catch (error) {
      setActionError(actionErrorMessage(error))
    }
  }

  async function removeItem(item: EquipmentItem) {
    setActionError(null)
    try {
      const details = await fetchEquipmentItem(camperId, item.id)
      deleteMutation.mutate({ id: item.id, etag: details.etag })
    } catch (error) {
      setActionError(actionErrorMessage(error))
    }
  }

  if (equipmentQuery.isPending) {
    return <p role="status" className="text-sm text-muted-foreground">{t('neutral.camper.equipment.loading')}</p>
  }

  if (equipmentQuery.isError) {
    return <p role="alert" className="text-sm text-danger-text">{t('neutral.camper.equipment.loadError')}</p>
  }

  return (
    <section className="grid gap-5 rounded-3xl border border-border bg-card p-6 shadow-xs" aria-labelledby="equipment-heading">
      <header className="flex flex-wrap items-end justify-between gap-3 border-b border-border pb-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            Dotazione e impianti di bordo
          </p>
          <h2 id="equipment-heading" className="text-xl font-semibold">{t('neutral.camper.equipment.title')}</h2>
          <p className="mt-1 text-sm text-muted-foreground">{t('neutral.camper.equipment.description')}</p>
        </div>
        <span className="rounded-full bg-secondary px-3 py-1 text-xs font-semibold text-secondary-foreground">
          {equipment.length} {equipment.length === 1 ? 'accessorio' : 'accessori'}
        </span>
      </header>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1.25fr)_minmax(19rem,0.75fr)] lg:items-start">
        <div className="space-y-4">
          {equipment.length > 0 && (
            <ul className="grid gap-3 sm:grid-cols-2" aria-label={t('neutral.camper.equipment.title')}>
              {equipment.map((item) => (
                <li key={item.id} className="flex flex-col justify-between rounded-2xl border border-border bg-background p-4">
                  <div>
                    <div className="flex items-start justify-between gap-2">
                      <h3 className="font-semibold text-foreground">{item.name}</h3>
                      <span className="shrink-0 rounded-full bg-secondary px-2.5 py-0.5 text-xs font-semibold text-secondary-foreground">
                        {categoryLabel(item.category)}
                      </span>
                    </div>
                    {item.installedOnUtc && (
                      <p className="mt-1 text-xs text-muted-foreground">
                        Installato il {formatInstalledDate(item.installedOnUtc)}
                      </p>
                    )}
                    {item.notes && <p className="mt-2 text-sm text-muted-foreground">{item.notes}</p>}
                  </div>
                  <div className="mt-4 flex flex-wrap gap-2 border-t border-border pt-3">
                    <Button type="button" variant="outline" size="sm" onClick={() => void beginEdit(item)}>
                      {t('neutral.camper.equipment.edit')}
                    </Button>
                    <Button type="button" variant="ghost" size="sm" disabled={deleteMutation.isPending}
                      aria-label={`${t('neutral.camper.equipment.remove')} ${item.name}`}
                      onClick={() => void removeItem(item)}>
                      {t('neutral.camper.equipment.remove')}
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          )}

          {equipment.length === 0 && (
            <p className="rounded-2xl border border-border bg-background p-6 text-sm text-muted-foreground">
              {t('neutral.camper.equipment.empty')}
            </p>
          )}

          {equipmentQuery.hasNextPage && (
            <Button type="button" variant="outline" disabled={equipmentQuery.isFetchingNextPage}
              onClick={() => void equipmentQuery.fetchNextPage()}>
              {equipmentQuery.isFetchingNextPage
                ? t('neutral.camper.equipment.loadingMore')
                : t('neutral.camper.equipment.loadMore')}
            </Button>
          )}

          {editing && (
            <div className="rounded-2xl border border-border-strong bg-background p-5">
              <h3 className="mb-4 font-semibold">{t('neutral.camper.equipment.editTitle')}</h3>
              <EquipmentForm values={editValues} onChange={setEditValues} onSubmit={submitEdit}
                pending={updateMutation.isPending} submitLabel={t('neutral.camper.equipment.save')}
                onCancel={() => setEditing(null)} idPrefix="edit" />
            </div>
          )}
        </div>

        <div className="rounded-2xl border border-border bg-background p-5">
          <h3 className="mb-4 text-base font-semibold">{t('neutral.camper.equipment.addTitle')}</h3>
          <EquipmentForm values={newEquipment} onChange={setNewEquipment} onSubmit={submitCreate}
            pending={createMutation.isPending} submitLabel={t('neutral.camper.equipment.add')} idPrefix="new" />
          {createMutation.isSuccess && (
            <p role="status" className="mt-3 text-sm text-foreground">{t('neutral.camper.equipment.added')}</p>
          )}
          {(createMutation.isError || updateMutation.isError || deleteMutation.isError || actionError) && (
            <p role="alert" className="mt-3 rounded-lg bg-background px-2 py-1 text-sm text-danger-text">
              {actionError ?? equipmentError(createMutation.error ?? updateMutation.error ?? deleteMutation.error)}
            </p>
          )}
        </div>
      </div>
    </section>
  )
}

function EquipmentForm({
  values,
  onChange,
  onSubmit,
  pending,
  submitLabel,
  onCancel,
  idPrefix,
}: {
  values: EquipmentFormValues
  onChange: (values: EquipmentFormValues) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  pending: boolean
  submitLabel: string
  onCancel?: () => void
  idPrefix: string
}) {
  function setField(key: keyof EquipmentFormValues, value: string) {
    onChange({ ...values, [key]: value })
  }

  return (
    <form onSubmit={onSubmit} noValidate>
      <FieldGroup className="grid gap-3.5 sm:grid-cols-2">
        <Field>
          <FieldLabel htmlFor={`${idPrefix}-equipment-name`}>{t('neutral.camper.equipment.name')}</FieldLabel>
          <FieldContent>
            <Input id={`${idPrefix}-equipment-name`} value={values.name} maxLength={60} required
              onChange={(event) => setField('name', event.target.value)} />
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={`${idPrefix}-equipment-category`}>{t('neutral.camper.equipment.category')}</FieldLabel>
          <FieldContent>
            <select id={`${idPrefix}-equipment-category`} value={values.category} required
              className="h-11 w-full cursor-pointer rounded-xl border border-input bg-card px-3.5 text-sm text-foreground shadow-2xs focus-visible:border-ring focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
              onChange={(event) => setField('category', event.target.value)}>
              <option value="">—</option>
              {categoryOptions().map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={`${idPrefix}-equipment-notes`}>{t('neutral.camper.equipment.notes')}</FieldLabel>
          <FieldContent>
            <Input id={`${idPrefix}-equipment-notes`} value={values.notes} maxLength={500}
              onChange={(event) => setField('notes', event.target.value)} />
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={`${idPrefix}-equipment-installed-on`}>{t('neutral.camper.equipment.installedOn')}</FieldLabel>
          <FieldContent>
            <Input id={`${idPrefix}-equipment-installed-on`} type="date" value={values.installedOnUtc}
              onChange={(event) => setField('installedOnUtc', event.target.value)} />
          </FieldContent>
        </Field>
        <div className="flex flex-wrap gap-2 sm:col-span-2">
          <Button type="submit" disabled={pending || !isValidForm(values)}>
            {pending ? t('neutral.camper.equipment.saving') : submitLabel}
          </Button>
          {onCancel && (
            <Button type="button" variant="outline" onClick={onCancel}>
              {t('neutral.camper.equipment.cancel')}
            </Button>
          )}
        </div>
      </FieldGroup>
    </form>
  )
}

function isValidForm(values: EquipmentFormValues): boolean {
  return values.name.trim().length > 0 && values.category !== ''
}

function toEquipmentInput(values: EquipmentFormValues): EquipmentInput {
  return {
    name: values.name.trim(),
    category: Number(values.category),
    notes: values.notes.trim() || null,
    installedOnUtc: values.installedOnUtc || null,
  }
}

function toFormValues(item: EquipmentItem): EquipmentFormValues {
  return {
    name: item.name,
    category: String(item.category),
    notes: item.notes ?? '',
    installedOnUtc: item.installedOnUtc ?? '',
  }
}

function categoryOptions() {
  return [
    { value: '0', label: t('neutral.camper.equipment.category.solar') },
    { value: '1', label: t('neutral.camper.equipment.category.battery') },
    { value: '2', label: t('neutral.camper.equipment.category.inverter') },
    { value: '3', label: t('neutral.camper.equipment.category.gasBottle') },
    { value: '4', label: t('neutral.camper.equipment.category.bikeRack') },
    { value: '5', label: t('neutral.camper.equipment.category.awning') },
    { value: '6', label: t('neutral.camper.equipment.category.levellers') },
    { value: '7', label: t('neutral.camper.equipment.category.other') },
  ]
}

function categoryLabel(category: number): string {
  return categoryOptions().find((option) => Number(option.value) === category)?.label
    ?? t('neutral.camper.equipment.category.other')
}

function formatInstalledDate(value: string): string {
  return new Intl.DateTimeFormat('it-IT', { dateStyle: 'medium', timeZone: 'UTC' })
    .format(new Date(`${value}T00:00:00Z`))
}

function actionErrorMessage(error: unknown): string {
  return equipmentError(error)
}

function equipmentError(error: unknown): string {
  return error instanceof ApiError && error.status === 412
    ? t('neutral.camper.equipment.stale')
    : t('neutral.camper.equipment.error')
}
