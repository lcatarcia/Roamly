import { useState, type FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { PageContainer } from '@/components/layout/PageContainer'
import { t } from '@/i18n/catalog'
import { ApiError, confirmEmail, resendEmailConfirmation } from '@/lib/api'

export function EmailConfirmationPage() {
  const [userId, setUserId] = useState('')
  const [token, setToken] = useState('')
  const [resendEmail, setResendEmail] = useState('')
  const mutation = useMutation({
    mutationFn: () => confirmEmail(userId.trim(), token),
    onSuccess: () => {
      setUserId('')
      setToken('')
    },
  })
  const resendMutation = useMutation({
    mutationFn: () => resendEmailConfirmation(resendEmail.trim()),
  })

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    mutation.reset()
    mutation.mutate()
  }

  function handleResendSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    resendMutation.reset()
    resendMutation.mutate()
  }

  return (
    <PageContainer size="form">
      <div>
        <h1 className="page-title">{t('neutral.emailConfirmation.title')}</h1>
        <p className="text-sm text-muted-foreground">
          {t('neutral.emailConfirmation.description')}
        </p>
        {import.meta.env.DEV && (
          <p className="mt-3 text-sm text-muted-foreground">
            {t('neutral.emailConfirmation.localDevelopment')}
          </p>
        )}
      </div>
      {mutation.isSuccess ? (
        <p role="status" className="text-sm text-foreground">
          {t('neutral.emailConfirmation.success')}
        </p>
      ) : (
        <form onSubmit={handleSubmit}>
          <FieldGroup>
            <Field>
              <FieldLabel htmlFor="confirm-user-id">{t('neutral.emailConfirmation.userId')}</FieldLabel>
              <FieldContent>
                <Input id="confirm-user-id" name="userId" autoComplete="off" required
                  value={userId} onChange={(event) => setUserId(event.target.value)} />
              </FieldContent>
            </Field>
            <Field>
              <FieldLabel htmlFor="confirm-token">{t('neutral.emailConfirmation.token')}</FieldLabel>
              <FieldContent>
                <Input id="confirm-token" name="token" type="password" autoComplete="off"
                  spellCheck={false} required value={token}
                  onChange={(event) => setToken(event.target.value)} />
              </FieldContent>
            </Field>
            {mutation.isError && (
              <p role="alert" className="text-sm text-danger-text">
                {confirmationErrorMessage(mutation.error)}
              </p>
            )}
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending
                ? t('neutral.emailConfirmation.submitting')
                : t('neutral.emailConfirmation.submit')}
            </Button>
          </FieldGroup>
        </form>
      )}
      {import.meta.env.DEV && (
        <section aria-labelledby="resend-confirmation-title" className="flex flex-col gap-4">
          <div>
            <h2 id="resend-confirmation-title" className="text-lg font-semibold">
              {t('neutral.emailConfirmation.resendTitle')}
            </h2>
            <p className="text-sm text-muted-foreground">
              {t('neutral.emailConfirmation.resendDescription')}
            </p>
            <p className="mt-2 text-sm text-muted-foreground">
              {t('neutral.emailConfirmation.resendLocalDevelopment')}
            </p>
          </div>
          {resendMutation.isSuccess && (
            <p role="status" className="text-sm text-foreground">
              {t('neutral.emailConfirmation.resendResult')}
            </p>
          )}
          <form onSubmit={handleResendSubmit}>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="resend-email">
                  {t('neutral.emailConfirmation.resendEmail')}
                </FieldLabel>
                <FieldContent>
                  <Input
                    id="resend-email"
                    name="email"
                    type="email"
                    autoComplete="email"
                    required
                    value={resendEmail}
                    onChange={(event) => setResendEmail(event.target.value)}
                  />
                </FieldContent>
              </Field>
              {resendMutation.isError && (
                <p role="alert" className="text-sm text-danger-text">
                  {t('neutral.emailConfirmation.resendError')}
                </p>
              )}
              <Button type="submit" disabled={resendMutation.isPending}>
                {resendMutation.isPending
                  ? t('neutral.emailConfirmation.resendSubmitting')
                  : t('neutral.emailConfirmation.resendSubmit')}
              </Button>
            </FieldGroup>
          </form>
        </section>
      )}
      <p className="text-sm text-muted-foreground">
        <Link className="underline underline-offset-4" to="/">
          {t('neutral.emailConfirmation.backToLogin')}
        </Link>
      </p>
    </PageContainer>
  )
}

function confirmationErrorMessage(error: unknown): string {
  return error instanceof ApiError && error.status === 400
    ? t('neutral.emailConfirmation.invalid')
    : t('neutral.emailConfirmation.error')
}
