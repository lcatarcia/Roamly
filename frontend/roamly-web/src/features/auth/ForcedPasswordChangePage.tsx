import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldDescription, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { PageContainer } from '@/components/layout/PageContainer'
import { ME_QUERY_KEY } from '@/features/account/accountQueries'
import { useAccountLogout } from '@/features/account/accountSession'
import { t } from '@/i18n/catalog'
import { ApiError, changePassword } from '@/lib/api'

/** Blocca l'area prodotto fino a quando il server conferma il cambio della password iniziale. */
export function ForcedPasswordChangePage() {
  const queryClient = useQueryClient()
  const logoutMutation = useAccountLogout()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [confirmationMismatch, setConfirmationMismatch] = useState(false)

  const changeMutation = useMutation({
    mutationFn: () => changePassword(currentPassword, newPassword),
    onSuccess: async () => {
      setCurrentPassword('')
      setNewPassword('')
      setConfirmation('')
      await queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY })
    },
    onError: (error) => {
      if (error instanceof ApiError && error.status === 401) {
        setCurrentPassword('')
      }
    },
  })

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    changeMutation.reset()
    if (newPassword !== confirmation) {
      setConfirmationMismatch(true)
      return
    }

    setConfirmationMismatch(false)
    changeMutation.mutate()
  }

  return (
    <PageContainer>
      <div>
        <h1 className="page-title">{t('neutral.passwordChange.title')}</h1>
        <p className="text-sm text-muted-foreground">{t('neutral.passwordChange.description')}</p>
      </div>

      <form onSubmit={handleSubmit}>
        <FieldGroup>
          <Field>
            <FieldLabel htmlFor="password-change-current">
              {t('neutral.passwordChange.currentPassword')}
            </FieldLabel>
            <FieldContent>
              <Input
                id="password-change-current"
                name="currentPassword"
                type="password"
                autoComplete="current-password"
                required
                value={currentPassword}
                onChange={(event) => setCurrentPassword(event.target.value)}
              />
            </FieldContent>
          </Field>

          <Field>
            <FieldLabel htmlFor="password-change-new">
              {t('neutral.passwordChange.newPassword')}
            </FieldLabel>
            <FieldContent>
              <Input
                id="password-change-new"
                name="newPassword"
                type="password"
                autoComplete="new-password"
                required
                value={newPassword}
                onChange={(event) => {
                  setNewPassword(event.target.value)
                  setConfirmationMismatch(false)
                }}
              />
            </FieldContent>
          </Field>

          <Field>
            <FieldLabel htmlFor="password-change-confirmation">
              {t('neutral.passwordChange.confirmPassword')}
            </FieldLabel>
            <FieldContent>
              <Input
                id="password-change-confirmation"
                name="confirmation"
                type="password"
                autoComplete="new-password"
                required
                aria-invalid={confirmationMismatch}
                aria-describedby={confirmationMismatch ? 'password-change-confirmation-error' : undefined}
                value={confirmation}
                onChange={(event) => {
                  setConfirmation(event.target.value)
                  setConfirmationMismatch(false)
                }}
              />
            </FieldContent>
            {confirmationMismatch && (
              <FieldDescription id="password-change-confirmation-error" role="alert">
                {t('neutral.passwordChange.passwordMismatch')}
              </FieldDescription>
            )}
          </Field>

          {changeMutation.isError && (
            <FieldDescription role="alert" className="text-danger-text">
              {passwordChangeErrorMessage(changeMutation.error)}
            </FieldDescription>
          )}

          <Button type="submit" disabled={changeMutation.isPending || logoutMutation.isPending}>
            {changeMutation.isPending
              ? t('neutral.passwordChange.submitting')
              : t('neutral.passwordChange.submit')}
          </Button>
          <Button
            type="button"
            variant="outline"
            disabled={changeMutation.isPending || logoutMutation.isPending}
            onClick={() => logoutMutation.mutate()}
          >
            {logoutMutation.isPending
              ? t('neutral.login.signingOut')
              : t('neutral.passwordChange.logout')}
          </Button>
          {logoutMutation.isError && (
            <FieldDescription role="alert" className="text-danger-text">
              {t('neutral.login.signOutError')}
            </FieldDescription>
          )}
        </FieldGroup>
      </form>
    </PageContainer>
  )
}

function passwordChangeErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401) {
      return t('neutral.passwordChange.currentPasswordError')
    }
    if (error.status === 400) {
      return t('neutral.passwordChange.passwordPolicyError')
    }
  }

  return t('neutral.passwordChange.error')
}
