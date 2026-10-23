import { useRef, useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { t } from '@/i18n/catalog'
import { ApiError, createCamper } from '@/lib/api'
import { createCombGuid } from '@/lib/comb-guid.mjs'
import { CAMPERS_QUERY_KEY } from '@/features/campers/queryKeys'

type VehicleKind = '' | '0' | '1' | '2' | '3' | '4'
interface CamperSubmission {
  id: string
  name: string
  vehicleKind: number
}

const VEHICLE_KINDS = [
  '0',
  '1',
  '2',
  '3',
  '4',
] as const

/** Primo passo autenticato per creare un camper; il resto delle specifiche resta fuori scope. */
export function CreateCamperForm() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [vehicleKind, setVehicleKind] = useState<VehicleKind>('')
  const [submitted, setSubmitted] = useState(false)
  const submissionRef = useRef<CamperSubmission | null>(null)
  const nameInputRef = useRef<HTMLInputElement>(null)
  const vehicleKindInputRef = useRef<HTMLSelectElement>(null)
  const createMutation = useMutation({
    mutationFn: (submission: CamperSubmission) =>
      createCamper(submission.id, submission.name, submission.vehicleKind),
    onSuccess: async (camper) => {
      submissionRef.current = null
      setName('')
      setVehicleKind('')
      setSubmitted(false)
      await queryClient.invalidateQueries({ queryKey: CAMPERS_QUERY_KEY })
      navigate(`/campers/${camper.id}`, { replace: true })
    },
  })

  const nameInvalid = submitted && name.trim().length === 0
  const vehicleKindInvalid = submitted && vehicleKind === ''

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    createMutation.reset()

    if (nameInvalid || name.trim().length === 0 || vehicleKindInvalid || vehicleKind === '') {
      if (name.trim().length === 0) {
        nameInputRef.current?.focus()
      } else {
        vehicleKindInputRef.current?.focus()
      }
      return
    }

    const normalizedName = name.trim()
    const selectedVehicleKind = Number(vehicleKind)
    const previousSubmission = submissionRef.current
    const submission =
      previousSubmission?.name === normalizedName
        && previousSubmission.vehicleKind === selectedVehicleKind
        ? previousSubmission
        : { id: createCombGuid(), name: normalizedName, vehicleKind: selectedVehicleKind }

    submissionRef.current = submission
    createMutation.mutate(submission)
  }

  return (
    <>
      <section className="rounded-xl border border-border bg-card p-6">
        <div className="mb-5">
          <h1 className="page-title">{t('neutral.camper.create.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('neutral.camper.create.description')}</p>
        </div>
        <CamperFields
          name={name}
          nameInvalid={nameInvalid}
          nameInputRef={nameInputRef}
          vehicleKind={vehicleKind}
          vehicleKindInvalid={vehicleKindInvalid}
          vehicleKindInputRef={vehicleKindInputRef}
          isPending={createMutation.isPending}
          onNameChange={setName}
          onVehicleKindChange={setVehicleKind}
          onSubmit={handleSubmit}
        />
      </section>
      {createMutation.isSuccess && (
        <p role="status" className="text-sm text-foreground">
          {t('neutral.camper.create.created')} {createMutation.data.name}
        </p>
      )}
      {createMutation.isPending && (
        <p role="status" aria-live="polite" className="text-sm text-muted-foreground">
          {t('neutral.camper.create.submitting')}
        </p>
      )}
      {createMutation.isError && (
        <p role="alert" className="text-sm text-danger-text">
          {createErrorMessage(createMutation.error)}
        </p>
      )}
    </>
  )
}

interface CamperFieldsProps {
  name: string
  nameInvalid: boolean
  nameInputRef: React.RefObject<HTMLInputElement | null>
  vehicleKind: VehicleKind
  vehicleKindInvalid: boolean
  vehicleKindInputRef: React.RefObject<HTMLSelectElement | null>
  isPending: boolean
  onNameChange: (name: string) => void
  onVehicleKindChange: (vehicleKind: VehicleKind) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}

function CamperFields({
  name,
  nameInvalid,
  nameInputRef,
  vehicleKind,
  vehicleKindInvalid,
  vehicleKindInputRef,
  isPending,
  onNameChange,
  onVehicleKindChange,
  onSubmit,
}: CamperFieldsProps) {
  return (
    <form onSubmit={onSubmit} noValidate>
      <FieldGroup>
        <NameField
          value={name}
          invalid={nameInvalid}
          inputRef={nameInputRef}
          disabled={isPending}
          onChange={onNameChange}
        />
        <VehicleKindField
          value={vehicleKind}
          invalid={vehicleKindInvalid}
          inputRef={vehicleKindInputRef}
          disabled={isPending}
          onChange={onVehicleKindChange}
        />
        <Button type="submit" disabled={isPending}>
          {isPending ? t('neutral.camper.create.submitting') : t('neutral.camper.create.submit')}
        </Button>
      </FieldGroup>
    </form>
  )
}

function NameField({
  value,
  invalid,
  inputRef,
  disabled,
  onChange,
}: {
  value: string
  invalid: boolean
  inputRef: React.RefObject<HTMLInputElement | null>
  disabled: boolean
  onChange: (name: string) => void
}) {
  return (
    <Field>
      <FieldLabel htmlFor="camper-name">{t('neutral.camper.create.name')}</FieldLabel>
      <FieldContent>
        <Input
          id="camper-name"
          name="name"
          ref={inputRef}
          autoComplete="off"
          maxLength={60}
          required
          disabled={disabled}
          aria-invalid={invalid}
          aria-describedby={invalid ? 'camper-name-error' : undefined}
          value={value}
          onChange={(event) => onChange(event.target.value)}
        />
        {invalid && (
          <FieldError id="camper-name-error">{t('neutral.camper.create.nameRequired')}</FieldError>
        )}
      </FieldContent>
    </Field>
  )
}

function VehicleKindField({
  value,
  invalid,
  inputRef,
  disabled,
  onChange,
}: {
  value: VehicleKind
  invalid: boolean
  inputRef: React.RefObject<HTMLSelectElement | null>
  disabled: boolean
  onChange: (vehicleKind: VehicleKind) => void
}) {
  return (
    <Field>
      <FieldLabel htmlFor="camper-vehicle-kind">{t('neutral.camper.create.vehicleKind')}</FieldLabel>
      <FieldContent>
        <select
          id="camper-vehicle-kind"
          name="vehicleKind"
          ref={inputRef}
          required
          disabled={disabled}
          aria-invalid={invalid}
          aria-describedby={invalid ? 'camper-vehicle-kind-error' : undefined}
          className="h-8 w-full rounded-lg border border-input bg-background px-2.5 text-sm text-foreground focus-visible:border-ring focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50 aria-invalid:border-destructive"
          value={value}
          onChange={(event) => onChange(event.target.value as VehicleKind)}
        >
          <option value="">—</option>
          {VEHICLE_KINDS.map((kind) => (
            <option key={kind} value={kind}>
              {vehicleKindLabel(kind)}
            </option>
          ))}
        </select>
        {invalid && (
          <FieldError id="camper-vehicle-kind-error">
            {t('neutral.camper.create.vehicleKindRequired')}
          </FieldError>
        )}
      </FieldContent>
    </Field>
  )
}

function vehicleKindLabel(kind: Exclude<VehicleKind, ''>): string {
  switch (kind) {
    case '0':
      return t('neutral.camper.create.motorhome')
    case '1':
      return t('neutral.camper.create.van')
    case '2':
      return t('neutral.camper.create.coachbuilt')
    case '3':
      return t('neutral.camper.create.alcove')
    case '4':
      return t('neutral.camper.create.caravan')
  }
}

function createErrorMessage(error: unknown): string {
  return error instanceof ApiError && error.status === 409
    ? t('neutral.camper.create.conflict')
    : t('neutral.camper.create.error')
}
