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
  onSourceChange,
  onMeasurementsChange,
  onApplyParsedDraft,
  onSaveManualOnly,
  isSavingProfile,
  profileSavedSuccess = false,
}: {
  camperId: string
  camperName: string
  dimensions: CamperDimensions
  manualSourceName: string
  manualSourceUrl: string
  manualMeasurements: CamperManualMeasurement[]
  onSourceChange: (sourceName: string, sourceUrl: string) => void
  onMeasurementsChange: (measurements: CamperManualMeasurement[]) => void
  onApplyParsedDraft: (draft: CamperManualParseResponse, saveImmediately: boolean) => void
  onSaveManualOnly?: () => void
  isSavingProfile: boolean
  profileSavedSuccess?: boolean
}) {
  const [pdfBase64, setPdfBase64] = useState<string | null>(null)
  const [selectedFileName, setSelectedFileName] = useState<string>('')
  const [rawText, setRawText] = useState<string>('')
  const [showRawText, setShowRawText] = useState(false)
  const [parsedDraft, setParsedDraft] = useState<CamperManualParseResponse | null>(null)
  const [displayMode, setDisplayMode] = useState<DisplayMode>('map')
  const [activeView, setActiveView] = useState<CamperManualView>('side')
  const [selectedId, setSelectedId] = useState<string>('')
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

  const canParse = Boolean(pdfBase64 || manualSourceUrl.trim() || rawText.trim())

  return (
    <section
      aria-labelledby="camper-manual-heading"
      className="mt-6 space-y-6 rounded-xl border border-border bg-card p-6"
    >
      <header className="flex flex-wrap items-start justify-between gap-4 border-b border-border pb-4">
        <div className="space-y-1">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            Documentazione tecnica e atlante dimensionale
          </p>
          <h2 id="camper-manual-heading" className="text-xl font-semibold">
            Manuale PDF, link esterno e mappa delle misure
          </h2>
          <p className="max-w-3xl text-sm text-muted-foreground">
            Importa specifiche tecniche e quote da un manuale PDF o da una scheda online (HTTPS), oppure consulta e aggiorna in un unico posto tutte le misure esterne e interne di {camperName || 'questo camper'}.
          </p>
        </div>
        <div className="flex flex-wrap gap-2" role="group" aria-label="Modalità di consultazione misure">
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
            Elenco misure ({effectiveMeasurements.length})
          </button>
          <button
            type="button"
            className={toggleChipClass(displayMode === 'zones')}
            aria-pressed={displayMode === 'zones'}
            onClick={() => setDisplayMode('zones')}
          >
            Esplora per zona
          </button>
        </div>
      </header>

      {/* Blocco 1: Importazione da PDF o Link esterno */}
      <div className="space-y-4 rounded-lg border border-border bg-background p-4 sm:p-5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h3 className="font-semibold">Importa specifiche e misure da PDF o link</h3>
            <p className="text-sm text-muted-foreground">
              Carica il manuale PDF del tuo veicolo o indica un URL esterno HTTPS per estrarre una bozza verificabile prima del salvataggio.
            </p>
          </div>
          {manualSourceName && (
            <span className="rounded-lg border border-border bg-card px-3 py-1 text-xs font-medium text-muted-foreground">
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
            <button
              type="button"
              className="rounded-lg border border-border-strong bg-card px-3 py-1.5 text-xs font-medium text-foreground hover:bg-muted"
              onClick={() => setShowRawText((current) => !current)}
            >
              {showRawText ? 'Nascondi testo manuale' : 'Incolla testo scheda tecnica'}
            </button>
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
                className="w-full rounded-lg border border-input bg-card p-2.5 text-sm text-foreground focus-visible:border-ring focus-visible:outline-none"
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
          <div className="space-y-4 rounded-lg border border-border-strong bg-background p-4">
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
          sourceName={manualSourceName || 'Profilo tecnico'}
          sourceUrl={manualSourceUrl}
          canDelete={manualMeasurements.some((m) => m.id === selectedMeasurement.id)}
          onSelect={handleSelectMeasurement}
          onUpdateValue={handleUpdateSelectedValue}
          onDelete={() => handleRemoveMeasurement(selectedMeasurement.id)}
        />
      )}

      {/* Blocco 3: Aggiunta rapida misure e Scheda di rilievo sul veicolo */}
      <div className="space-y-4 rounded-lg border border-border bg-background p-4 sm:p-5">
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
              onClick={() => setShowAddForm((current) => !current)}
            >
              {showAddForm ? 'Chiudi modulo misura' : 'Aggiungi misura'}
            </Button>
            {onSaveManualOnly && (
              <Button
                type="button"
                disabled={isSavingProfile}
                onClick={onSaveManualOnly}
              >
                {isSavingProfile ? 'Salvataggio…' : 'Salva misure e manuale'}
              </Button>
            )}
          </div>
        </div>
        {profileSavedSuccess && (
          <p role="status" className="text-sm text-foreground">
            Misure e riferimenti del manuale salvati nel profilo del camper.
          </p>
        )}

        <div className="flex flex-wrap gap-2" aria-label="Preset scheda di rilievo">
          {MEASUREMENT_PRESETS.map((preset) => (
            <button
              key={preset.id}
              type="button"
              className="rounded-lg border border-border-strong bg-card px-2.5 py-1.5 text-xs font-medium text-foreground hover:bg-muted"
              onClick={() => handlePresetSelect(preset)}
            >
              + {preset.name}
            </button>
          ))}
        </div>

        {showAddForm && (
          <div className="grid gap-3 rounded-lg border border-border bg-card p-4 sm:grid-cols-3">
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
                  className="h-8 w-full rounded-lg border border-input bg-background px-2.5 text-sm text-foreground"
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
                  className="h-8 w-full rounded-lg border border-input bg-background px-2.5 text-sm text-foreground"
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
    </section>
  )
}

function ZonesExplorer({
  camperName,
  dimensions,
  measurements,
  selectedMeasurement,
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
  compact = false,
}: {
  view: CamperManualView
  dimensions: CamperDimensions
  measurements: CamperManualMeasurement[]
  selectedId: string
  onSelect: (measurement: CamperManualMeasurement) => void
  compact?: boolean
}) {
  const dimensionCaption = view === 'side'
    ? `Lunghezza ${shortDimension(dimensions.lengthMm)} mm · altezza ${shortDimension(dimensions.heightMm)} mm`
    : view === 'front'
      ? `Larghezza ${shortDimension(dimensions.widthMm)} mm · altezza ${shortDimension(dimensions.heightMm)} mm`
      : `Lunghezza ${shortDimension(dimensions.lengthMm)} mm · larghezza ${shortDimension(dimensions.widthMm)} mm`

  return (
    <div className={`overflow-hidden rounded-lg bg-muted ${compact ? 'p-2' : 'p-3 sm:p-5'}`}>
      <svg
        className="h-auto w-full"
        viewBox="0 0 600 310"
        role="group"
        aria-label={`Schema illustrativo del camper, vista ${viewLabel(view).toLowerCase()}`}
      >
        <title>Schema illustrativo delle misure del camper</title>
        <desc>Seleziona un punto numerato per consultare la misura corrispondente.</desc>
        <g className="text-foreground" stroke="currentColor" strokeWidth="3">
          {view === 'side' && (
            <>
              <path
                d="M54 226V142c0-25 18-45 43-50l43-9h221c24 0 44 10 61 29l48 52 58 13v49H54Z"
                fill="var(--color-background)"
              />
              <path d="M384 90v72h81l-43-47c-11-13-23-21-38-25Z" fill="var(--color-surface-raised)" />
              <rect x="266" y="104" width="66" height="122" rx="4" fill="var(--color-surface-raised)" />
              <rect x="365" y="179" width="83" height="31" rx="3" fill="var(--color-surface-raised)" />
              <circle cx="154" cy="226" r="29" fill="var(--color-surface-raised-2)" />
              <circle cx="427" cy="226" r="29" fill="var(--color-surface-raised-2)" />
            </>
          )}
          {view === 'front' && (
            <>
              <path d="M179 226V126c0-42 30-73 72-73h98c42 0 72 31 72 73v100H179Z" fill="var(--color-background)" />
              <path d="M215 130c0-27 19-45 45-45h80c26 0 45 18 45 45v43H215v-43Z" fill="var(--color-surface-raised)" />
              <rect x="159" y="127" width="20" height="46" rx="8" fill="var(--color-surface-raised)" />
              <rect x="441" y="127" width="20" height="46" rx="8" fill="var(--color-surface-raised)" />
              <circle cx="221" cy="226" r="16" fill="var(--color-surface-raised-2)" />
              <circle cx="379" cy="226" r="16" fill="var(--color-surface-raised-2)" />
            </>
          )}
          {view === 'top' && (
            <>
              <path d="M151 49h298c31 0 53 23 53 54v106c0 31-22 53-53 53H151c-31 0-53-22-53-53V103c0-31 22-54 53-54Z" fill="var(--color-background)" />
              <path d="M161 68h195v175H161c-20 0-35-15-35-35v-105c0-20 15-35 35-35Z" fill="var(--color-surface-raised)" />
              <rect x="293" y="117" width="64" height="64" rx="8" fill="var(--color-surface-raised-2)" />
              <path d="M410 91v130M394 91v130" fill="none" />
            </>
          )}
        </g>

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
    'inline-flex items-center rounded-lg border px-3 py-2 text-sm transition-colors',
    'focus-visible:outline-2 focus-visible:outline-ring',
    selected
      ? 'border-foreground bg-foreground text-background'
      : 'border-border-strong bg-card text-foreground hover:bg-muted',
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
        buildInputFromProfile(mergedProfile, nextSourceName, nextSourceUrl, nextMeasurements),
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
      onSourceChange={(sourceName, sourceUrl) => {
        setManualSourceName(sourceName)
        setManualSourceUrl(sourceUrl)
      }}
      onMeasurementsChange={setManualMeasurements}
      onApplyParsedDraft={handleApplyParsedDraft}
      onSaveManualOnly={() =>
        saveMutation.mutate(
          buildInputFromProfile(
            currentProfile,
            manualSourceName,
            manualSourceUrl,
            manualMeasurements,
          ),
        )
      }
      isSavingProfile={saveMutation.isPending}
      profileSavedSuccess={saveMutation.isSuccess}
    />
  )
}

