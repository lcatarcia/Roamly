import { useRef, useState, type ChangeEvent, type FormEvent } from 'react'
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { AlertDialog } from '@base-ui/react/alert-dialog'
import { Trash2 } from 'lucide-react'

import { Button } from '@/components/ui/button'
import {
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Field, FieldContent, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { t } from '@/i18n/catalog'
import { createCombGuid } from '@/lib/comb-guid.mjs'
import {
  ApiError,
  createJournalEntry,
  deleteJournalAttachment,
  deleteJournalEntry,
  fetchJournalAttachmentDownload,
  fetchJournalEntries,
  uploadJournalAttachment,
  updateJournalEntry,
  type JournalAttachment,
  type JournalEntry,
  type JournalEntryInput,
} from '@/lib/api'
import { journalEntriesQueryKey } from '@/features/trips/queryKeys'

const MAX_ATTACHMENT_COUNT = 10
const MAX_ATTACHMENT_SIZE_BYTES = 10 * 1024 * 1024
const ALLOWED_ATTACHMENT_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp'])
const ATTACHMENT_ACCEPT = '.jpg,.jpeg,.png,.webp,image/jpeg,image/png,image/webp'
type AttachmentValidationKey =
  | 'neutral.journal.attachmentInvalidType'
  | 'neutral.journal.attachmentInvalidSize'

interface JournalEntriesProps {
  tripId: string
  etag: string
  onEtagChanged: (etag: string) => void
  onReload: () => void
}

export function JournalEntries({ tripId, etag, onEtagChanged, onReload }: JournalEntriesProps) {
  const queryClient = useQueryClient()
  const [editingId, setEditingId] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)
  const [mutationError, setMutationError] = useState<unknown>(null)
  const [attachmentDeleteTarget, setAttachmentDeleteTarget] = useState<{
    entryId: string
    attachment: JournalAttachment
  } | null>(null)
  const entriesQuery = useInfiniteQuery({
    queryKey: journalEntriesQueryKey(tripId),
    queryFn: ({ pageParam }) => fetchJournalEntries(tripId, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (page) => page.nextCursor ?? undefined,
    retry: false,
  })
  const entries = entriesQuery.data?.pages.flatMap((page) => page.items) ?? []

  function refreshEntries() {
    void queryClient.invalidateQueries({ queryKey: journalEntriesQueryKey(tripId) })
    onReload()
  }

  function onMutationSuccess(newEtag: string) {
    onEtagChanged(newEtag)
    setMutationError(null)
    setCreating(false)
    setEditingId(null)
    void queryClient.invalidateQueries({ queryKey: journalEntriesQueryKey(tripId) })
  }

  const saveMutation = useMutation({
    mutationFn: async ({ entryId, input }: { entryId: string | null; input: JournalEntryInput }) => (
      entryId
        ? updateJournalEntry(tripId, entryId, input, etag)
        : createJournalEntry(tripId, { ...input, id: createCombGuid() }, etag)
    ),
    onSuccess: ({ etag: newEtag }) => onMutationSuccess(newEtag),
    onError: setMutationError,
  })
  const deleteMutation = useMutation({
    mutationFn: (entry: JournalEntry) => deleteJournalEntry(tripId, entry.id, etag),
    onSuccess: ({ etag: newEtag }) => onMutationSuccess(newEtag),
    onError: setMutationError,
  })
  const uploadAttachmentMutation = useMutation({
    mutationFn: ({ entryId, file }: { entryId: string; file: File }) => (
      uploadJournalAttachment(tripId, entryId, file, etag)
    ),
    onSuccess: ({ etag: newEtag }) => onAttachmentMutationSuccess(newEtag),
    onError: setMutationError,
  })
  const deleteAttachmentMutation = useMutation({
    mutationFn: ({ entryId, attachmentId }: { entryId: string; attachmentId: string }) => (
      deleteJournalAttachment(tripId, entryId, attachmentId, etag)
    ),
    onSuccess: ({ etag: newEtag }) => {
      setAttachmentDeleteTarget(null)
      onAttachmentMutationSuccess(newEtag)
    },
    onError: (error) => {
      setAttachmentDeleteTarget(null)
      setMutationError(error)
    },
  })

  function onAttachmentMutationSuccess(newEtag: string) {
    onEtagChanged(newEtag)
    setMutationError(null)
    void queryClient.invalidateQueries({ queryKey: journalEntriesQueryKey(tripId) })
  }

  function uploadAttachment(entryId: string, file: File): Promise<unknown> {
    setMutationError(null)
    return uploadAttachmentMutation.mutateAsync({ entryId, file })
  }

  function confirmAttachmentDeletion(entryId: string, attachment: JournalAttachment) {
    setMutationError(null)
    setAttachmentDeleteTarget({ entryId, attachment })
  }

  function deleteAttachment() {
    if (!attachmentDeleteTarget) return
    deleteAttachmentMutation.mutate({
      entryId: attachmentDeleteTarget.entryId,
      attachmentId: attachmentDeleteTarget.attachment.id,
    })
  }

  function save(input: JournalEntryInput) {
    saveMutation.mutate({ entryId: editingId, input })
  }

  return (
    <section aria-labelledby="journal-heading" className="space-y-5 rounded-3xl border border-border bg-card p-6 shadow-xs">
      <div className="flex flex-wrap items-end justify-between gap-4 border-b border-border pb-4">
        <div>
          <h2 id="journal-heading" className="text-xl font-semibold">{t('narrative.trips.journalTitle')}</h2>
          <p className="mt-1 text-sm text-muted-foreground">{t('narrative.trips.journalDescription')}</p>
        </div>
        {!creating && editingId === null && (
          <Button onClick={() => setCreating(true)}>{t('neutral.journal.addEntry')}</Button>
        )}
      </div>

      {mutationError !== null && (
        <MutationError
          error={mutationError}
          onReload={refreshEntries}
        />
      )}

      {creating && (
        <JournalEntryForm
          pending={saveMutation.isPending}
          onSave={save}
          onCancel={() => setCreating(false)}
        />
      )}

      {entriesQuery.isPending ? (
        <p role="status" className="text-sm text-muted-foreground">{t('neutral.journal.loading')}</p>
      ) : entriesQuery.isError ? (
        <ErrorRetry
          message={t('neutral.journal.loadError')}
          onRetry={() => void entriesQuery.refetch()}
        />
      ) : entries.length === 0 ? (
        <p className="max-w-xl text-sm text-muted-foreground">
          {t('narrative.trips.journalEmpty')}
        </p>
      ) : (
        <>
          <ol className="divide-y divide-border">
            {entries.map((entry) => (
              <li key={entry.id} className="py-5 first:pt-0">
                {editingId === entry.id ? (
                  <JournalEntryForm
                    entry={entry}
                    pending={saveMutation.isPending}
                    onSave={save}
                    onCancel={() => setEditingId(null)}
                  />
                ) : (
                  <JournalEntryItem
                    tripId={tripId}
                    entry={entry}
                    onEdit={() => {
                      setMutationError(null)
                      setEditingId(entry.id)
                    }}
                    onDelete={() => deleteMutation.mutate(entry)}
                    deleteDisabled={entry.attachments.length > 0}
                    uploadPending={uploadAttachmentMutation.isPending}
                    onUpload={(file) => uploadAttachment(entry.id, file)}
                    onDeleteAttachment={(attachment) => confirmAttachmentDeletion(entry.id, attachment)}
                    deletingAttachmentId={deleteAttachmentMutation.isPending
                      ? attachmentDeleteTarget?.attachment.id ?? null
                      : null}
                  />
                )}
              </li>
            ))}
          </ol>
          {entriesQuery.hasNextPage && (
            <Button
              variant="outline"
              onClick={() => void entriesQuery.fetchNextPage()}
              disabled={entriesQuery.isFetchingNextPage}
            >
              {entriesQuery.isFetchingNextPage
                ? t('neutral.journal.loadingMore')
                : t('neutral.journal.loadMore')}
            </Button>
          )}
        </>
      )}
      <AlertDialog.Root
        open={attachmentDeleteTarget !== null}
        onOpenChange={(open) => {
          if (!open && !deleteAttachmentMutation.isPending) setAttachmentDeleteTarget(null)
        }}
      >
        <DialogContent showCloseButton={false}>
          <DialogHeader>
            <DialogTitle>{t('neutral.journal.attachmentDeleteTitle')}</DialogTitle>
            <DialogDescription>{t('neutral.journal.attachmentDeleteWarning')}</DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-2 sm:flex-row sm:justify-end">
            <AlertDialog.Close render={<Button variant="outline" disabled={deleteAttachmentMutation.isPending} />}>
              {t('neutral.journal.cancel')}
            </AlertDialog.Close>
            <Button
              variant="destructive"
              disabled={deleteAttachmentMutation.isPending}
              onClick={deleteAttachment}
            >
              <Trash2 aria-hidden="true" />
              {deleteAttachmentMutation.isPending
                ? t('neutral.journal.attachmentDeleting')
                : t('neutral.journal.attachmentConfirmDelete')}
            </Button>
          </div>
        </DialogContent>
      </AlertDialog.Root>
    </section>
  )
}

function JournalEntryForm({
  entry,
  pending,
  onSave,
  onCancel,
}: {
  entry?: JournalEntry
  pending: boolean
  onSave: (input: JournalEntryInput) => void
  onCancel: () => void
}) {
  const [date, setDate] = useState(entry?.entryOnUtc ?? '')
  const [title, setTitle] = useState(entry?.title ?? '')
  const [body, setBody] = useState(entry?.body ?? '')
  const [error, setError] = useState('')

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const cleanedTitle = title.trim()
    const cleanedBody = body.trim()
    if (!isCalendarDate(date) || cleanedTitle.length > 120 || !cleanedBody) {
      setError(t('neutral.journal.validation'))
      return
    }
    setError('')
    onSave({ entryOnUtc: date, title: cleanedTitle || null, body: cleanedBody })
  }

  return (
    <form onSubmit={submit} noValidate className="rounded-xl border border-border bg-background p-5">
      <h3 className="font-semibold">
        {entry ? t('neutral.journal.editTitle') : t('neutral.journal.newTitle')}
      </h3>
      <FieldGroup className="mt-4">
        <Field>
          <FieldLabel htmlFor={entry ? `entry-date-${entry.id}` : 'entry-date'}>
            {t('neutral.journal.date')}
          </FieldLabel>
          <FieldContent>
            <Input
              id={entry ? `entry-date-${entry.id}` : 'entry-date'}
              type="date"
              required
              value={date}
              onChange={(event) => setDate(event.target.value)}
            />
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={entry ? `entry-title-${entry.id}` : 'entry-title'}>
            {t('neutral.journal.title')}
          </FieldLabel>
          <FieldContent>
            <Input
              id={entry ? `entry-title-${entry.id}` : 'entry-title'}
              maxLength={120}
              value={title}
              onChange={(event) => setTitle(event.target.value)}
            />
          </FieldContent>
        </Field>
        <Field>
          <FieldLabel htmlFor={entry ? `entry-body-${entry.id}` : 'entry-body'}>
            {t('neutral.journal.body')}
          </FieldLabel>
          <FieldContent>
            <textarea
              id={entry ? `entry-body-${entry.id}` : 'entry-body'}
              required
              rows={4}
              className="w-full rounded-xl border border-input bg-card px-3.5 py-2.5 text-sm text-foreground shadow-2xs focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/40"
              value={body}
              onChange={(event) => setBody(event.target.value)}
            />
          </FieldContent>
        </Field>
        {error && <p role="alert" className="text-sm text-danger-text">{error}</p>}
        <div className="flex flex-wrap gap-3">
          <Button type="submit" disabled={pending}>
            {pending
              ? t('neutral.journal.saving')
              : entry ? t('neutral.journal.saveChanges') : t('neutral.journal.saveEntry')}
          </Button>
          <Button type="button" variant="outline" onClick={onCancel}>
            {t('neutral.journal.cancel')}
          </Button>
        </div>
      </FieldGroup>
    </form>
  )
}

function JournalEntryItem({
  tripId,
  entry,
  onEdit,
  onDelete,
  deleteDisabled,
  uploadPending,
  onUpload,
  onDeleteAttachment,
  deletingAttachmentId,
}: {
  tripId: string
  entry: JournalEntry
  onEdit: () => void
  onDelete: () => void
  deleteDisabled: boolean
  uploadPending: boolean
  onUpload: (file: File) => Promise<unknown>
  onDeleteAttachment: (attachment: JournalAttachment) => void
  deletingAttachmentId: string | null
}) {
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [validationError, setValidationError] = useState<AttachmentValidationKey | null>(null)
  const atAttachmentLimit = entry.attachments.length >= MAX_ATTACHMENT_COUNT

  function selectFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0] ?? null
    setSelectedFile(file)
    setValidationError(file ? attachmentValidationMessage(file) : null)
  }

  async function uploadSelectedFile() {
    if (!selectedFile || validationError || atAttachmentLimit || uploadPending) return
    try {
      await onUpload(selectedFile)
      setSelectedFile(null)
      setValidationError(null)
      if (fileInputRef.current) fileInputRef.current.value = ''
    } catch {
      // L'errore API, incluso 412, viene presentato dal contenitore Journal.
    }
  }

  return (
    <article className="space-y-4">
      <header>
        <time
          className="text-xs font-semibold uppercase tracking-wide text-muted-foreground"
          dateTime={entry.entryOnUtc}
        >
          {formatDate(entry.entryOnUtc)}
        </time>
        {entry.title && <h3 className="mt-1 text-lg font-semibold">{entry.title}</h3>}
        <p className="mt-2 whitespace-pre-wrap text-sm leading-6">{entry.body}</p>
      </header>
      <div className="flex flex-wrap gap-3">
        <Button variant="outline" onClick={onEdit}>{t('neutral.journal.editEntry')}</Button>
        <Button
          variant="outline"
          onClick={onDelete}
          disabled={deleteDisabled}
          aria-describedby={deleteDisabled ? `journal-delete-note-${entry.id}` : undefined}
        >
          {t('neutral.journal.deleteEntry')}
        </Button>
      </div>
      {deleteDisabled && (
        <p id={`journal-delete-note-${entry.id}`} className="text-sm text-muted-foreground">
          {t('neutral.journal.deleteEntryWithAttachments')}
        </p>
      )}
      <section aria-label={t('neutral.journal.attachments')} className="space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          <h4 className="font-medium">{t('neutral.journal.attachments')}</h4>
          <span className="text-sm text-muted-foreground">
            {formatAttachmentCount(entry.attachments.length)}
          </span>
        </div>
        {entry.attachments.length > 0 && (
          <ul className="space-y-3">
            {entry.attachments.map((attachment) => (
              <li key={attachment.id}>
                <JournalAttachmentItem
                  tripId={tripId}
                  entryId={entry.id}
                  attachment={attachment}
                  onDelete={() => onDeleteAttachment(attachment)}
                  deleting={deletingAttachmentId === attachment.id}
                />
              </li>
            ))}
          </ul>
        )}
        <div className="space-y-2 rounded-lg border border-border p-3">
          <Field>
            <FieldLabel htmlFor={`journal-attachment-${entry.id}`}>
              {t('neutral.journal.attachmentInput')}
            </FieldLabel>
            <FieldContent>
              <Input
                id={`journal-attachment-${entry.id}`}
                ref={fileInputRef}
                type="file"
                accept={ATTACHMENT_ACCEPT}
                aria-describedby={[
                  `journal-attachment-help-${entry.id}`,
                  validationError ? `journal-attachment-error-${entry.id}` : null,
                ].filter(Boolean).join(' ')}
                aria-invalid={Boolean(validationError)}
                disabled={atAttachmentLimit || uploadPending}
                onChange={selectFile}
              />
            </FieldContent>
          </Field>
          <p id={`journal-attachment-help-${entry.id}`} className="text-sm text-muted-foreground">
            {atAttachmentLimit
              ? t('neutral.journal.attachmentLimitReached')
              : t('neutral.journal.attachmentHelp')}
          </p>
          {validationError && (
            <p
              id={`journal-attachment-error-${entry.id}`}
              role="alert"
              className="text-sm text-danger-text"
            >
              {attachmentValidationText(validationError)}
            </p>
          )}
          <Button
            type="button"
            variant="outline"
            disabled={!selectedFile || Boolean(validationError) || atAttachmentLimit || uploadPending}
            onClick={() => void uploadSelectedFile()}
          >
            {uploadPending
              ? t('neutral.journal.attachmentUploading')
              : t('neutral.journal.attachmentUpload')}
          </Button>
        </div>
      </section>
    </article>
  )
}

function JournalAttachmentItem({
  tripId,
  entryId,
  attachment,
  onDelete,
  deleting,
}: {
  tripId: string
  entryId: string
  attachment: JournalAttachment
  onDelete: () => void
  deleting: boolean
}) {
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [downloadFailed, setDownloadFailed] = useState(false)
  const downloadMutation = useMutation({
    mutationFn: () => fetchJournalAttachmentDownload(
      tripId,
      entryId,
      attachment.id,
    ),
    onSuccess: ({ url }) => {
      setPreviewUrl(url)
      setDownloadFailed(false)
    },
    onError: () => setDownloadFailed(true),
  })

  return (
    <div className="space-y-2 rounded-lg border border-border p-3">
      <p className="text-sm">
        <span className="font-medium">{attachment.contentType}</span>
        {' · '}
        {formatAttachmentSize(attachment.sizeBytes)}
        {' · '}
        {formatAttachmentDate(attachment.createdAtUtc)}
      </p>
      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          variant="outline"
          disabled={downloadMutation.isPending || deleting}
          onClick={() => {
            if (previewUrl) {
              setPreviewUrl(null)
              return
            }
            setDownloadFailed(false)
            downloadMutation.mutate()
          }}
        >
          {downloadMutation.isPending
            ? t('neutral.journal.attachmentDownloading')
            : previewUrl
              ? t('neutral.journal.attachmentHidePreview')
              : t('neutral.journal.attachmentPreview')}
        </Button>
        <Button type="button" variant="outline" disabled={deleting} onClick={onDelete}>
          {deleting
            ? t('neutral.journal.attachmentDeleting')
            : t('neutral.journal.attachmentDelete')}
        </Button>
        {previewUrl && (
          <a
            className="inline-flex min-h-8 items-center rounded-lg px-2.5 text-sm font-medium text-accent-text underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-ring"
            href={previewUrl}
            target="_blank"
            rel="noopener noreferrer"
          >
            {t('neutral.journal.attachmentOpenOriginal')}
          </a>
        )}
      </div>
      {downloadMutation.isPending && (
        <p role="status" className="text-sm text-muted-foreground">
          {t('neutral.journal.attachmentDownloading')}
        </p>
      )}
      {downloadFailed && (
        <p role="alert" className="text-sm text-danger-text">
          {t('neutral.journal.attachmentDownloadError')}
        </p>
      )}
      {previewUrl && (
        <img
          className="max-h-80 max-w-full rounded-lg object-contain"
          src={previewUrl}
          alt={t('neutral.journal.attachmentPreviewAlt')}
        />
      )}
    </div>
  )
}

function attachmentValidationMessage(file: File): AttachmentValidationKey | null {
  if (!ALLOWED_ATTACHMENT_TYPES.has(file.type)) return 'neutral.journal.attachmentInvalidType'
  if (file.size === 0 || file.size > MAX_ATTACHMENT_SIZE_BYTES) {
    return 'neutral.journal.attachmentInvalidSize'
  }
  return null
}

function attachmentValidationText(key: AttachmentValidationKey): string {
  if (key === 'neutral.journal.attachmentInvalidType') {
    return t('neutral.journal.attachmentInvalidType')
  }
  return t('neutral.journal.attachmentInvalidSize')
}

function formatAttachmentCount(count: number): string {
  return t('neutral.journal.attachmentCount').replace('{count}', String(count))
}

function formatAttachmentSize(sizeBytes: number): string {
  if (sizeBytes < 1024) {
    return `${sizeBytes} ${t('neutral.journal.attachmentBytes')}`
  }
  if (sizeBytes < 1024 * 1024) {
    return formatSizeInUnit(sizeBytes / 1024, t('neutral.journal.attachmentKib'))
  }
  return formatSizeInUnit(sizeBytes / (1024 * 1024), t('neutral.journal.attachmentMib'))
}

function formatSizeInUnit(size: number, unit: string): string {
  return `${new Intl.NumberFormat('it-IT', { maximumFractionDigits: 1 }).format(size)} ${unit}`
}

function formatAttachmentDate(value: string): string {
  return new Intl.DateTimeFormat('it-IT', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'UTC',
  }).format(new Date(value))
}

function MutationError({ error, onReload }: { error: unknown; onReload: () => void }) {
  const stale = error instanceof ApiError && error.status === 412
  return (
    <div role="alert" className="space-y-3 rounded-lg border border-border bg-background p-4">
      <p className="text-sm text-danger-text">
        {stale
          ? t('neutral.journal.stale')
          : t('neutral.journal.mutationError')}
      </p>
      {stale && (
        <Button variant="outline" onClick={onReload}>{t('neutral.journal.reloadAndRetry')}</Button>
      )}
    </div>
  )
}

function ErrorRetry({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div role="alert" className="space-y-3 rounded-lg border border-border bg-background p-4">
      <p className="text-sm text-danger-text">{message}</p>
      <Button variant="outline" onClick={onRetry}>{t('neutral.journal.retry')}</Button>
    </div>
  )
}

function isCalendarDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false
  const parsed = new Date(`${value}T00:00:00Z`)
  return !Number.isNaN(parsed.valueOf()) && parsed.toISOString().slice(0, 10) === value
}

function formatDate(value: string): string {
  return new Intl.DateTimeFormat('it-IT', {
    dateStyle: 'long',
    timeZone: 'UTC',
  }).format(new Date(`${value}T00:00:00Z`))
}
