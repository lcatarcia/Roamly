import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldDescription, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { PageContainer } from '@/components/layout/PageContainer'
import { t } from '@/i18n/catalog'
import { ApiError, fetchMe, login, logout, type LoginProfile } from '@/lib/api'

const ME_QUERY_KEY = ['me'] as const

/**
 * Schermata di login (0.7): non contiene dati di dominio, e' il banco di
 * prova dei token in entrambi i temi. Chiamate reali verso il backend
 * (nessun mock): POST /api/v1/auth/login preceduto dal fetch del token CSRF
 * (vedi src/lib/api.ts). Le credenziali invalide mostrano un messaggio
 * generico, coerente con il backend che non fa oracolo sul campo sbagliato.
 */
export function LoginPage() {
  const queryClient = useQueryClient()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const meQuery = useQuery({
    queryKey: ME_QUERY_KEY,
    queryFn: fetchMe,
    staleTime: Infinity,
    // Niente retry automatico: un 401 e' gia' intercettato in fetchMe() e
    // risolve a null. Un vero errore di rete/server non deve tenere la UI
    // bloccata su "Verifica della sessione..." per i ritardi di backoff di
    // default di TanStack Query (fino a ~7s su 3 tentativi) - meglio
    // mostrare subito il form di login (fallback sicuro, nessun oracolo).
    retry: false,
  })

  const loginMutation = useMutation({
    mutationFn: () => login(email, password),
    onSuccess: (profile: LoginProfile) => {
      queryClient.setQueryData(ME_QUERY_KEY, {
        id: profile.id,
        email: profile.email,
        displayName: profile.displayName,
        reportingCurrency: '',
      })
    },
  })

  const logoutMutation = useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.setQueryData(ME_QUERY_KEY, null)
    },
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    loginMutation.reset()
    loginMutation.mutate()
  }

  if (meQuery.isLoading) {
    return (
      <PageContainer>
        <p className="text-sm text-muted-foreground">{t('neutral.login.checkingSession')}</p>
      </PageContainer>
    )
  }

  const currentUser = meQuery.data

  if (currentUser) {
    return (
      <PageContainer>
        <div className="rounded-xl border border-border bg-card p-6">
          <p className="text-sm text-muted-foreground">{t('neutral.login.signedInAs')}</p>
          <p className="numeral-display" style={{ fontSize: '24px' }}>
            {currentUser.displayName ?? currentUser.email}
          </p>
        </div>
        <Button
          type="button"
          variant="outline"
          onClick={() => logoutMutation.mutate()}
          disabled={logoutMutation.isPending}
        >
          {logoutMutation.isPending ? t('neutral.login.signingOut') : t('neutral.login.signOut')}
        </Button>
        {logoutMutation.isError && (
          <p role="alert" className="text-sm text-destructive">
            {t('neutral.login.signOutError')}
          </p>
        )}
      </PageContainer>
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
            <FieldDescription role="alert" className="text-destructive">
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
    </PageContainer>
  )
}

/** Registro neutro (R21): mai un dettaglio su quale campo sia sbagliato.
 * Il backend stesso non fa oracolo (401 generico su credenziali). */
function loginErrorMessage(error: unknown): string {
  if (error instanceof ApiError && error.status === 429) {
    return t('neutral.login.tooManyAttempts')
  }

  return t('neutral.login.invalidCredentials')
}
