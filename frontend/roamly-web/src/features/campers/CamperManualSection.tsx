import { useState, type ChangeEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import {
  CAMPER_DASHBOARD_QUERY_KEY,
  CAMPERS_QUERY_KEY,
  camperProfileQueryKey,
} from '@/features/campers/queryKeys'
import {
  fetchCamperProfile,
  parseCamperManual,
  updateCamperProfile,
  type CamperManualMeasurement,
  type CamperManualParseResponse,
  type CamperManualView,
  type CamperManualViewImages,
  type CamperProfile,
  type CamperProfileInput,
} from '@/lib/api'

interface CamperDimensions {
  lengthMm: string
  widthMm: string
  heightMm: string
}

type DisplayMode = 'map' | 'list' | 'zones'

const MAP_VIEWS: ReadonlyArray<{ id: CamperManualView; label: string }> = [
  { id: 'side', label: 'Laterale' },
  { id: 'front', label: 'Frontale' },
  { id: 'top', label: "Dall'alto" },
]

const ZONES: ReadonlyArray<string> = [
  'Ingombri esterni',
  'Cabina di guida',
  'Zona giorno / Dinette',
  'Cucina',
  'Bagno / Doccia',
  'Zona notte',
  'Gavone / Impianti',
]

interface MeasurementPreset {
  id: string
  name: string
  zone: string
  view: CamperManualView
  positionX: number
  positionY: number
  description: string
  placeholderValue: string
}

const MEASUREMENT_PRESETS: ReadonlyArray<MeasurementPreset> = [
  {
    id: 'porta-scorrevole',
    name: 'Porta scorrevole (luce netta)',
    zone: 'Zona giorno / Dinette',
    view: 'side',
    positionX: 56,
    positionY: 54,
    description: 'Larghezza × altezza luce netta e altezza gradino da terra.',
    placeholderValue: 'es. 1.070 × 1.480 mm',
  },
  {
    id: 'portelloni-posteriori',
    name: 'Portelloni posteriori (apertura utile)',
    zone: 'Gavone / Impianti',
    view: 'front',
    positionX: 50,
    positionY: 58,
    description: 'Luce netta totale, altezza utile e soglia di carico da terra.',
    placeholderValue: 'es. 1.560 × 1.520 mm',
  },
  {
    id: 'piano-cucina',
    name: 'Piano cucina',
    zone: 'Cucina',
    view: 'top',
    positionX: 54,
    positionY: 66,
    description: 'Altezza da terra, larghezza × profondità utile e dimensioni lavello.',
    placeholderValue: 'es. 900 × 480 mm (H 880 mm)',
  },
  {
    id: 'piatto-doccia',
    name: 'Piatto doccia e altezza utile',
    zone: 'Bagno / Doccia',
    view: 'top',
    positionX: 43,
    positionY: 36,
    description: 'Lunghezza × larghezza piatto doccia e altezza utile interna.',
    placeholderValue: 'es. 680 × 580 mm (H 1.880 mm)',
  },
  {
    id: 'gavone-sottoletto',
    name: 'Vano di carico sottoletto',
    zone: 'Gavone / Impianti',
    view: 'top',
    positionX: 18,
    positionY: 50,
    description: 'Lunghezza × larghezza × altezza utile sotto la rete sollevabile.',
    placeholderValue: 'es. 1.320 × 850 × 680 mm',
  },
  {
    id: 'passaggio-centrale',
    name: 'Passaggio centrale',
    zone: 'Zona giorno / Dinette',
    view: 'top',
    positionX: 50,
    positionY: 50,
    description: 'Larghezza minima corridoio tra blocco cucina e bagno.',
    placeholderValue: 'es. 510 mm',
  },
  {
    id: 'vano-batteria',
    name: 'Vano batteria servizi / fusibili',
    zone: 'Gavone / Impianti',
    view: 'side',
    positionX: 68,
    positionY: 74,
    description: 'Luce di accesso e dimensioni massime alloggiamento batteria.',
    placeholderValue: 'es. 355 × 175 × 190 mm',
  },
  {
    id: 'vano-bombole',
    name: 'Vano bombole gas',
    zone: 'Gavone / Impianti',
    view: 'side',
    positionX: 14,
    positionY: 72,
    description: 'Altezza utile × larghezza × profondità e diametro massimo bombola.',
    placeholderValue: 'es. 2 × 11 kg (Ø 300 × H 600 mm)',
  },
]

export function CamperManualSection({
  camperId,
  camperName,
  dimensions,
  manualSourceName,
  manualSourceUrl,
  manualMeasurements,
  manualViewImages,
  onSourceChange,
  onMeasurementsChange,
  onViewImagesChange,
  onApplyParsedDraft,
  onSaveManualOnly,
  isSavingProfile,
  profileSavedSuccess = false,
  compactToolsByDefault = false,
}: {
  camperId: string
  camperName: string
  dimensions: CamperDimensions
  manualSourceName: string
  manualSourceUrl: string
  manualMeasurements: CamperManualMeasurement[]
  manualViewImages?: CamperManualViewImages | null
  onSourceChange: (sourceName: string, sourceUrl: string) => void
  onMeasurementsChange: (measurements: CamperManualMeasurement[]) => void
  onViewImagesChange?: (viewImages: CamperManualViewImages, saveImmediately: boolean) => void
  onApplyParsedDraft: (draft: CamperManualParseResponse, saveImmediately: boolean) => void
  onSaveManualOnly?: () => void
  isSavingProfile: boolean
  profileSavedSuccess?: boolean
  compactToolsByDefault?: boolean
}) {
  const [pdfBase64, setPdfBase64] = useState<string | null>(null)
  const [selectedFileName, setSelectedFileName] = useState<string>('')
  const [rawText, setRawText] = useState<string>('')
  const [showRawText, setShowRawText] = useState(false)
  const [parsedDraft, setParsedDraft] = useState<CamperManualParseResponse | null>(null)
  const [displayMode, setDisplayMode] = useState<DisplayMode>('map')
  const [activeView, setActiveView] = useState<CamperManualView>('side')
  const [selectedId, setSelectedId] = useState<string>('')
  const [showImportPanel, setShowImportPanel] = useState(
    () => !compactToolsByDefault || manualMeasurements.length === 0,
  )
  const [showSurveyPanel, setShowSurveyPanel] = useState(() => !compactToolsByDefault)
  const [showAddForm, setShowAddForm] = useState(false)
  const [newName, setNewName] = useState('')
  const [newValue, setNewValue] = useState('')
  const [newZone, setNewZone] = useState<string>(ZONES[0])
  const [newView, setNewView] = useState<CamperManualView>('side')
  const [newDescription, setNewDescription] = useState('')
  const [newPage, setNewPage] = useState('')
  const [newPosX, setNewPosX] = useState('50')
  const [newPosY, setNewPosY] = useState('50')

  const effectiveMeasurements = manualMeasurements.length > 0
    ? manualMeasurements
    : buildFallbackMeasurements(dimensions)

  const selectedMeasurement =
    effectiveMeasurements.find((item) => item.id === selectedId) ??
    effectiveMeasurements.find((item) => item.view === activeView) ??
    effectiveMeasurements[0] ??
    null

  const parseMutation = useMutation({
    mutationFn: () =>
      parseCamperManual(camperId, {
        sourceName: manualSourceName.trim() || selectedFileName || null,
        sourceUrl: manualSourceUrl.trim() || null,
        pdfBase64,
        rawText: rawText.trim() || null,
      }),
    onSuccess: (response) => {
      setParsedDraft(response)
      if (response.measurements.length > 0) {
        setSelectedId(response.measurements[0].id)
        setActiveView(response.measurements[0].view)
      }
    },
  })

  function handlePdfSelected(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    if (!file) {
      setPdfBase64(null)
      setSelectedFileName('')
      return
    }

    setSelectedFileName(file.name)
    if (!manualSourceName.trim()) {
      onSourceChange(file.name, manualSourceUrl)
    }

    const reader = new FileReader()
    reader.onload = () => {
      const result = typeof reader.result === 'string' ? reader.result : ''
      const commaIndex = result.indexOf(',')
      setPdfBase64(commaIndex >= 0 ? result.slice(commaIndex + 1) : result)
    }
    reader.readAsDataURL(file)
  }

  function handleSelectMeasurement(measurement: CamperManualMeasurement) {
    setSelectedId(measurement.id)
    setActiveView(measurement.view)
  }

  function handlePresetSelect(preset: MeasurementPreset) {
    setShowAddForm(true)
    setNewName(preset.name)
    setNewZone(preset.zone)
    setNewView(preset.view)
    setNewDescription(preset.description)
    setNewPosX(String(preset.positionX))
    setNewPosY(String(preset.positionY))
    if (!newValue.trim()) {
      setNewValue(preset.placeholderValue.replace(/^es\.\s*/i, ''))
    }
  }

  function handleAddMeasurement() {
    if (!newName.trim() || !newValue.trim()) {
      return
    }

    const baseList = manualMeasurements.length > 0 ? manualMeasurements : effectiveMeasurements
    const id = slugify(`${newName}-${Date.now().toString(36).slice(-4)}`)
    const item: CamperManualMeasurement = {
      id,
      name: newName.trim(),
      value: newValue.trim(),
      zone: newZone.trim() || ZONES[0],
      view: newView,
      positionX: clampPercent(Number(newPosX), 50),
      positionY: clampPercent(Number(newPosY), 50),
      description: newDescription.trim() || null,
      page: newPage.trim() ? Number(newPage) : null,
    }

    const next = [...baseList, item]
    onMeasurementsChange(next)
    setSelectedId(item.id)
    setActiveView(item.view)
    setNewName('')
    setNewValue('')
    setNewDescription('')
    setNewPage('')
    setShowAddForm(false)
  }

  function handleRemoveMeasurement(id: string) {
    const next = manualMeasurements.filter((item) => item.id !== id)
    onMeasurementsChange(next)
  }

  function handleUpdateSelectedValue(nextValue: string) {
    if (!selectedMeasurement) {
      return
    }

    const baseList = manualMeasurements.length > 0 ? manualMeasurements : effectiveMeasurements
    const next = baseList.map((item) =>
      item.id === selectedMeasurement.id ? { ...item, value: nextValue } : item,
    )
    onMeasurementsChange(next)
  }

  async function handleViewImageSelected(targetView: CamperManualView, file: File) {
    if (!onViewImagesChange) {
      return
    }

    const dataUrl = await resizeImageFileToDataUrl(file)
    if (!dataUrl) {
      return
    }

    const nextViewImages: CamperManualViewImages = {
      side: manualViewImages?.side ?? null,
      front: manualViewImages?.front ?? null,
      top: manualViewImages?.top ?? null,
      [targetView]: dataUrl,
    }
    onViewImagesChange(nextViewImages, true)
  }

  function handleResetViewImage(targetView: CamperManualView) {
    if (!onViewImagesChange) {
      return
    }

    const nextViewImages: CamperManualViewImages = {
      side: manualViewImages?.side ?? null,
      front: manualViewImages?.front ?? null,
      top: manualViewImages?.top ?? null,
      [targetView]: null,
    }
    onViewImagesChange(nextViewImages, true)
  }

  const canParse = Boolean(pdfBase64 || manualSourceUrl.trim() || rawText.trim())

  return (
    <section
      aria-labelledby="camper-manual-heading"
      className="space-y-6 rounded-3xl border border-border bg-card p-6 shadow-xs"
    >
      <header className="flex flex-wrap items-start justify-between gap-4 border-b border-border pb-5">
        <div className="space-y-1.5">
          <div className="flex flex-wrap items-center gap-2">
            <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              Documentazione tecnica e atlante dimensionale
            </p>
            {manualSourceName && (
              <span className="rounded-full bg-secondary px-3 py-0.5 text-xs font-semibold text-secondary-foreground">
                Fonte: {manualSourceName}
              </span>
            )}
          </div>
          <h2 id="camper-manual-heading" className="text-xl font-semibold">
            Manuale PDF, link esterno e mappa delle misure
          </h2>
          <p className="max-w-3xl text-sm text-muted-foreground">
            Consulta in un colpo d&apos;occhio tutte le quote esterne e interne di {camperName || 'questo camper'}, oppure importa specifiche da un manuale PDF o link esterno.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex flex-wrap gap-1.5 rounded-full bg-background p-1 border border-border" role="group" aria-label="Modalità di consultazione misure">
            <button
              type="button"
              className={toggleChipClass(displayMode === 'map')}
              aria-pressed={displayMode === 'map'}
              onClick={() => setDisplayMode('map')}
            >
              Mappa interattiva
            </button>
            <button
              type="button"
              className={toggleChipClass(displayMode === 'list')}
              aria-pressed={displayMode === 'list'}
              onClick={() => setDisplayMode('list')}
            >
              Elenco ({effectiveMeasurements.length})
            </button>
            <button
              type="button"
              className={toggleChipClass(displayMode === 'zones')}
              aria-pressed={displayMode === 'zones'}
              onClick={() => setDisplayMode('zones')}
            >
              Per zona
            </button>
          </div>
          <Button
            type="button"
            variant="outline"
            size="sm"
            aria-expanded={showImportPanel}
            onClick={() => setShowImportPanel((current) => !current)}
          >
            {showImportPanel ? 'Nascondi importatore PDF/link' : 'Importa da PDF o link'}
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            aria-expanded={showSurveyPanel}
            onClick={() => setShowSurveyPanel((current) => !current)}
          >
            {showSurveyPanel ? 'Nascondi scheda rilievo' : '+ Aggiungi misura'}
          </Button>
          {onSaveManualOnly && (
            <Button
              type="button"
              size="sm"
              disabled={isSavingProfile}
              onClick={onSaveManualOnly}
            >
              {isSavingProfile ? 'Salvataggio…' : 'Salva misure e manuale'}
            </Button>
          )}
        </div>
      </header>

      {profileSavedSuccess && (
        <p role="status" className="rounded-xl border border-border bg-background px-4 py-2.5 text-sm font-medium text-foreground">
          Misure e riferimenti del manuale salvati nel profilo del camper.
        </p>
      )}

      {/* Blocco 1: Importazione da PDF o Link esterno */}
      {showImportPanel && (
      <div className="space-y-4 rounded-2xl border border-border bg-background p-4 sm:p-5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h3 className="font-semibold">Importa specifiche e misure da PDF o link</h3>
            <p className="text-sm text-muted-foreground">
              Carica il manuale PDF del tuo veicolo o indica un URL esterno HTTPS per estrarre una bozza verificabile prima del salvataggio.
            </p>
          </div>
          {manualSourceName && (
            <span className="rounded-full bg-secondary px-3 py-1 text-xs font-semibold text-secondary-foreground">
              Fonte attiva: {manualSourceName}
            </span>
          )}
        </div>

        <div className="grid gap-4 sm:grid-cols-3">
          <Field>
            <FieldLabel htmlFor="camper-manual-file">File PDF manuale o scheda</FieldLabel>
            <FieldContent>
              <Input
                id="camper-manual-file"
                type="file"
                accept="application/pdf,.pdf"
                onChange={handlePdfSelected}
              />
            </FieldContent>
          </Field>

          <Field>
            <FieldLabel htmlFor="camper-manual-source-name">Nome fonte / manuale</FieldLabel>
            <FieldContent>
              <Input
                id="camper-manual-source-name"
                type="text"
                placeholder="es. Manuale Adria Twin 540 SP Axess 2025"
                value={manualSourceName}
                onChange={(event) => onSourceChange(event.target.value, manualSourceUrl)}
              />
            </FieldContent>
          </Field>

          <Field>
            <FieldLabel htmlFor="camper-manual-source-url">Link esterno scheda o PDF (HTTPS)</FieldLabel>
            <FieldContent>
              <Input
                id="camper-manual-source-url"
                type="url"
                placeholder="https://..."
                value={manualSourceUrl}
                onChange={(event) => onSourceChange(manualSourceName, event.target.value)}
              />
            </FieldContent>
          </Field>
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap items-center gap-2">
            <Button
              type="button"
              disabled={!canParse || parseMutation.isPending}
              onClick={() => parseMutation.mutate()}
            >
              {parseMutation.isPending ? 'Analisi del documento in corso…' : 'Estrai bozza da manuale / link'}
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setShowRawText((current) => !current)}
            >
              {showRawText ? 'Nascondi testo manuale' : 'Incolla testo scheda tecnica'}
            </Button>
          </div>
          {manualSourceUrl.trim().startsWith('https://') && (
            <a
              href={manualSourceUrl.trim()}
              target="_blank"
              rel="noreferrer"
              className="text-xs font-semibold underline text-foreground"
            >
              Apri link esterno in nuova scheda
            </a>
          )}
        </div>

        {showRawText && (
          <Field>
            <FieldLabel htmlFor="camper-manual-raw-text">
              Testo copiato dal manuale o dalla scheda tecnica
            </FieldLabel>
            <FieldContent>
              <textarea
                id="camper-manual-raw-text"
                rows={4}
                value={rawText}
                placeholder="Incolla qui tabelle o righe tecniche (es. Lunghezza fuori tutto 5.413 mm, Acque chiare 100 L, Letto posteriore 196 x 145 cm...)"
                className="w-full rounded-xl border border-input bg-card p-3 text-sm text-foreground shadow-2xs focus-visible:border-ring focus-visible:outline-none"
                onChange={(event) => setRawText(event.target.value)}
              />
            </FieldContent>
          </Field>
        )}

        {parseMutation.isError && (
          <p role="alert" className="rounded-lg bg-background px-3 py-2 text-sm text-danger-text">
            Impossibile analizzare il documento o il link fornito. Verifica che il file PDF sia valido o che l&apos;URL inizi con https://.
          </p>
        )}

        {parsedDraft && (
          <div className="space-y-4 rounded-xl border border-border-strong bg-background p-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-highlight-text">
                  Bozza estratta dal documento · {parsedDraft.sourceName}
                </p>
                <h4 className="mt-1 font-semibold">
                  {parsedDraft.measurements.length} misure rilevate pronte per il profilo e la mappa
                </h4>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button
                  type="button"
                  variant="outline"
                  disabled={isSavingProfile}
                  onClick={() => {
                    onApplyParsedDraft(parsedDraft, false)
                    setParsedDraft(null)
                  }}
                >
                  Applica bozza ai campi e alla mappa
                </Button>
                <Button
                  type="button"
                  disabled={isSavingProfile}
                  onClick={() => {
                    onApplyParsedDraft(parsedDraft, true)
                    setParsedDraft(null)
                  }}
                >
                  {isSavingProfile ? 'Salvataggio…' : 'Applica e salva subito nel profilo'}
                </Button>
              </div>
            </div>

            <dl className="grid gap-2 text-xs sm:grid-cols-4">
              {parsedDraft.brand && (
                <DraftStat label="Marca / Modello" value={`${parsedDraft.brand} ${parsedDraft.model ?? ''}`.trim()} />
              )}
              {parsedDraft.year && (
                <DraftStat label="Anno" value={String(parsedDraft.year)} />
              )}
              {parsedDraft.lengthMm && (
                <DraftStat
                  label="Dimensioni (L × W × H)"
                  value={`${parsedDraft.lengthMm} × ${parsedDraft.widthMm ?? '—'} × ${parsedDraft.heightMm ?? '—'} mm`}
                />
              )}
              {(parsedDraft.freshWaterL || parsedDraft.greyWaterL) && (
                <DraftStat
                  label="Acqua pulita / grigia"
                  value={`${parsedDraft.freshWaterL ?? '—'} L / ${parsedDraft.greyWaterL ?? '—'} L`}
                />
              )}
            </dl>

            {parsedDraft.warnings.length > 0 && (
              <ul className="space-y-1 text-xs text-muted-foreground">
                {parsedDraft.warnings.map((warning) => (
                  <li key={warning}>• {warning}</li>
                ))}
              </ul>
            )}
          </div>
        )}
      </div>
      )}

      {/* Blocco 2: Mappa interattiva / Elenco / Zone */}
      {selectedMeasurement && displayMode === 'map' && (
        <div className="grid gap-5 lg:grid-cols-[minmax(0,1.25fr)_minmax(18rem,0.75fr)]">
          <div className="space-y-4 rounded-lg border border-border bg-background p-4 sm:p-5">
            <div className="flex flex-wrap items-end justify-between gap-3">
              <div>
                <h3 className="font-semibold">Mappa delle misure del camper</h3>
                <p className="text-sm text-muted-foreground">
                  Seleziona la vista e tocca un punto numerato per leggere o modificare la misura.
                </p>
              </div>
              <div className="flex flex-wrap gap-2" role="group" aria-label="Viste del camper">
                {MAP_VIEWS.map((mapView) => {
                  const countInView = effectiveMeasurements.filter((m) => m.view === mapView.id).length
                  return (
                    <button
                      key={mapView.id}
                      type="button"
                      className={toggleChipClass(activeView === mapView.id)}
                      aria-pressed={activeView === mapView.id}
                      onClick={() => {
                        setActiveView(mapView.id)
                        const firstInView = effectiveMeasurements.find((m) => m.view === mapView.id)
                        if (firstInView) {
                          setSelectedId(firstInView.id)
                        }
                      }}
                    >
                      {mapView.label} ({countInView})
                    </button>
                  )
                })}
              </div>
            </div>

            <CamperDiagram
              view={activeView}
              dimensions={dimensions}
              measurements={effectiveMeasurements.filter((m) => m.view === activeView)}
              selectedId={selectedMeasurement.id}
              onSelect={handleSelectMeasurement}
              customImageUrl={manualViewImages?.[activeView] ?? null}
              onUploadImage={
                onViewImagesChange
                  ? (file) => {
                      void handleViewImageSelected(activeView, file)
                    }
                  : undefined
              }
              onResetImage={
                onViewImagesChange ? () => handleResetViewImage(activeView) : undefined
              }
              isSaving={isSavingProfile}
            />

            <MainDimensionCards dimensions={dimensions} />

            <MeasurementButtonList
              measurements={effectiveMeasurements.filter((m) => m.view === activeView)}
              selectedId={selectedMeasurement.id}
              onSelect={handleSelectMeasurement}
            />
          </div>

          <MeasurementDetailCard
            measurement={selectedMeasurement}
            sourceName={manualSourceName || 'Profilo tecnico'}
            sourceUrl={manualSourceUrl}
            canDelete={manualMeasurements.some((m) => m.id === selectedMeasurement.id)}
            onUpdateValue={handleUpdateSelectedValue}
            onDelete={() => handleRemoveMeasurement(selectedMeasurement.id)}
          />
        </div>
      )}

      {selectedMeasurement && displayMode === 'list' && (
        <div className="grid gap-5 lg:grid-cols-[minmax(0,0.95fr)_minmax(0,1.05fr)]">
          <div className="space-y-4">
            <MainDimensionCards dimensions={dimensions} />
            <div className="rounded-lg border border-border bg-background p-4 sm:p-5">
              <h3 className="font-semibold">Tutte le misure a portata di mano ({effectiveMeasurements.length})</h3>
              <p className="mb-3 text-sm text-muted-foreground">
                Elenco completo di ingombri esterni, letti, cucina, bagno, serbatoi e vani tecnici.
              </p>
              <MeasurementButtonList
                measurements={effectiveMeasurements}
                selectedId={selectedMeasurement.id}
                onSelect={handleSelectMeasurement}
              />
            </div>
          </div>

          <div className="space-y-4 rounded-lg border border-border bg-background p-4 sm:p-5">
            <CamperDiagram
              view={selectedMeasurement.view}
              dimensions={dimensions}
              measurements={effectiveMeasurements.filter((m) => m.view === selectedMeasurement.view)}
              selectedId={selectedMeasurement.id}
              onSelect={handleSelectMeasurement}
              customImageUrl={manualViewImages?.[selectedMeasurement.view] ?? null}
              onUploadImage={
                onViewImagesChange
                  ? (file) => {
                      void handleViewImageSelected(selectedMeasurement.view, file)
                    }
                  : undefined
              }
              onResetImage={
                onViewImagesChange
                  ? () => handleResetViewImage(selectedMeasurement.view)
                  : undefined
              }
              isSaving={isSavingProfile}
              compact
            />
            <MeasurementDetailCard
              measurement={selectedMeasurement}
              sourceName={manualSourceName || 'Profilo tecnico'}
              sourceUrl={manualSourceUrl}
              canDelete={manualMeasurements.some((m) => m.id === selectedMeasurement.id)}
              onUpdateValue={handleUpdateSelectedValue}
              onDelete={() => handleRemoveMeasurement(selectedMeasurement.id)}
            />
          </div>
        </div>
      )}

      {selectedMeasurement && displayMode === 'zones' && (
        <ZonesExplorer
          camperName={camperName}
          dimensions={dimensions}
          measurements={effectiveMeasurements}
          selectedMeasurement={selectedMeasurement}
          manualViewImages={manualViewImages}
          onUploadViewImage={
            onViewImagesChange
              ? (targetView, file) => {
                  void handleViewImageSelected(targetView, file)
                }
              : undefined
          }
          onResetViewImage={
            onViewImagesChange ? (targetView) => handleResetViewImage(targetView) : undefined
          }
          isSavingProfile={isSavingProfile}
          sourceName={manualSourceName || 'Profilo tecnico'}
          sourceUrl={manualSourceUrl}
          canDelete={manualMeasurements.some((m) => m.id === selectedMeasurement.id)}
          onSelect={handleSelectMeasurement}
          onUpdateValue={handleUpdateSelectedValue}
          onDelete={() => handleRemoveMeasurement(selectedMeasurement.id)}
        />
      )}

      {/* Blocco 3: Aggiunta rapida misure e Scheda di rilievo sul veicolo */}
      {showSurveyPanel && (
      <div className="space-y-4 rounded-2xl border border-border bg-background p-4 sm:p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h3 className="font-semibold">Scheda di rilievo sul veicolo e quote personalizzate</h3>
            <p className="text-sm text-muted-foreground">
              Aggiungi o modifica una misura rilevata col metro (porte, cucina, doccia, gavone, vano bombole) e salva direttamente.
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setShowAddForm((current) => !current)}
            >
              {showAddForm ? 'Chiudi modulo misura' : 'Aggiungi misura personalizzata'}
            </Button>
            {onSaveManualOnly && (
              <Button
                type="button"
                size="sm"
                disabled={isSavingProfile}
                onClick={onSaveManualOnly}
              >
                {isSavingProfile ? 'Salvataggio…' : 'Salva misure e manuale'}
              </Button>
            )}
          </div>
        </div>

        <div className="flex flex-wrap gap-2" aria-label="Preset scheda di rilievo">
          {MEASUREMENT_PRESETS.map((preset) => (
            <button
              key={preset.id}
              type="button"
              className="cursor-pointer rounded-full border border-border-strong bg-secondary px-3 py-1.5 text-xs font-semibold text-secondary-foreground transition-colors hover:bg-muted"
              onClick={() => handlePresetSelect(preset)}
            >
              + {preset.name}
            </button>
          ))}
        </div>

        {showAddForm && (
          <div className="grid gap-3.5 rounded-2xl border border-border bg-card p-4 sm:grid-cols-3">
            <Field>
              <FieldLabel htmlFor="new-measurement-name">Elemento / Misura</FieldLabel>
              <FieldContent>
                <Input
                  id="new-measurement-name"
                  value={newName}
                  placeholder="es. Altezza interna utile"
                  onChange={(e) => setNewName(e.target.value)}
                />
              </FieldContent>
            </Field>
            <Field>
              <FieldLabel htmlFor="new-measurement-value">Valore rilevato</FieldLabel>
              <FieldContent>
                <Input
                  id="new-measurement-value"
                  value={newValue}
                  placeholder="es. 1.900 mm oppure 196 × 145 cm"
                  onChange={(e) => setNewValue(e.target.value)}
                />
              </FieldContent>
            </Field>
            <Field>
              <FieldLabel htmlFor="new-measurement-zone">Zona del camper</FieldLabel>
              <FieldContent>
                <select
                  id="new-measurement-zone"
                  value={newZone}
                  className="h-11 w-full cursor-pointer rounded-xl border border-input bg-card px-3.5 text-sm text-foreground shadow-2xs"
                  onChange={(e) => setNewZone(e.target.value)}
                >
                  {ZONES.map((zone) => (
                    <option key={zone} value={zone}>{zone}</option>
                  ))}
                </select>
              </FieldContent>
            </Field>
            <Field>
              <FieldLabel htmlFor="new-measurement-view">Vista sulla mappa</FieldLabel>
              <FieldContent>
                <select
                  id="new-measurement-view"
                  value={newView}
                  className="h-11 w-full cursor-pointer rounded-xl border border-input bg-card px-3.5 text-sm text-foreground shadow-2xs"
                  onChange={(e) => setNewView(e.target.value as CamperManualView)}
                >
                  {MAP_VIEWS.map((view) => (
                    <option key={view.id} value={view.id}>{view.label}</option>
                  ))}
                </select>
              </FieldContent>
            </Field>
            <Field>
              <FieldLabel htmlFor="new-measurement-pos">Posizione punto (X% · Y%)</FieldLabel>
              <FieldContent>
                <div className="grid grid-cols-2 gap-2">
                  <Input
                    id="new-measurement-pos"
                    type="number"
                    min={0}
                    max={100}
                    value={newPosX}
                    aria-label="Posizione X percentuale"
                    onChange={(e) => setNewPosX(e.target.value)}
                  />
                  <Input
                    type="number"
                    min={0}
                    max={100}
                    value={newPosY}
                    aria-label="Posizione Y percentuale"
                    onChange={(e) => setNewPosY(e.target.value)}
                  />
                </div>
              </FieldContent>
            </Field>
            <Field>
              <FieldLabel htmlFor="new-measurement-page">Pagina manuale (opz.)</FieldLabel>
              <FieldContent>
                <Input
                  id="new-measurement-page"
                  type="number"
                  min={1}
                  value={newPage}
                  placeholder="es. 7"
                  onChange={(e) => setNewPage(e.target.value)}
                />
              </FieldContent>
            </Field>
            <div className="sm:col-span-3">
              <Field>
                <FieldLabel htmlFor="new-measurement-desc">Note / dettagli della misura</FieldLabel>
                <FieldContent>
                  <Input
                    id="new-measurement-desc"
                    value={newDescription}
                    placeholder="es. Misura presa dal pavimento al rivestimento sotto l'oblò centrale"
                    onChange={(e) => setNewDescription(e.target.value)}
                  />
                </FieldContent>
              </Field>
            </div>
            <div className="sm:col-span-3 flex justify-end">
              <Button type="button" onClick={handleAddMeasurement}>
                Aggiungi alla mappa delle misure
              </Button>
            </div>
          </div>
        )}
      </div>
      )}
    </section>
  )
}

function ZonesExplorer({
  camperName,
  dimensions,
  measurements,
  selectedMeasurement,
  manualViewImages,
  onUploadViewImage,
  onResetViewImage,
  isSavingProfile,
  sourceName,
  sourceUrl,
  canDelete,
  onSelect,
  onUpdateValue,
  onDelete,
}: {
  camperName: string
  dimensions: CamperDimensions
  measurements: CamperManualMeasurement[]
  selectedMeasurement: CamperManualMeasurement
  manualViewImages?: CamperManualViewImages | null
  onUploadViewImage?: (view: CamperManualView, file: File) => void
  onResetViewImage?: (view: CamperManualView) => void
  isSavingProfile?: boolean
  sourceName: string
  sourceUrl: string
  canDelete: boolean
  onSelect: (measurement: CamperManualMeasurement) => void
  onUpdateValue: (value: string) => void
  onDelete: () => void
}) {
  const zones = Array.from(new Set(measurements.map((m) => m.zone)))
  const zoneMeasurements = measurements.filter((m) => m.zone === selectedMeasurement.zone)

  return (
    <div className="grid gap-5 md:grid-cols-[minmax(12rem,0.68fr)_minmax(0,1.32fr)]">
      <nav aria-label="Zone del camper" className="rounded-lg border border-border bg-background p-4">
        <h3 className="mb-3 font-semibold">Zone del camper</h3>
        <ul className="space-y-2">
          {zones.map((zone) => {
            const first = measurements.find((m) => m.zone === zone)
            if (!first) return null
            const isSelected = selectedMeasurement.zone === zone
            return (
              <li key={zone}>
                <button
                  type="button"
                  className={`${toggleChipClass(isSelected)} min-h-11 w-full justify-between text-left`}
                  aria-pressed={isSelected}
                  onClick={() => onSelect(first)}
                >
                  <span>{zone}</span>
                  <span className="ml-2 text-xs opacity-80">
                    {measurements.filter((m) => m.zone === zone).length}
                  </span>
                </button>
              </li>
            )
          })}
        </ul>
      </nav>

      <div className="space-y-4 rounded-lg border border-border bg-background p-4 sm:p-5">
        <header>
          <p className="text-sm text-muted-foreground">Zona selezionata · {camperName || 'Camper'}</p>
          <h3 className="text-lg font-semibold">{selectedMeasurement.zone}</h3>
        </header>
        <CamperDiagram
          view={selectedMeasurement.view}
          dimensions={dimensions}
          measurements={zoneMeasurements}
          selectedId={selectedMeasurement.id}
          onSelect={onSelect}
          customImageUrl={manualViewImages?.[selectedMeasurement.view] ?? null}
          onUploadImage={
            onUploadViewImage
              ? (file) => onUploadViewImage(selectedMeasurement.view, file)
              : undefined
          }
          onResetImage={
            onResetViewImage ? () => onResetViewImage(selectedMeasurement.view) : undefined
          }
          isSaving={isSavingProfile}
          compact
        />
        <MeasurementButtonList
          measurements={zoneMeasurements}
          selectedId={selectedMeasurement.id}
          onSelect={onSelect}
        />
        <MainDimensionCards dimensions={dimensions} />
        <MeasurementDetailCard
          measurement={selectedMeasurement}
          sourceName={sourceName}
          sourceUrl={sourceUrl}
          canDelete={canDelete}
          onUpdateValue={onUpdateValue}
          onDelete={onDelete}
        />
      </div>
    </div>
  )
}

function CamperDiagram({
  view,
  dimensions,
  measurements,
  selectedId,
  onSelect,
  customImageUrl,
  onUploadImage,
  onResetImage,
  isSaving = false,
  compact = false,
}: {
  view: CamperManualView
  dimensions: CamperDimensions
  measurements: CamperManualMeasurement[]
  selectedId: string
  onSelect: (measurement: CamperManualMeasurement) => void
  customImageUrl?: string | null
  onUploadImage?: (file: File) => void
  onResetImage?: () => void
  isSaving?: boolean
  compact?: boolean
}) {
  const dimensionCaption = view === 'side'
    ? `Lunghezza ${shortDimension(dimensions.lengthMm)} mm · altezza ${shortDimension(dimensions.heightMm)} mm`
    : view === 'front'
      ? `Larghezza ${shortDimension(dimensions.widthMm)} mm · altezza ${shortDimension(dimensions.heightMm)} mm`
      : `Lunghezza ${shortDimension(dimensions.lengthMm)} mm · larghezza ${shortDimension(dimensions.widthMm)} mm`

  const fileInputId = `camper-view-image-${view}-${compact ? 'compact' : 'main'}`

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file || !onUploadImage) {
      return
    }
    onUploadImage(file)
  }

  return (
    <div className={`space-y-3 overflow-hidden rounded-2xl border border-border bg-muted ${compact ? 'p-2.5' : 'p-3.5 sm:p-5'}`}>
      {onUploadImage && (
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-border bg-card px-3 py-2">
          <div className="flex items-center gap-2 text-xs">
            <span className="font-semibold text-foreground">
              {customImageUrl
                ? `Immagine personalizzata · Vista ${viewLabel(view).toLowerCase()}`
                : `Schema di default · Vista ${viewLabel(view).toLowerCase()}`}
            </span>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <label
              htmlFor={fileInputId}
              className="inline-flex h-8 cursor-pointer items-center justify-center rounded-full border border-border-strong bg-secondary px-3.5 text-xs font-semibold text-secondary-foreground shadow-2xs hover:bg-muted"
            >
              {isSaving
                ? 'Salvataggio…'
                : customImageUrl
                  ? `Cambia immagine (${viewLabel(view).toLowerCase()})`
                  : `Carica immagine (${viewLabel(view).toLowerCase()})`}
            </label>
            <input
              id={fileInputId}
              type="file"
              accept="image/png,image/jpeg,image/webp"
              className="sr-only"
              disabled={isSaving}
              aria-label={`Carica immagine personalizzata per la vista ${viewLabel(view).toLowerCase()}`}
              onChange={handleFileChange}
            />
            {customImageUrl && onResetImage && (
              <Button
                type="button"
                variant="outline"
                size="xs"
                disabled={isSaving}
                onClick={onResetImage}
              >
                Ripristina default
              </Button>
            )}
          </div>
        </div>
      )}

      <svg
        className="h-auto w-full"
        viewBox="0 0 600 310"
        role="group"
        aria-label={`Schema illustrativo del camper, vista ${viewLabel(view).toLowerCase()}`}
      >
        <title>Schema illustrativo delle misure del camper</title>
        <desc>Seleziona un punto numerato per consultare la misura corrispondente.</desc>

        {customImageUrl ? (
          <g>
            <rect
              x="20"
              y="10"
              width="560"
              height="252"
              rx="14"
              fill="var(--color-background)"
              stroke="currentColor"
              strokeWidth="1.5"
              className="text-foreground"
              strokeOpacity="0.2"
            />
            <image
              href={customImageUrl}
              x="26"
              y="16"
              width="548"
              height="240"
              preserveAspectRatio="xMidYMid meet"
            />
          </g>
        ) : (
          <g className="text-foreground" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
            {view === 'side' && (
              <>
                {/* Linea del suolo */}
                <line x1="34" y1="248" x2="566" y2="248" strokeWidth="1.5" strokeDasharray="6 6" opacity="0.4" />
                {/* Oblò sul tetto e tendalino */}
                <path d="M116 74h48l6 9h-60l6-9Z" fill="var(--color-surface-raised)" />
                <path d="M278 74h56l6 9h-68l6-9Z" fill="var(--color-surface-raised)" />
                {/* Scocca principale furgone camperizzato */}
                <path
                  d="M52 218V106c0-13 10-23 23-23h292c28 0 51 11 69 33l46 53 45 12c11 3 19 13 19 24v13H52Z"
                  fill="var(--color-background)"
                />
                {/* Fascia inferiore protettiva e paraurti */}
                <path d="M48 206h502v14c0 3-2 5-5 5H53c-3 0-5-2-5-5v-14Z" fill="var(--color-surface-raised)" />
                {/* Tendalino esterno */}
                <rect x="172" y="92" width="186" height="9" rx="4" fill="var(--color-surface-raised-2)" />
                {/* Finestrino cabina e parabrezza */}
                <path d="M380 96v68h88l-39-46c-12-14-28-22-49-22Z" fill="var(--color-surface-raised)" />
                <line x1="416" y1="98" x2="416" y2="164" strokeWidth="2" />
                {/* Porta scorrevole e finestra dinette */}
                <rect x="254" y="103" width="108" height="113" rx="6" fill="none" strokeWidth="2" />
                <rect x="268" y="112" width="80" height="46" rx="6" fill="var(--color-surface-raised)" />
                <rect x="260" y="168" width="14" height="5" rx="2.5" fill="var(--color-surface-raised-2)" />
                {/* Finestra posteriore zona notte */}
                <rect x="86" y="114" width="118" height="44" rx="6" fill="var(--color-surface-raised)" />
                {/* Griglia e sportelli impianti (vano bombole / cassetta) */}
                <rect x="76" y="172" width="44" height="28" rx="4" fill="var(--color-surface-raised)" />
                <rect x="136" y="174" width="32" height="24" rx="4" fill="var(--color-surface-raised)" />
                {/* Faro anteriore e fanale posteriore */}
                <path d="M526 184h18v14h-18a6 6 0 0 1-6-6v-2a6 6 0 0 1 6-6Z" fill="var(--color-surface-raised-2)" />
                <rect x="52" y="156" width="7" height="34" rx="3" fill="var(--color-surface-raised-2)" />
                {/* Ruote complete di passaruota e cerchi */}
                <circle cx="154" cy="222" r="26" fill="var(--color-surface-raised-2)" />
                <circle cx="154" cy="222" r="12" fill="var(--color-background)" />
                <circle cx="446" cy="222" r="26" fill="var(--color-surface-raised-2)" />
                <circle cx="446" cy="222" r="12" fill="var(--color-background)" />
              </>
            )}
            {view === 'front' && (
              <>
                {/* Linea del suolo */}
                <line x1="120" y1="248" x2="480" y2="248" strokeWidth="1.5" strokeDasharray="6 6" opacity="0.4" />
                {/* Profilo oblò superiore */}
                <rect x="256" y="42" width="88" height="10" rx="5" fill="var(--color-surface-raised)" />
                {/* Scocca frontale / posteriore simmetrica */}
                <path
                  d="M184 220V108c0-32 24-56 56-56h120c32 0 56 24 56 56v112H184Z"
                  fill="var(--color-background)"
                />
                {/* Specchietti retrovisori */}
                <rect x="150" y="122" width="28" height="44" rx="8" fill="var(--color-surface-raised)" />
                <line x1="178" y1="136" x2="184" y2="136" strokeWidth="3" />
                <rect x="422" y="122" width="28" height="44" rx="8" fill="var(--color-surface-raised)" />
                <line x1="416" y1="136" x2="422" y2="136" strokeWidth="3" />
                {/* Vetratura superiore / portelloni */}
                <path
                  d="M206 112c0-20 16-36 36-36h116c20 0 36 16 36 36v44H206v-44Z"
                  fill="var(--color-surface-raised)"
                />
                {/* Linea centrale battuta portelloni / riferimento asse */}
                <line x1="300" y1="76" x2="300" y2="220" strokeWidth="1.8" strokeDasharray="4 4" />
                {/* Calandra, fari e paraurti */}
                <rect x="198" y="170" width="42" height="18" rx="5" fill="var(--color-surface-raised-2)" />
                <rect x="360" y="170" width="42" height="18" rx="5" fill="var(--color-surface-raised-2)" />
                <rect x="248" y="170" width="104" height="22" rx="6" fill="var(--color-surface-raised)" />
                <rect x="178" y="202" width="244" height="18" rx="6" fill="var(--color-surface-raised)" />
                {/* Pneumatici a terra */}
                <rect x="192" y="220" width="34" height="28" rx="7" fill="var(--color-surface-raised-2)" />
                <rect x="374" y="220" width="34" height="28" rx="7" fill="var(--color-surface-raised-2)" />
              </>
            )}
            {view === 'top' && (
              <>
                {/* Specchietti esterni in pianta */}
                <rect x="444" y="34" width="16" height="14" rx="5" fill="var(--color-surface-raised)" />
                <rect x="444" y="248" width="16" height="14" rx="5" fill="var(--color-surface-raised)" />
                {/* Scocca esterna in pianta */}
                <path
                  d="M92 48h356c42 0 74 28 74 68v60c0 40-32 68-74 68H92c-18 0-32-14-32-32V80c0-18 14-32 32-32Z"
                  fill="var(--color-background)"
                />
                {/* Zona notte posteriore (letto matrimoniale trasversale) */}
                <rect x="74" y="60" width="132" height="172" rx="10" fill="var(--color-surface-raised)" />
                <rect x="82" y="72" width="28" height="66" rx="6" fill="var(--color-background)" />
                <rect x="82" y="154" width="28" height="66" rx="6" fill="var(--color-background)" />
                <line x1="142" y1="60" x2="142" y2="232" strokeWidth="1.5" strokeDasharray="5 5" />
                {/* Vano bagno / doccia */}
                <rect x="216" y="60" width="84" height="76" rx="8" fill="var(--color-surface-raised)" />
                <circle cx="258" cy="98" r="18" fill="var(--color-background)" />
                {/* Blocco cucina con lavello e fuochi */}
                <rect x="242" y="168" width="114" height="64" rx="8" fill="var(--color-surface-raised)" />
                <rect x="254" y="180" width="38" height="40" rx="6" fill="var(--color-background)" />
                <circle cx="314" cy="192" r="8" fill="var(--color-background)" />
                <circle cx="336" cy="206" r="8" fill="var(--color-background)" />
                {/* Zona giorno / Dinette (panca + tavolo) */}
                <rect x="312" y="60" width="38" height="72" rx="6" fill="var(--color-surface-raised-2)" />
                <rect x="356" y="70" width="44" height="58" rx="8" fill="var(--color-surface-raised)" />
                {/* Cabina di guida (sedili girevoli + parabrezza anteriore) */}
                <rect x="414" y="68" width="42" height="46" rx="8" fill="var(--color-surface-raised-2)" />
                <rect x="414" y="178" width="42" height="46" rx="8" fill="var(--color-surface-raised-2)" />
                <path
                  d="M472 64c22 10 34 34 34 82s-12 72-34 82V64Z"
                  fill="var(--color-surface-raised)"
                />
              </>
            )}
          </g>
        )}

        <g className="text-muted-foreground" fill="currentColor" fontSize="14" fontWeight="600">
          <text x="300" y="288" textAnchor="middle">{dimensionCaption}</text>
        </g>

        {measurements.map((measurement, index) => {
          const isSelected = selectedId === measurement.id
          const cx = toSvgX(measurement.positionX)
          const cy = toSvgY(measurement.positionY)
          return (
            <g
              key={measurement.id}
              role="button"
              tabIndex={0}
              aria-pressed={isSelected}
              aria-label={`${measurement.name}: ${measurement.value}`}
              className="cursor-pointer focus-visible:outline-none"
              onClick={() => onSelect(measurement)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                  event.preventDefault()
                  onSelect(measurement)
                }
              }}
            >
              <circle
                cx={cx}
                cy={cy}
                r={compact ? 20 : 23}
                className="fill-background text-foreground"
                stroke={isSelected ? 'var(--color-primary)' : 'currentColor'}
                strokeWidth={isSelected ? 4 : 3}
              />
              <text
                x={cx}
                y={cy + 5}
                textAnchor="middle"
                fontSize="13"
                fontWeight="700"
                className="pointer-events-none fill-current text-foreground"
              >
                {index + 1}
              </text>
            </g>
          )
        })}
      </svg>
    </div>
  )
}

function MainDimensionCards({ dimensions }: { dimensions: CamperDimensions }) {
  const items = [
    { label: 'Lunghezza', value: formatDimension(dimensions.lengthMm) },
    { label: 'Larghezza', value: formatDimension(dimensions.widthMm) },
    { label: 'Altezza', value: formatDimension(dimensions.heightMm) },
  ]

  return (
    <section className="space-y-2" aria-label="Ingombri esterni nel profilo">
      <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        Ingombri esterni nel profilo
      </p>
      <dl className="grid grid-cols-1 gap-2 sm:grid-cols-3">
        {items.map((item) => (
          <div key={item.label} className="rounded-lg border border-border bg-card p-3">
            <dt className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">{item.label}</dt>
            <dd className="mt-1 text-sm font-semibold">{item.value}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}

function MeasurementButtonList({
  measurements,
  selectedId,
  onSelect,
}: {
  measurements: CamperManualMeasurement[]
  selectedId: string
  onSelect: (measurement: CamperManualMeasurement) => void
}) {
  return (
    <ul className="space-y-2">
      {measurements.map((measurement, idx) => {
        const isSelected = selectedId === measurement.id
        return (
          <li key={measurement.id}>
            <button
              type="button"
              className={`${toggleChipClass(isSelected)} min-h-12 w-full justify-between gap-3 text-left`}
              aria-pressed={isSelected}
              onClick={() => onSelect(measurement)}
            >
              <span className="min-w-0">
                <span className="block font-medium">
                  {idx + 1}. {measurement.name}
                </span>
                <span className="block text-xs opacity-80">
                  {measurement.zone} · Vista {viewLabel(measurement.view).toLowerCase()}
                </span>
              </span>
              <span className="shrink-0 text-sm font-semibold">{measurement.value}</span>
            </button>
          </li>
        )
      })}
    </ul>
  )
}

function MeasurementDetailCard({
  measurement,
  sourceName,
  sourceUrl,
  canDelete,
  onUpdateValue,
  onDelete,
}: {
  measurement: CamperManualMeasurement
  sourceName: string
  sourceUrl: string
  canDelete: boolean
  onUpdateValue: (value: string) => void
  onDelete: () => void
}) {
  return (
    <section
      className="space-y-4 rounded-lg border border-border bg-background p-4 sm:p-5"
      aria-live="polite"
      aria-label="Dettaglio misura selezionata"
    >
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-highlight-text">
          Misura selezionata · {viewLabel(measurement.view)}
        </p>
        <h3 className="mt-1 font-semibold">{measurement.name}</h3>
        <p className="numeral-display mt-2 text-2xl">{measurement.value}</p>
      </div>

      {measurement.description && (
        <p className="text-sm text-muted-foreground">{measurement.description}</p>
      )}

      <Field>
        <FieldLabel htmlFor="selected-measurement-value">Aggiorna valore misura</FieldLabel>
        <FieldContent>
          <Input
            id="selected-measurement-value"
            value={measurement.value}
            onChange={(e) => onUpdateValue(e.target.value)}
          />
        </FieldContent>
      </Field>

      <dl className="grid gap-2 text-sm sm:grid-cols-2">
        <div>
          <dt className="font-medium">Zona</dt>
          <dd className="text-muted-foreground">{measurement.zone}</dd>
        </div>
        <div>
          <dt className="font-medium">Fonte</dt>
          <dd className="text-muted-foreground">{sourceName}</dd>
        </div>
        {measurement.page && (
          <div>
            <dt className="font-medium">Pagina</dt>
            <dd className="text-muted-foreground">Pag. {measurement.page}</dd>
          </div>
        )}
        {sourceUrl && (
          <div>
            <dt className="font-medium">Link scheda</dt>
            <dd className="truncate text-muted-foreground">
              <a href={sourceUrl} target="_blank" rel="noreferrer" className="underline">
                Apri documento
              </a>
            </dd>
          </div>
        )}
      </dl>

      {canDelete && (
        <Button type="button" variant="destructive" size="sm" onClick={onDelete}>
          Rimuovi questa misura
        </Button>
      )}
    </section>
  )
}

function DraftStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border bg-background p-2.5">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 font-semibold text-foreground">{value}</dd>
    </div>
  )
}

function buildFallbackMeasurements(dimensions: CamperDimensions): CamperManualMeasurement[] {
  return [
    {
      id: 'lunghezza-fuori-tutto',
      name: 'Lunghezza fuori tutto',
      value: formatDimension(dimensions.lengthMm),
      zone: 'Ingombri esterni',
      view: 'side',
      positionX: 50,
      positionY: 88,
      description: 'Quota longitudinale esterna del veicolo salvata nel profilo.',
      page: 1,
    },
    {
      id: 'altezza-fuori-tutto',
      name: 'Altezza fuori tutto',
      value: formatDimension(dimensions.heightMm),
      zone: 'Ingombri esterni',
      view: 'side',
      positionX: 20,
      positionY: 32,
      description: 'Altezza massima esterna del veicolo.',
      page: 1,
    },
    {
      id: 'larghezza-fuori-tutto',
      name: 'Larghezza fuori tutto',
      value: formatDimension(dimensions.widthMm),
      zone: 'Ingombri esterni',
      view: 'front',
      positionX: 50,
      positionY: 82,
      description: 'Larghezza esterna della scocca.',
      page: 1,
    },
  ]
}

function toSvgX(percentX: number): number {
  const clamped = Math.max(0, Math.min(100, Number.isFinite(percentX) ? percentX : 50))
  return Math.round(64 + (clamped / 100) * 472)
}

function toSvgY(percentY: number): number {
  const clamped = Math.max(0, Math.min(100, Number.isFinite(percentY) ? percentY : 50))
  return Math.round(48 + (clamped / 100) * 195)
}

function clampPercent(value: number, fallback: number): number {
  if (!Number.isFinite(value)) return fallback
  return Math.max(0, Math.min(100, Math.round(value)))
}

function slugify(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '')
    .slice(0, 80) || 'misura'
}

function toggleChipClass(selected: boolean): string {
  return [
    'inline-flex cursor-pointer items-center rounded-full border px-3.5 py-1.5 text-xs font-semibold transition-colors',
    'focus-visible:outline-2 focus-visible:outline-ring',
    selected
      ? 'border-primary bg-primary text-primary-foreground shadow-2xs'
      : 'border-transparent bg-secondary text-secondary-foreground hover:bg-muted',
  ].join(' ')
}

function viewLabel(view: CamperManualView): string {
  return MAP_VIEWS.find((item) => item.id === view)?.label ?? MAP_VIEWS[0].label
}

function formatDimension(value: string): string {
  if (!value.trim()) {
    return 'Non disponibile'
  }

  const parsed = Number(value)
  return Number.isFinite(parsed)
    ? `${new Intl.NumberFormat('it-IT').format(parsed)} mm`
    : 'Non disponibile'
}

function shortDimension(value: string): string {
  if (!value.trim()) {
    return '—'
  }

  const parsed = Number(value)
  return Number.isFinite(parsed) ? new Intl.NumberFormat('it-IT').format(parsed) : '—'
}

async function resizeImageFileToDataUrl(file: File): Promise<string | null> {
  const rawDataUrl = await new Promise<string | null>((resolve) => {
    const reader = new FileReader()
    reader.onload = () => resolve(typeof reader.result === 'string' ? reader.result : null)
    reader.onerror = () => resolve(null)
    reader.readAsDataURL(file)
  })

  if (!rawDataUrl) {
    return null
  }

  return await new Promise<string>((resolve) => {
    const img = new Image()
    img.onload = () => {
      try {
        const maxWidth = 1100
        const maxHeight = 580
        const scale = Math.min(1, maxWidth / (img.width || 1), maxHeight / (img.height || 1))
        const targetWidth = Math.max(1, Math.round(img.width * scale))
        const targetHeight = Math.max(1, Math.round(img.height * scale))
        const canvas = document.createElement('canvas')
        canvas.width = targetWidth
        canvas.height = targetHeight
        const ctx = canvas.getContext('2d')
        if (!ctx) {
          resolve(rawDataUrl)
          return
        }
        ctx.drawImage(img, 0, 0, targetWidth, targetHeight)
        const compressed = canvas.toDataURL('image/webp', 0.86)
        resolve(compressed.startsWith('data:image/') ? compressed : rawDataUrl)
      } catch {
        resolve(rawDataUrl)
      }
    }
    img.onerror = () => resolve(rawDataUrl)
    img.src = rawDataUrl
  })
}

export function CamperDashboardManualPanel({ camperId }: { camperId: string }) {
  const profileQuery = useQuery({
    queryKey: camperProfileQueryKey(camperId),
    queryFn: () => fetchCamperProfile(camperId),
    enabled: camperId.length > 0,
    retry: false,
  })

  if (profileQuery.isPending || profileQuery.isError || !profileQuery.data) {
    return null
  }

  return (
    <CamperDashboardManualEditor
      key={profileQuery.data.etag}
      camperId={camperId}
      etag={profileQuery.data.etag}
      profile={profileQuery.data.profile}
    />
  )
}

function CamperDashboardManualEditor({
  camperId,
  etag,
  profile,
}: {
  camperId: string
  etag: string
  profile: CamperProfile
}) {
  const queryClient = useQueryClient()
  const [currentEtag, setCurrentEtag] = useState(etag)
  const [currentProfile, setCurrentProfile] = useState(profile)
  const [manualSourceName, setManualSourceName] = useState(profile.manualSourceName ?? '')
  const [manualSourceUrl, setManualSourceUrl] = useState(profile.manualSourceUrl ?? '')
  const [manualMeasurements, setManualMeasurements] = useState<CamperManualMeasurement[]>(
    profile.manualMeasurements ?? [],
  )
  const [manualViewImages, setManualViewImages] = useState<CamperManualViewImages>(
    profile.manualViewImages ?? {},
  )

  const saveMutation = useMutation({
    mutationFn: (input: CamperProfileInput) => updateCamperProfile(camperId, input, currentEtag),
    onSuccess: async (result) => {
      queryClient.setQueryData(camperProfileQueryKey(camperId), {
        profile: result.value,
        etag: result.etag,
      })
      setCurrentEtag(result.etag)
      setCurrentProfile(result.value)
      setManualSourceName(result.value.manualSourceName ?? '')
      setManualSourceUrl(result.value.manualSourceUrl ?? '')
      setManualMeasurements(result.value.manualMeasurements ?? [])
      setManualViewImages(result.value.manualViewImages ?? {})
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: CAMPER_DASHBOARD_QUERY_KEY }),
        queryClient.invalidateQueries({ queryKey: CAMPERS_QUERY_KEY }),
      ])
    },
  })

  function buildInputFromProfile(
    base: CamperProfile,
    sourceName: string,
    sourceUrl: string,
    measurements: CamperManualMeasurement[],
    viewImages: CamperManualViewImages,
  ): CamperProfileInput {
    return {
      name: base.name,
      brand: base.brand,
      model: base.model,
      year: base.year,
      plateNumber: base.plateNumber,
      vehicleKind: base.vehicleKind,
      lengthMm: base.lengthMm,
      widthMm: base.widthMm,
      heightMm: base.heightMm,
      kerbWeightKg: base.kerbWeightKg,
      maxWeightKg: base.maxWeightKg,
      freshWaterL: base.freshWaterL,
      greyWaterL: base.greyWaterL,
      fuelTankL: base.fuelTankL,
      gasKg: base.gasKg,
      fuelKind: base.fuelKind,
      averageConsumptionLPer100Km: base.averageConsumptionLPer100Km,
      manualSourceName: sourceName.trim() || null,
      manualSourceUrl: sourceUrl.trim() || null,
      manualMeasurements: measurements,
      manualViewImages: viewImages,
    }
  }

  function handleApplyParsedDraft(draft: CamperManualParseResponse, saveImmediately: boolean) {
    const nextSourceName = draft.sourceName || manualSourceName
    const nextSourceUrl = draft.sourceUrl ?? manualSourceUrl
    const extractedIds = new Set(draft.measurements.map((item) => item.id))
    const preserved = manualMeasurements.filter((item) => !extractedIds.has(item.id))
    const nextMeasurements =
      draft.measurements.length > 0 ? [...draft.measurements, ...preserved] : manualMeasurements

    const nextLength = draft.lengthMm ?? currentProfile.lengthMm
    const nextWidth = draft.widthMm ?? currentProfile.widthMm
    const nextHeight = draft.heightMm ?? currentProfile.heightMm
    const dimensionsOk = nextLength !== null && nextWidth !== null && nextHeight !== null

    const nextKerb = draft.kerbWeightKg ?? currentProfile.kerbWeightKg
    const nextMaxW = draft.maxWeightKg ?? currentProfile.maxWeightKg
    const weightsOk = nextKerb !== null && nextMaxW !== null

    const nextFresh = draft.freshWaterL ?? currentProfile.freshWaterL
    const nextGrey = draft.greyWaterL ?? currentProfile.greyWaterL
    const nextFuelTank = draft.fuelTankL ?? currentProfile.fuelTankL
    const nextGas = draft.gasKg ?? currentProfile.gasKg
    const capacitiesOk =
      nextFresh !== null && nextGrey !== null && nextFuelTank !== null && nextGas !== null

    const mergedProfile: CamperProfile = {
      ...currentProfile,
      brand: draft.brand ?? currentProfile.brand,
      model: draft.model ?? currentProfile.model,
      year: draft.year ?? currentProfile.year,
      vehicleKind: draft.vehicleKind ?? currentProfile.vehicleKind,
      fuelKind: draft.fuelKind ?? currentProfile.fuelKind,
      lengthMm: dimensionsOk ? nextLength : currentProfile.lengthMm,
      widthMm: dimensionsOk ? nextWidth : currentProfile.widthMm,
      heightMm: dimensionsOk ? nextHeight : currentProfile.heightMm,
      kerbWeightKg: weightsOk ? nextKerb : currentProfile.kerbWeightKg,
      maxWeightKg: weightsOk ? nextMaxW : currentProfile.maxWeightKg,
      freshWaterL: capacitiesOk ? nextFresh : currentProfile.freshWaterL,
      greyWaterL: capacitiesOk ? nextGrey : currentProfile.greyWaterL,
      fuelTankL: capacitiesOk ? nextFuelTank : currentProfile.fuelTankL,
      gasKg: capacitiesOk ? nextGas : currentProfile.gasKg,
      averageConsumptionLPer100Km:
        draft.averageConsumptionLPer100Km ?? currentProfile.averageConsumptionLPer100Km,
    }

    setCurrentProfile(mergedProfile)
    setManualSourceName(nextSourceName)
    setManualSourceUrl(nextSourceUrl)
    setManualMeasurements(nextMeasurements)

    if (saveImmediately) {
      saveMutation.mutate(
        buildInputFromProfile(
          mergedProfile,
          nextSourceName,
          nextSourceUrl,
          nextMeasurements,
          manualViewImages,
        ),
      )
    }
  }

  return (
    <CamperManualSection
      camperId={camperId}
      camperName={currentProfile.name}
      dimensions={{
        lengthMm: currentProfile.lengthMm !== null ? String(currentProfile.lengthMm) : '',
        widthMm: currentProfile.widthMm !== null ? String(currentProfile.widthMm) : '',
        heightMm: currentProfile.heightMm !== null ? String(currentProfile.heightMm) : '',
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
        if (saveImmediately) {
          saveMutation.mutate(
            buildInputFromProfile(
              currentProfile,
              manualSourceName,
              manualSourceUrl,
              manualMeasurements,
              nextViewImages,
            ),
          )
        }
      }}
      onApplyParsedDraft={handleApplyParsedDraft}
      onSaveManualOnly={() =>
        saveMutation.mutate(
          buildInputFromProfile(
            currentProfile,
            manualSourceName,
            manualSourceUrl,
            manualMeasurements,
            manualViewImages,
          ),
        )
      }
      isSavingProfile={saveMutation.isPending}
      profileSavedSuccess={saveMutation.isSuccess}
      compactToolsByDefault
    />
  )
}

