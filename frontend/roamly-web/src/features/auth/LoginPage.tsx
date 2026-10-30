import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { BrandLogo } from '@/components/brand/BrandLogo'
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
        <div className="border-b border-border bg-muted/50">
          <div className="mx-auto flex w-full max-w-7xl flex-wrap items-center justify-between gap-3 px-4 py-3 sm:px-6 lg:px-8">
            <div className="flex min-w-0 flex-wrap items-center gap-x-4 gap-y-1">
              <p className="text-xs font-medium text-muted-foreground">
                {t('neutral.login.signedInAs')}{' '}
                <span className="font-semibold text-foreground">
                  {currentUser.displayName ?? currentUser.email}
                </span>
              </p>
              <Link
                className="inline-flex items-center rounded-full bg-card px-3 py-1 text-xs font-semibold text-foreground shadow-2xs transition-colors hover:bg-secondary"
                to="/account/privacy"
              >
                {t('neutral.account.privacy.link')}
              </Link>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => logoutMutation.mutate()}
              disabled={logoutMutation.isPending}
            >
              {logoutMutation.isPending ? t('neutral.login.signingOut') : t('neutral.login.signOut')}
            </Button>
          </div>
        </div>
        <div className=" pt-3">
          <TripNavigation />
        </div>
        {logoutMutation.isError && (
          <p role="alert" className="mx-auto max-w-7xl px-4 text-sm text-danger-text sm:px-6 lg:px-8">
            {t('neutral.login.signOutError')}
          </p>
        )}
        <div className="pb-20 md:pb-8">
          <CamperRoutes />
        </div>
      </>
    )
  }

  return (
    <PageContainer size="form">
      <div className="rounded-3xl border border-border bg-card p-6 shadow-xs sm:p-8">
        <div className="mb-6 flex items-center gap-4">
          <BrandLogo variant="icon" className="size-16" />
          <div className="space-y-2">
            <h1 className="page-title">{t('neutral.login.title')}</h1>
            <p className="text-sm text-foreground">{t('neutral.login.description')}</p>
          </div>
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
              <div className="rounded-xl border border-border bg-background p-3">
                <FieldDescription role="alert" className="text-danger-text">
                  {loginErrorMessage(loginMutation.error)}
                </FieldDescription>
              </div>
            )}

            <Button type="submit" size="lg" className="w-full" disabled={loginMutation.isPending}>
              {loginMutation.isPending
                ? t('neutral.login.submitting')
                : t('neutral.login.submit')}
            </Button>
          </FieldGroup>
        </form>
        {import.meta.env.DEV && (
          <p className="mt-5 border-t border-border pt-4 text-sm text-foreground">
            {t('neutral.login.noAccount')}{' '}
            <Link className="font-semibold underline underline-offset-4" to="/register">
              {t('neutral.login.register')}
            </Link>
          </p>
        )}
      </div>
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
