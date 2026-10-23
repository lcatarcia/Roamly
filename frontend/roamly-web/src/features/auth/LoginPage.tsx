import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldDescription, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { PageContainer } from '@/components/layout/PageContainer'
import { clearAccountScopedQueries, ME_QUERY_KEY } from '@/features/account/accountQueries'
import { useAccountLogout, useAccountSession } from '@/features/account/accountSession'
import { CamperRoutes } from '@/features/campers/CamperRoutes'
import { TripNavigation } from '@/features/trips/TripNavigation'
import { ForcedPasswordChangePage } from '@/features/auth/ForcedPasswordChangePage'
import { RestrictedSessionPage } from '@/features/privacy/PrivacyPage'
import { t } from '@/i18n/catalog'
import { ApiError, login } from '@/lib/api'

/**
 * Gestisce la sessione, il login e il primo flusso autenticato di dominio.
 * Le chiamate sono reali verso il backend (nessun mock): ogni mutazione
 * recupera il token CSRF appena prima della richiesta (vedi src/lib/api.ts).
 * Le credenziali invalide mostrano un messaggio generico, coerente con il
 * backend che non fa oracolo sul campo sbagliato.
 */
export function LoginPage() {
  const queryClient = useQueryClient()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const meQuery = useAccountSession()
  const logoutMutation = useAccountLogout()

  const loginMutation = useMutation({
    mutationFn: () => login(email, password),
    onSuccess: async (profile) => {
      setPassword('')
      clearAccountScopedQueries(queryClient)
      if (profile.sessionMode === 'limited') {
        queryClient.setQueryData(ME_QUERY_KEY, { mode: 'limited' as const })
        return
      }

      await queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY })
    },
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    loginMutation.reset()
    loginMutation.mutate()
  }

  if (meQuery.isFetching) {
    return (
      <PageContainer>
        <p className="text-sm text-muted-foreground">{t('neutral.login.checkingSession')}</p>
      </PageContainer>
    )
  }

  const currentUser = meQuery.isError ? null : meQuery.data

  if (currentUser?.mode === 'limited') {
    return <RestrictedSessionPage />
  }

  if (currentUser?.mode === 'normal' && currentUser.mustChangePassword) {
    return <ForcedPasswordChangePage />
  }

  if (currentUser?.mode === 'normal') {
    return (
      <>
        <div className="mx-auto flex w-full max-w-5xl items-center justify-between gap-4 px-4 py-4 sm:px-6">
          <div className="min-w-0">
            <p className="text-xs text-muted-foreground">{t('neutral.login.signedInAs')}</p>
            <p className="truncate text-sm font-semibold">
              {currentUser.displayName ?? currentUser.email}
            </p>
            <Link
              className="text-sm text-muted-foreground underline underline-offset-4"
              to="/account/privacy"
            >
              {t('neutral.account.privacy.link')}
            </Link>
          </div>
          <Button
            type="button"
            variant="outline"
            onClick={() => logoutMutation.mutate()}
            disabled={logoutMutation.isPending}
          >
            {logoutMutation.isPending ? t('neutral.login.signingOut') : t('neutral.login.signOut')}
          </Button>
        </div>
        <TripNavigation />
        {logoutMutation.isError && (
          <p role="alert" className="mx-auto max-w-5xl px-4 text-sm text-danger-text sm:px-6">
            {t('neutral.login.signOutError')}
          </p>
        )}
        <div className="pb-20 md:pb-0">
          <CamperRoutes />
        </div>
      </>
    )
  }

  return (
    <PageContainer>
      <div>
        <h1 className="page-title">{t('neutral.login.title')}</h1>
        <p className="text-sm text-muted-foreground">{t('neutral.login.description')}</p>
      </div>

      <form onSubmit={handleSubmit} noValidate>
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="login-email">{t('neutral.login.email')}</FieldLabel>
            <FieldContent>
              <Input
                id="login-email"
                name="email"
                type="email"
                autoComplete="username"
                required
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            </FieldContent>
          </Field>

          <Field>
            <FieldLabel htmlFor="login-password">{t('neutral.login.password')}</FieldLabel>
            <FieldContent>
              <Input
                id="login-password"
                name="password"
                type="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
            </FieldContent>
          </Field>

          {loginMutation.isError && (
            <FieldDescription role="alert" className="text-danger-text">
              {loginErrorMessage(loginMutation.error)}
            </FieldDescription>
          )}

          <Button type="submit" disabled={loginMutation.isPending}>
            {loginMutation.isPending
              ? t('neutral.login.submitting')
              : t('neutral.login.submit')}
          </Button>
        </FieldGroup>
      </form>
      {import.meta.env.DEV && (
        <p className="text-sm text-muted-foreground">
          {t('neutral.login.noAccount')}{' '}
          <Link className="underline underline-offset-4" to="/register">
            {t('neutral.login.register')}
          </Link>
        </p>
      )}
    </PageContainer>
  )
}

/** Registro neutro (R21): mai un dettaglio su quale campo sia sbagliato.
 * Il backend stesso non fa oracolo (401 generico su credenziali). */
function loginErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 429) {
      return t('neutral.login.tooManyAttempts')
    }

    if (error.status === 401) {
      return t('neutral.login.invalidCredentials')
    }
  }

  return t('neutral.login.serviceError')
}
