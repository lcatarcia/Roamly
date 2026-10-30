import { useEffect, useLayoutEffect, useRef, useState, type FormEvent } from 'react'
import { AlertDialog } from '@base-ui/react/alert-dialog'
import { useInfiniteQuery, useMutation, useQueryClient, type InfiniteData } from '@tanstack/react-query'

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
  createChecklistItem,
  createTripChecklist,
  confirmChecklistSeed,
  deleteChecklistItem,
  deleteTripChecklist,
  fetchChecklistSeedPreview,
  fetchTripChecklists,
  reorderChecklistItems,
  updateChecklistItem,
  updateTripChecklist,
  type ChecklistDetails,
  type ChecklistInput,
  type ChecklistItemDetails,
  type ChecklistSeedPreview,
  type PageResponse,
} from '@/lib/api'
import { tripChecklistsQueryKey } from '@/features/trips/queryKeys'

const CHECKLIST_KIND = {
  departure: 0,
  arrival: 1,
  storage: 2,
} as const

type ChecklistPage = { page: PageResponse<ChecklistDetails>; etag: string }
type ChecklistPages = InfiniteData<ChecklistPage, string | null>
type ConfirmableSeedPreview = Omit<ChecklistSeedPreview, 'items'> & {
  id: string
  etag: string
  items: (ChecklistSeedPreview['items'][number] & { id: string })[]
}

type ChecklistAction =
  | { type: 'save-checklist'; checklistId: string | null; input: ChecklistInput }
  | { type: 'delete-checklist'; checklistId: string }
  | { type: 'create-item'; checklistId: string; text: string }
  | {
    type: 'update-item'
    checklistId: string
    itemId: string
    text: string
    isDone: boolean
  }
  | { type: 'delete-item'; checklistId: string; itemId: string }
  | { type: 'reorder'; checklistId: string; itemIds: string[] }

interface TripChecklistsProps {
  tripId: string
  etag: string
  onEtagChanged: (etag: string) => void
}

export function TripChecklists({ tripId, etag, onEtagChanged }: TripChecklistsProps) {
  const queryClient = useQueryClient()
  const queryKey = tripChecklistsQueryKey(tripId)
  const etagRef = useRef(etag)
  const onEtagChangedRef = useRef(onEtagChanged)
  const [creating, setCreating] = useState(false)
  const [editingChecklistId, setEditingChecklistId] = useState<string | null>(null)
  const [editingItemId, setEditingItemId] = useState<string | null>(null)
  const [addingItemTo, setAddingItemTo] = useState<string | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<DeleteTarget | null>(null)
  const [seedKind, setSeedKind] = useState(String(CHECKLIST_KIND.departure))
  const [seedPreview, setSeedPreview] = useState<ConfirmableSeedPreview | null>(null)
  const checklistsQuery = useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam }) => fetchTripChecklists(tripId, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: ({ page }) => page.nextCursor ?? undefined,
    retry: false,
  })
  const checklistsData = checklistsQuery.data
  const checklists = checklistsData?.pages.flatMap(({ page }) => page.items) ?? []

  const seedPreviewMutation = useMutation({
    mutationFn: (kind: number) => fetchChecklistSeedPreview(tripId, kind),
    onSuccess: ({ value, etag: previewEtag }) => {
      setSeedPreview({
        ...value,
        id: createCombGuid(),
        etag: previewEtag,
        items: value.items.map((item) => ({ ...item, id: createCombGuid() })),
      })
    },
  })
  const seedConfirmationMutation = useMutation({
    mutationFn: (preview: ConfirmableSeedPreview) => confirmChecklistSeed(
      tripId,
      {
        id: preview.id,
        kind: preview.kind,
        items: preview.items.map(({ id, templateKey }) => ({ id, templateKey })),
      },
      preview.etag,
    ),
    onSuccess: ({ etag: newEtag }) => {
      etagRef.current = newEtag
      onEtagChangedRef.current(newEtag)
      setSeedPreview(null)
      void queryClient.invalidateQueries({ queryKey })
    },
  })

  useLayoutEffect(() => {
    etagRef.current = etag
  }, [etag])
  useLayoutEffect(() => {
    onEtagChangedRef.current = onEtagChanged
  }, [onEtagChanged])

  useEffect(() => {
    const currentEtag = checklistsData?.pages.at(-1)?.etag
    if (currentEtag && currentEtag !== etagRef.current) {
      etagRef.current = currentEtag
      onEtagChangedRef.current(currentEtag)
    }
  }, [checklistsData, checklistsQuery.dataUpdatedAt])

  const mutation = useMutation({
    mutationFn: async (
      action: ChecklistAction,
    ): Promise<{ action: ChecklistAction; etag: string; value?: ChecklistDetails }> => {
      const currentEtag = etagRef.current
      if (action.type === 'save-checklist' && action.checklistId) {
        const result = await updateTripChecklist(tripId, action.checklistId, action.input, currentEtag)
        return { ...result, action }
      }
      if (action.type === 'save-checklist') {
        const result = await createTripChecklist(
          tripId,
          { ...action.input, id: createCombGuid() },
          currentEtag,
        )
        return { ...result, action }
      }
      if (action.type === 'delete-checklist') {
        const result = await deleteTripChecklist(tripId, action.checklistId, currentEtag)
        return { ...result, action }
      }
      if (action.type === 'create-item') {
        const result = await createChecklistItem(
          tripId,
          action.checklistId,
          { id: createCombGuid(), text: action.text },
          currentEtag,
        )
        return { ...result, action }
      }
      if (action.type === 'update-item') {
        const result = await updateChecklistItem(
          tripId,
          action.checklistId,
          action.itemId,
          { text: action.text, isDone: action.isDone },
          currentEtag,
        )
        return { ...result, action }
      }
      if (action.type === 'delete-item') {
        const result = await deleteChecklistItem(
          tripId,
          action.checklistId,
          action.itemId,
          currentEtag,
        )
        return { ...result, action }
      }
      const result = await reorderChecklistItems(
        tripId,
        action.checklistId,
        action.itemIds,
        currentEtag,
      )
      return { ...result, action }
    },
    onSuccess: ({ etag: newEtag, value }, action) => {
      etagRef.current = newEtag
      onEtagChanged(newEtag)
      if (value) updateChecklistCache(queryClient, queryKey, value, action)
      setCreating(false)
      setEditingChecklistId(null)
      setEditingItemId(null)
      setAddingItemTo(null)
      setDeleteTarget(null)
      setSeedPreview(null)
      void queryClient.invalidateQueries({ queryKey })
    },
  })
  const staleConflict = mutationConflict(mutation.error)
  const seedBusy = seedPreviewMutation.isPending || seedConfirmationMutation.isPending
  const busy = mutation.isPending || staleConflict || seedBusy
  const seedError = seedConfirmationMutation.error ?? seedPreviewMutation.error
  const seedStale = seedContextConflict(seedConfirmationMutation.error)

  async function reloadAndDiscard() {
    const result = await checklistsQuery.refetch()
    if (!result.isError) {
      mutation.reset()
      setCreating(false)
      setEditingChecklistId(null)
      setEditingItemId(null)
      setAddingItemTo(null)
      setDeleteTarget(null)
    }
  }

  function run(action: ChecklistAction) {
    setSeedPreview(null)
    mutation.reset()
    mutation.mutate(action)
  }

  return (
    <section aria-labelledby="checklists-heading" className="space-y-5 rounded-3xl border border-border bg-card p-6 shadow-xs">
      <div className="flex flex-wrap items-end justify-between gap-4 border-b border-border pb-4">
        <div>
          <h2 id="checklists-heading" className="text-xl font-semibold">
            {t('neutral.checklists.title')}
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            {t('neutral.checklists.description')}
          </p>
        </div>
        {!creating && editingChecklistId === null && (
          <Button
            onClick={() => {
              mutation.reset()
              setCreating(true)
            }}
            disabled={busy}
          >
            {t('neutral.checklists.add')}
          </Button>
        )}
      </div>

      {mutation.error && (
        <div role="alert" className="space-y-3 rounded-lg border border-border bg-background p-4">
          <p className="text-sm text-danger-text">
            {staleConflict
              ? t('neutral.checklists.stale')
              : t('neutral.checklists.mutationError')}
          </p>
          {staleConflict ? (
            <Button variant="outline" onClick={() => void reloadAndDiscard()}>
              {t('neutral.checklists.reloadDiscard')}
            </Button>
          ) : (
            <Button variant="outline" onClick={() => mutation.reset()}>
              {t('neutral.checklists.dismissError')}
            </Button>
          )}
        </div>
      )}

      <section
        aria-labelledby="checklist-seed-heading"
        className="space-y-4 rounded-xl border border-border bg-background p-5"
      >
        <div>
          <h3 id="checklist-seed-heading" className="font-semibold">
            {t('neutral.checklists.seedTitle')}
          </h3>
          <p className="mt-1 text-sm text-muted-foreground">
            {t('neutral.checklists.seedDescription')}
          </p>
        </div>
        <Field className="max-w-sm">
          <FieldLabel htmlFor="checklist-seed-kind">
            {t('neutral.checklists.seedKind')}
          </FieldLabel>
          <FieldContent>
            <select
              id="checklist-seed-kind"
              className="min-h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm focus-visible:outline-2 focus-visible:outline-ring"
              value={seedKind}
              disabled={busy}
              onChange={(event) => {
                setSeedKind(event.target.value)
                setSeedPreview(null)
                seedPreviewMutation.reset()
                seedConfirmationMutation.reset()
              }}
            >
              <option value={CHECKLIST_KIND.departure}>{t('neutral.checklists.departure')}</option>
              <option value={CHECKLIST_KIND.arrival}>{t('neutral.checklists.arrival')}</option>
              <option value={CHECKLIST_KIND.storage}>{t('neutral.checklists.storage')}</option>
            </select>
          </FieldContent>
        </Field>
        <Button
          variant="outline"
          disabled={busy}
          onClick={() => {
            setSeedPreview(null)
            seedConfirmationMutation.reset()
            seedPreviewMutation.reset()
            seedPreviewMutation.mutate(Number(seedKind))
          }}
        >
          {seedPreviewMutation.isPending
            ? t('neutral.checklists.previewing')
            : t('neutral.checklists.previewSeed')}
        </Button>
        {seedError && (
          <div role="alert" className="space-y-3 rounded-lg border border-border p-4">
            <p className="text-sm text-danger-text">
              {seedContextConflict(seedConfirmationMutation.error)
                ? t('neutral.checklists.seedStale')
                : t('neutral.checklists.seedError')}
            </p>
            <Button
              variant="outline"
              onClick={() => {
                if (seedStale) {
                  const kind = seedPreview?.kind ?? Number(seedKind)
                  setSeedPreview(null)
                  seedConfirmationMutation.reset()
                  seedPreviewMutation.reset()
                  seedPreviewMutation.mutate(kind)
                } else if (seedConfirmationMutation.error && seedPreview) {
                  seedConfirmationMutation.mutate(seedPreview)
                } else {
                  seedPreviewMutation.reset()
                  seedPreviewMutation.mutate(Number(seedKind))
                }
              }}
            >
              {seedStale
                ? t('neutral.checklists.seedRefreshPreview')
                : seedConfirmationMutation.error && seedPreview
                  ? t('neutral.checklists.seedRetryConfirmation')
                  : t('neutral.checklists.seedRetryPreview')}
            </Button>
          </div>
        )}
        {seedPreview && (
          <section
            role="region"
            aria-labelledby="checklist-seed-preview-title"
            aria-live="polite"
            className="space-y-4"
          >
            <h4 id="checklist-seed-preview-title" className="font-medium">{seedPreview.name}</h4>
            <ol className="list-decimal space-y-1 pl-5 text-sm">
              {seedPreview.items.map((item) => <li key={item.templateKey}>{item.text}</li>)}
            </ol>
            <div className="flex flex-wrap gap-3">
              <Button
                disabled={busy || seedStale}
                onClick={() => seedConfirmationMutation.mutate(seedPreview)}
              >
                {seedConfirmationMutation.isPending
                  ? t('neutral.checklists.seedConfirming')
                  : t('neutral.checklists.seedConfirm')}
              </Button>
              <Button
                type="button"
                variant="outline"
                disabled={busy}
                onClick={() => {
                  setSeedPreview(null)
                  seedConfirmationMutation.reset()
                }}
              >
                {t('neutral.checklists.seedCancel')}
              </Button>
            </div>
          </section>
        )}
      </section>

      {checklistsQuery.isPending ? (
        <p role="status" className="text-sm text-muted-foreground">
          {t('neutral.checklists.loading')}
        </p>
      ) : checklistsQuery.isError ? (
        <ChecklistError
          message={t('neutral.checklists.loadError')}
          onRetry={() => void checklistsQuery.refetch()}
        />
      ) : (
        <>
          {creating && (
            <ChecklistForm
              pending={busy}
              onSave={(input) => run({ type: 'save-checklist', checklistId: null, input })}
              onCancel={() => setCreating(false)}
            />
          )}
          {checklists.length === 0 ? (
            <p className="max-w-xl text-sm text-muted-foreground">
              {t('neutral.checklists.empty')}
            </p>
          ) : (
            <ol className="divide-y divide-border">
              {checklists.map((checklist) => (
                <li key={checklist.id} className="py-5 first:pt-0">
                  {editingChecklistId === checklist.id ? (
                    <ChecklistForm
                      checklist={checklist}
                      pending={busy}
                      onSave={(input) => run({
                        type: 'save-checklist',
                        checklistId: checklist.id,
                        input,
                      })}
                      onCancel={() => setEditingChecklistId(null)}
                    />
                  ) : (
                    <ChecklistCard
                      checklist={checklist}
                      busy={busy}
                      editingItemId={editingItemId}
                      addingItem={addingItemTo === checklist.id}
                      onEdit={() => {
                        mutation.reset()
                        setEditingChecklistId(checklist.id)
                      }}
                      onDelete={() => setDeleteTarget({ type: 'checklist', checklist })}
                      onAddItem={() => {
                        mutation.reset()
                        setAddingItemTo(checklist.id)
                      }}
                      onSaveItem={(item, text) => run({
                        type: 'update-item',
                        checklistId: checklist.id,
                        itemId: item.id,
                        text,
                        isDone: item.isDone,
                      })}
                      onToggleItem={(item, isDone) => run({
                        type: 'update-item',
                        checklistId: checklist.id,
                        itemId: item.id,
                        text: item.text,
                        isDone,
                      })}
                      onDeleteItem={(item) => setDeleteTarget({
                        type: 'item',
                        checklistId: checklist.id,
                        item,
                      })}
                      onCreateItem={(text) => run({
                        type: 'create-item',
                        checklistId: checklist.id,
                        text,
                      })}
                      onMoveItem={(itemIndex, offset) => {
                        const orderedItems = [...checklist.items].sort((left, right) => (
                          left.sortOrder - right.sortOrder
                        ))
                        const targetIndex = itemIndex + offset
                        if (targetIndex < 0 || targetIndex >= orderedItems.length) return
                        const [moved] = orderedItems.splice(itemIndex, 1)
                        orderedItems.splice(targetIndex, 0, moved)
                        run({
                          type: 'reorder',
                          checklistId: checklist.id,
                          itemIds: orderedItems.map((item) => item.id),
                        })
                      }}
                      onEditItem={(itemId) => {
                        mutation.reset()
                        setEditingItemId(itemId)
                      }}
                      onCancelItem={() => setEditingItemId(null)}
                      onCancelAddItem={() => setAddingItemTo(null)}
                    />
                  )}
                </li>
              ))}
            </ol>
          )}
          {checklistsQuery.hasNextPage && (
            <Button
              variant="outline"
              onClick={() => void checklistsQuery.fetchNextPage()}
              disabled={checklistsQuery.isFetchingNextPage || busy}
            >
              {checklistsQuery.isFetchingNextPage
                ? t('neutral.checklists.loadingMore')
                : t('neutral.checklists.loadMore')}
            </Button>
          )}
        </>
      )}

      <AlertDialog.Root
        open={deleteTarget !== null}
        onOpenChange={(open) => {
          if (!open && !mutation.isPending) setDeleteTarget(null)
        }}
      >
        <DialogContent showCloseButton={false}>
          <DialogHeader>
            <DialogTitle>
              {deleteTarget?.type === 'item'
                ? t('neutral.checklists.deleteItemTitle')
                : t('neutral.checklists.deleteTitle')}
            </DialogTitle>
            <DialogDescription>
              {deleteTarget?.type === 'item'
                ? t('neutral.checklists.deleteItemWarning')
                : t('neutral.checklists.deleteWarning')}
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-2 sm:flex-row sm:justify-end">
            <AlertDialog.Close render={<Button variant="outline" disabled={mutation.isPending} />}>
              {t('neutral.checklists.keep')}
            </AlertDialog.Close>
            <Button
              variant="destructive"
              disabled={mutation.isPending}
              onClick={() => {
                if (deleteTarget?.type === 'checklist') {
                  run({ type: 'delete-checklist', checklistId: deleteTarget.checklist.id })
                } else if (deleteTarget) {
                  run({
                    type: 'delete-item',
                    checklistId: deleteTarget.checklistId,
                    itemId: deleteTarget.item.id,
                  })
                }
              }}
            >
              {mutation.isPending
                ? t('neutral.checklists.deleting')
                : t('neutral.checklists.confirmDelete')}
            </Button>
          </div>
        </DialogContent>
      </AlertDialog.Root>
    </section>
  )
}

type DeleteTarget =
  | { type: 'checklist'; checklist: ChecklistDetails }
  | { type: 'item'; checklistId: string; item: ChecklistItemDetails }

function ChecklistForm({
  checklist,
  pending,
  onSave,
  onCancel,
}: {
  checklist?: ChecklistDetails
  pending: boolean
  onSave: (input: ChecklistInput) => void
  onCancel: () => void
}) {
  const [name, setName] = useState(checklist?.name ?? '')
  const [kind, setKind] = useState(String(checklist?.kind ?? CHECKLIST_KIND.departure))
  const [error, setError] = useState('')
  const fieldId = checklist?.id ?? 'nuova'

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const cleanedName = name.trim()
    if (!cleanedName || cleanedName.length > 80) {
      setError(t('neutral.checklists.validation'))
      return
    }
    setError('')
    onSave({ name: cleanedName, kind: Number(kind) })
  }

  return (
    <form onSubmit={submit} noValidate className="rounded-xl border border-border bg-background p-5">
      <h3 className="font-semibold">
        {checklist ? t('neutral.checklists.editTitle') : t('neutral.checklists.newTitle')}
      </h3>
      <FieldGroup className="mt-4">
        <Field>
          <FieldLabel htmlFor={`checklist-name-${fieldId}`}>
            {t('neutral.checklists.name')}
          </FieldLabel>
          <FieldContent>
            <Input
              id={`checklist-name-${fieldId}`}
              maxLength={80}
              required
              value={name}
              disabled={pending}
              onChange={(event) => setName(event.target.value)}
            />
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={`checklist-kind-${fieldId}`}>
            {t('neutral.checklists.kind')}
          </FieldLabel>
          <FieldContent>
            <select
              id={`checklist-kind-${fieldId}`}
              className="min-h-10 w-full rounded-lg border border-input bg-background px-3 py-2 text-sm focus-visible:outline-2 focus-visible:outline-ring"
              value={kind}
              disabled={pending}
              onChange={(event) => setKind(event.target.value)}
            >
              <option value={CHECKLIST_KIND.departure}>{t('neutral.checklists.departure')}</option>
              <option value={CHECKLIST_KIND.arrival}>{t('neutral.checklists.arrival')}</option>
              <option value={CHECKLIST_KIND.storage}>{t('neutral.checklists.storage')}</option>
            </select>
          </FieldContent>
        </Field>
        {error && <p role="alert" className="text-sm text-danger-text">{error}</p>}
        <div className="flex flex-wrap gap-3">
          <Button type="submit" disabled={pending}>
            {pending
              ? t('neutral.checklists.saving')
              : checklist ? t('neutral.checklists.saveChanges') : t('neutral.checklists.save')}
          </Button>
          <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>
            {t('neutral.checklists.cancel')}
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}

function ChecklistCard({
  checklist,
  busy,
  editingItemId,
  addingItem,
  onEdit,
  onDelete,
  onAddItem,
  onSaveItem,
  onToggleItem,
  onDeleteItem,
  onCreateItem,
  onMoveItem,
  onEditItem,
  onCancelItem,
  onCancelAddItem,
}: {
  checklist: ChecklistDetails
  busy: boolean
  editingItemId: string | null
  addingItem: boolean
  onEdit: () => void
  onDelete: () => void
  onAddItem: () => void
  onSaveItem: (item: ChecklistItemDetails, text: string) => void
  onToggleItem: (item: ChecklistItemDetails, isDone: boolean) => void
  onDeleteItem: (item: ChecklistItemDetails) => void
  onCreateItem: (text: string) => void
  onMoveItem: (index: number, offset: number) => void
  onEditItem: (itemId: string) => void
  onCancelItem: () => void
  onCancelAddItem: () => void
}) {
  const items = [...checklist.items].sort((left, right) => left.sortOrder - right.sortOrder)
  return (
    <article aria-label={checklist.name} className="space-y-4">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {checklistKindLabel(checklist.kind)}
          </p>
          <h3 className="mt-1 text-lg font-semibold">{checklist.name}</h3>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" disabled={busy} onClick={onEdit}>
            {t('neutral.checklists.edit')}
          </Button>
          <Button variant="outline" disabled={busy} onClick={onDelete}>
            {t('neutral.checklists.delete')}
          </Button>
        </div>
      </header>
      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t('neutral.checklists.itemsEmpty')}</p>
      ) : (
        <ol className="divide-y divide-border rounded-lg border border-border">
          {items.map((item, index) => (
            <li key={item.id} className="flex flex-wrap items-center gap-3 p-3">
              {editingItemId === item.id ? (
                <ItemForm
                  item={item}
                  pending={busy}
                  onSave={(text) => onSaveItem(item, text)}
                  onCancel={onCancelItem}
                />
              ) : (
                <>
                  <label className="flex min-w-0 flex-1 items-center gap-3">
                    <input
                      type="checkbox"
                      checked={item.isDone}
                      disabled={busy}
                      aria-label={`${item.isDone
                        ? t('neutral.checklists.markUndone')
                        : t('neutral.checklists.markDone')}: ${item.text}`}
                      className="size-4 shrink-0 accent-primary focus-visible:outline-2 focus-visible:outline-ring"
                      onChange={(event) => onToggleItem(item, event.target.checked)}
                    />
                    <span className={`text-sm ${item.isDone ? 'text-muted-foreground line-through' : ''}`}>
                      {item.text}
                    </span>
                  </label>
                  <div className="flex flex-wrap gap-2">
                    <Button
                      variant="outline"
                      disabled={busy || index === 0}
                      aria-label={`${t('neutral.checklists.moveUp')}: ${item.text}`}
                      onClick={() => onMoveItem(index, -1)}
                    >
                      {t('neutral.checklists.moveUp')}
                    </Button>
                    <Button
                      variant="outline"
                      disabled={busy || index === items.length - 1}
                      aria-label={`${t('neutral.checklists.moveDown')}: ${item.text}`}
                      onClick={() => onMoveItem(index, 1)}
                    >
                      {t('neutral.checklists.moveDown')}
                    </Button>
                    <Button variant="outline" disabled={busy} onClick={() => onEditItem(item.id)}>
                      {t('neutral.checklists.editItem')}
                    </Button>
                    <Button
                      variant="outline"
                      disabled={busy}
                      onClick={() => onDeleteItem(item)}
                    >
                      {t('neutral.checklists.deleteItem')}
                    </Button>
                  </div>
                </>
              )}
            </li>
          ))}
        </ol>
      )}
      {addingItem && (
        <ItemForm
          pending={busy}
          onSave={onCreateItem}
          onCancel={onCancelAddItem}
        />
      )}
      {!addingItem && editingItemId === null && (
        <Button variant="outline" disabled={busy} onClick={onAddItem}>
          {t('neutral.checklists.addItem')}
        </Button>
      )}
    </article>
  )
}

function ItemForm({
  item,
  pending,
  onSave,
  onCancel,
}: {
  item?: ChecklistItemDetails
  pending: boolean
  onSave: (text: string) => void
  onCancel: () => void
}) {
  const [text, setText] = useState(item?.text ?? '')
  const [error, setError] = useState('')
  const fieldId = item?.id ?? 'nuova'

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const cleanedText = text.trim()
    if (!cleanedText || cleanedText.length > 200) {
      setError(t('neutral.checklists.itemValidation'))
      return
    }
    setError('')
    onSave(cleanedText)
  }

  return (
    <form onSubmit={submit} noValidate className="w-full space-y-3">
      <Field>
        <FieldLabel htmlFor={`checklist-item-${fieldId}`}>
          {item ? t('neutral.checklists.itemText') : t('neutral.checklists.newItem')}
        </FieldLabel>
        <FieldContent>
          <Input
            id={`checklist-item-${fieldId}`}
            maxLength={200}
            required
            value={text}
            disabled={pending}
            onChange={(event) => setText(event.target.value)}
          />
        </FieldContent>
      </Field>
      {error && <p role="alert" className="text-sm text-danger-text">{error}</p>}
      <div className="flex flex-wrap gap-3">
        <Button type="submit" disabled={pending}>
          {pending
            ? t('neutral.checklists.saving')
            : item ? t('neutral.checklists.saveChanges') : t('neutral.checklists.saveItem')}
        </Button>
        <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>
          {t('neutral.checklists.cancel')}
        </Button>
      </div>
    </form>
  )
}

function ChecklistError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div role="alert" className="space-y-3 rounded-lg border border-border bg-background p-4">
      <p className="text-sm text-danger-text">{message}</p>
      <Button variant="outline" onClick={onRetry}>{t('neutral.checklists.retry')}</Button>
    </div>
  )
}

function mutationConflict(error: unknown): boolean {
  return error instanceof ApiError && error.status === 412
}

function seedContextConflict(error: unknown): boolean {
  return error instanceof ApiError && (error.status === 409 || error.status === 412)
}

function checklistKindLabel(kind: number): string {
  if (kind === CHECKLIST_KIND.departure) return t('neutral.checklists.departure')
  if (kind === CHECKLIST_KIND.arrival) return t('neutral.checklists.arrival')
  return t('neutral.checklists.storage')
}

function updateChecklistCache(
  queryClient: ReturnType<typeof useQueryClient>,
  queryKey: ReturnType<typeof tripChecklistsQueryKey>,
  checklist: ChecklistDetails,
  action: ChecklistAction,
) {
  queryClient.setQueryData<ChecklistPages>(queryKey, (cached) => {
    if (!cached) return cached
    const found = cached.pages.some(({ page }) => page.items.some((item) => item.id === checklist.id))
    const pages = cached.pages.map(({ page, ...rest }, pageIndex) => ({
      ...rest,
      page: {
        ...page,
        items: page.items.some((item) => item.id === checklist.id)
          ? page.items.map((item) => item.id === checklist.id ? checklist : item)
          : action.type === 'save-checklist' && action.checklistId === null && pageIndex === 0 && !found
            ? [...page.items, checklist]
            : page.items,
      },
    }))
    return { ...cached, pages }
  })
}
