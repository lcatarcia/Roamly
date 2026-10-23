import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { t } from '@/i18n/catalog'
import { ApiError, fetchCamperProfile, updateCamperProfile, type CamperProfile, type CamperProfileInput } from '@/lib/api'
import {
  CAMPER_DASHBOARD_QUERY_KEY,
  CAMPERS_QUERY_KEY,
  camperProfileQueryKey,
} from '@/features/campers/queryKeys'

type ProfileValues = Record<keyof CamperProfileInput, string>

const DIMENSIONS: Array<{ key: keyof ProfileValues; label: string }> = [
  { key: 'lengthMm', label: t('neutral.camper.profile.lengthMm') },
  { key: 'widthMm', label: t('neutral.camper.profile.widthMm') },
  { key: 'heightMm', label: t('neutral.camper.profile.heightMm') },
]
const WEIGHTS: Array<{ key: keyof ProfileValues; label: string }> = [
  { key: 'kerbWeightKg', label: t('neutral.camper.profile.kerbWeightKg') },
  { key: 'maxWeightKg', label: t('neutral.camper.profile.maxWeightKg') },
]
const CAPACITIES: Array<{ key: keyof ProfileValues; label: string }> = [
  { key: 'freshWaterL', label: t('neutral.camper.profile.freshWaterL') },
  { key: 'greyWaterL', label: t('neutral.camper.profile.greyWaterL') },
  { key: 'fuelTankL', label: t('neutral.camper.profile.fuelTankL') },
  { key: 'gasKg', label: t('neutral.camper.profile.gasKg') },
]

/** Schermata completa del profilo tecnico, con ETag conservato tra GET e PUT. */
export function CamperProfileForm({ camperId }: { camperId: string }) {
  const profileQuery = useQuery({
    queryKey: camperProfileQueryKey(camperId),
    queryFn: () => fetchCamperProfile(camperId),
    enabled: camperId.length > 0,
    retry: false,
  })

  if (profileQuery.isPending) {
    return <p role="status" className="text-sm text-muted-foreground">{t('neutral.camper.profile.loading')}</p>
  }

  if (profileQuery.isError || !profileQuery.data) {
    return <p role="alert" className="text-sm text-danger-text">{t('neutral.camper.profile.error')}</p>
  }

  return (
    <CamperProfileEditor
      camperId={camperId}
      etag={profileQuery.data.etag}
      profile={profileQuery.data.profile}
    />
  )
}

function CamperProfileEditor({
  camperId,
  etag,
  profile,
}: {
  camperId: string
  etag: string
  profile: CamperProfile
}) {
  const queryClient = useQueryClient()
  const [values, setValues] = useState(() => toProfileValues(profile))
  const [currentEtag, setCurrentEtag] = useState(etag)
  const [nameInvalid, setNameInvalid] = useState(false)

  const saveMutation = useMutation({
    mutationFn: ({ profile, etag }: { profile: CamperProfileInput; etag: string }) =>
      updateCamperProfile(camperId, profile, etag),
    onSuccess: async (result) => {
      queryClient.setQueryData(camperProfileQueryKey(camperId), {
        profile: result.value,
        etag: result.etag,
      })
      setCurrentEtag(result.etag)
      setValues(toProfileValues(result.value))
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: CAMPER_DASHBOARD_QUERY_KEY }),
        queryClient.invalidateQueries({ queryKey: CAMPERS_QUERY_KEY }),
      ])
    },
    onError: async (error) => {
      if (error instanceof ApiError && error.status === 412) {
        try {
          const latest = await fetchCamperProfile(camperId)
          queryClient.setQueryData(camperProfileQueryKey(camperId), latest)
          setCurrentEtag(latest.etag)
          setValues(toProfileValues(latest.profile))
        } catch {
          // Il messaggio di conflitto resta visibile anche se il refresh non riesce.
        }
      }
    },
  })

  function updateField(key: keyof ProfileValues, value: string) {
    setValues((current) => ({ ...current, [key]: value }))
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setNameInvalid(!values.name.trim())
    if (!values.name.trim()) {
      return
    }

    saveMutation.mutate({
      profile: toProfileInput(values),
      etag: currentEtag,
    })
  }

  return (
    <section className="rounded-xl border border-border bg-card p-6">
      <header className="mb-5">
        <h1 className="page-title">{t('neutral.camper.profile.title')}</h1>
        <p className="text-sm text-muted-foreground">{t('neutral.camper.profile.description')}</p>
        <p className="mt-2 text-sm text-muted-foreground">{t('neutral.camper.profile.groupsNote')}</p>
      </header>
      <form onSubmit={handleSubmit} noValidate>
        <FieldGroup>
          <fieldset className="grid gap-4 rounded-lg border border-border p-4 sm:grid-cols-2">
            <legend className="px-1 text-sm font-semibold">{t('neutral.camper.profile.identity')}</legend>
            <ProfileField fieldKey="name" label={t('neutral.camper.profile.name')} value={values.name}
              required invalid={nameInvalid} onChange={updateField} />
            <ProfileField fieldKey="brand" label={t('neutral.camper.profile.brand')} value={values.brand}
              onChange={updateField} />
            <ProfileField fieldKey="model" label={t('neutral.camper.profile.model')} value={values.model}
              onChange={updateField} />
            <ProfileField fieldKey="year" label={t('neutral.camper.profile.year')} value={values.year}
              type="number" onChange={updateField} />
            <ProfileField fieldKey="plateNumber" label={t('neutral.camper.profile.plateNumber')} value={values.plateNumber}
              onChange={updateField} />
            <SelectProfileField fieldKey="vehicleKind" label={t('neutral.camper.profile.vehicleKind')}
              value={values.vehicleKind} options={vehicleOptions()} onChange={updateField} />
          </fieldset>

          <ProfileFieldset title={t('neutral.camper.profile.dimensions')} fields={DIMENSIONS}
            values={values} onChange={updateField} unit={t('neutral.camper.profile.millimetres')} />
          <ProfileFieldset title={t('neutral.camper.profile.weights')} fields={WEIGHTS}
            values={values} onChange={updateField} unit={t('neutral.camper.profile.kilograms')} />
          <ProfileFieldset title={t('neutral.camper.profile.capacities')} fields={CAPACITIES}
            values={values} onChange={updateField} />

          <fieldset className="grid gap-4 rounded-lg border border-border p-4 sm:grid-cols-2">
            <legend className="px-1 text-sm font-semibold">{t('neutral.camper.profile.fuel')}</legend>
            <SelectProfileField fieldKey="fuelKind" label={t('neutral.camper.profile.fuelKind')}
              value={values.fuelKind} options={fuelOptions()} onChange={updateField} optional />
            <ProfileField fieldKey="averageConsumptionLPer100Km"
              label={t('neutral.camper.profile.averageConsumption')} value={values.averageConsumptionLPer100Km}
              type="number" step="0.1" onChange={updateField} />
          </fieldset>

          <Button type="submit" disabled={saveMutation.isPending}>
            {saveMutation.isPending ? t('neutral.camper.profile.saving') : t('neutral.camper.profile.save')}
          </Button>
        </FieldGroup>
      </form>
      {saveMutation.isSuccess && (
        <p role="status" className="mt-4 text-sm text-foreground">{t('neutral.camper.profile.saved')}</p>
      )}
      {saveMutation.isError && (
        <p role="alert" className="mt-4 rounded-lg bg-background px-2 py-1 text-sm text-danger-text">
          {saveErrorMessage(saveMutation.error)}
        </p>
      )}
    </section>
  )
}

function ProfileFieldset({
  title,
  fields,
  values,
  onChange,
  unit,
}: {
  title: string
  fields: Array<{ key: keyof ProfileValues; label: string }>
  values: ProfileValues
  onChange: (key: keyof ProfileValues, value: string) => void
  unit?: string
}) {
  return (
    <fieldset className="grid gap-4 rounded-lg border border-border p-4 sm:grid-cols-2">
      <legend className="px-1 text-sm font-semibold">{title}{unit ? ` · ${unit}` : ''}</legend>
      {fields.map((field) => (
        <ProfileField key={field.key} fieldKey={field.key} label={field.label} value={values[field.key]}
          type="number" onChange={onChange} />
      ))}
    </fieldset>
  )
}

function ProfileField({
  fieldKey,
  label,
  value,
  onChange,
  type = 'text',
  step = '1',
  required = false,
  invalid = false,
}: {
  fieldKey: keyof ProfileValues
  label: string
  value: string
  onChange: (key: keyof ProfileValues, value: string) => void
  type?: 'text' | 'number'
  step?: string
  required?: boolean
  invalid?: boolean
}) {
  const id = `camper-profile-${fieldKey}`
  return (
    <Field>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <FieldContent>
        <Input id={id} name={fieldKey} type={type} step={step} value={value}
          required={required} aria-invalid={invalid}
          aria-describedby={invalid ? `${id}-error` : undefined}
          onChange={(event) => onChange(fieldKey, event.target.value)} />
        {invalid && <span id={`${id}-error`} className="text-sm text-danger-text">{t('neutral.camper.profile.nameRequired')}</span>}
      </FieldContent>
    </Field>
  )
}

function SelectProfileField({
  fieldKey,
  label,
  value,
  options,
  onChange,
  optional = false,
}: {
  fieldKey: keyof ProfileValues
  label: string
  value: string
  options: Array<{ value: string; label: string }>
  onChange: (key: keyof ProfileValues, value: string) => void
  optional?: boolean
}) {
  const id = `camper-profile-${fieldKey}`
  return (
    <Field>
      <FieldLabel htmlFor={id}>{label}</FieldLabel>
      <FieldContent>
        <select id={id} name={fieldKey} value={value}
          required={!optional}
          className="h-8 w-full rounded-lg border border-input bg-background px-2.5 text-sm text-foreground focus-visible:border-ring focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
          onChange={(event) => onChange(fieldKey, event.target.value)}>
          {optional && <option value="">{t('neutral.camper.profile.notSpecified')}</option>}
          {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
        </select>
      </FieldContent>
    </Field>
  )
}

function toProfileValues(profile: CamperProfile): ProfileValues {
  return {
    name: profile.name,
    brand: profile.brand ?? '',
    model: profile.model ?? '',
    year: textValue(profile.year),
    plateNumber: profile.plateNumber ?? '',
    vehicleKind: String(profile.vehicleKind),
    lengthMm: textValue(profile.lengthMm),
    widthMm: textValue(profile.widthMm),
    heightMm: textValue(profile.heightMm),
    kerbWeightKg: textValue(profile.kerbWeightKg),
    maxWeightKg: textValue(profile.maxWeightKg),
    freshWaterL: textValue(profile.freshWaterL),
    greyWaterL: textValue(profile.greyWaterL),
    fuelTankL: textValue(profile.fuelTankL),
    gasKg: textValue(profile.gasKg),
    fuelKind: textValue(profile.fuelKind),
    averageConsumptionLPer100Km: textValue(profile.averageConsumptionLPer100Km),
  }
}

function toProfileInput(values: ProfileValues): CamperProfileInput {
  return {
    name: values.name.trim(),
    brand: optionalText(values.brand),
    model: optionalText(values.model),
    year: optionalNumber(values.year),
    plateNumber: optionalText(values.plateNumber),
    vehicleKind: Number(values.vehicleKind),
    lengthMm: optionalNumber(values.lengthMm),
    widthMm: optionalNumber(values.widthMm),
    heightMm: optionalNumber(values.heightMm),
    kerbWeightKg: optionalNumber(values.kerbWeightKg),
    maxWeightKg: optionalNumber(values.maxWeightKg),
    freshWaterL: optionalNumber(values.freshWaterL),
    greyWaterL: optionalNumber(values.greyWaterL),
    fuelTankL: optionalNumber(values.fuelTankL),
    gasKg: optionalNumber(values.gasKg),
    fuelKind: values.fuelKind ? Number(values.fuelKind) : null,
    averageConsumptionLPer100Km: optionalNumber(values.averageConsumptionLPer100Km),
  }
}

function textValue(value: string | number | null): string {
  return value === null ? '' : String(value)
}

function optionalText(value: string): string | null {
  return value.trim() ? value.trim() : null
}

function optionalNumber(value: string): number | null {
  return value.trim() ? Number(value) : null
}

function vehicleOptions() {
  return [
    { value: '0', label: t('neutral.camper.create.motorhome') },
    { value: '1', label: t('neutral.camper.create.van') },
    { value: '2', label: t('neutral.camper.create.coachbuilt') },
    { value: '3', label: t('neutral.camper.create.alcove') },
    { value: '4', label: t('neutral.camper.create.caravan') },
  ]
}

function fuelOptions() {
  return [
    { value: '0', label: t('neutral.camper.profile.fuelDiesel') },
    { value: '1', label: t('neutral.camper.profile.fuelPetrol') },
    { value: '2', label: t('neutral.camper.profile.fuelLpg') },
    { value: '3', label: t('neutral.camper.profile.fuelCng') },
    { value: '4', label: t('neutral.camper.profile.fuelElectric') },
    { value: '5', label: t('neutral.camper.profile.fuelHybrid') },
  ]
}

function saveErrorMessage(error: unknown): string {
  return error instanceof ApiError && error.status === 412
    ? t('neutral.camper.profile.stale')
    : t('neutral.camper.profile.saveError')
}
