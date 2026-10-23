#!/usr/bin/env node

import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import https from 'node:https'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { AxeBuilder } from '@axe-core/playwright'
import { chromium } from 'playwright'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const VITE_CLI = path.join(ROOT, 'node_modules/vite/bin/vite.js')
const PORT = 4322
const BASE_URL = `https://127.0.0.1:${PORT}`
const CAMPER_ID = '0199cafe-1000-7000-8000-000000000001'
const TRIP_ID = '0199cafe-2000-7000-8000-000000000001'
const STOP_ID = '0199cafe-3000-7000-8000-000000000001'
const NEW_STOP_ID = '0199cafe-3000-7000-8000-000000000002'
const ENTRY_ID = '0199cafe-4000-7000-8000-000000000001'
const ATTACHMENT_ID = '0199cafe-5000-7000-8000-000000000001'
const EXPENSE_ID = '0199cafe-6000-7000-8000-000000000001'
const SECOND_EXPENSE_ID = '0199cafe-6000-7000-8000-000000000002'
const EXPENSE_CURSOR = 'expense-after-first'
const CHECKLIST_CURSOR = 'checklist-after-first'
const CHECKLIST_ID = '0199cafe-7000-7000-8000-000000000001'
const SECOND_CHECKLIST_ID = '0199cafe-7000-7000-8000-000000000002'
const CHECKLIST_ITEM_ID = '0199cafe-8000-7000-8000-000000000001'
const SECOND_CHECKLIST_ITEM_ID = '0199cafe-8000-7000-8000-000000000002'

async function main() {
  const preview = spawn(process.execPath, [
    VITE_CLI,
    'preview',
    '--port',
    String(PORT),
    '--host',
    '127.0.0.1',
    '--strictPort',
  ], { cwd: ROOT, stdio: 'pipe' })
  const browser = await startBrowser(preview)

  try {
    const context = await browser.newContext({ ignoreHTTPSErrors: true })
    const page = await context.newPage()
    const observed = await mockApi(page)
    await page.goto(BASE_URL, { waitUntil: 'networkidle' })

    await page.getByRole('link', { name: 'Viaggi' }).click()
    await page.getByRole('heading', { name: 'I tuoi viaggi' }).waitFor()
    await page.getByText(/Non hai ancora iniziato un viaggio/).waitFor()
    await assertAccessible(page, 'lista viaggi vuota')
    await page.getByRole('link', { name: 'Crea viaggio' }).click()
    await page.getByLabel('Camper').selectOption(CAMPER_ID)
    await page.getByLabel('Nome del viaggio').fill('Weekend in Bretagna')
    await page.getByLabel('Prima tappa').fill('Saint-Malo')
    await page.getByRole('button', { name: 'Salva viaggio' }).click()

    await page.getByRole('heading', { name: 'Weekend in Bretagna' }).waitFor()
    await page.getByText(/Saint-Malo/).waitFor()
    await page.getByRole('heading', { name: 'Spese' }).waitFor()
    await page.getByText('Non ci sono ancora spese registrate per questo viaggio.').waitFor()
    await assertAccessible(page, 'spese vuote')
    await page.getByRole('button', { name: 'Aggiungi spesa' }).click()
    await page.getByLabel('Importo').fill('0')
    await page.getByLabel('Valuta (codice ISO 4217)').fill('EUR')
    await page.getByLabel('Data').fill('2026-10-09')
    await page.getByRole('button', { name: 'Salva spesa' }).click()
    await page.getByRole('alert').filter({ hasText: 'Inserisci un importo diverso da zero' }).waitFor()
    await page.getByLabel('Importo').fill('42,75')
    await page.getByLabel('Categoria').selectOption('0')
    await page.getByLabel('Descrizione').fill('Pieno a Saint-Malo')
    await page.getByLabel('Chilometraggio (km, facoltativo)').fill('18450')
    await assertAccessible(page, 'modulo nuova spesa')
    await page.getByRole('button', { name: 'Salva spesa' }).click()
    await page.getByText('42,75 EUR').waitFor()
    assert.equal(observed.expenseCreated, true)
    await page.getByRole('button', { name: 'Carica altre spese' }).click()
    await page.getByText('12,5 USD').waitFor()
    assert.equal(observed.expensePagination, true)

    await page.getByRole('button', { name: 'Modifica spesa' }).first().click()
    await page.getByLabel('Descrizione').fill('Rifornimento aggiornato')
    await page.getByRole('button', { name: 'Salva modifiche' }).click()
    await page.getByText(/cambiata altrove/).waitFor()
    assert.equal(await page.getByLabel('Descrizione').inputValue(), 'Rifornimento aggiornato')
    await page.getByRole('button', { name: 'Ricarica e scarta le modifiche' }).click()
    await page.getByText('Modifica concorrente dal server', { exact: true }).waitFor()
    assert.equal(await page.getByRole('button', { name: 'Salva modifiche' }).count(), 0)
    assert.equal(observed.expenseConflictReloaded, true)
    assert.equal(observed.expenseUpdateRequests, 1, 'il draft respinto non va reinviato con il nuovo ETag')

    const deleteSecondExpense = page.getByRole('button', { name: 'Elimina spesa' }).nth(1)
    await deleteSecondExpense.click()
    await page.getByRole('alertdialog').getByText(/L'eliminazione è definitiva/).waitFor()
    assert.equal(observed.expenseDeleted, false, 'la spesa non va eliminata senza conferma')
    await page.getByRole('alertdialog').getByRole('button', { name: 'Mantieni spesa' }).click()
    await page.getByRole('button', { name: 'Elimina spesa' }).nth(1).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Conferma eliminazione' }).click()
    await page.getByText('12,5 USD').waitFor({ state: 'detached' })
    assert.equal(observed.expenseDeleted, true)

    await page.getByRole('heading', { name: 'Checklist', exact: true }).waitFor()
    await page.getByText('Non hai ancora creato checklist per questo viaggio.').waitFor()
    await page.getByRole('button', { name: 'Aggiungi checklist' }).click()
    await page.getByLabel('Nome della checklist').fill('Partenza')
    await page.getByLabel('Momento del viaggio').selectOption('0')
    await assertAccessible(page, 'nuova checklist')
    await page.getByRole('button', { name: 'Salva checklist' }).click()
    await page.getByRole('heading', { name: 'Partenza' }).waitFor()
    assert.equal(observed.checklistCreated, true)

    await page.getByRole('button', { name: 'Aggiungi checklist' }).click()
    await page.getByLabel('Nome della checklist').fill('Arrivo')
    await page.getByLabel('Momento del viaggio').selectOption('1')
    await page.getByRole('button', { name: 'Salva checklist' }).click()
    await page.getByRole('button', { name: 'Carica altre checklist' }).waitFor()
    await page.getByRole('button', { name: 'Carica altre checklist' }).click()
    await page.getByRole('heading', { name: 'Arrivo' }).waitFor()
    assert.equal(observed.checklistPagination, true)

    await page.getByRole('button', { name: 'Modifica checklist' }).first().click()
    await page.getByLabel('Nome della checklist').fill('Bozza da conservare')
    await page.getByRole('button', { name: 'Salva modifiche' }).click()
    await page.getByText(/Il viaggio e' cambiato altrove/).waitFor()
    assert.equal(await page.getByLabel('Nome della checklist').inputValue(), 'Bozza da conservare')
    await page.getByRole('button', { name: 'Ricarica e scarta le modifiche' }).click()
    await page.getByRole('heading', { name: 'Nome confermato dal server' }).waitFor()
    assert.equal(observed.checklistConflictReloaded, true)
    assert.equal(observed.checklistUpdateRequests, 1, 'la bozza stantia non va reinviata')

    await page.getByRole('button', { name: 'Modifica checklist' }).first().click()
    await page.getByLabel('Nome della checklist').fill('Partenza aggiornata')
    await page.getByLabel('Momento del viaggio').selectOption('2')
    await page.getByRole('button', { name: 'Salva modifiche' }).click()
    await page.getByRole('heading', { name: 'Partenza aggiornata' }).waitFor()
    assert.equal(observed.checklistUpdated, true)

    await page.getByRole('button', { name: 'Aggiungi voce' }).first().click()
    await page.getByLabel('Nuova voce').fill('Controllare acqua')
    await page.getByRole('button', { name: 'Salva voce' }).click()
    await page.getByText('Controllare acqua', { exact: true }).waitFor()
    await page.getByRole('button', { name: 'Aggiungi voce' }).first().click()
    await page.getByLabel('Nuova voce').fill('Chiudere finestre')
    await page.getByRole('button', { name: 'Salva voce' }).click()
    await page.getByText('Chiudere finestre', { exact: true }).waitFor()
    assert.equal(observed.checklistItemCreated, 2)

    await page.getByRole('checkbox', { name: 'Segna come completata: Controllare acqua' }).click()
    await page.getByRole('checkbox', { name: 'Segna come da fare: Controllare acqua' }).waitFor()
    assert.equal(observed.checklistItemCompleted, true)
    await page.getByRole('button', { name: 'Modifica voce' }).first().click()
    await page.getByLabel('Testo della voce').fill('Verificare il livello dell’acqua')
    await page.getByRole('button', { name: 'Salva modifiche' }).click()
    await page.getByText('Verificare il livello dell’acqua', { exact: true }).waitFor()
    assert.equal(observed.checklistItemUpdated, true)
    await page.getByRole('button', { name: 'Sposta in alto: Chiudere finestre' }).click()
    await page.getByRole('article', { name: 'Partenza aggiornata' })
      .locator('ol > li').first().getByText('Chiudere finestre').waitFor()
    assert.equal(observed.checklistReordered, true)
    await page.getByRole('button', { name: 'Elimina voce' }).first().click()
    await page.getByRole('alertdialog').getByText('La voce sarà eliminata definitivamente.').waitFor()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Conferma eliminazione' }).click()
    await page.getByText('Chiudere finestre', { exact: true }).waitFor({ state: 'detached' })
    assert.equal(observed.checklistItemDeleted, true)

    const deleteArrival = page.getByRole('article', { name: 'Arrivo' })
    await deleteArrival.getByRole('button', { name: 'Elimina checklist' }).click()
    await page.getByRole('alertdialog').getByText(/tutte le sue voci saranno eliminate/).waitFor()
    assert.equal(observed.checklistDeleted, false, 'la checklist richiede conferma esplicita')
    const checklistDeleteResponse = page.waitForResponse((response) =>
      response.request().method() === 'DELETE'
      && response.url().includes(`/checklists/${SECOND_CHECKLIST_ID}`))
    await page.getByRole('alertdialog').getByRole('button', { name: 'Conferma eliminazione' }).click()
    assert.equal((await checklistDeleteResponse).status(), 204)
    await page.getByRole('heading', { name: 'Arrivo' }).waitFor({ state: 'detached' })
    assert.equal(observed.checklistDeleted, true)

    await page.getByLabel('Momento della checklist suggerita').selectOption('0')
    await page.getByRole('button', { name: 'Mostra anteprima checklist' }).click()
    await page.getByText('chiudi finestre', { exact: true }).waitFor()
    assert.equal(observed.checklistSeedPreviewRequests, 1)
    assert.equal(observed.checklistSeedConfirmRequests, 0, 'la preview non deve salvare una checklist')

    const ambiguousSeed = page.waitForResponse((response) =>
      response.request().method() === 'POST'
      && response.url().endsWith('/checklists/seed'))
    await page.getByRole('button', { name: 'Conferma e crea checklist' }).click()
    assert.equal((await ambiguousSeed).status(), 503)
    await page.getByRole('alert')
      .filter({ hasText: /non è stato possibile preparare o salvare/i })
      .waitFor()
    await page.getByRole('region', { name: 'Partenza' }).waitFor()
    assert.equal(observed.checklistSeedPreviewRequests, 1, 'un errore ambiguo non deve rigenerare la preview')

    const staleSeed = page.waitForResponse((response) =>
      response.request().method() === 'POST'
      && response.url().endsWith('/checklists/seed'))
    await page.getByRole('button', { name: 'Riprova la stessa conferma' }).click()
    assert.equal((await staleSeed).status(), 412)
    await page.getByRole('alert')
      .filter({ hasText: /anteprima non è più attuale/i })
      .waitFor()
    assert.equal(observed.checklistSeedConfirmRequests, 2, 'il 412 non va ritentato automaticamente')
    assert.equal(observed.checklistSeedPreviewRequests, 1, 'il 412 non deve creare una nuova preview automaticamente')

    const refreshedPreview = page.waitForResponse((response) =>
      response.request().method() === 'GET'
      && response.url().includes('/checklists/seed-preview'))
    await page.getByRole('button', { name: 'Aggiorna anteprima' }).click()
    assert.equal((await refreshedPreview).status(), 200)
    await page.getByText('verifica dotazioni aggiornate', { exact: true }).waitFor()
    await page.getByRole('region', { name: 'Partenza' }).waitFor()
    assert.equal(observed.checklistSeedPreviewRequests, 2)
    const confirmedSeed = page.waitForResponse((response) =>
      response.request().method() === 'POST'
      && response.url().endsWith('/checklists/seed'))
    await page.getByRole('button', { name: 'Conferma e crea checklist' }).click()
    assert.equal((await confirmedSeed).status(), 201)
    await page.getByRole('article', { name: 'Partenza' }).waitFor()
    assert.equal(observed.checklistSeedConfirmed, true)
    assert.equal(observed.checklistSeedPreviewRequests, 2)
    assert.equal(observed.checklistSeedConfirmRequests, 3)

    await page.getByText(/Non ci sono ancora ricordi in questo viaggio/).waitFor()
    await page.getByRole('button', { name: 'Modifica viaggio' }).click()
    await assertAccessible(page, 'modifica inline del viaggio')
    assert.equal(await page.getByLabel('Nome del viaggio').inputValue(), 'Weekend in Bretagna')
    assert.equal(await page.getByLabel('Data di inizio pianificata').inputValue(), '')
    assert.equal(await page.getByLabel('Nome tappa 1').inputValue(), 'Saint-Malo')
    assert.equal(await page.getByLabel('Distanza totale stimata (km)').inputValue(), '')
    assert.equal(await page.getByLabel('Durata totale stimata (minuti)').inputValue(), '')
    assert.equal(await page.getByRole('heading', { name: 'Stima manuale', level: 2 }).count(), 0)
    await page.getByLabel('Distanza totale stimata (km)').fill('123.456')
    await page.getByLabel('Durata totale stimata (minuti)').fill('12.5')
    await page.getByRole('button', { name: 'Salva viaggio' }).click()
    await page.getByRole('alert')
      .filter({ hasText: 'La distanza deve essere non negativa con al massimo 2 decimali e la durata deve essere un numero intero di minuti compreso tra 0 e 2.147.483.647.' })
      .waitFor()
    assert.equal(observed.tripUpdateRequests, 0, 'stime non valide non devono essere inviate')
    await page.getByLabel('Distanza totale stimata (km)').fill('-1')
    await page.getByLabel('Durata totale stimata (minuti)').fill('2147483648')
    await page.getByRole('button', { name: 'Salva viaggio' }).click()
    await page.getByRole('alert')
      .filter({ hasText: 'La distanza deve essere non negativa con al massimo 2 decimali e la durata deve essere un numero intero di minuti compreso tra 0 e 2.147.483.647.' })
      .waitFor()
    assert.equal(observed.tripUpdateRequests, 0, 'stime negative o non finite non devono essere inviate')
    await page.getByLabel('Distanza totale stimata (km)').fill('1234,5')
    await page.getByLabel('Durata totale stimata (minuti)').fill('725')
    await page.getByLabel('Nome del viaggio').fill('   ')
    await page.getByLabel('Nome tappa 1').fill('   ')
    await page.getByRole('button', { name: 'Salva viaggio' }).click()
    await page.getByRole('alert')
      .filter({ hasText: 'Inserisci un nome per il viaggio e per ogni tappa.' })
      .waitFor()
    await page.getByLabel('Nome del viaggio').fill('Weekend in Bretagna')
    await page.getByLabel('Nome tappa 1').fill('Saint-Malo')
    await page.getByLabel('Nome del viaggio').fill('Bretagna in autunno')
    await page.getByLabel('Data di inizio pianificata').fill('2026-10-12')
    await page.getByLabel('Data di fine pianificata').fill('2026-10-19')
    await page.getByLabel('Nome tappa 1').fill('Rennes')
    await page.getByLabel('Arrivo tappa 1').fill('2026-10-12')
    await page.getByLabel('Partenza tappa 1').fill('2026-10-13')
    await page.getByLabel('Note tappa 1').fill('Prima sosta')
    await page.getByRole('button', { name: 'Salva viaggio' }).click()
    await page.getByRole('status').filter({ hasText: 'Viaggio aggiornato.' }).waitFor()
    await page.getByRole('heading', { name: 'Bretagna confermata dal server' }).waitFor()
    await page.getByRole('heading', { name: 'Stima manuale' }).waitFor()
    await page.getByText('1234,5 km').waitFor()
    await page.getByText('725 min').waitFor()
    assert.equal(observed.tripUpdated, true)
    await page.getByRole('button', { name: 'Modifica viaggio' }).click()
    assert.equal(await page.getByLabel('Distanza totale stimata (km)').inputValue(), '1234.5')
    assert.equal(await page.getByLabel('Durata totale stimata (minuti)').inputValue(), '725')
    await page.getByLabel('Distanza totale stimata (km)').fill('')
    await page.getByRole('button', { name: 'Aggiungi tappa' }).click()
    await page.getByLabel('Nome tappa 2').fill('Dinan')
    await page.getByLabel('Arrivo tappa 2').fill('2026-10-14')
    await page.getByLabel('Partenza tappa 2').fill('2026-10-15')
    await page.getByLabel('Note tappa 2').fill('Sosta nuova')
    await page.getByRole('button', { name: 'Sposta in alto tappa 2' }).click()
    assert.equal(await page.getByLabel('Nome tappa 1').inputValue(), 'Dinan')
    await page.getByRole('button', { name: 'Salva viaggio' }).click()
    await page.getByRole('status').filter({ hasText: 'Viaggio aggiornato.' }).waitFor()
    assert.equal(observed.stopOrderSaved, true)
    assert.equal(await page.getByRole('heading', { name: 'Stima manuale' }).count(), 1)
    assert.equal(await page.getByText('1234,5 km').count(), 0)
    await page.getByText('725 min').waitFor()
    await page.getByRole('button', { name: 'Modifica viaggio' }).click()
    assert.equal(await page.getByLabel('Distanza totale stimata (km)').inputValue(), '')
    assert.equal(await page.getByLabel('Durata totale stimata (minuti)').inputValue(), '725')
    await page.getByRole('button', { name: 'Scarta modifiche' }).click()

    await page.getByRole('button', { name: 'Modifica viaggio' }).click()
    await page.getByRole('button', { name: 'Rimuovi tappa 2' }).click()
    await page.getByRole('button', { name: 'Salva viaggio' }).click()
    await page.getByRole('alert')
      .filter({ hasText: "Questa tappa e' collegata a un ricordo e non puo' essere rimossa." })
      .waitFor()
    assert.equal(observed.stopRemovalConflict, true)
    await page.getByRole('button', { name: 'Salva viaggio' }).click()
    await page.getByRole('alert')
      .filter({ hasText: "Non e' stato possibile aggiornare il viaggio." })
      .waitFor()
    assert.equal(observed.tripGenericConflict, true)
    await page.getByLabel('Nome tappa 1').fill('Bozza locale')
    await page.getByLabel('Distanza totale stimata (km)').fill('9999999999999999,99')
    await page.getByLabel('Durata totale stimata (minuti)').fill('999')
    await page.getByRole('button', { name: 'Salva viaggio' }).click()
    await page.getByRole('alert')
      .filter({ hasText: "Il viaggio e' cambiato altrove." })
      .waitFor()
    assert.equal(await page.getByLabel('Nome tappa 1').inputValue(), 'Bozza locale')
    assert.equal(
      await page.getByLabel('Distanza totale stimata (km)').inputValue(),
      '9999999999999999,99',
    )
    assert.equal(await page.getByLabel('Durata totale stimata (minuti)').inputValue(), '999')
    assert.equal(observed.tripConflict, true)
    assert.equal(observed.tripUpdateRequests, 5, 'un PUT rifiutato con 412 non va ripetuto')
    const reloadedTrip = page.waitForResponse((response) =>
      response.url().endsWith(`/api/v1/trips/${TRIP_ID}`)
      && response.request().method() === 'GET')
    await page.getByRole('button', { name: 'Ricarica e scarta le modifiche' }).click()
    await reloadedTrip
    await page.getByRole('heading', { name: 'Versione server' }).waitFor()
    await page.getByText('725 min').waitFor()
    assert.equal(await page.getByText('1234,5 km').count(), 0)
    assert.equal(observed.tripConflictReloaded, true)

    await assertAccessible(page, 'Journal vuoto')
    await page.getByRole('button', { name: 'Aggiungi ricordo' }).click()
    assert.equal(
      await page.locator('input[type="file"]').count(),
      0,
      'il caricamento di immagini deve essere disponibile solo dopo la creazione del ricordo',
    )
    await page.getByRole('button', { name: 'Salva ricordo' }).click()
    await page.getByRole('alert').filter({ hasText: 'Inserisci una data valida e un testo.' }).waitFor()
    await page.getByLabel('Data').fill('2026-10-09')
    await page.getByLabel('Titolo').fill('Arrivo sul mare')
    await page.getByLabel('Testo').fill('   ')
    await page.getByRole('button', { name: 'Salva ricordo' }).click()
    await page.getByRole('alert').filter({ hasText: 'Inserisci una data valida e un testo.' }).waitFor()
    await page.getByLabel('Testo').fill('Il vento ci ha accolti al tramonto.')
    await assertAccessible(page, 'modulo nuovo ricordo')
    await page.getByRole('button', { name: 'Salva ricordo' }).click()
    await page.getByText('Il vento ci ha accolti al tramonto.').waitFor()

    await page.getByRole('button', { name: 'Modifica ricordo' }).click()
    await page.getByLabel('Titolo').fill('Prima sera sul mare')
    await page.getByRole('button', { name: 'Salva modifiche' }).click()
    await page.getByText(/Il viaggio e' cambiato/).waitFor()
    const refreshedTrip = page.waitForResponse((response) =>
      response.url().endsWith(`/api/v1/trips/${TRIP_ID}`)
      && response.request().method() === 'GET')
    await page.getByRole('button', { name: 'Ricarica prima di riprovare' }).click()
    await refreshedTrip
    await page.getByRole('button', { name: 'Salva modifiche' }).click()
    await page.getByText('Prima sera sul mare', { exact: true }).waitFor()
    await page.locator('time[datetime="2026-10-09"]').nth(1).waitFor()
    const attachmentInput = page.getByLabel('Immagine (JPEG, PNG o WebP)')
    await attachmentInput.setInputFiles({
      name: 'note.txt',
      mimeType: 'text/plain',
      buffer: Buffer.from('non è un’immagine'),
    })
    await page.getByRole('alert').filter({ hasText: 'Seleziona un’immagine JPEG, PNG o WebP.' }).waitFor()
    await attachmentInput.setInputFiles({
      name: 'troppo-grande.png',
      mimeType: 'image/png',
      buffer: Buffer.alloc(10 * 1024 * 1024 + 1),
    })
    await page.getByRole('alert').filter({ hasText: 'Ogni immagine può pesare al massimo 10 MiB.' }).waitFor()
    await attachmentInput.setInputFiles({
      name: 'vuoto.png',
      mimeType: 'image/png',
      buffer: Buffer.alloc(0),
    })
    await page.getByRole('alert').filter({ hasText: 'Ogni immagine può pesare al massimo 10 MiB.' }).waitFor()
    assert.equal(observed.attachmentUploadRequests, 0, 'i file non validi non devono raggiungere l’API')

    await attachmentInput.setInputFiles({
      name: 'tramonto.png',
      mimeType: 'image/png',
      buffer: Buffer.from('mock-image'),
    })
    const staleUpload = page.waitForResponse((response) =>
      response.request().method() === 'POST'
      && response.url().endsWith(`/journal-entries/${ENTRY_ID}/attachments`))
    await page.getByRole('button', { name: 'Carica immagine' }).click()
    assert.equal((await staleUpload).status(), 412)
    await page.getByRole('alert').filter({ hasText: /Il viaggio e' cambiato/ }).waitFor()
    assert.equal(observed.attachmentUploadRequests, 1, 'un upload respinto non va ritentato automaticamente')
    const attachmentConflictReload = page.waitForResponse((response) =>
      response.url().endsWith(`/api/v1/trips/${TRIP_ID}`)
      && response.request().method() === 'GET')
    await page.getByRole('button', { name: 'Ricarica prima di riprovare' }).click()
    await attachmentConflictReload
    assert.equal(observed.attachmentUploadRequests, 1, 'il recupero 412 ricarica soltanto i dati')
    const successfulUpload = page.waitForResponse((response) =>
      response.request().method() === 'POST'
      && response.url().endsWith(`/journal-entries/${ENTRY_ID}/attachments`))
    await page.getByRole('button', { name: 'Carica immagine' }).click()
    assert.equal((await successfulUpload).status(), 201)
    await page.getByText('1 di 10 immagini', { exact: true }).waitFor()
    await page.getByText('image/png', { exact: true }).waitFor()
    const attachmentMetadata = page.getByText(/image\/png.*10 byte/)
    await attachmentMetadata.waitFor()
    assert.match(await attachmentMetadata.textContent(), /9 ott/)
    assert.equal(observed.attachmentUploads, 1)
    assert.equal(observed.attachmentConflictReloaded, true)

    await page.getByRole('button', { name: 'Mostra anteprima', exact: true }).click()
    const attachmentPreview = page.getByRole('img', { name: 'Anteprima immagine allegata.' })
    await attachmentPreview.waitFor()
    const downloadUrl = `https://storage.mock.test/${ATTACHMENT_ID}?sig=short-lived`
    assert.equal(await attachmentPreview.getAttribute('src'), downloadUrl)
    const originalLink = page.getByRole('link', { name: 'Apri originale' })
    assert.equal(await originalLink.getAttribute('href'), downloadUrl)
    assert.equal(await originalLink.getAttribute('target'), '_blank')
    assert.equal(observed.attachmentDownloads, 1)
    const originalTabPromise = page.waitForEvent('popup')
    await originalLink.click()
    const originalTab = await originalTabPromise
    await originalTab.waitForLoadState('load')
    assert.equal(originalTab.url(), downloadUrl)
    await originalTab.close()
    await page.getByRole('button', { name: 'Nascondi anteprima' }).click()

    for (let count = 2; count <= 10; count++) {
      await attachmentInput.setInputFiles({
        name: `foto-${count}.jpg`,
        mimeType: 'image/jpeg',
        buffer: Buffer.from(`mock-image-${count}`),
      })
      const uploaded = page.waitForResponse((response) =>
        response.request().method() === 'POST'
        && response.url().endsWith(`/journal-entries/${ENTRY_ID}/attachments`))
      await page.getByRole('button', { name: 'Carica immagine' }).click()
      assert.equal((await uploaded).status(), 201)
      await page.getByText(`${count} di 10 immagini`, { exact: true }).waitFor()
    }
    assert.equal(await attachmentInput.isDisabled(), true, 'non si possono caricare più di 10 immagini')
    assert.equal(await page.getByRole('button', { name: 'Carica immagine' }).isDisabled(), true)
    assert.equal(observed.attachmentUploads, 10)

    const deleteAttachment = page.getByRole('button', { name: 'Elimina immagine' }).first()
    await deleteAttachment.click()
    const attachmentDialog = page.getByRole('alertdialog')
    await attachmentDialog.getByText('Questa immagine sarà eliminata definitivamente e non potrà essere recuperata.').waitFor()
    await assertAccessible(page, 'conferma eliminazione immagine')
    assert.equal(observed.attachmentDeleted, false, 'l’immagine richiede una conferma esplicita')
    await attachmentDialog.getByRole('button', { name: 'Annulla' }).click()
    assert.equal(observed.attachmentDeleteRequests, 0, 'annullare la conferma non deve eliminare l’immagine')
    await page.getByRole('button', { name: 'Elimina immagine' }).first().click()
    const deleteResponse = page.waitForResponse((response) =>
      response.request().method() === 'DELETE'
      && response.url().endsWith(`/journal-entries/${ENTRY_ID}/attachments/${ATTACHMENT_ID}`))
    await page.getByRole('alertdialog')
      .getByRole('button', { name: "Elimina definitivamente l'immagine" })
      .click()
    assert.equal((await deleteResponse).status(), 204)
    await page.getByText('9 di 10 immagini', { exact: true }).waitFor()
    assert.equal(observed.attachmentDeleted, true)
    assert.equal(observed.attachmentDeleteRequests, 1)

    assert.equal(
      await page.getByRole('button', { name: 'Elimina ricordo' }).isDisabled(),
      true,
      'un ricordo con immagini non deve poter essere eliminato',
    )
    await page.getByText('Rimuovi prima tutte le immagini allegate per eliminare il ricordo.').waitFor()

    await page.getByRole('button', { name: 'Inizia viaggio' }).click()
    await page.getByRole('status').filter({ hasText: 'Stato del viaggio aggiornato.' }).waitFor()
    await page.getByRole('button', { name: 'Completa viaggio' }).click()
    await page.getByRole('status').filter({ hasText: 'Stato del viaggio aggiornato.' }).waitFor()
    await page.getByRole('button', { name: 'Riapri viaggio' }).click()
    await page.getByRole('status').filter({ hasText: 'Stato del viaggio aggiornato.' }).waitFor()
    await page.getByRole('button', { name: 'Annulla viaggio' }).click()
    await page.getByRole('alertdialog').getByText(/L'annullamento e' definitivo/).waitFor()
    await assertAccessible(page, 'conferma annullamento')
    const cancellationDialog = page.getByRole('alertdialog')
    const keepTripButton = cancellationDialog.getByRole('button', { name: 'Mantieni viaggio' })
    const cancelTripButton = cancellationDialog.getByRole('button', { name: 'Conferma annullamento' })
    assert.ok(await cancellationDialog.getAttribute('aria-labelledby'))
    assert.ok(await cancellationDialog.getAttribute('aria-describedby'))
    assert.equal(await keepTripButton.evaluate((button) => button === document.activeElement), true)
    await page.keyboard.press('Shift+Tab')
    await page.waitForFunction(() => document.activeElement?.textContent === 'Conferma annullamento')
    assert.equal(await cancelTripButton.evaluate((button) => button === document.activeElement), true)
    await page.keyboard.press('Tab')
    await page.waitForFunction(() => document.activeElement?.textContent === 'Mantieni viaggio')
    assert.equal(await keepTripButton.evaluate((button) => button === document.activeElement), true)
    await page.keyboard.press('Tab')
    await page.waitForFunction(() => document.activeElement?.textContent === 'Conferma annullamento')
    assert.equal(await cancelTripButton.evaluate((button) => button === document.activeElement), true)
    await page.keyboard.press('Escape')
    await cancellationDialog.waitFor({ state: 'detached' })
    await page.waitForFunction(() => document.activeElement?.textContent === 'Annulla viaggio')
    assert.equal(await page.getByRole('button', { name: 'Annulla viaggio' })
      .evaluate((button) => button === document.activeElement), true)
    assert.deepEqual(observed.statusTransitions, [1, 2, 1])
    await page.getByRole('button', { name: 'Annulla viaggio' }).click()
    await page.getByRole('alertdialog').waitFor()
    await page.getByRole('button', { name: 'Conferma annullamento' }).click()
    await page.getByRole('status').filter({ hasText: 'Stato del viaggio aggiornato.' }).waitFor()
    assert.deepEqual(observed.statusTransitions, [1, 2, 1, 3])
    assert.equal(await page.getByRole('button', { name: 'Aggiungi spesa' }).count(), 0)
    await page.getByRole('button', { name: 'Modifica spesa' }).click()
    await page.getByLabel('Descrizione').fill('Correzione dopo annullamento')
    await page.getByRole('button', { name: 'Salva modifiche' }).click()
    await page.getByText('Correzione dopo annullamento', { exact: true }).waitFor()
    assert.equal(observed.cancelledTripExpenseUpdated, true)
    await page.getByRole('link', { name: 'Torna ai viaggi' }).click()
    await page.getByRole('link', { name: /Versione server/ }).getByText('Annullato').waitFor()
    assert.equal(observed.tripListRefreshed, true)
    await page.getByRole('link', { name: /Versione server/ }).click()
    assert.equal(await page.getByRole('button', { name: 'Inizia viaggio' }).count(), 0)
    assert.equal(await page.getByRole('button', { name: 'Completa viaggio' }).count(), 0)
    assert.equal(await page.getByRole('button', { name: 'Riapri viaggio' }).count(), 0)
    await page.getByRole('button', { name: 'Modifica viaggio' }).click()
    assert.equal(await page.getByRole('button', { name: 'Aggiungi tappa' }).count(), 0)
    await page.getByRole('button', { name: 'Scarta modifiche' }).click()

    assert.deepEqual(observed, {
      tripCreated: true,
      tripUpdated: true,
      stopOrderSaved: true,
      stopRemovalConflict: true,
      tripGenericConflict: true,
      tripConflict: true,
      tripConflictReloaded: true,
      tripUpdateRequests: 5,
      statusTransitions: [1, 2, 1, 3],
      tripListRefreshed: true,
      expenseCreated: true,
      expensePagination: true,
      expenseConflictReloaded: true,
      expenseUpdateRequests: 2,
      expenseDeleted: true,
      cancelledTripExpenseUpdated: true,
      checklistCreated: true,
      checklistCollectionEtag: true,
      checklistPagination: true,
      checklistConflictReloaded: true,
      checklistUpdateRequests: 2,
      checklistUpdated: true,
      checklistItemCreated: 2,
      checklistItemCompleted: true,
      checklistItemUpdated: true,
      checklistReordered: true,
      checklistItemDeleted: true,
      checklistDeleted: true,
      checklistSeedPreviewRequests: 2,
      checklistSeedConfirmRequests: 3,
      checklistSeedConfirmed: true,
      checklistSeedRetryKeptIds: true,
      checklistSeedRefreshChangedIds: true,
      entryCreated: true,
      entryUpdated: true,
      entryDeleted: false,
      attachmentUploadRequests: 11,
      attachmentUploads: 10,
      attachmentConflictReloaded: true,
      attachmentDownloads: 1,
      attachmentDeleteRequests: 1,
      attachmentDeleted: true,
    })
    await assertTripsAreRestricted(browser)
    console.log('trip-journal-flow-check: superato (viaggio e allegati Journal verificati con API mock).')
  } finally {
    await browser.close()
    preview.kill()
  }
}

async function mockApi(page) {
  let tripEtag = '"trip-v1"'
  let tripVersion = 1
  let trip = null
  let entry = null
  let expenses = []
  let checklists = []
  const expenseEtags = new Map()
  let firstExpenseUpdateIsStale = true
  let expenseUpdateRequests = 0
  let firstUpdateIsStale = true
  let firstChecklistUpdateIsStale = true
  let checklistUpdateRequests = 0
  let checklistItemCreateCount = 0
  let checklistSeedConflictOnce = true
  let checklistSeedTransientFailureOnce = true
  let firstSeedRequestIdentities = null
  let tripUpdateCount = 0
  let attachmentUploadRequests = 0
  let attachmentNeedsReload = false
  let attachmentStaleUpload = true
  const observed = {
    tripCreated: false,
    tripUpdated: false,
    stopOrderSaved: false,
    stopRemovalConflict: false,
    tripGenericConflict: false,
    tripConflict: false,
    tripConflictReloaded: false,
    tripUpdateRequests: 0,
    statusTransitions: [],
    tripListRefreshed: false,
    expenseCreated: false,
    expensePagination: false,
    expenseConflictReloaded: false,
    expenseUpdateRequests: 0,
    expenseDeleted: false,
    cancelledTripExpenseUpdated: false,
    checklistCreated: false,
    checklistCollectionEtag: false,
    checklistPagination: false,
    checklistConflictReloaded: false,
    checklistUpdateRequests: 0,
    checklistUpdated: false,
    checklistItemCreated: 0,
    checklistItemCompleted: false,
    checklistItemUpdated: false,
    checklistReordered: false,
    checklistItemDeleted: false,
    checklistDeleted: false,
    checklistSeedPreviewRequests: 0,
    checklistSeedConfirmRequests: 0,
    checklistSeedConfirmed: false,
    checklistSeedRetryKeptIds: false,
    checklistSeedRefreshChangedIds: false,
    entryCreated: false,
    entryUpdated: false,
    entryDeleted: false,
    attachmentUploadRequests: 0,
    attachmentUploads: 0,
    attachmentConflictReloaded: false,
    attachmentDownloads: 0,
    attachmentDeleteRequests: 0,
    attachmentDeleted: false,
  }

  await page.context().route('https://storage.mock.test/**', (route) => route.fulfill({
    status: 200,
    contentType: 'image/png',
    body: Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l0cAAAAASUVORK5CYII=',
      'base64',
    ),
  }))

  async function fulfillChecklistMutation(route, checklist, status = 200) {
    tripEtag = `"trip-v${++tripVersion}"`
    const headers = { ETag: tripEtag }
    if (status === 204) {
      await route.fulfill({ status, headers })
      return
    }
    await route.fulfill({
      status,
      contentType: 'application/json',
      headers,
      body: JSON.stringify(checklist),
    })
  }

  await page.route('**/api/v1/me', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 'owner',
      email: 'owner@example.test',
      displayName: 'Persona',
    }),
  }))
  await page.route('**/api/v1/csrf-token', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: '{"token":"flow-csrf"}',
  }))
  await page.route(/\/api\/v1\/campers(?:\?[^/]*)?$/, (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      items: [{ id: CAMPER_ID, name: 'Il Grigio', vehicleKind: 1 }],
      nextCursor: null,
    }),
  }))
  await page.route(/\/api\/v1\/trips(?:\?[^/]*)?$/, (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ items: trip ? [trip] : [], nextCursor: null }),
  }).then(() => {
    if (trip?.status === 3) observed.tripListRefreshed = true
  }))
  await page.route(`**/api/v1/campers/${CAMPER_ID}/expenses**`, async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    if (request.method() === 'GET') {
      assert.equal(url.searchParams.get('tripId'), TRIP_ID)
      const after = url.searchParams.get('after')
      if (after) {
        assert.equal(after, EXPENSE_CURSOR)
        observed.expensePagination = true
      }
      const items = after ? expenses.slice(1) : expenses.slice(0, 1)
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items,
          nextCursor: !after && expenses.length > 1 ? EXPENSE_CURSOR : null,
        }),
      })
      return
    }

    assert.equal(request.method(), 'POST')
    assertMutationHeaders(route)
    const body = request.postDataJSON()
    assert.ok(body.id, 'l’ID della spesa deve essere generato client-side')
    assert.equal(body.tripId, TRIP_ID)
    assert.equal(body.amount.currency, 'EUR')
    assert.equal(body.amount.amount, 42.75)
    assert.equal(body.category, 0)
    assert.equal(body.description, 'Pieno a Saint-Malo')
    assert.equal(body.odometerKm, 18450)
    const expense = {
      id: EXPENSE_ID,
      camperId: CAMPER_ID,
      tripId: TRIP_ID,
      amount: body.amount,
      category: body.category,
      incurredOnUtc: body.incurredOnUtc,
      description: body.description,
      odometerKm: body.odometerKm,
    }
    expenses = [
      expense,
      {
        id: SECOND_EXPENSE_ID,
        camperId: CAMPER_ID,
        tripId: TRIP_ID,
        amount: { amount: 12.5, currency: 'USD' },
        category: 2,
        incurredOnUtc: '2026-10-10',
        description: 'Notte in campeggio',
        odometerKm: null,
      },
    ]
    expenseEtags.set(EXPENSE_ID, '"expense-v1"')
    expenseEtags.set(SECOND_EXPENSE_ID, '"expense-second-v1"')
    observed.expenseCreated = true
    await route.fulfill({
      status: 201,
      contentType: 'application/json',
      headers: { ETag: '"expense-v1"' },
      body: JSON.stringify(expense),
    })
  })
  await page.route('**/api/v1/expenses/**', async (route) => {
    const request = route.request()
    const expenseId = new URL(request.url()).pathname.split('/').at(-1)
    const expense = expenses.find((item) => item.id === expenseId)
    if (request.method() === 'GET') {
      if (expenseId === EXPENSE_ID && expense?.description === 'Modifica concorrente dal server') {
        observed.expenseConflictReloaded = true
      }
      await route.fulfill({
        status: expense ? 200 : 404,
        contentType: 'application/json',
        headers: { ETag: expenseEtags.get(expenseId) },
        body: JSON.stringify(expense),
      })
      return
    }
    assertMutationHeaders(route)
    assert.equal(request.headers()['if-match'], expenseEtags.get(expenseId))
    if (request.method() === 'PUT') {
      expenseUpdateRequests++
      observed.expenseUpdateRequests = expenseUpdateRequests
      if (expenseId === EXPENSE_ID && firstExpenseUpdateIsStale) {
        firstExpenseUpdateIsStale = false
        expenseEtags.set(expenseId, '"expense-v2-external"')
        expenses = expenses.map((item) => item.id === expenseId
          ? { ...item, description: 'Modifica concorrente dal server' }
          : item)
        await route.fulfill({
          status: 412,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'Precondition Failed', status: 412 }),
        })
        return
      }
      const body = request.postDataJSON()
      assert.equal(body.tripId, TRIP_ID)
      if (trip?.status === 3) {
        assert.equal(body.description, 'Correzione dopo annullamento')
        observed.cancelledTripExpenseUpdated = true
      } else {
        assert.fail('la bozza obsoleta non deve essere reinviata dopo il ricaricamento')
      }
      const updated = { ...expense, ...body }
      expenses = expenses.map((item) => item.id === expenseId ? updated : item)
      const newEtag = trip?.status === 3 ? '"expense-v4"' : '"expense-v3"'
      expenseEtags.set(expenseId, newEtag)
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { ETag: newEtag },
        body: JSON.stringify(updated),
      })
      return
    }
    assert.equal(request.method(), 'DELETE')
    assert.equal(expenseId, SECOND_EXPENSE_ID)
    expenses = expenses.filter((item) => item.id !== expenseId)
    observed.expenseDeleted = true
    await route.fulfill({ status: 204 })
  })
  await page.route(`**/api/v1/campers/${CAMPER_ID}/trips`, async (route) => {
    assertMutationHeaders(route)
    const request = route.request().postDataJSON()
    assert.ok(request.id, 'l’ID del viaggio deve essere generato client-side')
    assert.equal('estimatedDistanceKm' in request, false, 'la creazione non deve inviare stime')
    assert.equal('estimatedDurationMinutes' in request, false, 'la creazione non deve inviare stime')
    trip = {
      id: TRIP_ID,
      camperId: CAMPER_ID,
      name: request.name,
      status: 0,
      plannedStartOnUtc: null,
      plannedEndOnUtc: null,
      actualStartOnUtc: null,
      actualEndOnUtc: null,
      estimatedDistanceKm: null,
      estimatedDurationMinutes: null,
      stops: request.stops.map((stop, index) => ({
        id: STOP_ID,
        sequenceNo: index,
        name: stop.name,
        arrivalOnUtc: null,
        departureOnUtc: null,
        position: null,
        notes: null,
      })),
    }
    observed.tripCreated = true
    await route.fulfill({
      status: 201,
      contentType: 'application/json',
      headers: { Location: `/api/v1/campers/${CAMPER_ID}/trips/${TRIP_ID}` },
      body: JSON.stringify(trip),
    })
  })
  await page.route(`**/api/v1/trips/${TRIP_ID}**`, async (route) => {
    const method = route.request().method()
    const url = new URL(route.request().url())
    const tripPath = `/api/v1/trips/${TRIP_ID}`
    const checklistPath = `${tripPath}/checklists`
    const journalPath = `${tripPath}/journal-entries`
    const entryPath = `${journalPath}/${ENTRY_ID}`
    const attachmentPath = `${entryPath}/attachments`
    if (url.pathname === `${checklistPath}/seed-preview` && method === 'GET') {
      assert.equal(url.searchParams.get('kind'), '0')
      observed.checklistSeedPreviewRequests++
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { ETag: tripEtag },
        body: JSON.stringify({
          name: 'Partenza',
          kind: 0,
          catalogVersion: '1',
          items: observed.checklistSeedPreviewRequests === 1
            ? [{ templateKey: 'departure.windows', text: 'chiudi finestre' }]
            : [
                { templateKey: 'departure.windows', text: 'chiudi finestre' },
                { templateKey: 'departure.updated', text: 'verifica dotazioni aggiornate' },
              ],
        }),
      })
      return
    }
    if (url.pathname === `${checklistPath}/seed` && method === 'POST') {
      assertMutationHeaders(route)
      assert.equal(route.request().headers()['if-match'], tripEtag)
      const request = route.request().postDataJSON()
      assert.ok(request.id, 'l’identificativo della checklist suggerita deve restare stabile')
      assert.equal(request.kind, 0)
      assert.deepEqual(
        request.items.map((item) => item.templateKey),
        observed.checklistSeedPreviewRequests === 1
          ? ['departure.windows']
          : ['departure.windows', 'departure.updated'],
      )
      assert.ok(request.items[0].id)
      assert.equal('text' in request.items[0], false, 'il client non deve inviare il testo da salvare')
      observed.checklistSeedConfirmRequests++
      if (observed.checklistSeedConfirmRequests === 1) {
        firstSeedRequestIdentities = {
          id: request.id,
          itemIds: request.items.map((item) => item.id),
        }
      } else if (observed.checklistSeedConfirmRequests === 2) {
        assert.equal(request.id, firstSeedRequestIdentities.id)
        assert.deepEqual(request.items.map((item) => item.id), firstSeedRequestIdentities.itemIds)
        observed.checklistSeedRetryKeptIds = true
      } else if (observed.checklistSeedConfirmRequests === 3) {
        assert.notEqual(request.id, firstSeedRequestIdentities.id)
        assert.notDeepEqual(request.items.map((item) => item.id), firstSeedRequestIdentities.itemIds)
        observed.checklistSeedRefreshChangedIds = true
      }
      if (checklistSeedTransientFailureOnce) {
        checklistSeedTransientFailureOnce = false
        await route.fulfill({
          status: 503,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'Service Unavailable', status: 503 }),
        })
        return
      }
      if (checklistSeedConflictOnce) {
        checklistSeedConflictOnce = false
        await route.fulfill({
          status: 412,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'Precondition Failed', status: 412 }),
        })
        return
      }

      const checklist = {
        id: request.id,
        tripId: TRIP_ID,
        name: 'Partenza',
        kind: 0,
        items: request.items.map((item, index) => ({
          id: item.id,
          text: 'chiudi finestre',
          isDone: false,
          sortOrder: index + 1,
        })),
      }
      checklists.push(checklist)
      observed.checklistSeedConfirmed = true
      await fulfillChecklistMutation(route, checklist, 201)
      return
    }
    if (url.pathname === attachmentPath && method === 'POST') {
      assertMutationHeaders(route)
      assert.equal(route.request().headers()['if-match'], tripEtag)
      assert.match(route.request().headers()['content-type'], /^multipart\/form-data; boundary=/)
      assert.match(route.request().postDataBuffer().toString(), /filename="[^"]+"/)
      attachmentUploadRequests++
      observed.attachmentUploadRequests = attachmentUploadRequests
      if (attachmentStaleUpload) {
        attachmentStaleUpload = false
        attachmentNeedsReload = true
        tripEtag = `"trip-v${++tripVersion}-attachment-external"`
        await route.fulfill({
          status: 412,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'Precondition Failed', status: 412 }),
        })
        return
      }
      const uploadedFile = route.request().postDataBuffer().toString()
      const attachment = {
        id: entry.attachments.length === 0
          ? ATTACHMENT_ID
          : `${ATTACHMENT_ID}-${entry.attachments.length + 1}`,
        contentType: /filename="[^"]+\.jpe?g"/i.test(uploadedFile)
          ? 'image/jpeg'
          : 'image/png',
        sizeBytes: uploadedFile.includes('tramonto.png') ? 10 : uploadedFile.length,
        createdAtUtc: '2026-10-09T09:10:00Z',
      }
      entry.attachments.push(attachment)
      tripEtag = `"trip-v${++tripVersion}-attachment"`
      observed.attachmentUploads++
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        headers: { ETag: tripEtag },
        body: JSON.stringify(attachment),
      })
      return
    }
    if (url.pathname.startsWith(`${attachmentPath}/`)) {
      const segments = url.pathname.slice(attachmentPath.length + 1).split('/')
      const attachmentId = segments[0]
      if (segments.length === 2 && segments[1] === 'download' && method === 'GET') {
        assert.ok(entry.attachments.some((attachment) => attachment.id === attachmentId))
        observed.attachmentDownloads++
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            url: `https://storage.mock.test/${attachmentId}?sig=short-lived`,
            expiresAtUtc: '2026-10-09T09:15:00Z',
          }),
        })
        return
      }
      assert.equal(method, 'DELETE')
      assertMutationHeaders(route)
      assert.equal(route.request().headers()['if-match'], tripEtag)
      observed.attachmentDeleteRequests++
      entry.attachments = entry.attachments.filter((attachment) => attachment.id !== attachmentId)
      tripEtag = `"trip-v${++tripVersion}-attachment-delete"`
      observed.attachmentDeleted = true
      await route.fulfill({ status: 204, headers: { ETag: tripEtag } })
      return
    }
    if (url.pathname === checklistPath && method === 'GET') {
      assert.equal(url.searchParams.get('limit'), '50')
      observed.checklistCollectionEtag = Boolean(tripEtag)
      const after = url.searchParams.get('after')
      if (after) {
        assert.equal(after, CHECKLIST_CURSOR)
        observed.checklistPagination = true
      }
      const items = after ? checklists.slice(1) : checklists.slice(0, 1)
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { ETag: tripEtag },
        body: JSON.stringify({
          items,
          nextCursor: !after && checklists.length > 1 ? CHECKLIST_CURSOR : null,
        }),
      })
      if (checklists.some((item) => item.name === 'Nome confermato dal server')) {
        observed.checklistConflictReloaded = true
      }
      return
    }
    if (url.pathname === checklistPath && method === 'POST') {
      assertMutationHeaders(route)
      assert.equal(route.request().headers()['if-match'], tripEtag)
      const request = route.request().postDataJSON()
      assert.ok(request.id, 'l’ID della checklist deve essere generato client-side')
      assert.equal(request.name, checklists.length === 0 ? 'Partenza' : 'Arrivo')
      assert.equal(request.kind, checklists.length === 0 ? 0 : 1)
      const checklist = {
        id: checklists.length === 0 ? CHECKLIST_ID : SECOND_CHECKLIST_ID,
        tripId: TRIP_ID,
        name: request.name,
        kind: request.kind,
        items: [],
      }
      checklists.push(checklist)
      observed.checklistCreated = true
      await fulfillChecklistMutation(route, checklist, 201)
      return
    }
    if (url.pathname.startsWith(`${checklistPath}/`)) {
      const segments = url.pathname.slice(checklistPath.length + 1).split('/')
      const checklist = checklists.find((item) => item.id === segments[0])
      assert.ok(checklist, 'la checklist deve appartenere al viaggio mockato')
      if (segments.length === 1 && method === 'PUT') {
        assertMutationHeaders(route)
        assert.equal(route.request().headers()['if-match'], tripEtag)
        const request = route.request().postDataJSON()
        checklistUpdateRequests++
        observed.checklistUpdateRequests = checklistUpdateRequests
        if (firstChecklistUpdateIsStale) {
          firstChecklistUpdateIsStale = false
          assert.equal(request.name, 'Bozza da conservare')
          checklist.name = 'Nome confermato dal server'
          tripEtag = `"trip-v${++tripVersion}-checklist-external"`
          await route.fulfill({
            status: 412,
            contentType: 'application/problem+json',
            body: JSON.stringify({ title: 'Precondition Failed', status: 412 }),
          })
          return
        }
        assert.equal(request.name, 'Partenza aggiornata')
        assert.equal(request.kind, 2)
        Object.assign(checklist, request)
        observed.checklistUpdated = true
        await fulfillChecklistMutation(route, checklist)
        return
      }
      if (segments.length === 1 && method === 'DELETE') {
        assertMutationHeaders(route)
        assert.equal(route.request().headers()['if-match'], tripEtag)
        checklists = checklists.filter((item) => item.id !== checklist.id)
        observed.checklistDeleted = true
        await fulfillChecklistMutation(route, undefined, 204)
        return
      }
      if (segments[1] === 'items' && method === 'POST') {
        assertMutationHeaders(route)
        assert.equal(route.request().headers()['if-match'], tripEtag)
        const request = route.request().postDataJSON()
        assert.ok(request.id, 'l’ID della voce deve essere generato client-side')
        checklistItemCreateCount++
        assert.equal(
          request.text,
          checklistItemCreateCount === 1 ? 'Controllare acqua' : 'Chiudere finestre',
        )
        const item = {
          id: checklistItemCreateCount === 1 ? CHECKLIST_ITEM_ID : SECOND_CHECKLIST_ITEM_ID,
          text: request.text,
          isDone: false,
          sortOrder: checklist.items.length,
        }
        checklist.items.push(item)
        observed.checklistItemCreated = checklistItemCreateCount
        await fulfillChecklistMutation(route, checklist, 201)
        return
      }
      if (segments[1] === 'items' && segments[2] === 'order' && method === 'PUT') {
        assertMutationHeaders(route)
        assert.equal(route.request().headers()['if-match'], tripEtag)
        const { itemIds } = route.request().postDataJSON()
        assert.deepEqual(itemIds, [SECOND_CHECKLIST_ITEM_ID, CHECKLIST_ITEM_ID])
        checklist.items = itemIds.map((id, sortOrder) => ({
          ...checklist.items.find((item) => item.id === id),
          sortOrder,
        }))
        observed.checklistReordered = true
        await fulfillChecklistMutation(route, checklist)
        return
      }
      if (segments[1] === 'items' && segments.length === 3) {
        const item = checklist.items.find((candidate) => candidate.id === segments[2])
        assert.ok(item, 'la voce deve appartenere alla checklist mockata')
        assertMutationHeaders(route)
        assert.equal(route.request().headers()['if-match'], tripEtag)
        if (method === 'PUT') {
          const request = route.request().postDataJSON()
          if (request.isDone) observed.checklistItemCompleted = true
          if (request.text === 'Verificare il livello dell’acqua') {
            observed.checklistItemUpdated = true
          }
          Object.assign(item, request)
          await fulfillChecklistMutation(route, checklist)
          return
        }
        assert.equal(method, 'DELETE')
        checklist.items = checklist.items
          .filter((candidate) => candidate.id !== item.id)
          .map((candidate, sortOrder) => ({ ...candidate, sortOrder }))
        observed.checklistItemDeleted = true
        await fulfillChecklistMutation(route, checklist)
        return
      }
    }
    if (url.pathname === tripPath && method === 'GET') {
      if (trip?.name === 'Versione server') observed.tripConflictReloaded = true
      if (attachmentNeedsReload) {
        attachmentNeedsReload = false
        observed.attachmentConflictReloaded = true
      }
      await route.fulfill({
        status: trip ? 200 : 404,
        contentType: 'application/json',
        headers: { ETag: tripEtag },
        body: JSON.stringify(trip),
      })
      return
    }
    if (url.pathname === `${tripPath}/transitions` && method === 'POST') {
      assertMutationHeaders(route)
      assert.equal(route.request().headers()['if-match'], tripEtag)
      const { status } = route.request().postDataJSON()
      observed.statusTransitions.push(status)
      trip = {
        ...trip,
        status,
        actualStartOnUtc: status === 1 ? '2026-10-09' : trip.actualStartOnUtc,
        actualEndOnUtc: status === 2 ? '2026-10-09' : status === 1 ? null : trip.actualEndOnUtc,
      }
      tripEtag = `"trip-v${++tripVersion}"`
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { ETag: tripEtag },
        body: JSON.stringify(trip),
      })
      return
    }
    if (url.pathname === tripPath && method === 'PUT') {
      assertMutationHeaders(route)
      assert.equal(route.request().headers()['if-match'], tripEtag)
      const request = route.request().postDataJSON()
      tripUpdateCount++
      observed.tripUpdateRequests = tripUpdateCount
      if (tripUpdateCount === 1) {
        assert.equal(request.name, 'Bretagna in autunno')
        assert.equal(request.plannedStartOnUtc, '2026-10-12')
        assert.equal(request.plannedEndOnUtc, '2026-10-19')
        assert.equal(request.estimatedDistanceKm, '1234.5')
        assert.equal(request.estimatedDurationMinutes, 725)
        assert.deepEqual(request.stops, [{
          id: STOP_ID,
          name: 'Rennes',
          arrivalOnUtc: '2026-10-12',
          departureOnUtc: '2026-10-13',
          position: null,
          notes: 'Prima sosta',
        }])
      } else if (tripUpdateCount === 2) {
        assert.equal(request.name, 'Bretagna confermata dal server')
        assert.equal(request.estimatedDistanceKm, null, 'il campo vuoto deve cancellare solo la distanza')
        assert.equal(request.estimatedDurationMinutes, 725, 'la durata indipendente resta invariata')
        assert.equal(request.stops.length, 2)
        assert.equal(request.stops[0].name, 'Dinan')
        assert.equal('id' in request.stops[0], false, 'la tappa nuova non deve riutilizzare un ID')
        assert.equal(request.stops[1].id, STOP_ID, 'la tappa mantenuta conserva il proprio ID')
        assert.deepEqual(request.stops.map((stop) => stop.name), ['Dinan', 'Rennes'])
        assert.equal(request.stops[0].arrivalOnUtc, '2026-10-14')
        assert.equal(request.stops[0].departureOnUtc, '2026-10-15')
        assert.equal(request.stops[0].notes, 'Sosta nuova')
        observed.stopOrderSaved = true
      } else if (tripUpdateCount === 3) {
        observed.stopRemovalConflict = true
        await route.fulfill({
          status: 409,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            type: 'https://roamly.dev/problems/trip-stop-referenced',
            title: 'Conflict',
            status: 409,
          }),
        })
        return
      } else if (tripUpdateCount === 4) {
        observed.tripGenericConflict = true
        await route.fulfill({
          status: 409,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            type: 'https://roamly.dev/problems/another-conflict',
            title: 'Conflict',
            status: 409,
          }),
        })
        return
      } else if (tripUpdateCount === 5) {
        assert.equal(request.estimatedDistanceKm, '9999999999999999.99')
        assert.equal(request.estimatedDurationMinutes, 999)
        observed.tripConflict = true
        trip = { ...trip, name: 'Versione server' }
        tripEtag = `"trip-v${++tripVersion}-external"`
        await route.fulfill({
          status: 412,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'Precondition Failed', status: 412 }),
        })
        return
      }
      trip = {
        ...trip,
        ...request,
        name: tripUpdateCount === 1 ? 'Bretagna confermata dal server' : request.name,
        stops: request.stops.map((stop, index) => ({
        ...stop,
        id: stop.id ?? NEW_STOP_ID,
        sequenceNo: index,
        })),
      }
      tripEtag = `"trip-v${++tripVersion}"`
      observed.tripUpdated = true
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { ETag: tripEtag },
        body: JSON.stringify(trip),
      })
      return
    }
    if (url.pathname === journalPath && method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: entry ? [entry] : [], nextCursor: null }),
      })
      return
    }
    if (url.pathname === journalPath && method === 'POST') {
      assertMutationHeaders(route)
      assert.equal(route.request().headers()['if-match'], tripEtag)
      const request = route.request().postDataJSON()
      assert.ok(request.id, 'l’ID del ricordo deve essere generato client-side')
      entry = {
        id: ENTRY_ID,
        tripId: TRIP_ID,
        tripStopId: null,
        entryOnUtc: request.entryOnUtc,
        title: request.title,
        body: request.body,
        createdAtUtc: '2026-10-09T09:00:00Z',
        updatedAtUtc: null,
        attachments: [],
      }
      tripEtag = `"trip-v${++tripVersion}"`
      observed.entryCreated = true
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        headers: { ETag: tripEtag },
        body: JSON.stringify(entry),
      })
      return
    }
    if (url.pathname === entryPath && method === 'PUT') {
      assertMutationHeaders(route)
      assert.equal(route.request().headers()['if-match'], tripEtag)
      if (firstUpdateIsStale) {
        firstUpdateIsStale = false
        tripEtag = '"trip-v2-external"'
        await route.fulfill({
          status: 412,
          contentType: 'application/problem+json',
          body: JSON.stringify({ title: 'Conflict', status: 412 }),
        })
        return
      }
      entry = { ...entry, ...route.request().postDataJSON() }
      tripEtag = `"trip-v${++tripVersion}"`
      observed.entryUpdated = true
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        headers: { ETag: tripEtag },
        body: JSON.stringify(entry),
      })
      return
    }
    if (url.pathname === entryPath && method === 'DELETE') {
      assertMutationHeaders(route)
      assert.equal(route.request().headers()['if-match'], tripEtag)
      entry = null
      tripEtag = '"trip-v4"'
      observed.entryDeleted = true
      await route.fulfill({ status: 204, headers: { ETag: tripEtag } })
      return
    }
    await route.fulfill({ status: 405 })
  })
  return observed
}

function assertMutationHeaders(route) {
  const headers = route.request().headers()
  assert.equal(headers['x-roamly-request'], '1')
  assert.equal(headers['x-xsrf-token'], 'flow-csrf')
}

async function assertAccessible(page, flowStep) {
  const result = await new AxeBuilder({ page }).analyze()
  assert.deepEqual(
    result.violations.map(({ id, impact, help }) => ({ id, impact, help })),
    [],
    `Violazioni accessibilità in: ${flowStep}`,
  )
}

async function assertTripsAreRestricted(browser) {
  const anonymousContext = await browser.newContext({ ignoreHTTPSErrors: true })
  const anonymousPage = await anonymousContext.newPage()
  await anonymousPage.route('**/api/v1/me', (route) => route.fulfill({ status: 401 }))
  await anonymousPage.goto(`${BASE_URL}/trips`, { waitUntil: 'networkidle' })
  await anonymousPage.getByRole('heading', { name: 'Accedi' }).waitFor()
  assert.equal(await anonymousPage.getByRole('link', { name: 'Viaggi' }).count(), 0)
  await anonymousContext.close()

  const limitedContext = await browser.newContext({ ignoreHTTPSErrors: true })
  const limitedPage = await limitedContext.newPage()
  await limitedPage.route('**/api/v1/me', (route) => route.fulfill({
    status: 403,
    contentType: 'application/problem+json',
    body: JSON.stringify({
      type: 'https://roamly.dev/problems/restricted-session',
      title: 'Accesso limitato',
      status: 403,
    }),
  }))
  await limitedPage.goto(`${BASE_URL}/trips`, { waitUntil: 'networkidle' })
  await limitedPage.getByRole('heading', { name: 'Accesso limitato' }).waitFor()
  assert.equal(await limitedPage.getByRole('link', { name: 'Viaggi' }).count(), 0)
  await limitedContext.close()
}

async function startBrowser(preview) {
  await waitForServer()
  if (preview.exitCode !== null) {
    throw new Error('vite preview si è chiuso prima di avviare il test.')
  }
  return chromium.launch()
}

function waitForServer(timeoutMs = 30000) {
  const startedAt = Date.now()
  return new Promise((resolve, reject) => {
    const poll = () => {
      const request = https.get(BASE_URL, { rejectUnauthorized: false }, (response) => {
        response.resume()
        resolve()
      })
      request.on('error', () => {
        if (Date.now() - startedAt >= timeoutMs) {
          reject(new Error(`Timeout in attesa di ${BASE_URL}`))
        } else {
          setTimeout(poll, 300)
        }
      })
    }
    poll()
  })
}

main().catch((error) => {
  console.error(error)
  process.exitCode = 1
})
