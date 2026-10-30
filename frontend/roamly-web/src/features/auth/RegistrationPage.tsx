import { useRef, useState, type FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Link } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { PageContainer } from '@/components/layout/PageContainer'
import { t } from '@/i18n/catalog'
import { ApiError, register } from '@/lib/api'

export function RegistrationPage() {
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const confirmationRef = useRef<HTMLInputElement>(null)
  const mutation = useMutation({
    mutationFn: () => register(email.trim(), password, displayName.trim() || undefined),
  })
  const passwordsMismatch = submitted && password !== confirmation

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    mutation.reset()
    if (password !== confirmation) {
      confirmationRef.current?.focus()
      return
    }
    mutation.mutate()
  }

  if (mutation.isSuccess) {
    return <RegistrationAcceptedPage />
  }

  return (
    <PageContainer size="form">
      <div>
        <h1 className="page-title">{t('neutral.registration.title')}</h1>
        <p className="text-sm text-muted-foreground">{t('neutral.registration.description')}</p>
      </div>
      <form onSubmit={handleSubmit}>
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="register-email">{t('neutral.registration.email')}</FieldLabel>
            <FieldContent>
              <Input id="register-email" name="email" type="email" autoComplete="email" required
                value={email} onChange={(event) => setEmail(event.target.value)} />
            </FieldContent>
          </Field>
          <Field>
            <FieldLabel htmlFor="register-display-name">{t('neutral.registration.displayName')}</FieldLabel>
            <FieldContent>
              <Input id="register-display-name" name="displayName" autoComplete="name"
                value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
            </FieldContent>
          </Field>
          <Field>
            <FieldLabel htmlFor="register-password">{t('neutral.registration.password')}</FieldLabel>
            <FieldContent>
              <Input id="register-password" name="password" type="password"
                autoComplete="new-password" required value={password}
                onChange={(event) => setPassword(event.target.value)} />
            </FieldContent>
          </Field>
          <Field>
            <FieldLabel htmlFor="register-confirm-password">{t('neutral.registration.confirmPassword')}</FieldLabel>
            <FieldContent>
              <Input id="register-confirm-password" name="confirmPassword" type="password"
                autoComplete="new-password" required ref={confirmationRef}
                aria-invalid={passwordsMismatch}
                aria-describedby={passwordsMismatch ? 'register-password-mismatch' : undefined}
                value={confirmation} onChange={(event) => setConfirmation(event.target.value)} />
              {passwordsMismatch && (
                <FieldError id="register-password-mismatch">
                  {t('neutral.registration.passwordMismatch')}
                </FieldError>
              )}
            </FieldContent>
          </Field>
          {mutation.isError && (
            <p role="alert" className="text-sm text-danger-text">
              {registrationErrorMessage(mutation.error)}
            </p>
          )}
          <Button type="submit" disabled={mutation.isPending}>
            {mutation.isPending
              ? t('neutral.registration.submitting')
              : t('neutral.registration.submit')}
          </Button>
        </FieldGroup>
      </form>
      <p className="text-sm text-muted-foreground">
        <Link className="underline underline-offset-4" to="/">{t('neutral.registration.backToLogin')}</Link>
      </p>
    </PageContainer>
  )
}

function RegistrationAcceptedPage() {
  return (
    <PageContainer size="form">
      <section aria-labelledby="registration-accepted-title">
        <h1 id="registration-accepted-title" className="page-title">
          {t('neutral.registration.acceptedTitle')}
        </h1>
        <p role="status" className="mt-2 text-sm text-muted-foreground">
          {t('neutral.registration.acceptedMessage')}
        </p>
        {import.meta.env.DEV && (
          <p className="mt-4 text-sm text-muted-foreground">
            {t('neutral.registration.localDevelopment')}
          </p>
        )}
        <div className="mt-5 flex flex-col items-start gap-3">
          <Button render={<Link to="/email/confirm" />}>
            {t('neutral.registration.confirmEmailLink')}
          </Button>
          <Link className="text-sm underline underline-offset-4" to="/">
            {t('neutral.registration.backToLogin')}
          </Link>
        </div>
      </section>
    </PageContainer>
  )
}

function registrationErrorMessage(error: unknown): string {
  return error instanceof ApiError && error.status === 400
    ? t('neutral.registration.validationError')
    : t('neutral.registration.error')
}
