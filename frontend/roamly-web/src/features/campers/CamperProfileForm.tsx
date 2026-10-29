import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { CamperManualSection } from '@/features/campers/CamperManualSection'
import { t } from '@/i18n/catalog'
import {
  ApiError,
  fetchCamperProfile,
  updateCamperProfile,
  type CamperManualMeasurement,
  type CamperManualParseResponse,
  type CamperManualViewImages,
  type CamperProfile,
  type CamperProfileInput,
} from '@/lib/api'
import {
  CAMPER_DASHBOARD_QUERY_KEY,
  CAMPERS_QUERY_KEY,
  camperProfileQueryKey,
} from '@/features/campers/queryKeys'

type EditableFieldKey = Exclude<
  keyof CamperProfileInput,
  'manualSourceName' | 'manualSourceUrl' | 'manualMeasurements' | 'manualViewImages'
>
type ProfileValues = Record<EditableFieldKey, string>

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
  const [manualSourceName, setManualSourceName] = useState(() => profile.manualSourceName ?? '')
  const [manualSourceUrl, setManualSourceUrl] = useState(() => profile.manualSourceUrl ?? '')
  const [manualMeasurements, setManualMeasurements] = useState<CamperManualMeasurement[]>(
    () => profile.manualMeasurements ?? [],
  )
  const [manualViewImages, setManualViewImages] = useState<CamperManualViewImages>(
    () => profile.manualViewImages ?? {},
  )
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
      setManualSourceName(result.value.manualSourceName ?? '')
      setManualSourceUrl(result.value.manualSourceUrl ?? '')
      setManualMeasurements(result.value.manualMeasurements ?? [])
      setManualViewImages(result.value.manualViewImages ?? {})
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
          setManualSourceName(latest.profile.manualSourceName ?? '')
          setManualSourceUrl(latest.profile.manualSourceUrl ?? '')
          setManualMeasurements(latest.profile.manualMeasurements ?? [])
          setManualViewImages(latest.profile.manualViewImages ?? {})
        } catch {
          // Il messaggio di conflitto resta visibile anche se il refresh non riesce.
        }
      }
    },
  })

  function updateField(key: keyof ProfileValues, value: string) {
    setValues((current) => ({ ...current, [key]: value }))
  }

  function handleApplyParsedDraft(draft: CamperManualParseResponse, saveImmediately: boolean) {
    const nextValues = mergeDraftIntoValues(values, draft)
    const nextSourceName = draft.sourceName || manualSourceName
    const nextSourceUrl = draft.sourceUrl ?? manualSourceUrl
    const nextMeasurements = mergeDraftMeasurements(manualMeasurements, draft.measurements)

    setValues(nextValues)
    setManualSourceName(nextSourceName)
    setManualSourceUrl(nextSourceUrl)
    setManualMeasurements(nextMeasurements)

    if (saveImmediately && nextValues.name.trim()) {
      saveMutation.mutate({
        profile: toProfileInput(
          nextValues,
          nextSourceName,
          nextSourceUrl,
          nextMeasurements,
          manualViewImages,
          true,
        ),
        etag: currentEtag,
      })
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setNameInvalid(!values.name.trim())
    if (!values.name.trim()) {
      return
    }

    saveMutation.mutate({
      profile: toProfileInput(
        values,
        manualSourceName,
        manualSourceUrl,
        manualMeasurements,
        manualViewImages,
        false,
      ),
      etag: currentEtag,
    })
  }

  return (
    <div className="space-y-6">
    <section className="rounded-3xl border border-border bg-card p-6 shadow-xs sm:p-8">
      <header className="mb-6">
        <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          Scheda tecnica e omologazione
        </p>
        <h1 className="page-title mt-1">{t('neutral.camper.profile.title')}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t('neutral.camper.profile.description')}</p>
        <p className="mt-2 rounded-xl border border-border bg-background px-3.5 py-2 text-xs text-muted-foreground">{t('neutral.camper.profile.groupsNote')}</p>
      </header>
      <form onSubmit={handleSubmit} noValidate>
        <FieldGroup>
          <fieldset className="grid gap-4 rounded-2xl border border-border bg-background p-5 sm:grid-cols-3">
            <legend className="px-2 text-xs font-bold uppercase tracking-wider text-foreground">{t('neutral.camper.profile.identity')}</legend>
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

          <fieldset className="grid gap-4 rounded-2xl border border-border bg-background p-5 sm:grid-cols-2">
            <legend className="px-2 text-xs font-bold uppercase tracking-wider text-foreground">{t('neutral.camper.profile.fuel')}</legend>
            <SelectProfileField fieldKey="fuelKind" label={t('neutral.camper.profile.fuelKind')}
              value={values.fuelKind} options={fuelOptions()} onChange={updateField} optional />
            <ProfileField fieldKey="averageConsumptionLPer100Km"
              label={t('neutral.camper.profile.averageConsumption')} value={values.averageConsumptionLPer100Km}
              type="number" step="0.1" onChange={updateField} />
          </fieldset>

          <div className="flex justify-end pt-2">
            <Button type="submit" size="lg" disabled={saveMutation.isPending}>
              {saveMutation.isPending ? t('neutral.camper.profile.saving') : t('neutral.camper.profile.save')}
            </Button>
          </div>
        </FieldGroup>
      </form>
      {saveMutation.isSuccess && (
        <p role="status" className="mt-4 rounded-xl border border-border bg-background px-4 py-2.5 text-sm font-medium text-foreground">{t('neutral.camper.profile.saved')}</p>
      )}
      {saveMutation.isError && (
        <p role="alert" className="mt-4 rounded-xl bg-background px-3 py-2 text-sm text-danger-text">
          {saveErrorMessage(saveMutation.error)}
        </p>
      )}
    </section>

    <CamperManualSection
      camperId={camperId}
      camperName={values.name}
      dimensions={{
        lengthMm: values.lengthMm,
        widthMm: values.widthMm,
        heightMm: values.heightMm,
      }}
      manualSourceName={manualSourceName}
      manualSourceUrl={manualSourceUrl}
      manualMeasurements={manualMeasurements}
      manualViewImages={manualViewImages}
      onSourceChange={(sourceName, sourceUrl) => {
        setManualSourceName(sourceName)
        setManualSourceUrl(sourceUrl)
      }}
      onMeasurementsChange={setManualMeasurements}
      onViewImagesChange={(nextViewImages, saveImmediately) => {
        setManualViewImages(nextViewImages)
        if (saveImmediately && values.name.trim()) {
          saveMutation.mutate({
            profile: toProfileInput(
              values,
              manualSourceName,
              manualSourceUrl,
              manualMeasurements,
              nextViewImages,
              true,
            ),
            etag: currentEtag,
          })
        }
      }}
      onApplyParsedDraft={handleApplyParsedDraft}
      onSaveManualOnly={() => {
        if (!values.name.trim()) return
        saveMutation.mutate({
          profile: toProfileInput(
            values,
            manualSourceName,
            manualSourceUrl,
            manualMeasurements,
            manualViewImages,
            true,
          ),
          etag: currentEtag,
        })
      }}
      isSavingProfile={saveMutation.isPending}
      profileSavedSuccess={saveMutation.isSuccess}
    />
    </div>
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
    <fieldset className="grid gap-4 rounded-2xl border border-border bg-background p-5 sm:grid-cols-2">
      <legend className="px-2 text-xs font-bold uppercase tracking-wider text-foreground">{title}{unit ? ` · ${unit}` : ''}</legend>
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
          className="h-11 w-full cursor-pointer rounded-xl border border-input bg-card px-3.5 text-sm text-foreground shadow-2xs focus-visible:border-ring focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50"
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

function toProfileInput(
  values: ProfileValues,
  manualSourceName: string,
  manualSourceUrl: string,
  manualMeasurements: CamperManualMeasurement[],
  manualViewImages: CamperManualViewImages,
  guardPartialGroups: boolean,
): CamperProfileInput {
  const lengthMm = optionalNumber(values.lengthMm)
  const widthMm = optionalNumber(values.widthMm)
  const heightMm = optionalNumber(values.heightMm)
  const dimensionsComplete = lengthMm !== null && widthMm !== null && heightMm !== null

  const kerbWeightKg = optionalNumber(values.kerbWeightKg)
  const maxWeightKg = optionalNumber(values.maxWeightKg)
  const weightsComplete = kerbWeightKg !== null && maxWeightKg !== null

  const freshWaterL = optionalNumber(values.freshWaterL)
  const greyWaterL = optionalNumber(values.greyWaterL)
  const fuelTankL = optionalNumber(values.fuelTankL)
  const gasKg = optionalNumber(values.gasKg)
  const capacitiesComplete =
    freshWaterL !== null && greyWaterL !== null && fuelTankL !== null && gasKg !== null

  return {
    name: values.name.trim(),
    brand: optionalText(values.brand),
    model: optionalText(values.model),
    year: optionalNumber(values.year),
    plateNumber: optionalText(values.plateNumber),
    vehicleKind: Number(values.vehicleKind),
    lengthMm: !guardPartialGroups || dimensionsComplete ? lengthMm : null,
    widthMm: !guardPartialGroups || dimensionsComplete ? widthMm : null,
    heightMm: !guardPartialGroups || dimensionsComplete ? heightMm : null,
    kerbWeightKg: !guardPartialGroups || weightsComplete ? kerbWeightKg : null,
    maxWeightKg: !guardPartialGroups || weightsComplete ? maxWeightKg : null,
    freshWaterL: !guardPartialGroups || capacitiesComplete ? freshWaterL : null,
    greyWaterL: !guardPartialGroups || capacitiesComplete ? greyWaterL : null,
    fuelTankL: !guardPartialGroups || capacitiesComplete ? fuelTankL : null,
    gasKg: !guardPartialGroups || capacitiesComplete ? gasKg : null,
    fuelKind: values.fuelKind ? Number(values.fuelKind) : null,
    averageConsumptionLPer100Km: optionalNumber(values.averageConsumptionLPer100Km),
    manualSourceName: optionalText(manualSourceName),
    manualSourceUrl: optionalText(manualSourceUrl),
    manualMeasurements,
    manualViewImages,
  }
}

function mergeDraftIntoValues(
  current: ProfileValues,
  draft: CamperManualParseResponse,
): ProfileValues {
  return {
    ...current,
    brand: draft.brand ?? current.brand,
    model: draft.model ?? current.model,
    year: draft.year !== null ? String(draft.year) : current.year,
    vehicleKind: draft.vehicleKind !== null ? String(draft.vehicleKind) : current.vehicleKind,
    lengthMm: draft.lengthMm !== null ? String(draft.lengthMm) : current.lengthMm,
    widthMm: draft.widthMm !== null ? String(draft.widthMm) : current.widthMm,
    heightMm: draft.heightMm !== null ? String(draft.heightMm) : current.heightMm,
    kerbWeightKg: draft.kerbWeightKg !== null ? String(draft.kerbWeightKg) : current.kerbWeightKg,
    maxWeightKg: draft.maxWeightKg !== null ? String(draft.maxWeightKg) : current.maxWeightKg,
    freshWaterL: draft.freshWaterL !== null ? String(draft.freshWaterL) : current.freshWaterL,
    greyWaterL: draft.greyWaterL !== null ? String(draft.greyWaterL) : current.greyWaterL,
    fuelTankL: draft.fuelTankL !== null ? String(draft.fuelTankL) : current.fuelTankL,
    gasKg: draft.gasKg !== null ? String(draft.gasKg) : current.gasKg,
    fuelKind: draft.fuelKind !== null ? String(draft.fuelKind) : current.fuelKind,
    averageConsumptionLPer100Km:
      draft.averageConsumptionLPer100Km !== null
        ? String(draft.averageConsumptionLPer100Km)
        : current.averageConsumptionLPer100Km,
  }
}

function mergeDraftMeasurements(
  existing: CamperManualMeasurement[],
  extracted: CamperManualMeasurement[],
): CamperManualMeasurement[] {
  if (extracted.length === 0) {
    return existing
  }

  const extractedIds = new Set(extracted.map((item) => item.id))
  const preservedCustom = existing.filter((item) => !extractedIds.has(item.id))
  return [...extracted, ...preservedCustom]
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
