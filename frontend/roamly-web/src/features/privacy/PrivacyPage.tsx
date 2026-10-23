import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { PageContainer } from '@/components/layout/PageContainer'
import {
  ACCOUNT_DELETION_PREVIEW_QUERY_KEY,
  ACCOUNT_DELETION_QUERY_KEY,
  clearAccountScopedQueries,
  ME_QUERY_KEY,
} from '@/features/account/accountQueries'
import { useAccountLogout, useAccountSession } from '@/features/account/accountSession'
import { t } from '@/i18n/catalog'
import {
  ApiError,
  fetchAccountDeletionPreview,
  fetchAccountDeletionStatus,
  fetchAccountExport,
  revokeAccountDeletion,
  scheduleAccountDeletion,
  type AccountDeletionStatus,
  type NormalAccountSession,
} from '@/lib/api'

const RECENT_PASSWORD_PROBLEM = 'https://roamly.dev/problems/recent-password-required'

export function PrivacyPage() {
  const sessionQuery = useAccountSession()

  if (sessionQuery.isPending || sessionQuery.isFetching) {
    return <PageMessage message={t('neutral.account.checkingSession')} />
  }

  if (sessionQuery.isError) {
    return (
      <PageContainer>
        <p role="alert" className="text-sm text-danger-text">
          {t('neutral.account.sessionError')}
        </p>
        <Link className="text-sm underline underline-offset-4" to="/">
          {t('neutral.account.backToLogin')}
        </Link>
      </PageContainer>
    )
  }

  if (sessionQuery.data?.mode === 'limited') {
    return <RestrictedSessionPage />
  }

  if (!sessionQuery.data) {
    return (
      <PageContainer>
        <h1 className="page-title">{t('neutral.account.privacy.title')}</h1>
        <p role="status" className="text-sm text-muted-foreground">
          {t('neutral.account.loginRequired')}
        </p>
        <Link className="text-sm underline underline-offset-4" to="/">
          {t('neutral.account.backToLogin')}
        </Link>
      </PageContainer>
    )
  }

  return <NormalPrivacyPage session={sessionQuery.data} />
}

export function RestrictedSessionPage() {
  const queryClient = useQueryClient()
  const logoutMutation = useAccountLogout()
  const [wasRevoked, setWasRevoked] = useState(false)
  const deletionQuery = useQuery({
    queryKey: ACCOUNT_DELETION_QUERY_KEY,
    queryFn: fetchAccountDeletionStatus,
    retry: false,
  })
  const revokeMutation = useMutation({
    mutationFn: revokeAccountDeletion,
    onSuccess: () => {
      clearAccountScopedQueries(queryClient, { preserveDeletionStatus: true })
      queryClient.setQueryData(ME_QUERY_KEY, { mode: 'limited' })
      queryClient.setQueryData(ACCOUNT_DELETION_QUERY_KEY, null)
      setWasRevoked(true)
    },
  })

  return (
    <PageContainer className="max-w-xl justify-start">
      <div>
        <h1 className="page-title">{t('neutral.account.limited.title')}</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          {t('neutral.account.limited.description')}
        </p>
      </div>

      {deletionQuery.isFetching && (
        <p role="status" className="text-sm text-muted-foreground">
          {t('neutral.account.limited.loading')}
        </p>
      )}
      {deletionQuery.isError && !deletionQuery.isFetching && (
        <p role="alert" className="text-sm text-danger-text">
          {t('neutral.account.limited.statusError')}
        </p>
      )}
      {!deletionQuery.isFetching && deletionQuery.data && (
        <ScheduledRequest status={deletionQuery.data} />
      )}
      {deletionQuery.data === null && !deletionQuery.isFetching && !deletionQuery.isError && !wasRevoked && (
        <p role="status" className="rounded-lg border border-border bg-card p-4 text-sm">
          {t('neutral.account.limited.noActiveRequest')}
        </p>
      )}
      {wasRevoked && (
        <p role="status" className="rounded-lg border border-border bg-card p-4 text-sm">
          {t('neutral.account.limited.revoked')}
        </p>
      )}
      {revokeMutation.isError && (
        <p role="alert" className="text-sm text-danger-text">
          {t('neutral.account.limited.revokeError')}
        </p>
      )}

      <div className="flex flex-wrap gap-3">
        {deletionQuery.data?.status === 'scheduled' && (
          <Button
            type="button"
            variant="default"
            className="border-destructive"
            disabled={revokeMutation.isPending}
            onClick={() => revokeMutation.mutate()}
          >
            {revokeMutation.isPending
              ? t('neutral.account.limited.revoking')
              : t('neutral.account.limited.revoke')}
          </Button>
        )}
        <Button
          type="button"
          variant="outline"
          disabled={logoutMutation.isPending}
          onClick={() => logoutMutation.mutate()}
        >
          {logoutMutation.isPending
            ? t('neutral.login.signingOut')
            : t('neutral.account.limited.logout')}
        </Button>
      </div>
      {logoutMutation.isError && (
        <p role="alert" className="text-sm text-danger-text">
          {t('neutral.login.signOutError')}
        </p>
      )}
      <Link className="text-sm underline underline-offset-4" to="/account/privacy">
        {t('neutral.account.privacy.link')}
      </Link>
    </PageContainer>
  )
}

function NormalPrivacyPage({ session }: { session: NormalAccountSession }) {
  const queryClient = useQueryClient()
  const logoutMutation = useAccountLogout()
  const [password, setPassword] = useState('')
  const [exportError, setExportError] = useState<unknown>(null)
  const deletionQuery = useQuery({
    queryKey: ACCOUNT_DELETION_QUERY_KEY,
    queryFn: fetchAccountDeletionStatus,
    retry: false,
  })
  const previewQuery = useQuery({
    queryKey: ACCOUNT_DELETION_PREVIEW_QUERY_KEY,
    queryFn: fetchAccountDeletionPreview,
    retry: false,
    enabled: deletionQuery.isSuccess && deletionQuery.data === null,
  })
  const scheduleMutation = useMutation({
    mutationFn: scheduleAccountDeletion,
    onSuccess: (schedule) => {
      clearAccountScopedQueries(queryClient)
      queryClient.setQueryData(ME_QUERY_KEY, { mode: 'limited' })
      queryClient.setQueryData(ACCOUNT_DELETION_QUERY_KEY, {
        status: 'scheduled',
        ...schedule,
      } satisfies AccountDeletionStatus)
    },
  })
  const exportMutation = useMutation({
    mutationFn: fetchAccountExport,
    onSuccess: downloadExport,
    onError: setExportError,
  })

  function submitDeletion(event: FormEvent) {
    event.preventDefault()
    const submittedPassword = password
    setPassword('')
    scheduleMutation.reset()
    scheduleMutation.mutate(submittedPassword)
  }

  function submitExport() {
    setExportError(null)
    exportMutation.mutate()
  }

  const canConfirmDeletion =
    deletionQuery.isSuccess && deletionQuery.data === null && previewQuery.isSuccess

  return (
    <PageContainer className="max-w-2xl justify-start">
      <header>
        <Link className="text-sm text-muted-foreground underline underline-offset-4" to="/">
          {t('neutral.account.backToCampers')}
        </Link>
        <h1 className="page-title mt-4">{t('neutral.account.privacy.title')}</h1>
        <p className="mt-2 text-sm text-muted-foreground">{t('neutral.account.privacy.description')}</p>
        <p className="mt-2 text-sm text-muted-foreground">
          {session.displayName ?? session.email}
        </p>
      </header>

      {(deletionQuery.isPending || (deletionQuery.data === null && previewQuery.isPending)) && (
        <p role="status" className="text-sm text-muted-foreground">
          {t('neutral.account.privacy.loading')}
        </p>
      )}
      {deletionQuery.isError && <AccountLoadError />}
      {deletionQuery.isSuccess && deletionQuery.data?.status === 'scheduled' && (
        <ScheduledRequest status={deletionQuery.data} />
      )}

      {deletionQuery.isSuccess && deletionQuery.data === null && previewQuery.isError && (
        <AccountLoadError />
      )}
      {canConfirmDeletion && previewQuery.data && (
        <section className="rounded-xl border border-border bg-card p-5" aria-labelledby="deletion-heading">
          <h2 id="deletion-heading" className="text-lg font-semibold">
            {t('neutral.account.deletion.title')}
          </h2>
          <p className="mt-2 text-sm">
            {t('neutral.account.deletion.previewGenerated')} {formatDateTime(previewQuery.data.generatedAtUtc)}
          </p>
          <ul className="mt-3 space-y-1 text-sm">
            {previewQuery.data.entityCounts.map((entry) => (
              <li key={entry.entityType}>{entry.entityType}: {entry.count}</li>
            ))}
          </ul>
          <p className="mt-2 text-sm font-medium">
            {t('neutral.account.deletion.total')}: {previewQuery.data.totalCount}
          </p>
          <p className="mt-3 text-sm">
            {t('neutral.account.deletion.disclaimer')}
          </p>
          <p className="mt-3 text-sm font-medium">
            {t('neutral.account.deletion.beforeConfirm')}
          </p>

          <form className="mt-5" onSubmit={submitDeletion}>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="deletion-password">
                  {t('neutral.account.deletion.password')}
                </FieldLabel>
                <FieldContent>
                  <Input
                    id="deletion-password"
                    name="password"
                    type="password"
                    autoComplete="current-password"
                    required
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                  />
                </FieldContent>
              </Field>
              {scheduleMutation.isError && (
                <p role="alert" className="rounded-lg bg-background px-2 py-1 text-sm text-danger-text">
                  {deletionErrorMessage(scheduleMutation.error)}
                </p>
              )}
              <Button
                type="submit"
                variant="default"
                className="border-destructive"
                disabled={scheduleMutation.isPending}
              >
                {scheduleMutation.isPending
                  ? t('neutral.account.deletion.submitting')
                  : t('neutral.account.deletion.submit')}
              </Button>
            </FieldGroup>
          </form>
        </section>
      )}

      <section className="rounded-xl border border-border bg-card p-5" aria-labelledby="export-heading">
        <h2 id="export-heading" className="text-lg font-semibold">
          {t('neutral.account.export.title')}
        </h2>
        <p className="mt-2 text-sm">{t('neutral.account.export.description')}</p>
        <Button className="mt-4" type="button" disabled={exportMutation.isPending} onClick={submitExport}>
          {exportMutation.isPending
            ? t('neutral.account.export.downloading')
            : t('neutral.account.export.submit')}
        </Button>
        {exportMutation.isError && (
          <p role="alert" className="mt-3 rounded-lg bg-background px-2 py-1 text-sm text-danger-text">
            {exportErrorMessage(exportError)}
          </p>
        )}
      </section>

      <div className="flex flex-wrap items-center gap-4">
        <Button
          type="button"
          variant="outline"
          disabled={logoutMutation.isPending}
          onClick={() => logoutMutation.mutate()}
        >
          {logoutMutation.isPending ? t('neutral.login.signingOut') : t('neutral.login.signOut')}
        </Button>
        {logoutMutation.isError && (
          <p role="alert" className="text-sm text-danger-text">
            {t('neutral.login.signOutError')}
          </p>
        )}
      </div>
    </PageContainer>
  )
}

function ScheduledRequest({ status }: { status: AccountDeletionStatus }) {
  return (
    <section className="rounded-xl border border-border bg-card p-5" aria-labelledby="scheduled-heading">
      <h2 id="scheduled-heading" className="font-semibold">
        {t('neutral.account.deletion.scheduledTitle')}
      </h2>
      <p className="mt-2 text-sm">
        {t('neutral.account.deletion.requestedAt')}: {formatDateTime(status.deletionRequestedAtUtc)}
      </p>
      <p className="mt-2 text-sm">
        {t('neutral.account.deletion.deadline')}: {formatDateTime(status.deletionScheduledForUtc)}
      </p>
      <p className="mt-3 text-sm">
        {t('neutral.account.deletion.afterSchedule')}
      </p>
    </section>
  )
}

function AccountLoadError() {
  return (
    <p role="alert" className="rounded-lg border border-border bg-background p-4 text-sm text-danger-text">
      {t('neutral.account.privacy.loadError')}
    </p>
  )
}

function PageMessage({ message }: { message: string }) {
  return (
    <PageContainer>
      <p role="status" className="text-sm text-muted-foreground">{message}</p>
    </PageContainer>
  )
}

function downloadExport(blob: Blob) {
  const objectUrl = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = objectUrl
  link.download = 'roamly-account-export.zip'
  link.click()
  window.setTimeout(() => URL.revokeObjectURL(objectUrl), 0)
}

function exportErrorMessage(error: unknown): string {
  if (hasProblemType(error, RECENT_PASSWORD_PROBLEM)) {
    return t('neutral.account.export.recentPasswordRequired')
  }

  return t('neutral.account.export.error')
}

function deletionErrorMessage(error: unknown): string {
  if (error instanceof ApiError && error.status === 401) {
    return t('neutral.account.deletion.invalidPassword')
  }
  if (error instanceof ApiError && error.status === 429) {
    return t('neutral.account.deletion.tooManyAttempts')
  }
  return t('neutral.account.deletion.error')
}

function hasProblemType(error: unknown, expectedType: string): boolean {
  return error instanceof ApiError
    && error.problem !== null
    && typeof error.problem === 'object'
    && 'type' in error.problem
    && error.problem.type === expectedType
}

function formatDateTime(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  return new Intl.DateTimeFormat('it-IT', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    timeZoneName: 'short',
  }).format(date)
}
