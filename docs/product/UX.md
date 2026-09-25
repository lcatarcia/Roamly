# Roamly — UX e Design System

Stato: attivo · Ultimo aggiornamento: 2026-09-24
Origine: `docs/archive/Roamly_Planning_2026-09-24.md` §14, §15, §16

---

## 1. Direzione UX

Il frontend **non deve sembrare un gestionale**. Deve avere una personalità propria.

### Principi visuali

> I principi sotto erano scritti per il prodotto finito. [`ADR-0007`](../adr/0007-design-system.md) li ha riclassificati per fase: **due di essi non sono principi dell'MVP, sono obiettivi di Phase 2-3**, e continuare a trattarli come principi significa progettare schermate attorno a contenuto che non esiste.

| Principio | Stato nell'MVP (Phase 0-1) |
|---|---|
| premium, minimal, whitespace | ✅ principio attivo |
| ottima tipografia | ✅ principio attivo — Fraunces + Inter, **per eccezione** (R22) |
| dark/light mode ben progettati | ✅ **attivo in Phase 1**, con 47 coppie di contrasto misurate e verificate in CI (R14) |
| mobile-first, responsive | ✅ principio attivo — body mai sotto 15px, target touch ≥ 44px |
| travel-oriented | ✅ attivo, ma espresso da **materiale e lessico**, non da fotografia |
| micro-animazioni | ⚠️ ammesse solo in CSS e in forma minima (vedi §6). Nessuna libreria di motion in Phase 0/1 |
| card dinamiche | ❌ **ritirato.** Non esiste un componente `Card` generico (R16): le superfici si chiamano `MetricBlock`, `MaintenanceRow`, `Panel`, `EmptyState` |
| **map-centric** | ❌ **obiettivo di Phase 3**, non principio dell'MVP. Nessun provider geo (decisione #3 aperta) |
| **fotografico** | ❌ **obiettivo di Phase 2**, non principio dell'MVP. Nessun upload, nessuno storage (decisione #8 aperta) |

### Principi di composizione dell'MVP

Sostituiscono, nel frattempo, ciò che avrebbero fatto foto e mappe:

- **Un solo soggetto dominante per schermata.** Il numero più importante è il più grande della pagina.
- **Il dato numerico è la superficie primaria** al posto della fotografia, con trattamento editoriale (serif sui numerali, label in caps micro, densità come gerarchia).
- **Editoriale sulla dashboard, tabellare sulle liste.** Una lista di 14 manutenzioni si legge per scansione: renderla editoriale la peggiora. È la regola di composizione più importante dell'MVP.
- **Materiale al posto dell'immagine:** superficie `sand`, contour lines SVG, texture — generati in CSS/SVG, proprietari per costruzione.
- **`Ribbon` come unica continuità** tra MVP e visione: asse km/tempo in Phase 1, tappe in Phase 2, geometria del percorso in Phase 3.
- **Registro doppio (R21):** narrativo su navigazione ed empty state; **neutro, letterale e ripetitivo** su denaro, privacy, errori e azioni distruttive.

### Sensazione desiderata

Non:

> "Sto compilando dati del mio camper."

Ma:

> "Sto entrando nel mio mondo di viaggio."

> ⚠️ **Al giorno zero questa sensazione è più debole di così, e va detto.** Senza foto e senza mappe, le prime schermate di un account nuovo sono un titolo, un form e una lista vuota. Mitigazioni **parziali** decise in ADR-0007: seed di manutenzioni tipiche (vedi `FEATURES.md` §5) ed empty state progettati **prima** dei dati reali, non dopo.

---

## 2. Home / Camper Dashboard (MVP)

> Il mockup precedente mostrava "Your next adventure", viaggi e costi: **feature di Phase 2-3, in inglese**. È stato sostituito con la dashboard reale dell'MVP, in italiano.

```text
┌──────────────────────────────────────────────────────────┐
│  IL MIO CAMPER                          (label micro)    │
│  Hymer B-Class 588                      (.page-title)    │
│                                                          │
│           94.120 km                     (.numeral-display)│
│           LETTURA ATTUALE                                │
│                                                          │
│  ultimo tagliando          ADESSO            prossimo    │
│  ●━━━━━━━━━━━━━━━━━━━━━━━━━━━━◆━━━━━━━━━━━━━━━━━━━━━━━○  │
│  82.400 km                                   97.400 km   │
│  mar 2026                                    set 2026    │
│                                        ┗━ fra 3.280 km   │
└──────────────────────────────────────────────────────────┘

┌───────────────────────────┐ ┌───────────────────────────┐
│ PROSSIME SCADENZE         │ │ SPESA MANUTENZIONE 2026   │
│ Revisione   [ scaduta ]   │ │ € 890                     │
│ Tagliando   [ in scad. ]  │ │ 4 interventi              │
│ Bombole     [ ok ]        │ │                           │
└───────────────────────────┘ └───────────────────────────┘
```

Regole di lettura del mockup:

- **Un solo numerale dominante** (`MetricBlock`) e **una sola** `.page-title` per schermata.
- Il `Ribbon` è l'asse della manutenzione, **non una mappa**. ⚠️ Il suo asse km dipende dal prerequisito **D4** (lettura del contachilometri): decisione in carico a **@archimedes**. Se il dato non esiste, il `Ribbon` degrada ad asse solo temporale e **non stima i km** (R25).
- I badge di scadenza non usano solo il colore: portano testo esplicito.
- Le liste (manutenzioni, storico) **non** seguono questo trattamento: sono tabellari, con `tabular-nums`.

Dettaglio completo di token, componenti e sequenza in [`ADR-0007`](../adr/0007-design-system.md).

---

## 3. Design System

Elementi definiti in [`ADR-0007`](../adr/0007-design-system.md): color palette, token semantici, typography, spacing, radius, shadows, focus, motion, empty state, stati di caricamento. Ancora **non** definiti perché privi di contenuto: map components e charts (Phase 3).

Non vogliamo una UI "standard React". Vogliamo una UI riconoscibile.

> ✅ **Decisione #7 chiusa da [`ADR-0007`](../adr/0007-design-system.md)** (2026-09-24, severità MEDIUM). Base tecnica: **Tailwind CSS v4** (`@theme`, CSS-first) + **shadcn/ui usato come generatore di codice** (oggi su Base UI) + **token proprietari** + **componenti di dominio custom**. Il codice generato è **posseduto, non dipeso**: si edita e si sostituisce un componente alla volta.
>
> La personalità sta nei **token** e nei **componenti di dominio** (`Ribbon`, `MetricBlock`, `CamperHeader`, `EmptyState`), non nei bottoni.
>
> L'ADR è la fonte di verità: qui restano solo le cose che servono ogni giorno a chi implementa. **Le regole invarianti sono R11-R25**, tutte con un verificatore bloccante in CI.

### Strategia di costruzione — chiusa

I **cinque correttivi anti-look (R11)**, da applicare **prima** del primo componente generato, altrimenti il prodotto è riconoscibile come "app fatta con shadcn":

1. palette forest/sand al posto di neutral/zinc;
2. **scala di radius differenziata**, mai un raggio uniforme;
3. bordo **caldo** + ombra ampia e morbida, e **meno bordi** — separare per spazio, non per linea;
4. densità **non** uniforme: un soggetto dominante per schermata;
5. accento tipografico display sui numeri.

#### Token semantici (R13) — i componenti usano solo questa colonna

I letterali (`sand-100`, `night-950`…) vivono **solo** in `styles/theme.css`. Nessun colore letterale nel codice (**R12**): è la regola che tiene basso il costo di inversione di tutto il resto.

| Token semantico | Tema chiaro | Tema scuro | Uso |
|---|---|---|---|
| `--color-surface` | `sand-100` | `night-950` | fondo dell'app |
| `--color-surface-raised` | `sand-200` | `night-900` | pannelli, righe selezionate |
| `--color-surface-raised-2` | `white` | `night-800` | dialog, popover |
| `--color-text` | `ink` | `sand-100` | testo primario |
| `--color-text-muted` | `forest-700` | `bark-400` | testo secondario, label |
| `--color-numeral` | `ink` | `dune-300` | il numero dominante |
| `--color-accent-surface` | `clay-500` | `clay-500` | **CTA primario** (fondo) |
| `--color-accent-on-surface` | `ink` | `ink` | testo del CTA primario |
| `--color-accent-text` | `clay-700` | `clay-400` | link, icone di accento |
| `--color-highlight-text` | `sun-700` | `sun-400` | scadenze imminenti |
| `--color-highlight-surface` | `sun-400` + `ink` | `night-800` + `sun-400` | badge "in scadenza" |
| `--color-danger-text` | `danger-700` | `danger-400` | testo/icona distruttiva |
| `--color-danger-surface` | `danger-600` | `danger-500` | fondo del bottone distruttivo |
| `--color-danger-on-surface` | `white` | `white` | testo sul bottone distruttivo |
| `--color-border-soft` | `sand-200` | `night-800` | decorativo, nessun requisito |
| `--color-border-strong` | `bark-500` | `bark-400` | input, toggle, asse del `Ribbon` |
| `--color-focus` | `forest-700` | `sun-400` | anello di focus |
| `--color-focus-contrast` | `sand-100` | `night-950` | secondo anello del focus |

**CTA primario: `clay-500` con testo `ink`** (5.03:1, identico nei due temi). Il CTA "clay + testo bianco" è **respinto**: 3.20:1, inaccessibile.

**`danger-*` è separato da `clay` ed è invariante:** compare **solo** su azioni distruttive e irreversibili (**R17**).

Ogni coppia colore/superficie usata dal prodotto è dichiarata in `design/contrast-pairs.json` e verificata in CI su **entrambi i temi** (**R14**): 47 coppie, di cui 44 con soglia obbligatoria. Una coppia non dichiarata **rompe la build**.

#### Scala tipografica

```text
.numeral-display   40-56px   Fraunces   wght 500-600      (il numero dominante)
.page-title        28-34px   Fraunces   wght 500          (1 per schermata)
section-title      18-20px   Inter      wght 600
card-title         16-17px   Inter      wght 600
body               15-16px   Inter      wght 400
data / tabellare   14-15px   Inter      wght 500 + tabular-nums
label micro        11-12px   Inter      wght 600, uppercase, tracking .08em
```

Mobile: `.numeral-display` 32-40px, `.page-title` 24-28px, **body invariato e mai sotto 15px** — motivazione di dominio, non di stile: i camperisti non sono un target giovane e leggono spesso in luce sfavorevole.

**Regola Fraunces (R22): si applica per eccezione dichiarata, mai per default.** Esistono **due sole** classi (`.numeral-display`, `.page-title`), massimo **due occorrenze per schermata**. Vietato su titoli di card in lista, label, form, bottoni, nav e su qualunque numero in tabella. Budget font totale **≤ 120 KB** verificato in CI (**R18**): se si sfora, **esce Fraunces**, non si alza il budget. Font e icone **self-hosted**, nessun asset da terzi (**R19**) — è un requisito di privacy, non di performance.

#### Radius, ombre, spacing, focus

```css
--radius-pill: 999px;  /* bottoni */
--radius-sm:   10px;   /* input, badge */
--radius-md:   16px;   /* blocchi piccoli */
--radius-lg:   24px;   /* blocchi grandi */
--radius-xl:   32px;   /* blocco del dato dominante */

--shadow-soft:     0 12px 40px rgb(16 35 28 / .08);
--shadow-floating: 0 24px 70px rgb(16 35 28 / .16);
```

**Spacing:** scala 4/8. La densità **non** è uniforme ed è essa stessa gerarchia: blocco dominante 24-32px di respiro interno, liste e tabelle 12-16px.

**Ombre:** calde e ampie nel tema chiaro, **assenti in dark** (vedi §7).

**Focus:** anello a **doppio cerchio** (`--color-focus` + `--color-focus-contrast`), perché un solo anello non può passare su superfici chiare e scure insieme. Non è un dettaglio: ADR-0004 richiede un percorso da tastiera affidabile su una conferma irreversibile.

#### Struttura dei componenti

`components/ui/` contiene **solo** wrapper di primitive e non conosce il dominio; `components/domain/` è dove vive l'identità e importa da `ui/` **solo** `Button` e `Field` (**R15**). **Non esiste un componente `Card` generico** (**R16**). Le cartelle `map/` (Phase 3), `travel/` (Phase 2) e `motion/` **non vanno create ora**.

### Schermate obbligatorie da [`ADR-0004`](../adr/0004-privacy-and-erasure.md)

Tre schermate poco attraenti e non rimandabili. **Si progettano insieme alla dashboard (1.1), non alla fine della Phase 1.**

> **Principio: qui il design system si disattiva.** Il rischio non è che siano brutte, è che siano **belle**. Su queste tre schermate: niente microcopy narrativa, niente Fraunces, niente CTA clay, niente animazione di successo, nessun contour. **Registro neutro, letterale, ripetitivo** (R21, R22). Non è rinuncia all'identità: è identità.

| Schermata | Requisiti di design |
|---|---|
| **Informativa privacy** | contenuto statico versionato; misura 60-70 caratteri, body 16px, interlinea 1.6; indice ancorato. L'unica azione registra `PrivacyPolicyVersionSeen`: il testo è **`Ho letto`**, mai `Accetto` — **non è un consenso**. Aggiornamento di versione: avviso **non bloccante** in dashboard, **mai un modal** (bloccare l'app lo trasformerebbe in un consenso forzato) |
| **Scarica i miei dati** | dichiarare in UI **cosa contiene e cosa non contiene** lo ZIP ("è un formato tecnico: completo, ma non pensato per essere letto come un album"). **Re-autenticazione con password in un campo della stessa pagina, non in un modal** (un modal che chiede la password è indistinguibile da un phishing). Stato asincrono esplicito `In preparazione → Pronto → download` con `aria-live`, nessuno spinner indefinito. Errori in registro neutro |
| **Elimina account** | **pagina dedicata, non modal** (un modal si chiude con `Esc` o con un click fuori, e lo stesso gesto è vicino a quello che conferma). **Doppia conferma con contenuto diverso nei due passi**; il passo 2 richiede un input che non si produce per inerzia: scrivere `ELIMINA` (parola del dominio, confronto trim + case-insensitive) **e** la password. Bottone distruttivo: `danger-surface` + **icona** + label esplicita "Elimina definitivamente il mio account" (**R17**), mai "Conferma". Conteggio delle entità coinvolte. **"Annulla" neutro ma pienamente leggibile (≥4.5:1)**: un "Annulla" a basso contrasto è un dark pattern. Dopo la conferma: logout immediato e pagina finale che ripete data e modalità di revoca |

**Date assolute, dal server (R23).** La UI mostra la stessa `DeletionScheduledForUtc` che il database ha scritto: nessun ricalcolo lato client, nessuna data relativa ("fra 30 giorni") come unica forma. Se il client ricalcolasse e la configurazione della grazia cambiasse, la UI mentirebbe su una promessa persistita.

> ### 🔴 Gap D7 — bloccante per il rilascio di "Elimina account"
>
> ADR-0004 prevede `DELETE /api/v1/me/deletion` per revocare entro la grazia. **Ma l'account è immediatamente inaccessibile: da quale interfaccia l'utente revoca?** Non può fare login.
>
> Opzioni sul tavolo: (a) login che, riconosciuto lo stato "in cancellazione", porta a una **sola** pagina di revoca; (b) **link firmato** con scadenza nell'email di conferma; (c) solo contatto umano via email, nessuna UI.
>
> **Non è una decisione di design.** In carico a **@sentinel** e **@hermes**.
>
> **Conseguenza vincolante:** il testo che spiega *come* si annulla **non è scrivibile** finché la decisione non è chiusa, e **non è ammesso un segnaposto in produzione**. Una schermata che promette 30 giorni di grazia senza dire come esercitarla trasforma una grazia reale in una **grazia nominale**, che è peggio del silenzio. La schermata si **progetta** in Phase 1; il suo **rilascio** è bloccato. È un criterio di uscita della Phase 1, non un dettaglio di copy.

> Una conferma ambigua su un'azione irreversibile è un problema di **sicurezza**, non di UX.

---

## 4. i18n e unità di misura

> 🔴 **Non è più una "predisposizione": è un prerequisito di R21** ([`ADR-0007`](../adr/0007-design-system.md)). Il registro doppio è verificabile **solo** se le stringhe stanno in un catalogo i18n: senza catalogo, la regola non è controllabile da nessun lint ed è quindi un proposito.
>
> Requisiti operativi:
> - **nessun letterale di testo nel JSX** (lint bloccante);
> - due namespace: **`neutral.*`** (denaro, privacy, errori, azioni distruttive) e **`narrative.*`** (navigazione, empty state). Le schermate di `features/privacy/**` possono importare **solo** `neutral.*`;
> - `lang="it"` sul documento; formattazione di numeri, date e valute via **`Intl`**, mai a mano;
> - **le date delle azioni irreversibili vengono dal server** (R23), non dal client.
>
> Unità canoniche di **presentazione**: km, litri, kg, metri. Devono coincidere con le unità canoniche di persistenza dichiarate in `docs/architecture/CONTEXT.md`.
> Le valute non sono un problema di formattazione: sono parte del dato (`Money`).

---

## 5. Offline

> ⚠️ **Decisione aperta #10 (review §4), severità MEDIUM. Owner: @archimedes.**
>
> I camperisti sono spesso senza rete. Checklist di partenza, journal e dettagli del viaggio sarebbero i candidati naturali alla consultazione offline.
>
> Ha impatto architetturale forte (sync, conflitti) e va deciso presto — **anche solo per escluderlo esplicitamente** dalla v1.
>
> ⚠️ **Vincolo emerso da [`ADR-0004`](../adr/0004-privacy-and-erasure.md), prima non tracciato da nessuno:** una PWA che persiste dati sul dispositivo crea una **copia dei dati personali fuori dal perimetro del server**. La cancellazione dell'account **non la raggiunge**. Se la #10 entra in scope servono una policy di purge del client al logout e alla cancellazione, e una menzione esplicita nell'informativa.

### Impatti di design della #10, se entra in scope

Non sono dettagli di rifinitura: sono **tre componenti non previsti** dall'inventario dell'MVP.

| Elemento | Requisito |
|---|---|
| **Stato di sincronizzazione per entità** | ogni entità mostra se è sincronizzata, in attesa o in errore. **Mai solo con il colore** (stessa logica di R17): serve testo o icona |
| **UI di conflitto** | quando la stessa entità è stata modificata su due dispositivi, l'utente deve poter vedere le due versioni e scegliere. È la schermata più difficile del set |
| **Indicatore di connessione** | persistente, non un toast: uno stato che scompare è indistinguibile dall'assenza del problema |

Tutti e tre vanno dichiarati nel manifesto delle coppie di contrasto (R14) **in entrambi i temi** prima di essere implementati, e rispettano la checklist di §6.

---

## 6. Motion e accessibilità

Dettaglio e motivazioni in [`ADR-0007`](../adr/0007-design-system.md). Qui c'è ciò che serve per implementare.

**Nessuna libreria di motion in Phase 0/1.** Solo CSS. Motion (con `LazyMotion`) entra in Phase 2 **se e solo se** servono exit/layout animation.

```css
--ease-out:    cubic-bezier(.22, 1, .36, 1);
--ease-in-out: cubic-bezier(.65, 0, .35, 1);
--dur-micro:    140ms;
--dur-standard: 260ms;
--dur-slow:     420ms;
```

| Effetto ammesso | Durata |
|---|---|
| Hover/focus su bottoni, link, righe | 140ms |
| Apertura dialog (opacity + scale .98→1) | 200ms |
| Toast (slide + fade) | 260ms |
| Disegno del `Ribbon` al primo mount (`stroke-dashoffset`) | 600-800ms, **una volta per sessione** — unica animazione "firma" dell'MVP |
| Reveal del primo blocco della dashboard | 400ms, `translateY(12px)`, **una sola volta per pagina** |

**Vietati nell'MVP**, ciascuno con un motivo: sequenza di entrata coreografata dell'hero (tassa ricorrente su una dashboard aperta ogni giorno) · page transition (latenza percepita a ogni navigazione) · parallax (non c'è nulla da parallassare) · animazione del camper al successo (ostacolo alla terza volta su 30) · scroll storytelling (un'app autenticata ha task, non capitoli) · animazioni ambient in loop (batteria) · **camper cursor: mai** — regressione di accessibilità, nessun equivalente touch. **Non è un rinvio, è un no definitivo.**

### Accessibilità — requisiti non negoziabili

- **R20 — ogni animazione di entrata parte dallo stato finale.** Lo stato base è `opacity: 1` / `stroke-dashoffset: 0`; l'animazione lo porta via e lo riporta, **mai il contrario**. Con `prefers-reduced-motion: reduce` nessun elemento deve restare invisibile. Il `Ribbon` è il candidato numero uno a rompersi qui.
- **Focus a doppio anello visibile su tutte e sei le superfici** (3 chiare + 3 scure).
- **Target touch ≥ 44px.**
- **Contrasto: R14**, 47 coppie verificate in CI su entrambi i temi. Una coppia usata e non dichiarata rompe la build.
- **axe-core eseguito due volte**, una per tema. Un componente che passa solo in chiaro fallisce la suite.
- Tastiera completa e in ordine sensato, `aria-live` sugli stati asincroni, `lang="it"`.

### Checklist di completamento pagina

Da eseguire **prima** di considerare finita una schermata.

```text
[ ] La schermata ha UN soggetto dominante, non sei elementi pari?
[ ] Il numero più importante è il più grande della pagina?
[ ] Esiste uno stato vuoto progettato, non un testo di ripiego?
[ ] Tutte le coppie usate sono nel manifesto e passano, in ENTRAMBI i temi?
[ ] Il focus è visibile su tutte e sei le superfici (3 chiare + 3 scure)?
[ ] Tutto è raggiungibile da tastiera, in ordine sensato?
[ ] Fraunces compare al massimo due volte?
[ ] I numeri in tabella sono tabular-nums?
[ ] Con prefers-reduced-motion nessun elemento resta invisibile?
[ ] C'è al massimo una animazione di entrata?
[ ] Il registro è corretto (narrativo in navigazione, neutro su denaro/privacy/errori)?
[ ] Le stringhe passano dal layer i18n, nessun letterale nel JSX?
[ ] Le voci da seed sono distinguibili senza usare solo il colore?
[ ] Il mobile è progettato o solo compresso? Target touch ≥ 44px?
[ ] La pagina è stata guardata in dark, non solo testata?
```

---

## 7. Dark mode

> **Fatto, non proposta: la dark mode è in Phase 1 per scelta esplicita dell'utente, contro la raccomandazione di @pixel** (che era rimandarla alla Phase 2 garantendo solo la struttura dei token). La scelta è legittima e argomentata ("i camperisti usano l'app di sera"). **Il costo è dichiarato sotto e va pagato, non dimenticato.**

### Il costo accettato

| Voce | Solo chiaro | Con dark in Phase 1 |
|---|---:|---:|
| Coppie nel manifesto | 22 | **47** |
| Coppie con soglia obbligatoria | 22 | **44** |
| Esiti negativi dichiarati da mitigare | 0 | **3** |
| Superfici su cui verificare il focus | 3 | **6** |
| Varianti di ogni componente da rivedere | ×1 | **×2** |
| Regressioni strutturali nuove | — | **1** (vedi sotto) |

Più: Phase 0 allungata di ~1 giorno. **Senza i tre verificatori di CI** (contrasto sulle 47 coppie, copertura del manifesto, axe ×2 temi) **la dark mode degrada in silenzio entro due mesi** e l'accessibilità del tema scuro diventa un'affermazione senza prova. La scelta è sostenibile *perché* automatizzata, non malgrado.

### Le cinque leve del calore

In dark la superficie `sand` **non esiste** — un fondo chiaro-caldo su schermo notturno abbaglia. Se ci si limita a invertire si ottiene una dashboard scura qualunque, cioè si perde l'identità appena decisa. Il calore si sposta dalla superficie a tutto il resto:

1. **La superficie è un nero verde,** non un grigio: `night-950` `#0C1411`. Primo segnale, costo zero.
2. **Il testo non è bianco:** `sand-100` (15.78:1). `white` resta ammesso **solo** sul bottone distruttivo, dove il registro *deve* essere clinico.
3. **I bordi sono caldi e portano il peso che in chiaro portava la superficie:** `--color-border-strong` = `bark-400`, una terra chiara, non un grigio.
4. **Gli accenti caldi in dark sono più forti, non più deboli:** `clay-400` 7.08, `sun-400` 10.12, `dune-300` 11.19. **In dark la palette Forest & Sand dà il meglio di sé** — è l'argomento reale a favore della scelta.
5. **L'elevazione non è un'ombra.** Le ombre sono **disattivate** in dark: gradino di superficie + hairline caldo. Ma il gradino `night-900`/`night-950` è **1.15:1** e `night-800`/`night-950` è **1.52:1**: **non è un confine percepibile**. Dove il confine porta informazione (input, controlli, righe selezionabili, asse del `Ribbon`) **serve un bordo `bark-400`**, non un cambio di superficie.

Il **CTA non va ridefinito**: `clay-500` + `ink` è 5.03:1 in entrambi i temi, ed è più distintivo in dark che in chiaro. Unico vincolo: **mai posarlo su `night-800`** senza dichiarare la coppia — R14 bloccherebbe, ed è il comportamento voluto.

### Contour e texture si invertono

| Elemento | Chiaro | Scuro |
|---|---|---|
| `ContourPattern` | `stroke: rgb(23 32 28 / .07)` | `stroke: rgb(243 235 221 / .06)` — **opacità minore**, non maggiore |
| `NoiseOverlay` | `mix-blend-mode: multiply`, ~.035 | `mix-blend-mode: screen`, ~.02 |
| Elevazione | `--shadow-soft` | ombra disattivata, gradino + hairline `bark-400`. **Nessun glow** |
| Gradienti decorativi | `sand-200 → sand-100` | `night-900 → night-950`, **mai** con testo sopra |

Invariato in entrambi i temi: **una sola occorrenza di contour per schermata**, mai sotto testo, mai sotto dati.

### 🔴 La regressione che il tema chiaro non ha

In dark `danger-400` e `clay-400` hanno un **rapporto reciproco di 1.15:1**: in scala di grigi, e per un utente con deuteranopia o protanopia, **sono lo stesso colore**. Non è risolvibile giocando sulla tinta — su fondo quasi nero, qualunque testo che passi AA sta in una banda di luminanza stretta, e due rossi in quella banda si assomigliano per costruzione.

Si risolve con la struttura, non con il colore:

- **R17 è vincolante, non consigliata:** ogni azione distruttiva porta **almeno tre** segnali — token `danger-*`, **icona**, **testo esplicito dell'azione**. Sulla conferma finale si aggiunge un input che non si produce per inerzia.
- **Il bottone distruttivo in dark usa `danger-500` con testo `white`**, mentre il CTA primario usa `clay-500` con testo `ink`: **chiaro-su-scuro contro scuro-su-chiaro** è percepibile anche in scala di grigi.
- **CTA primario e azione distruttiva non compaiono mai nella stessa riga di azioni.**

### Implementazione

Toggle di tema con persistenza e rispetto di `prefers-color-scheme` (Phase 0). I valori scuri vivono nel blocco `[data-theme="dark"]` di `styles/theme.css`: **nessun componente conosce il tema**, perché usa solo token semantici (R13). È esattamente ciò che rende la dark mode un blocco di override invece di un refactor.
