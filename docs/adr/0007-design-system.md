# ADR-0007 — Design system: identità senza fotografia, token misurati, dark mode in Phase 1

- **Stato:** Accettato
- **Data:** 2026-09-24
- **Owner:** @pixel
- **Decisore:** utente
- **Severità:** MEDIUM (con una componente HIGH: i token di `danger` e il flusso di cancellazione di ADR-0004)
- **Chiude:** decisione #7 · vincolo posto da ADR-0004 su @pixel
- **Apre:** due punti non risolti qui — lettura del contachilometri (→ @archimedes), interfaccia di revoca della cancellazione (→ @sentinel, @hermes)

---

## Contesto

Roamly deve avere una base componenti frontend prima della Phase 0 (`ROADMAP.md` §2). Lo stack è fissato: React + Vite + TypeScript, nessun SSR (`ARCHITECTURE.md` §1), vincolo same-site di ADR-0003. Uno sviluppatore singolo, nessun designer, nessuna review esterna, orizzonte su sei fasi. Il rischio registrato in `OPEN-DECISIONS.md` è **I9 — refactor totale della UI**.

Il problema vero non è scegliere una libreria. È questo:

> **L'identità visiva dichiarata in `UX.md` §1 e in `VISION.md` si regge su fotografia outdoor e mappe. Nell'MVP non esiste né l'una né l'altra.** Non sono "in ritardo": sono **assenti**. Non c'è upload (Documenti fuori dalla Phase 1 per ADR-0004), non c'è storage (decisione #8 aperta), non c'è provider geo (decisione #3 aperta).

Quindi: si costruisce un'identità forte su un MVP che mostra un camper, una lista di manutenzioni e tre schermate di privacy — **senza buttare via niente** quando foto e mappe arriveranno?

La risposta di questo ADR è **sì, ma non con gli elementi del documento di direzione** (`docs/archive/Design_Direction_Input_2026-09-24.md`). Quel documento è una direzione per **un sito**: hero full-bleed, scroll storytelling, navbar trasparente, scala tipografica 72-120px, landing "Scopri". Roamly MVP è **un'app interamente autenticata senza una sola pagina indicizzabile**. Il documento va usato come fonte di token e di lessico, **non come piano**, e i suoi scarti vanno registrati qui — altrimenti fra sei mesi qualcuno reimplementerà lo scroll storytelling.

### Vincoli reali

- Nessun budget fotografico, nessun asset stock acquistabile, nessuna fotocamera puntata su un camper reale.
- Nessun designer: ogni scelta estetica di questo documento è **argomentata ma non validata da nessuno**.
- Tre schermate obbligatorie e ostili imposte da ADR-0004 (informativa, export, cancellazione), con un'azione irreversibile da confermare.
- Il prodotto è in italiano, l'utente tipico non è giovane e legge spesso in condizioni di luce sfavorevoli.

---

## Fatti verificati, non verificati e falsi

Come in ADR-0003 e ADR-0004, questa tabella è il contenuto informativo principale. Un ADR di design che non distingue ciò che è misurato da ciò che è gusto non serve a niente.

### ✅ Verificati

| Fatto | Fonte |
|---|---|
| **Tutti i contrast ratio di questo documento sono calcolati** con la formula WCAG 2.x (luminanza relativa, sRGB), non stimati né letti a occhio | calcolo diretto, riproducibile con lo script di R14 |
| `clay-500` `#C97952` su `sand-100` = **2.80:1** → fallisce AA (4.5) **e** la soglia 3:1 dei componenti non testuali (1.4.11): non è salvabile nemmeno come "solo icone" | calcolo |
| `white` su `clay-500` = **3.20:1** → **il CTA "clay + testo bianco" del documento di direzione non è accessibile** | calcolo |
| `ink` `#17201C` su `clay-500` = **5.03:1** → il CTA scelto (D3) passa AA | calcolo |
| `sun-400` `#E6B85C` su `sand-100` = **1.56:1** → inutilizzabile sul tema chiaro; su `forest-950` = **8.89:1** | calcolo |
| `moss-500` su `sand-100` = **3.07:1** → non è un colore di testo | calcolo |
| `sand-200` su `sand-100` = **1.14:1** → non è un bordo di controllo | calcolo |
| `bark-500` `#8A7A5E` ≥ 3:1 su tutte e tre le superfici chiare (3.53 / 3.10 / 4.04) | calcolo |
| **Dark:** `sand-100` su `night-950` `#0C1411` = **15.78:1**; `clay-400` `#E08A5C` su `night-950` = **7.08:1**; `sun-400` su `night-950` = **10.12:1**; `bark-400` `#A4906E` su `night-950` = **6.04:1** | calcolo |
| **Dark:** il CTA `clay-500` + `ink` **regge senza modifiche**: la coppia resta 5.03:1 ed è indipendente dal tema; il bordo del bottone contro `night-950` è 5.64:1, ben oltre il 3:1 di 1.4.11 | calcolo |
| **Dark:** `night-900` su `night-950` = **1.15:1** e `night-800` su `night-950` = **1.52:1** → **il salto di superficie NON è un confine percepibile**: l'elevazione da sola non può portare informazione | calcolo |
| **Dark:** `danger-400` `#E8705A` e `clay-400` `#E08A5C` hanno un rapporto reciproco di **1.15:1** → in scala di grigi sono lo stesso colore. Nel tema chiaro il rapporto `danger-600`/`clay-500` è 2.29:1, già debole | calcolo |
| **Fraunces** è variable a 4 assi (`wght`, `opsz`, `SOFT`, `WONK`), licenza OFL; woff2 full-axis **~190 KB** | web, set-2026 |
| **Base UI** è a v1.0 stabile da dicembre 2025 (~35 componenti); a set-2026 è su v1.8.x | web |
| **shadcn/ui** usa oggi **Base UI** come layer di comportamento di default; Radix resta supportato con percorso di migrazione | web |
| **Tailwind CSS v4** è stabile, configurazione **CSS-first via `@theme`**, plugin ufficiale `@tailwindcss/vite` | web |
| **Motion** (ex Framer Motion): ~34 KB gzip completo, ~4.6 KB con `LazyMotion`+`m` | web |
| Il documento di direzione raccomanda **Next.js** (§44-45) e **Radix** "se servono componenti accessibili" (§44): la seconda indicazione è **superata** | lettura + web |

### ⚠️ Non verificati — assunzioni dichiarate

| Affermazione | Perché non verificata |
|---|---|
| Il subset `latin` + `wght`-only di Fraunces sta "nell'ordine delle decine di KB" | **non misurato.** Dipende dai glifi e dagli assi congelati. **Va misurato in Phase 0 (0.3), non assunto** — è il motivo per cui R18 esiste |
| "~1 giorno" per i cinque correttivi anti-look; tutte le stime in giorni di §Sequenza | stime senza storico di velocità |
| "Il dato numerico come superficie primaria funziona" | **è la tesi centrale di questo ADR e non è validata da nessun utente.** È il rischio principale |
| Che l'asse km del `Ribbon` sia calcolabile | **dipende da un dato di dominio non ancora deciso** (D4 → @archimedes). Vedi §Il Ribbon e il suo prerequisito |
| Che il registro doppio sia percepito come coerenza e non come incoerenza | assunzione di prodotto, letteratura a favore, nessun test su questo prodotto |
| Che i cinque tratti del "look shadcn" siano davvero i cinque che contano | giudizio professionale argomentato, non misura |
| Conformità legale dei testi delle tre schermate ADR-0004 | **non è una valutazione che do.** Progettate per chiarezza, non certificate |
| Che la dark mode sia usata abbastanza da ripagare il suo costo di verifica | **assunzione dell'utente** ("i camperisti usano l'app di sera"), non misurata. Vedi tradeoff 10 |

### ❌ Falsi o superati — dal documento di direzione

| Affermazione | Stato |
|---|---|
| "La fotografia outdoor è la superficie primaria" (§56.3) | ❌ **Non vero per l'MVP.** Nessun upload, nessun budget, nessuno storage. È il buco che questo ADR riempie |
| "La mappa come spazio narrativo" (§56.2) | ❌ Non disponibile prima della Phase 3 (decisione #3 aperta) |
| "Next.js" come stack (§44) | ❌ Escluso da `ARCHITECTURE.md` §1 + same-site di ADR-0003 |
| "Radix UI se servono componenti accessibili" (§44) | ❌ Superato: Base UI è il default di shadcn/ui |
| Clay come colore sia di CTA sia di alert (§3) | ❌ **Difetto di sicurezza su ADR-0004**, non di estetica. Serve `danger-*` |
| Scala tipografica 72-120px (§7) | ❌ Tarata su una landing. Inapplicabile |
| Navbar trasparente (§8), hero full-bleed (§9), layout "Scopri" (§37) | ❌ Presuppongono una landing pubblica che l'MVP non ha |
| Le 5 fasi di §53 | ❌ Scollegate dalla `ROADMAP.md` reale |
| "Camper cursor" (§50.2) | ❌ Regressione di accessibilità. **Non è un rinvio: è un no definitivo** |
| Variante "Night Camp" come **seconda identità** (§4) | ❌ Respinta come identità parallela, ✅ **riusata come rampa di superfici della dark mode** — vedi §Dark mode |

### Una smentita a una premessa ricorrente

> *"Camper e manutenzioni sono contenuto povero e tabellare."*

**Vero per la lista, falso per il camper.** Un camper è un oggetto singolo con identità e stato: materiale editoriale legittimo. Una lista di 14 manutenzioni è materiale tabellare, e renderla editoriale la **peggiora** (una lista omogenea si legge per scansione; l'asimmetria la rallenta). **La regola giusta non è "trattare tutto editorialmente" né "accettare che sia tabellare": è separare i due casi.** È la regola di composizione più importante dell'MVP e il documento di direzione non la contiene.

---

## Opzioni considerate

Tre livelli vanno decisi insieme ma **non confusi**: **L1** comportamento/accessibilità (dialog, focus trap, menu, combobox), **L2** styling engine, **L3** look (token e componenti di dominio). Su L2 non c'è decisione reale: **Tailwind v4** è coerente con lo stack e il suo `@theme` è letteralmente il modello di token richiesto.

### Opzione 1 — Tutto da zero, nessuna primitiva esterna

- **Pro:** zero dipendenze, zero look ereditato, bundle minimo.
- **Contro:** il costo non è nei bottoni, è nel **focus trap**. Dialog, dropdown, combobox, tabs con gestione corretta di tastiera, `aria-*`, scroll lock, restore del focus e portali sono settimane — ed è il tipo di codice in cui uno sviluppatore singolo **non sa di aver sbagliato**. ADR-0004 richiede una conferma accessibile su un'azione irreversibile: è il peggior posto possibile per un focus trap artigianale.
- **Complessità:** ALTA.

### Opzione 2 — Tailwind v4 + Base UI headless, tutti i componenti scritti a mano

- **Pro:** accessibilità risolta da una libreria stabile, **zero look ereditato**, massima aderenza ai token.
- **Contro:** i primi ~10 giorni di Phase 0 sono CSS, non prodotto. È l'opzione 1 con il rischio di accessibilità rimosso e quasi tutto il costo di tempo intatto.
- **Complessità:** MEDIO-ALTA. **Resta l'alternativa da riaprire** se arriva un secondo sviluppatore o un designer.

### Opzione 3 — Tailwind v4 + shadcn/ui come generatore + token proprietari + componenti di dominio custom ⭐ **SCELTA**

- **Pro:** è l'unica opzione che **comprime il tempo sul lavoro non-differenziante** (input, dialog, select, toast) **lasciandolo intatto su quello differenziante** (`Ribbon`, `MetricBlock`, `CamperHeader`, empty state, le tre schermate privacy). Il codice viene **copiato nel repo**: non è una dipendenza, si edita, si rimuove componente per componente.
- **Contro:** **il look shadcn è riconoscibile ed è il rischio reale della #7.** Non si neutralizza cambiando i colori. La mitigazione (R11) è efficace ma **si può dimenticare**, ed è la debolezza strutturale di questa scelta.
- **Complessità:** BASSA-MEDIA. **Costo di inversione BASSO e per-componente.**

### Opzione 4 — UI kit completo tematizzato (Mantine / MUI / Chakra) — **respinta, dopo essere stata inizialmente scelta**

> **Questa sezione esiste perché la decisione è stata invertita durante l'istruttoria, e qualcuno la riproporrà.**

L'utente aveva inizialmente scelto questa opzione — con un argomento legittimo: è la più veloce all'inizio e porta già pronti i componenti data-heavy (tabelle, date picker, form) che sono **esattamente** ciò che serve in Phase 1. Presentato il costo di inversione e il legame con il rischio registrato I9, l'utente ha cambiato scelta consapevolmente.

**Motivo del rifiuto, in tre punti:**

1. **È l'unica delle cinque opzioni con costo di inversione ALTO.** Il look non è "configurato", è ereditato in profondità: il theming passa dall'API del kit, e ciò che il kit non espone non si cambia. Uscirne non è per-componente: è un big bang.
2. **Contraddice direttamente `UX.md` §3** ("non vogliamo una UI standard React"). Un kit tematizzato produce un prodotto riconoscibile come "app fatta con quel kit" — che è precisamente ciò che la #7 doveva evitare.
3. **È il modo più probabile di realizzare il rischio I9** ("refactor totale della UI") fra 18 mesi, cioè nel momento in cui il `Ribbon` e i blocchi editoriali devono convivere con i layout del kit. Il guadagno di velocità è reale e si incassa **nelle prime tre settimane**; il conto si paga **una volta sola e tutto insieme**.

**Quando la scelta andrebbe riconsiderata:** se Roamly dovesse produrre in fretta un backoffice o una superficie amministrativa **non identitaria**, un kit completo su *quella* superficie sarebbe difendibile — ma come prodotto separato, non come base dell'app.

### Opzione 5 — Base UI diretto, shadcn usato solo come riferimento di implementazione

Variante difendibile dell'opzione 3: tutti i vantaggi senza il codice generato, a costo di più lavoro manuale. Non scelta perché il costo manuale è il collo di bottiglia di uno sviluppatore singolo, e perché il codice generato è comunque **da leggere, non da accettare**.

---

## Decisione

1. **Base tecnica (#7 chiusa):** **Tailwind CSS v4** (configurazione CSS-first, `@theme`) + **shadcn/ui come generatore di codice** (oggi su Base UI) + **token proprietari** + **componenti di dominio custom**. Il codice generato è **posseduto**, non dipeso.
2. **Identità dell'MVP su tre elementi**, con la fotografia **dichiarata assente fino alla Phase 2** e le mappe fino alla Phase 3:
   - **(a) `Ribbon`** — la route come primitiva grafica astratta. In Phase 1 **non è geografica**: è l'asse km/tempo della manutenzione. In Phase 2 sono le tappe del viaggio, in Phase 3 la geometria del percorso. **È l'unica continuità reale tra MVP e visione.**
   - **(b) Il dato numerico come superficie primaria**, al posto della fotografia, con trattamento editoriale (un soggetto dominante per schermata, serif sui numeri, label in caps micro, densità come gerarchia).
   - **(c) Materiale: `sand` + contour lines SVG + texture**, generati in CSS/SVG, costo di bundle trascurabile, proprietari per definizione perché sono asset generati.
3. **CTA primario: `clay-500` `#C97952` con testo `ink` `#17201C`** — 5.03:1 ✅. Il CTA "clay + bianco" del documento di direzione (3.20:1) è respinto perché inaccessibile.
4. **Token semantici a due livelli obbligatori** (semantici sopra i letterali). Non è più un'opzione di struttura: è il prerequisito della dark mode, ed è ciò che tiene basso il costo di inversione di tutto il resto.
5. **Dark mode in Phase 1**, con palette completa definita in questo ADR e matrice di contrasto misurata. **Scelta dell'utente contro la raccomandazione di @pixel**, con accettazione esplicita del raddoppio della matrice di verifica (vedi tradeoff 10).
6. **Token `danger-*` separato da `clay`**, invariante: compare **solo** su azioni distruttive e irreversibili.
7. **Tipografia: Fraunces + Inter.** Fraunces **per eccezione dichiarata**, mai per default, subset `latin`, **solo asse `wght`**, **budget font totale ≤ 120 KB** verificato in CI.
8. **Registro doppio**, regola scritta: narrativo su navigazione ed empty state; **neutro, letterale e ripetitivo** su denaro, privacy, errori e azioni distruttive.
9. **Le tre schermate di ADR-0004 si progettano insieme alla dashboard**, non alla fine della Phase 1.
10. **Seed di manutenzioni tipiche alla creazione del camper**, modificabili ed eliminabili dall'utente, visibilmente distinguibili dai dati inseriti.
11. **Nessuna libreria di motion in Phase 0/1.** Solo CSS. Motion entra in Phase 2 con `LazyMotion`, se e solo se servono exit/layout animation.
12. **Regole invarianti R11-R25**, ciascuna con un verificatore. Stesso criterio di ADR-0004: *nessun requisito deve dipendere dalla memoria dello sviluppatore.*

**Due punti NON decisi qui, e dichiarati aperti:**

- **La lettura del contachilometri** (prerequisito di dominio del `Ribbon`) → @archimedes, domain model.
- **L'interfaccia da cui si revoca la cancellazione dell'account** → @sentinel + @hermes. **Blocca il rilascio della terza schermata di ADR-0004.**

---

## Regole invarianti — R11-R25

> Numerazione in continuità con R1-R7 (`SECURITY.md` §2.2) e R8-R10 (ADR-0004). **R1-R10 non vengono toccate.**

Criterio, invariato: *una regola di design che si applica "ricordandosene" non è una regola, è un proposito. La differenza è che la prima rompe la build.*

| # | Regola | Verificata da |
|---|---|---|
| **R11** | **I cinque correttivi anti-look sono applicati PRIMA del primo componente generato.** Sono: (1) palette forest/sand al posto di neutral/zinc; (2) **scala di radius differenziata**, mai un raggio uniforme; (3) bordo **caldo** + ombra ampia e morbida, e **meno bordi** — separare per spazio, non per linea; (4) densità **non** uniforme (un soggetto dominante per schermata); (5) accento tipografico display sui numeri | script di CI `design:guard` (**bloccante**): verifica che `theme.css` definisca ≥4 valori distinti di `--radius-*`, che `components.json` non contenga il preset di default, che nessun file contenga `--radius: 0.5rem`. Più: il primo `shadcn add` è consentito solo se `theme.css` esiste ed è non vuoto |
| **R12** | **Nessun colore letterale nel codice.** Fuori da `styles/theme.css` non esistono hex, `rgb()`, `hsl()`, né classi Tailwind di colore arbitrarie (`bg-[#...]`, `text-[rgb(...)]`) | lint (**bloccante**): regola stylelint + regex ESLint su `src/**` con unica eccezione `styles/theme.css`. Senza R12, tutte le voci 🟢 della tabella dei costi di inversione diventano 🔴 |
| **R13** | **Token a due livelli.** I componenti usano **solo** token semantici (`--color-surface`, `--color-text-muted`, `--color-danger-text`…). I token letterali (`--color-sand-100`, `--color-night-950`…) esistono **solo** in `theme.css` e sono referenziabili **solo** dai semantici | lint (**bloccante**): occorrenza di un token letterale fuori da `theme.css` fallisce. È il prerequisito tecnico della dark mode e della futura *dynamic atmosphere* |
| **R14** | **Ogni coppia colore/superficie usata dal prodotto è dichiarata in `design/contrast-pairs.json` con la sua soglia (4.5 / 3.0) e la passa, in ENTRAMBI i temi.** Le coppie che **non** passano sono ammesse solo se dichiarate esplicitamente come non-portatrici di informazione, con la mitigazione indicata | test di CI (**bloccante**): lo script legge i valori reali da `theme.css` (tema chiaro e blocco `[data-theme="dark"]`), ricalcola WCAG 2.x su ogni coppia del manifesto e fallisce sotto soglia. Secondo test: ogni combinazione semantica **usata nel codice** deve esistere nel manifesto — una coppia non dichiarata rompe la build. **È la R8 del design system: protegge il futuro, non il presente** |
| **R15** | **`components/ui/` contiene solo wrapper di primitive, mai dominio.** Nessun nome, nessuna prop, nessun tipo di `ui/` conosce camper, manutenzioni, scadenze o denaro. Specularmente, `components/domain/` **non importa** da `ui/` nulla oltre `Button` e `Field` | test di convenzione sugli import (**bloccante**, tipo dependency-cruiser): un import `domain/ → ui/` fuori dalla whitelist fallisce; un identificatore di dominio dentro `ui/` fallisce |
| **R16** | **Non esiste un componente `Card` generico.** Le superfici sono `MetricBlock`, `MaintenanceRow`, `Panel`, `EmptyState`: nomi che dicono cosa contengono | lint su naming (**bloccante**): export o file chiamati `Card`, `Box`, `Wrapper`, `Container` generico in `src/components/**` falliscono. È il correttivo (4) di R11 reso non dimenticabile: il "foglio di card indistinguibili" nasce dall'esistenza del componente `Card` |
| **R17** | **Il colore non è mai l'unico segnale di distruttività.** Ogni superficie o azione distruttiva porta **almeno tre** segnali: token `danger-*`, **icona**, **testo esplicito dell'azione** ("Elimina definitivamente il mio account", mai "Conferma"). Sulla conferma finale si aggiunge un **input che non si produce per inerzia** | test unitario (**bloccante**) su `DangerButton`/`DangerDialog`: il render deve contenere icona **e** label non generica; lint che vieta `--color-danger-*` fuori da `components/domain/danger/**`. **Motivo misurato:** in dark `danger-400` e `clay-400` hanno un rapporto reciproco di **1.15:1** — in scala di grigi sono lo stesso colore. La regola non è prudenza: copre un difetto reale della palette |
| **R18** | **Budget font: ≤ 120 KB totali** di woff2 serviti dall'applicazione (Inter + Fraunces sommati, dopo subsetting). Fraunces è servito **solo** con subset `latin` e **solo** asse `wght` | script di CI (**bloccante**): somma i byte di `public/fonts/**/*.woff2` e fallisce sopra 120 KB; secondo check: ogni `@font-face` punta a un percorso locale e `src` non contiene domini esterni. **Se il budget viene sforato, esce Fraunces** — non si alza il budget |
| **R19** | **Nessun asset da terze parti.** Font, icone, CSS, script: tutto self-hosted sullo stesso dominio | scan di CI (**bloccante**) su `index.html`, CSS e bundle alla ricerca di URL assoluti esterni. È un requisito **di privacy** (ADR-0004: nessun trasferimento dell'IP dell'utente a un terzo), non di performance |
| **R20** | **Ogni animazione di entrata parte dallo stato finale.** Lo stato base di un elemento è `opacity: 1` / `stroke-dashoffset: 0`; l'animazione lo porta *via* e lo riporta, solo se il motion è consentito. Mai il contrario | test (**bloccante**) con `prefers-reduced-motion: reduce` emulato: nessun elemento della pagina ha `opacity` computata `< 1` né `visibility: hidden` residua. È il modo più comune di rompere una pagina con reduced-motion, e il `Ribbon` è il candidato numero uno |
| **R21** | **Registro doppio, verificabile.** Le stringhe di privacy, denaro, errori e azioni distruttive vivono nel namespace i18n `neutral.*` e non possono usare microcopy narrativa né il font display. Le stringhe narrative vivono in `narrative.*` e non possono comparire in quelle superfici | lint (**bloccante**): nessun letterale di testo nel JSX (R21 richiede l'i18n); le schermate di `features/privacy/**` possono importare **solo** chiavi `neutral.*`. **Senza catalogo i18n questa regola non è verificabile** — è il vero argomento per fare l'i18n subito |
| **R22** | **Fraunces per eccezione.** Non esiste un token `--font-display` applicato a `h1..h6`. Esistono **due sole** classi: `.numeral-display` e `.page-title`. Massimo **una** `.page-title` e **due** occorrenze totali di Fraunces per schermata. **Vietato** su titoli di card in lista, label, form, bottoni, nav, e su qualunque numero in tabella | lint su CSS (**bloccante**): `font-family: var(--font-display)` ammesso solo nelle due classi. Test di render: conteggio delle occorrenze per pagina. I numeri in tabella usano Inter con `font-variant-numeric: tabular-nums` — requisito **funzionale**, le cifre devono incolonnarsi |
| **R23** | **Le date e i conteggi delle azioni irreversibili vengono dal server.** La UI mostra la stessa `DeletionScheduledForUtc` che il database ha scritto; nessun ricalcolo lato client, nessuna data relativa ("fra 30 giorni") come unica forma | review + test: la schermata di cancellazione non contiene aritmetica su date. **Motivo:** se il client ricalcola e la configurazione della grazia cambia, **la UI mente su una promessa persistita** (ADR-0004 §3) |
| **R24** | **I dati da seed sono visibilmente distinguibili** da quelli inseriti dall'utente, in entrambi i temi, con un segnale non solo cromatico (badge testuale "suggerita"), e **eliminabili in blocco** con una sola azione | test di render sul componente di lista + verifica manuale in checklist di pagina. **Motivo:** un dato suggerito che sembra un dato reale è peggio dell'assenza di dato — su una manutenzione, è un dato che l'utente potrebbe credere di aver registrato |
| **R25** | **Nessun componente di dominio assume dati non presenti nel contratto API.** Il `Ribbon` rende l'asse km **solo** se il payload contiene la lettura corrente; in sua assenza degrada all'asse temporale e **non stima i km** | test del componente (**bloccante**) con payload privo di `letturaCorrente`: deve rendere l'asse temporale senza errori e senza cifre inventate. Una stima dei km presentata come dato è indistinguibile da un dato reale per l'utente |

---

## Design token — livello 1: letterali

I letterali vivono **solo** in `styles/theme.css` (R13). Nessun componente li nomina.

```css
@theme {
  /* ---------- superfici chiare ---------- */
  --color-white:      #FCFBF7;
  --color-sand-100:   #F3EBDD;
  --color-sand-200:   #E7DDCB;

  /* ---------- superfici scure (rampa "Night Camp", adattata) ---------- */
  --color-night-950:  #0C1411;   /* fondo app dark */
  --color-night-900:  #14231C;   /* superficie sollevata */
  --color-night-800:  #203A2D;   /* superficie sollevata 2 / hairline caldo */

  /* ---------- verdi ---------- */
  --color-forest-950: #10231C;
  --color-forest-900: #173329;
  --color-forest-700: #28513F;
  --color-moss-700:   #536B49;
  --color-moss-500:   #6F8F62;
  --color-sage-400:   #8FA67F;
  --color-sage-300:   #AFC19F;

  /* ---------- caldi ---------- */
  --color-clay-700:   #975B3D;
  --color-clay-600:   #A16142;
  --color-clay-500:   #C97952;
  --color-clay-400:   #E08A5C;   /* dark only */
  --color-sun-700:    #7E6533;
  --color-sun-400:    #E6B85C;   /* solo su fondi scuri */
  --color-dune-300:   #D9C6A5;   /* dark only: numerali e testo caldo */

  /* ---------- terre / bordi ---------- */
  --color-bark-500:   #8A7A5E;   /* bordo di controllo, tema chiaro */
  --color-bark-400:   #A4906E;   /* bordo di controllo + testo muted, dark */

  /* ---------- pericolo ---------- */
  --color-danger-700: #7F2A1B;
  --color-danger-600: #9B2C1E;
  --color-danger-500: #C0402C;   /* dark only: superficie distruttiva */
  --color-danger-400: #E8705A;   /* dark only: testo/icona distruttiva */

  /* ---------- inchiostro ---------- */
  --color-ink:        #17201C;
}
```

## Design token — livello 2: semantici

**Questa tabella è il contratto.** I componenti usano esclusivamente la colonna "Token semantico". Le due colonne di valori sono un dettaglio di `theme.css`.

| Token semantico | Tema chiaro | Tema scuro | Uso |
|---|---|---|---|
| `--color-surface` | `sand-100` | `night-950` | fondo dell'app |
| `--color-surface-raised` | `sand-200` | `night-900` | pannelli, blocchi, righe selezionate |
| `--color-surface-raised-2` | `white` | `night-800` | superficie massima (dialog, popover) |
| `--color-text` | `ink` | `sand-100` | testo primario |
| `--color-text-muted` | `forest-700` | `bark-400` | testo secondario, label |
| `--color-numeral` | `ink` | `dune-300` | il numero dominante |
| `--color-accent-surface` | `clay-500` | `clay-500` | **CTA primario** (fondo) |
| `--color-accent-on-surface` | `ink` | `ink` | testo del CTA primario |
| `--color-accent-text` | `clay-700` | `clay-400` | link, icone di accento |
| `--color-highlight-text` | `sun-700` | `sun-400` | scadenze imminenti, evidenziazioni |
| `--color-highlight-surface` | `sun-400` + `ink` | `night-800` + `sun-400` | badge "in scadenza" |
| `--color-danger-text` | `danger-700` | `danger-400` | testo/icona distruttiva |
| `--color-danger-surface` | `danger-600` | `danger-500` | fondo del bottone distruttivo |
| `--color-danger-on-surface` | `white` | `white` | testo sul bottone distruttivo |
| `--color-border-soft` | `sand-200` | `night-800` | **decorativo**, nessun requisito |
| `--color-border-strong` | `bark-500` | `bark-400` | input, checkbox, toggle, asse del `Ribbon` |
| `--color-focus` | `forest-700` | `sun-400` | anello di focus |
| `--color-focus-contrast` | `sand-100` | `night-950` | secondo anello del focus |

### Matrice di contrasto — tema chiaro (22 coppie dichiarate)

Soglie: **4.5:1** testo normale (AA 1.4.3) · **3:1** testo large ≥24px o ≥18.66px bold, e componenti/grafici non testuali (AA 1.4.11).

| # | Foreground | Background | Ratio | Soglia | Esito |
|---:|---|---|---:|---:|---|
| L1 | `ink` | `sand-100` | **14.08** | 4.5 | ✅ |
| L2 | `ink` | `sand-200` | **12.39** | 4.5 | ✅ |
| L3 | `ink` | `white` | **16.10** | 4.5 | ✅ |
| L4 | `forest-700` | `sand-100` | **7.58** | 4.5 | ✅ |
| L5 | `forest-700` | `sand-200` | **6.67** | 4.5 | ✅ |
| L6 | `clay-700` | `sand-100` | **4.57** | 4.5 | ✅ (margine sottile) |
| L7 | `clay-700` | `white` | **5.23** | 4.5 | ✅ |
| L8 | `sun-700` | `sand-100` | **4.67** | 4.5 | ✅ |
| L9 | `moss-700` | `sand-100` | **4.97** | 4.5 | ✅ |
| L10 | `danger-700` | `sand-100` | **7.89** | 4.5 | ✅ |
| L11 | `danger-700` | `white` | **9.02** | 4.5 | ✅ |
| L12 | `ink` | `clay-500` | **5.03** | 4.5 | ✅ **CTA primario (D3)** |
| L13 | `white` | `danger-600` | **7.31** | 4.5 | ✅ bottone distruttivo |
| L14 | `ink` | `sun-400` | **9.03** | 4.5 | ✅ badge |
| L15 | `bark-500` | `sand-100` | **3.53** | 3.0 | ✅ bordo controllo |
| L16 | `bark-500` | `sand-200` | **3.10** | 3.0 | ✅ |
| L17 | `bark-500` | `white` | **4.04** | 3.0 | ✅ |
| L18 | `sand-100` | `forest-950` | **13.87** | 4.5 | ✅ blocco scuro nel tema chiaro |
| L19 | `sage-300` | `forest-950` | **8.56** | 4.5 | ✅ |
| L20 | `sun-400` | `forest-950` | **8.89** | 4.5 | ✅ |
| L21 | `clay-500` | `forest-950` | **4.96** | 4.5 | ✅ |
| L22 | `white` | `forest-700` | **8.67** | 4.5 | ✅ |

**Coppie vietate e misurate** (non nel manifesto perché non si usano): `clay-500` su `sand-100` 2.80 ❌ · `sun-400` su `sand-100` 1.56 ❌ · `moss-500` su `sand-100` 3.07 ❌ (mai testo) · `sand-200` su `sand-100` 1.14 ❌ (mai bordo di controllo) · `white` su `clay-500` 3.20 ❌.

---

## Dark mode

> **Questa sezione esiste perché l'utente ha scelto la dark mode in Phase 1 contro la raccomandazione dell'owner.** La raccomandazione era rimandarla alla Phase 2 garantendo solo la struttura dei token. La scelta è legittima e argomentata ("i camperisti usano l'app di sera"), ma **raddoppia la matrice di contrasto e introduce una regressione che il tema chiaro non ha** (R17). Il costo è quantificato sotto, non accennato.

### Il problema vero: in dark la superficie calda sparisce

L'identità decisa si regge su `sand` — un fondo caldo, non bianco. In dark quella superficie **non esiste**: qualunque fondo chiaro-caldo su uno schermo notturno abbaglia. Se ci si limita a invertire, si ottiene **una dashboard scura qualunque**, cioè si perde esattamente l'identità appena decisa.

La risposta non è un colore. È spostare il calore **dalla superficie a tutto il resto**, su cinque leve:

1. **La superficie non è neutra: è un nero verde.** `#0C1411` non è `#111111`. La differenza è piccola in numero e visibile in schermo: una dashboard scura generica usa grigi neutri o blu-grigi; Roamly usa un nero che tende al verde bosco. **È il primo segnale, e costa zero.**
2. **Il testo non è bianco.** `--color-text` in dark è `sand-100` `#F3EBDD` (15.78:1), non `white`. Un testo sabbia su nero-verde è caldo; un testo bianco puro è clinico. `white` in dark resta ammesso **solo** sul bottone distruttivo (dove il registro *deve* essere clinico).
3. **I bordi sono caldi e portano il peso che in chiaro portava la superficie.** `--color-border-strong` in dark è `bark-400` `#A4906E`, una terra chiara — non un grigio. È la leva più efficace, perché in dark i bordi sono molto più presenti che in chiaro (vedi punto 5).
4. **Gli accenti caldi in dark sono più forti, non più deboli.** `clay-400` (7.08), `sun-400` (10.12), `dune-300` (11.19) passano con ampio margine, mentre nel tema chiaro `clay` e `sun` erano il problema. **La dark mode è cromaticamente il tema in cui la palette Forest & Sand dà il meglio.** È l'argomento a favore della scelta dell'utente, ed è reale.
5. **L'elevazione non può essere un'ombra.** Su `#0C1411` un'ombra non si vede: `--shadow-soft` in dark è sostituito da **gradino di superficie + hairline caldo**. Ma — misurato — `night-900` su `night-950` è **1.15:1** e `night-800` su `night-950` è **1.52:1**: il gradino **non è un confine percepibile**. Conseguenza operativa: dove il confine porta informazione (input, controlli, righe selezionabili, asse del `Ribbon`) **serve un bordo `bark-400`**, non un cambio di superficie. Il cambio di superficie resta ammesso solo come segnale ridondante.

### "Night Camp" — valutazione

La variante §4 del documento di direzione proponeva: `#0C1411 · #14231C · #203A2D · #708C68 · #D9C6A5 · #E8A35C · #F5F0E6`, con *fotografie notturne, mappe luminose, glow controllato, typography gigantesca*.

| Parte | Esito |
|---|---|
| Rampa di superfici `#0C1411 / #14231C / #203A2D` | ✅ **Adottata integralmente** come `night-950/900/800`. È già calda e verde: fa esattamente il lavoro del punto 1 |
| `#D9C6A5` | ✅ Adottato come `dune-300`, ma **ricollocato**: non è un testo generico, è il colore del **numerale dominante** (11.19:1) |
| `#708C68`, `#E8A35C` | ⚠️ **Non adottati**: sostituiti da token **misurati** della palette principale (`sage-400`, `clay-400`, `sun-400`). Due rossi/arancio quasi uguali in due palette diverse sono il modo di ritrovarsi con un `danger` indistinguibile |
| `#F5F0E6` come testo | ❌ Sostituito da `sand-100`, già in palette: un secondo bianco-sabbia è una duplicazione senza beneficio |
| "Glow molto controllato" | ❌ **Respinto.** Un alone attorno agli elementi su una UI densa di dati abbassa la leggibilità del testo adiacente, non è esprimibile in un token verificabile da R14, e costa compositing su ogni frame. Il glow è un effetto da landing cinematografica |
| "Typography gigantesca", "fotografie notturne", "mappe luminose" | ❌ Non applicabili: nessuna foto fino alla Phase 2, nessuna mappa fino alla Phase 3, e la scala tipografica dell'app è già decisa (max ~56px sui numerali) |

> **In sintesi: "Night Camp" è una buona rampa di superfici dentro una cattiva seconda identità.** Si prende la rampa. Non si prende l'identità parallela — fare due identità con uno sviluppatore singolo è il modo di non finirne nessuna.

### Matrice di contrasto — tema scuro (25 coppie dichiarate)

| # | Foreground | Background | Ratio | Soglia | Esito |
|---:|---|---|---:|---:|---|
| D1 | `sand-100` | `night-950` | **15.78** | 4.5 | ✅ testo primario |
| D2 | `sand-100` | `night-900` | **13.78** | 4.5 | ✅ |
| D3 | `sand-100` | `night-800` | **10.41** | 4.5 | ✅ |
| D4 | `dune-300` | `night-950` | **11.19** | 4.5 | ✅ numerale dominante |
| D5 | `dune-300` | `night-900` | **9.77** | 4.5 | ✅ |
| D6 | `bark-400` | `night-950` | **6.04** | 4.5 | ✅ testo muted |
| D7 | `bark-400` | `night-900` | **5.28** | 4.5 | ✅ |
| D8 | `bark-400` | `night-800` | **3.99** | 3.0 | ✅ bordo di controllo |
| D9 | `sage-400` | `night-950` | **7.06** | 4.5 | ✅ muted alternativo, verde |
| D10 | `clay-400` | `night-950` | **7.08** | 4.5 | ✅ link e icone di accento |
| D11 | `clay-400` | `night-900` | **6.18** | 4.5 | ✅ |
| D12 | `sun-400` | `night-950` | **10.12** | 4.5 | ✅ highlight |
| D13 | `sun-400` | `night-900` | **8.84** | 4.5 | ✅ |
| D14 | `sun-400` | `night-800` | **6.67** | 3.0 | ✅ **anello di focus in dark** |
| D15 | `danger-400` | `night-950` | **6.14** | 4.5 | ✅ testo distruttivo |
| D16 | `danger-400` | `night-900` | **5.36** | 4.5 | ✅ |
| D17 | `ink` | `clay-500` | **5.03** | 4.5 | ✅ **CTA primario: invariato** |
| D18 | `clay-500` | `night-950` | **5.64** | 3.0 | ✅ confine del CTA |
| D19 | `clay-500` | `night-900` | **4.93** | 3.0 | ✅ |
| D20 | `white` | `danger-500` | **5.06** | 4.5 | ✅ bottone distruttivo in dark |
| D21 | `danger-500` | `night-950` | **3.57** | 3.0 | ✅ confine |
| D22 | `danger-500` | `night-900` | **3.12** | 3.0 | ✅ (margine sottile: **vietato** su `night-800`, 2.74 ❌) |
| D23 | `night-900` | `night-950` | **1.15** | — | ⚠️ **dichiarata non-portante**: l'elevazione non è un segnale. Mitigazione: bordo `bark-400` dove il confine conta |
| D24 | `night-800` | `night-950` | **1.52** | — | ⚠️ idem |
| D25 | `danger-400` | `clay-400` (reciproco) | **1.15** | — | ❌ **regressione strutturale dichiarata.** Mitigazione obbligatoria: **R17** |

**Totale manifesto: 47 coppie** — 22 chiare + 25 scure, di cui **44 con soglia obbligatoria** e **3 con esito negativo dichiarato e mitigazione vincolata** (D23, D24, D25).

### Il CTA in dark: regge, verificato

`clay-500` con testo `ink` è **5.03:1 in entrambi i temi**, perché la coppia è interna al bottone e non dipende dal fondo della pagina. L'unica verifica aggiuntiva richiesta in dark è il **confine** del bottone contro la superficie (1.4.11, soglia 3:1): `clay-500` su `night-950` = **5.64** ✅, su `night-900` = **4.93** ✅.

> **Il CTA non va ridefinito.** È anzi il pezzo che funziona meglio nel passaggio: un bottone terracotta con testo verde scuro su fondo nero-verde è più distintivo in dark che in chiaro. Unico vincolo: **il CTA non va mai posato su `night-800`** in un pannello sollevato senza verificare — la coppia non è nel manifesto e R14 la bloccherebbe, che è il comportamento voluto.

### Il pericolo in dark: la parte onesta

Nel tema chiaro `danger-600` e `clay-500` hanno un rapporto reciproco di 2.29:1 — già debole. **In dark scende a 1.15:1**: per un utente con deuteranopia o protanopia, `danger-400` e `clay-400` sono **lo stesso colore**.

Non è risolvibile giocando sulla tinta: su un fondo quasi nero, qualunque testo che passi AA deve stare in una banda di luminanza stretta e alta, e due rossi in quella banda si assomigliano per costruzione. **Quindi non lo risolvo con un colore, lo risolvo con la struttura:**

- **R17 diventa vincolante, non consigliata**: colore + icona + testo esplicito dell'azione, sempre.
- **Il bottone distruttivo in dark usa `danger-500` con testo `white`**, mentre il CTA primario usa `clay-500` con testo `ink`. **Il colore del testo sul fondo è il differenziatore strutturale**: chiaro su scuro contro scuro su chiaro, percepibile anche in scala di grigi.
- **CTA primario e azione distruttiva non compaiono mai nella stessa riga di azioni.** Nel flusso di ADR-0004 questo è già vero per costruzione (sono pagine diverse) e va mantenuto.

### Contour line e texture in dark: si invertono

In chiaro sono **sottrazioni di luminosità**; in dark vanno **invertite in addizioni**, altrimenti spariscono.

| Elemento | Chiaro | Scuro |
|---|---|---|
| `ContourPattern` (SVG) | `stroke: rgb(23 32 28 / .07)` — inchiostro sottratto a `sand` | `stroke: rgb(243 235 221 / .06)` — sabbia aggiunta al nero-verde. **Opacità minore**, non maggiore: su fondo scuro lo stesso alfa appare più marcato |
| `NoiseOverlay` | `mix-blend-mode: multiply`, opacità ~.035 | `mix-blend-mode: screen` (o `plus-lighter`), opacità ~.02 |
| Elevazione | `--shadow-soft: 0 12px 40px rgb(16 35 28 / .08)` | ombra **disattivata**; gradino di superficie + hairline `bark-400`. Nessun glow (R11, correttivo 3) |
| Gradienti decorativi | `sand-200 → sand-100` | `night-900 → night-950`, **mai** con testo sopra |

Regole invariate in entrambi i temi: **una sola occorrenza di contour per schermata**, mai sotto testo, mai sotto dati.

### Il costo di verifica che è stato accettato

| Voce | Solo chiaro | Con dark in Phase 1 |
|---|---:|---:|
| Coppie nel manifesto | 22 | **47** |
| Coppie con soglia obbligatoria | 22 | **44** |
| Esiti negativi dichiarati da mitigare | 0 | **3** |
| Superfici su cui verificare il focus ring | 3 | **6** |
| Varianti di ogni componente da rivedere a occhio | ×1 | **×2** |
| Regressioni strutturali nuove | — | **1** (R17: `danger`/`clay` indistinguibili in scala di grigi) |

**Automazione — è la condizione che rende la scelta sostenibile.** Tre verificatori, tutti bloccanti in CI:

1. **`scripts/contrast-check.mjs`** — legge i valori **reali** da `theme.css` (blocco `@theme` e blocco `[data-theme="dark"]`), risolve i semantici sui letterali, ricalcola WCAG 2.x su tutte le 47 coppie del manifesto `design/contrast-pairs.json` e fallisce sotto soglia. Nessun database, nessun browser: gira in meno di un secondo, quindi **nessuno lo disattiverà**. È deliberato: lo stesso criterio di R8 in ADR-0004 — il verificatore che protegge il futuro deve essere il più economico.
2. **Check di copertura** — ogni combinazione di token semantici effettivamente usata nel codice deve esistere nel manifesto. **Una coppia nuova non dichiarata rompe la build.** Senza questo, il manifesto invecchia in silenzio ed è come non averlo: è esattamente il fallimento che R8 intercetta sul modello EF, trasposto sui colori.
3. **axe-core sui test di componente, eseguito due volte**, una per tema, tramite un wrapper di test che imposta `data-theme`. Un componente che passa solo in chiaro fallisce la suite.

> Senza questi tre verificatori la dark mode in Phase 1 **degrada in silenzio entro due mesi**, e l'accessibilità del tema scuro diventa un'affermazione senza prova. La scelta dell'utente è accettabile *perché* accompagnata dall'automazione, non malgrado.

---

## Tipografia

**Pairing:** Fraunces (display, OFL) + Inter (testo, OFL). Entrambi self-hosted (R19), sottoinsiemizzati, entro 120 KB totali (R18).

**Regola fondante (R22):** *Fraunces si applica per eccezione dichiarata, mai per default. Il default di ogni superficie è Inter.* Un display serif in un'app densa di dati è attivamente dannoso **come font dei titoli** e ideale **come font dei numeri**: poche glifi, dimensione grande, nessun problema di densità. È l'inversione del cliché, ed è il punto in cui Fraunces guadagna l'identità invece di pagarla.

### Scala

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

| Uso di Fraunces | Verdetto |
|---|---|
| Numeri grandi (`94.120`, `€ 340`, `3.280 km`) | ✅ il caso ideale |
| Titolo di pagina | ✅ uno per schermata |
| Titolo di sezione | ⚠️ solo se non ce ne sono sei |
| Titoli di card in lista | ❌ vietato — 12 titoli serif in colonna rallentano la scansione |
| Label, form, bottoni, nav, numeri in tabella | ❌ vietato |

### Consegna dei font

1. Subset `latin` + `€ ° ' "` e accentate italiane, con `pyftsubset`/`glyphhanger`; **assi `opsz`, `SOFT`, `WONK` congelati**, resta solo `wght`.
2. Self-hosting obbligatorio (R19). Motivo dirimente: caricare font da un CDN terzo trasferisce l'IP dell'utente a un terzo, e questo progetto ha appena scritto ADR-0004 e deve pubblicare un'informativa. Il self-hosting **rimuove il problema invece di doverlo dichiarare**.
3. `<link rel="preload">` **solo su Inter**. Fraunces serve sopra la piega ma su pochi glifi: precaricarlo compete con i dati.
4. `font-display: swap` + `size-adjust`/`ascent-override` calcolati una volta sul fallback. È il sostituto di `next/font` ed è ciò che evita il layout shift.
5. **Verifica del budget (R18):** `du` dei woff2 serviti in CI. **Se si supera 120 KB, esce Fraunces** e resta Inter con `font-variation-settings` più marcati sui numerali. Il budget non si alza: è la clausola che rende la scelta reversibile a costo zero.

---

## Spacing, radius, ombre, motion

**Spacing** — scala 4/8 (coincide con il default Tailwind). La densità **non** è uniforme, ed è essa stessa gerarchia: blocco dominante `24-32px` di respiro interno, liste e tabelle `12-16px`. **La differenza di densità è il correttivo (4) di R11.**

**Radius** — deliberatamente **non** uniforme: è il tratto più riconoscibile di una UI generata con i default.

```css
--radius-pill: 999px;  /* bottoni */
--radius-sm:   10px;   /* input, badge */
--radius-md:   16px;   /* blocchi piccoli */
--radius-lg:   24px;   /* blocchi grandi */
--radius-xl:   32px;   /* blocco del dato dominante */
```

**Ombre** — calde e ampie nel tema chiaro, assenti in dark (vedi §Dark mode):

```css
--shadow-soft:     0 12px 40px rgb(16 35 28 / .08);
--shadow-floating: 0 24px 70px rgb(16 35 28 / .16);
```

**Focus** — anello a **doppio cerchio**, perché un solo anello non può passare su superfici chiare e scure insieme: `outline: 2px solid var(--color-focus); outline-offset: 2px;` con un secondo anello `var(--color-focus-contrast)`. In chiaro `forest-700` (7.58 su `sand-100`); in dark `sun-400` (10.12 su `night-950`, 6.67 su `night-800`). **Non è un dettaglio:** ADR-0004 richiede un percorso da tastiera affidabile su una conferma irreversibile.

**Target touch** ≥ 44px. **Motion**: CSS puro, nessuna libreria in Phase 0/1.

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

**Vietati nell'MVP e con motivo**, tutti dal documento di direzione: sequenza di entrata coreografata dell'hero (tassa ricorrente su una dashboard aperta ogni giorno) · page transition con linea clay (350-650ms × ogni navigazione = latenza percepita) · parallax (non c'è nulla da parallassare) · camper che percorre una route al success (ostacolo alla terza volta su 30) · scroll storytelling (un'app autenticata ha task, non capitoli) · animazioni ambient in loop (consumo di batteria su un'app aperta 40 secondi al giorno) · **camper cursor: mai** (regressione di accessibilità, nessun equivalente touch).

`prefers-reduced-motion: reduce` azzera durate e iterazioni, e **R20** garantisce che l'azzeramento non lasci elementi invisibili.

---

## Struttura dei componenti per l'MVP

```text
src/
├── styles/
│   └── theme.css              [SUBITO]  @theme, [data-theme="dark"], reduced-motion
│
├── design/
│   └── contrast-pairs.json    [SUBITO]  manifesto delle 47 coppie (R14)
│
├── components/
│   ├── ui/                    [SUBITO]  generati con shadcn, poi POSSEDUTI. Mai dominio (R15)
│   │   ├── Button                       primary / secondary / ghost / danger
│   │   ├── Input, Field                 label, errore, descrizione
│   │   ├── Select
│   │   ├── Dialog
│   │   ├── Toast
│   │   ├── Badge
│   │   └── Table                        lista manutenzioni — tabellare, NON editoriale
│   │
│   ├── domain/                [SUBITO]  <-- QUI VIVE L'IDENTITÀ
│   │   ├── Ribbon                       asse km/tempo, orientabile h/v
│   │   ├── MetricBlock                  numero dominante + label micro
│   │   ├── CamperHeader                 hero tipografico
│   │   ├── DueBadge                     scaduto / in scadenza / ok
│   │   ├── SeedTag                      marca le voci da seed (R24)
│   │   ├── EmptyState                   contour + microcopy narrativa
│   │   └── danger/                      DangerButton, DangerDialog (R17)
│   │
│   ├── layout/                [SUBITO]  AppShell, PageContainer, Section
│   └── decor/                 [SUBITO]  ContourPattern (1 SVG, 2 varianti), NoiseOverlay
│
│   ├── motion/                [VUOTO in Phase 1 — solo CSS in theme.css]
│   ├── map/                   [NON NASCE prima della Phase 3]
│   └── travel/                [NON NASCE prima della Phase 2]
```

**Cartelle che NON devono nascere ora**, contro il consiglio del documento di direzione §48: `map/` (bloccata dalla #3), `travel/` (Phase 2), `outdoor/WeatherCard` (nessun provider meteo), `motion/ParallaxImage`, `motion/RouteDraw`, `motion/PageTransition`.

> Creare una cartella vuota per una fase futura è il modo più economico di sbagliare l'astrazione: quando arriverà il contenuto, il posto sarà già occupato da un'idea vecchia di sei mesi.

**Tre regole strutturali**, tutte verificate: `domain/` non importa da `ui/` oltre `Button` e `Field` (**R15**) · nessun `Card` generico (**R16**) · nessun colore letterale, solo semantici (**R12**, **R13**).

### Checklist di completamento pagina

Sostituisce la §55 del documento di direzione (6 voci su 15 parlavano di mappe e immagini).

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

## Il `Ribbon` e il suo prerequisito di dominio non chiuso

In Phase 1 il `Ribbon` disegna l'asse della manutenzione, che nel dominio è **doppio**: km *e* tempo (`FEATURES.md` §2).

```text
  ultimo tagliando                        ADESSO              prossimo
  ●───────────────────────────────────────◆──────────────────────○
  82.400 km                            94.120 km            97.400 km
  mar 2026                                                  set 2026
                                                       ┗━ fra 3.280 km
```

Verticale, lo stesso componente è lo storico. **Non due componenti: uno, con due orientamenti.**

> ⚠️ **Il `Ribbon` dipende da un prerequisito di dominio non ancora chiuso.** Per rendere l'asse km servono tre dati: `ultimoIntervento` (km + data), `regolaRicorrenza` e **`letturaContachilometriCorrente`**. **L'esistenza del terzo non è decisa** — la domanda è stata girata a **@archimedes** perché appartiene al domain model (passo 3 di `ROADMAP.md` §4), non al design. **Va chiusa prima di implementare il `Ribbon` (1.3 in Phase 1).**

### Comportamento di fallback, se la risposta è "solo km all'ultimo intervento"

Il componente **non si butta e non si rimanda**: degrada, in modo definito da **R25**.

1. **L'asse diventa solo temporale.** Estremi: data dell'ultimo intervento → data della prossima scadenza; il marcatore "adesso" è la data odierna, che è sempre nota. Il ribbon resta geometricamente pieno e conserva il suo valore di lettura.
2. **I km diventano testo, non geometria.** "Ultimo a 82.400 km · ogni 15.000 km" compare come dato sotto l'asse, senza posizione.
3. **Vietato stimare i km** per percorrenza media o per qualunque euristica (R25). Una stima renderizzata sull'asse è indistinguibile da una misura per chi guarda, e su una manutenzione questo significa far credere a un utente di avere margine che non ha. **Il rischio è di sicurezza del mezzo, non di UI.**
4. **L'API resta la stessa.** Il payload espone `letturaCorrente` come opzionale; il componente sceglie la modalità in base alla presenza del campo. Il giorno in cui il dato esiste, l'asse km si accende senza toccare il componente.
5. **Conseguenza sull'identità, dichiarata:** in modalità solo-tempo il `Ribbon` è più vicino a una progress bar temporale e perde parte della sua ragion d'essere (rimuovere il calcolo mentale "ogni 15.000 km, ultimo a 82.400"). **L'identità dell'MVP si indebolisce, non crolla:** restano `MetricBlock`, il trattamento editoriale e il materiale.

---

## Le tre schermate di ADR-0004

### Il principio: qui il design system si disattiva

Il rischio non è che siano brutte. È che siano **belle**: il resto del sistema è fatto per rendere le azioni invitanti, e qui l'obiettivo è l'opposto.

> **Su queste tre schermate: niente microcopy narrativa, niente Fraunces, niente CTA clay, niente animazione di successo, nessun contour. Registro neutro, letterale, ripetitivo.** (R21, R22)

Non è rinuncia all'identità: **è identità.** Un prodotto che cambia registro quando le poste in gioco cambiano comunica serietà meglio di qualunque hero.

### 1. Informativa privacy

- Contenuto statico versionato; tipografia da lettura lunga (misura 60-70 caratteri, body 16px, interlinea 1.6); indice ancorato.
- In fondo, l'unica azione: un pulsante neutro che registra `PrivacyPolicyVersionSeen`. **Non è un consenso** (ADR-0004 §8) → il testo è **`Ho letto`**, mai `Accetto`. La distinzione va nel design system come nota esplicita, perché il primo istinto di chiunque è scrivere "Accetto".
- Aggiornamento di versione: avviso **non bloccante** in dashboard. **Non un modal**: bloccare l'app per un aggiornamento dell'informativa la trasforma di fatto in un consenso forzato — l'opposto di ciò che ADR-0004 dice che sia.

### 2. Scarica i miei dati

Percorso: `Impostazioni → Privacy e dati → Scarica i miei dati`.

1. Spiegazione letterale di cosa contiene lo ZIP e **soprattutto di cosa non contiene**. `VISION.md` dichiara il limite ("machine-readable, non curato"): **quel limite va scritto nella UI, non solo nel documento.** Testo: *"Riceverai un archivio ZIP con i tuoi dati in formato JSON. È un formato tecnico: è completo, ma non è pensato per essere letto come un album."*
2. **Re-autenticazione con password in un campo della stessa pagina**, non in un modal: un modal che chiede la password è indistinguibile da un tentativo di phishing agli occhi di un utente attento.
3. Stato asincrono esplicito: `In preparazione → Pronto → download`, con `aria-live`. Nessuno spinner indefinito.
4. Errore in registro neutro: *"Non è stato possibile preparare l'archivio. Riprova."* — **non** "La strada si è interrotta".

### 3. Elimina account — e il gap che ne blocca il rilascio

"Difficile da attivare per sbaglio" è un requisito verificabile e si ottiene con l'**attrito**, non con l'avvertimento.

```text
PASSO 1 — /impostazioni/privacy/elimina-account

  Elimina il tuo account

  Cosa succede, nell'ordine:

  1. Il tuo account viene reso inaccessibile subito.
  2. I tuoi dati restano recuperabili fino al 24 ottobre 2026.
     [ ??? — COME si annulla: BLOCCATO, vedi sotto ]
  3. Il 24 ottobre 2026 i dati vengono cancellati fisicamente.
     Dopo quella data non sono recuperabili in alcun modo:
     né da noi, né da un backup, né su richiesta.

  Riguarda: 1 camper, 14 manutenzioni, 2 documenti.

  Vuoi prima scaricare i tuoi dati?   [ Scarica i miei dati ]

  [ Annulla ]        [ Continua alla cancellazione ]   <- testo, non bottone pieno


PASSO 2 — /impostazioni/privacy/elimina-account/conferma

  Conferma definitiva
  Stai cancellando l'account lorenzo@esempio.it.
  Data di cancellazione definitiva: 24 ottobre 2026.

  Scrivi ELIMINA per confermare   [________]
  La tua password                 [________]

  [ Annulla ]   [ ⚠ Elimina definitivamente il mio account ]
                   danger-surface + icona + label esplicita (R17),
                   abilitato solo se entrambi i campi sono validi
```

**Ogni scelta ha un motivo:**

- **Pagina dedicata, non modal.** Un modal si chiude con `Esc` o con un click fuori — e lo stesso gesto che lo chiude per sbaglio è vicino a quello che lo conferma. Una pagina ha un URL, un back, e non ha "click fuori".
- **Doppia conferma con contenuto diverso nei due passi.** Due dialog identici addestrano a cliccare due volte. Il passo 2 richiede **un input che non si produce per inerzia**.
- **Date assolute, dal server (R23).** "Fra 30 giorni" richiede un calcolo. E ADR-0004 persiste `DeletionScheduledForUtc` proprio perché quella data è **una promessa**: la UI mostra la data che il database ha scritto.
- **Il conteggio degli oggetti** rende concreta la perdita: costa una query e vale più di tre paragrafi.
- **`ELIMINA` è la parola del dominio**, non `DELETE`; confronto trim + case-insensitive (fallire per uno spazio è crudeltà senza beneficio di sicurezza).
- **"Annulla" neutro ma pienamente leggibile (≥4.5:1)**: un "Annulla" a basso contrasto è un dark pattern.
- **Dopo la conferma: logout immediato** e una pagina finale che ripete data e modalità di revoca. **Quella pagina è l'unico posto in cui l'utente può ancora leggere come annullare** — se fosse un toast, l'informazione sarebbe persa.

> ### 🔴 Gap aperto che BLOCCA questa schermata
>
> ADR-0004 prevede `DELETE /api/v1/me/deletion` per revocare entro la grazia. **Ma l'account è immediatamente inaccessibile: da quale interfaccia l'utente revoca?** Non può fare login.
>
> Opzioni sul tavolo: (a) un login che, riconoscendo lo stato "in cancellazione", porta a una **sola** pagina di revoca; (b) un **link firmato** con scadenza nell'email di conferma; (c) solo contatto umano via email, nessuna UI.
>
> **Questo ADR non lo risolve: non è una decisione di design.** È girato a **@sentinel** e **@hermes**.
>
> **Conseguenza operativa, vincolante:** il passo 2 della lista della schermata **non è scrivibile** finché la decisione non è chiusa — e **non è ammesso un testo segnaposto in produzione**. Una schermata che promette 30 giorni di grazia senza dire come esercitarla trasforma una grazia reale in una **grazia nominale**, che è peggio del silenzio. La schermata si **progetta** in Phase 1 insieme alle altre due; il suo **rilascio** è bloccato dalla chiusura del gap. È un criterio di uscita della Phase 1, non un dettaglio di copy.

---

## Sequenza, allineata a `ROADMAP.md`

Sostituisce le 5 fasi del documento di direzione (§53), scollegate dal progetto.

### Phase 0 — Foundation (voce "base componenti frontend" di `ROADMAP.md` §2)

| # | Attività | Stima ⚠️ |
|---|---|---|
| 0.1 | Vite + React + TS + Tailwind v4 (`@tailwindcss/vite`) + React Router + TanStack Query | 0.5 g |
| 0.2 | `theme.css`: letterali + **semantici a due livelli**, blocco `[data-theme="dark"]`, reduced-motion, focus a doppio anello | **1 g** (era 0.5 senza dark) |
| 0.3 | Font self-hosted, sottoinsiemizzati, preload, `size-adjust`, **misura reale del subset** (R18) | 0.5 g |
| 0.4 | **Cinque correttivi anti-look (R11)** in `theme.css` e `components.json`, **prima del primo `shadcn add`** | 0.5 g |
| 0.5 | `ui/`: Button, Input, Field, Dialog, Toast — **solo questi** | 1 g |
| 0.6 | `AppShell` + `PageContainer` + **toggle di tema con persistenza e rispetto di `prefers-color-scheme`** | 0.75 g |
| 0.7 | Schermata di login (non contiene dati: è il banco di prova dei token, in **due** temi) | 0.5 g |
| 0.8 | **`contrast-pairs.json` + i tre verificatori di CI** (contrasto, copertura, axe ×2 temi) + lint R12/R13 | **1 g** (era 0.5 senza dark) |

**Criterio di uscita:** login funzionante che attraversa lo stack, **in entrambi i temi**, con CI verde su tutte le 47 coppie.

### Phase 1 — Camper (MVP 1.0)

| # | Attività |
|---|---|
| 1.1 | `MetricBlock` + `CamperHeader` → Camper Dashboard. **Primo momento in cui l'identità è visibile** |
| 1.2 | Form Create Camper (`Field`, `Select`, validazione tipizzata) + **seed manutenzioni** con `SeedTag` (R24) |
| 1.3 | `Ribbon` orizzontale + `DueBadge` → reminder. **Preceduto dalla chiusura di D4 con @archimedes** |
| 1.4 | `Table` + `Ribbon` verticale → lista e storico manutenzioni (tabellare, non editoriale) |
| 1.5 | Form Add Maintenance (ricorrenza km/tempo: il controllo più difficile dell'MVP) |
| 1.6 | `EmptyState` + `ContourPattern` — **prima dei dati reali, non dopo** |
| 1.7 | **Le tre schermate ADR-0004** + `DangerDialog`, **progettate insieme a 1.1** (D10). Rilascio della terza **bloccato** dal gap di revoca |
| 1.8 | Responsive: bottom nav mobile **senza `+` centrale** (con 2 sezioni sarebbe un guscio vuoto; entra in Phase 2) |
| 1.9 | Passata di accessibilità **× 2 temi**: tastiera, focus, `aria-live`, `lang="it"` |

### Phase 2 — Trips

Motion con `LazyMotion` **se** servono exit/layout animation · `TripCard` · `Ribbon` con tappe reali · **prima fotografia** (dipende dalla #8: i contour arretrano, il layout no) · rivalutare `PageTransition` e `PassportStamp`.

### Phase 3 — Maps & Places

Nasce `map/`. Stile mappa proprietario. **Il `Ribbon` prende la geometria del percorso: è il momento in cui si verifica se la scommessa ha retto.**

---

## Costo di inversione

| Scelta | Costo | Motivazione |
|---|---|---|
| **Token semantici a due livelli** | 🟢 **BASSO** | È la scelta che *riduce* il costo di inversione di tutte le altre. Senza, la dark mode sarebbe stata un refactor invece di un blocco di override |
| **Valori concreti della palette** | 🟢 **BASSO**, **condizionato a R12** | Se nessun componente usa colori letterali, cambiare la palette è un edit in `theme.css`. Senza il lint, è un find-and-replace su tutto il codice: **la differenza tra 🟢 e 🔴 è una regola di lint, non una buona intenzione** |
| **shadcn/ui come generatore** | 🟢 **BASSO** | Codice copiato nel repo. Si sostituisce un componente alla volta, senza big bang. È precisamente il motivo della scelta |
| **Tailwind v4 come styling engine** | 🟡 **MEDIO** | Uscirne significa riscrivere le classi di ogni componente. Ma i **token restano**: sono CSS custom properties standard. La parte identitaria non è in ostaggio |
| **Base UI come layer di comportamento** | 🟡 **MEDIO** | Tocca l'API di ~6 componenti di `ui/`. Contenuto perché `domain/` non la usa (R15) |
| **Dark mode in Phase 1** | 🟡 **MEDIO, e asimmetrico** | Aggiungerla dopo sarebbe stato 🟢 grazie ai token a due livelli. **Toglierla adesso**, invece, è 🟡: significa rimuovere un tema che gli utenti hanno già visto, e la rimozione di una feature visibile costa più della sua assenza. **La scelta dell'utente ha alzato il costo di inversione, non lo ha abbassato** |
| **Token `danger-*` separato da `clay`** | 🟢 **BASSO** ora, 🔴 **ALTO** dopo | Introdurlo ora costa 4 righe. Introdurlo dopo che `clay` è stato usato per 30 alert significa rileggere ogni occorrenza per decidere quali erano "pericolo". **Unica voce con costo non lineare: va fatta adesso** |
| **CTA `clay-500` + `ink`** | 🟢 **BASSO** | Due token semantici. E regge in entrambi i temi senza modifiche (verificato) |
| **`Ribbon` come primitiva astratta** | 🟡 **MEDIO** | Se l'asse km non è calcolabile o la route geografica avrà bisogni strutturalmente diversi, si sarà costruito un componente specifico invece di una primitiva. **È la scommessa esplicita di questo ADR.** Il resto dell'MVP non dipende da lui |
| **Fraunces** | 🟢 **BASSO** | Due `@font-face` e due classi. Se il budget non regge, esce (R18) |
| **Il dato come superficie primaria** | 🟢 **BASSO** | È una regola di composizione, non una struttura. Quando arriva la fotografia, il blocco dominante cambia contenuto; il layout regge |
| **Contour/texture come sostituto della fotografia** | 🟢 **BASSO** | Si sostituisce il contenuto di un blocco. È il motivo per cui vanno usati *dove andrà la foto* |
| **Registro doppio** | 🟡 **MEDIO** | Cambiare idea dopo significa riscrivere ogni stringa. **Mitigato dall'i18n**: se le stringhe sono in un catalogo, è un file. È il vero argomento per fare l'i18n subito |
| **Seed di manutenzioni** | 🟡 **MEDIO** | Toglierlo dopo lascia dati creati da una feature che non esiste più, su account reali. Serve una migrazione o una marcatura persistente, non solo un flag di UI |
| **Nessuna libreria di motion in Phase 1** | 🟢 **BASSO** | Aggiungerla in Phase 2 è un `npm i`; le transizioni CSS restano valide |
| **Cartelle `map/` e `travel/` non create** | 🟢 **BASSO (e negativo)** | Non crearle **riduce** il costo futuro: non si eredita un'astrazione sbagliata |
| **App interamente client-side (no SSR)** | 🔴 **ALTO** | Cambierebbe framework, build, deploy e — con ADR-0003 — il modello di sessione. Già deciso in `ARCHITECTURE.md` §1, qui solo registrato |

---

## Tradeoff negativi accettati

1. **L'MVP sarà visivamente più sobrio della visione, e al giorno zero anche più povero di quanto sembri.** Senza foto e senza mappe, le prime schermate di un account nuovo sono un titolo, un form e una lista vuota. **L'identità è più debole esattamente nel momento in cui viene giudicata.** Il seed (D9) e gli empty state progettati sono una mitigazione **parziale**, e dichiararlo è più utile che promettere il contrario.
2. **Il `Ribbon` è una scommessa, e ha un prerequisito che non è chiuso.** Se D4 dà la risposta sfavorevole, il componente degrada e perde parte della sua ragion d'essere (R25).
3. **Il rischio "look generato" non è eliminato: è gestito.** R11 lo neutralizza **solo se applicato prima del primo componente**. La scelta dipende da un'azione che si può dimenticare — è la sua debolezza principale, ed è il motivo per cui R11 ha un verificatore invece di essere una raccomandazione.
4. **Fraunces costa peso per un beneficio non misurabile.** Il subset non è ancora stato misurato. Il beneficio è estetico e non ha metrica: **è un atto di fede, contenuto da un budget.**
5. **L'approccio editoriale vale per la dashboard, non per le liste.** Quindi lista manutenzioni e form **restano UI convenzionale**: la percentuale di superficie realmente identitaria nell'MVP è bassa — forse il 30%. È la scelta corretta (un form deve essere veloce, non memorabile) ma va detta.
6. **Le tre schermate ADR-0004 costano ~2-3 giorni di UI** per funzionalità che, con un solo utente, nessuno userà. Il prezzo era già accettato dall'ADR-0004; qui si registra che ha una componente frontend non banale, finora non stimata da nessun documento.
7. **Nessun designer, nessuna review esterna.** Le scelte estetiche sono argomentate, non validate. Il primo feedback reale arriverà dal primo utente e potrebbe smentire la tesi centrale: *"il dato come superficie primaria"* potrebbe risultare semplicemente **freddo**.
8. **Niente Motion in Phase 1 significa niente exit animation sui dialog.** È percepibile. Accettato per non aggiungere una dipendenza prima di avere il problema.
9. **Il seed crea dati che l'utente non ha inserito.** Per quanto marcati (R24), sono righe nel suo account: un utente potrebbe credere di averle registrate lui, o scoprirle in un export di ADR-0004 senza riconoscerle. La mitigazione è la marcatura e la cancellazione in blocco; **non è una mitigazione completa**.
10. **La dark mode in Phase 1 è il tradeoff più costoso di questo ADR, ed è stato scelto contro la raccomandazione dell'owner.** In concreto:
    - **la matrice di verifica passa da 22 a 47 coppie** e ogni componente va rivisto in due varianti;
    - **introduce una regressione che il tema chiaro non aveva**: `danger` e `clay` in dark sono indistinguibili in scala di grigi (1.15:1), quindi la sicurezza dell'azione distruttiva si regge interamente su R17 e non più anche sul colore;
    - **allunga la Phase 0 di ~1 giorno** e rallenta ogni schermata della Phase 1 di una frazione non misurata;
    - **alza il costo di inversione** della dark mode stessa da 🟢 a 🟡 (tradeoff: tornare indietro significa togliere qualcosa che l'utente ha già visto);
    - **degrada in silenzio** se i tre verificatori di CI non vengono scritti in 0.8: un tema scuro non verificato è una dichiarazione di accessibilità senza prova.
    **Il beneficio è reale e va detto anche quello:** in dark la palette Forest & Sand funziona meglio che in chiaro, i tre colori più problematici (`clay`, `sun`, `moss`) diventano tutti utilizzabili, e l'uso serale in camper è uno scenario di dominio plausibile. **La scelta è difendibile; il suo costo è che nessuna delle due matrici può più essere verificata a occhio.**

---

## Trigger di revisione

| Trigger | Azione |
|---|---|
| **Il contrasto viene violato in un PR** | Il check di 0.8 **deve fallire la build**, non essere un promemoria. Se qualcuno lo disattiva, la dark mode va rimessa in discussione lo stesso giorno |
| **D4 chiusa da @archimedes** | Se la lettura del contachilometri non esiste → il `Ribbon` va in modalità solo-tempo (R25) e va rivalutato se resti l'elemento identitario n.1 o scenda al n.3 |
| **Il gap di revoca (D7) resta aperto a fine Phase 1** | La Phase 1 **non esce**. Non si rilascia una grazia di cui non si sa dire come si esercita |
| **La decisione #8 abilita l'upload di foto** | La fotografia entra come superficie e i contour arretrano. Verificare che i blocchi dominanti accettino un'immagine **senza cambiare layout** |
| **La decisione #3 sblocca le mappe** | Il `Ribbon` deve accettare geometria reale: **verifica della scommessa** |
| **La decisione #10 porta la PWA in scope** | Servono stati "non sincronizzato" per entità, UI di conflitto, indicatore di connessione — tre componenti non previsti. Più, da ADR-0004: purge locale al logout e menzione nell'informativa |
| **Il bundle CSS+font supera 200 KB**, o **R18 fallisce** | Esce Fraunces (non si alza il budget) |
| **LCP > 2.5s su 4G simulato** | Rivalutare strategia font e code splitting per rotta. Se restasse irrisolvibile, è **l'unico scenario che riapre seriamente la questione SSR** |
| **Entra in scope una landing pubblica indicizzabile** | Risposta attesa: **pagina statica separata**, non migrazione dell'app su Next |
| **Il primo utente reale descrive la UI come "fredda" o "un gestionale"** | Riaprire la tesi del dato come superficie primaria. **È il test vero di questo ADR** |
| **Arriva un secondo sviluppatore o un designer** | Riaprire la #7: l'opzione 2 (Base UI puro) diventa sostenibile, il vincolo "sviluppatore singolo" cade |
| **shadcn/ui cambia di nuovo layer di primitive, o Base UI si ferma** | Nessuna azione immediata: il codice è nel repo. Rivalutare alla prossima aggiunta di componenti |
| **Nasce un backoffice** | Un UI kit completo su quella superficie torna difendibile — come prodotto separato, non come base dell'app |

---

## Vincoli derivati per le decisioni aperte

| Decisione | Vincolo posto da questo ADR |
|---|---|
| **#8 storage file e foto** (@vulcan) | **(a)** La fotografia è la superficie primaria della visione: finché la #8 è aperta l'identità resta incompleta **per scelta** → la priorità della #8 è più alta di quella percepita oggi. **(b)** Derivati responsive (≥3 larghezze) e formato moderno **lato server/CDN**: senza `next/image` il frontend non ridimensiona, e un JPEG da 4 MB diventa il problema di performance dell'app. **(c)** **Placeholder/blur hash nel payload**, altrimenti le card editoriali fanno layout shift. **(d)** Vincolo privacy: la UI di "Scarica i miei dati" deve dire **se le foto sono nello ZIP o dietro URL a scadenza** (ADR-0004 F3) |
| **#3 provider geo** (@archimedes) | **(a)** Il map style deve essere **personalizzabile** (style spec tipo MapLibre/Mapbox). Un provider che serve solo tile raster fisse rende la mappa un widget di terzi dentro una UI Roamly: **è un criterio di selezione, non una preferenza estetica**. **(b)** Serve una mappa **in due temi**, ora che la dark mode è in Phase 1: uno stile scuro coerente con `night-950` è un requisito, non un extra. **(c)** La geometria del percorso deve essere una polilinea consumabile dal `Ribbon`. **(d)** Inviare posizioni a un provider attiva i consensi di ADR-0004 → **serve una UI di consenso che oggi non esiste in nessun mockup** |
| **#10 offline / PWA** (@archimedes) | Se entra: stato "non sincronizzato" per entità, UI di conflitto, indicatore di connessione (tre componenti non previsti). Purge del client al logout e alla cancellazione + menzione nell'informativa (ADR-0004). **Se esce, va scritto in `UX.md` §5**: un design system che non sa se dovrà rappresentare stati offline progetta male gli stati di caricamento |
| **#9 deploy** (@vulcan) | **(a)** Il frontend è un bundle statico (dist di Vite): **nessun runtime Node**, e questo **semplifica la #9** — beneficio collaterale dell'aver escluso Next.js. **(b)** Cache header: `index.html` no-cache, asset con hash `immutable`; i font self-hosted sullo stesso dominio, `immutable`. **(c)** **Nessun asset da CDN di terzi (R19)**: è un vincolo di privacy, da verificare in review, non da assumere |
| **#6 DB di test** (@argus) | Nessun vincolo nuovo. Ma i verificatori di R12/R14/R15 sono test di convenzione **senza database**: vanno in CI come quelli di R1/R2 e non dipendono dalla #6 |
| **Gap — i18n** | **Promosso da "predisposizione futura" a prerequisito di una regola attiva.** Il registro doppio (R21) è verificabile solo se le stringhe sono in un catalogo: con stringhe inline nel JSX, "registro neutro su privacy e denaro" non è né verificabile né revisionabile. Requisito minimo: catalogo `it` unico, chiavi namespaced (`neutral.*` / `narrative.*`), nessun letterale nel JSX, date/numeri/`Money` via `Intl`, **mai concatenando stringhe** |
| **Gap — seed data** (@archimedes) | Le manutenzioni tipiche sono contenuto di prodotto (`FEATURES.md` §5): servono una lista curata, un'origine persistita (per R24) e una cancellazione in blocco. **Non è solo UI**: tocca il modello |
| **Gap — lettura del contachilometri (D4)** (@archimedes) | 🔴 **Prerequisito di dominio del `Ribbon`.** Va chiuso prima di 1.3. Fallback definito in R25 |
| **Gap — revoca della cancellazione (D7)** (@sentinel, @hermes) | 🔴 **Blocca il rilascio della schermata "Elimina account".** Nessun testo segnaposto ammesso in produzione |
| **@hermes — contratti API** | `Ribbon`: `ultimoIntervento` (km + data), `regolaRicorrenza`, `letturaCorrente` **opzionale**. Cancellazione: `DeletionScheduledForUtc` **dal server** (R23) e conteggio delle entità coinvolte. `Money` come importo + valuta nel payload, mai una stringa formattata |
| **@argus — testing** | I tre verificatori di §Dark mode; il lint R12/R13; i test di convenzione R15/R16; il test reduced-motion R20; il test tastiera sul flusso di cancellazione; axe-core **eseguito due volte, una per tema** |

---

## Cosa NON si fa nell'MVP

| Non si fa | Perché | Trigger che lo rende obbligatorio |
|---|---|---|
| Fotografia | Nessun upload, nessuno storage, nessun budget | #8 chiusa → Phase 2 |
| Mappe | #3 aperta | Phase 3 |
| Libreria di motion | Tutto ciò che serve è CSS a costo zero; `LazyMotion` è complessità senza un problema | Servono exit/layout animation (Phase 2) |
| Bottom nav con `+` centrale | Con 2 sezioni è un guscio vuoto | Terza sezione (Trips) |
| Page transition, parallax, scroll storytelling, glow, animazioni ambient | Latenza percepita e consumo, su un'app aperta ogni giorno per 40 secondi | Misurazione che ne dimostri il beneficio — cioè mai, per come sono formulate |
| Camper cursor | Regressione di accessibilità documentata | **Mai** |
| "Dynamic atmosphere" (mare/montagna/deserto) | Richiede una classificazione della destinazione che **non esiste come dato** prima della Phase 3 | Phase 3. Costo basso grazie ai token semantici (R13) |
| Passport / journey stamps | Dipende da "luogo visitato", concetto di Phase 3. **Nota**: uno stamp è un artefatto generato, quindi è anche un modo di avere immagini proprietarie senza fotografia — vale la pena ricordarsene | Phase 3 |
| Digital campfire / community | La community è Phase 6 ed **esplicitamente condizionale** | Phase 6, se mai |
| Componente `Card` generico | R16 | Mai |
| Secondo icon set | Un solo set (Lucide), `stroke-width` 1.5-2px | Mai |

---

## Checklist di propagazione — file per file

> Ordine consigliato: prima `UX.md` (è il documento che chiude la #7), poi `OPEN-DECISIONS.md`, poi il resto.

| # | File | Cosa cambiare |
|---:|---|---|
| 1 | `docs/product/UX.md` §1 | **Riscrivere i principi.** "map-centric" e "fotografico" **non sono principi dell'MVP**: marcarli come obiettivi di Phase 2-3. Aggiungere: un soggetto dominante per schermata; **editoriale sulla dashboard, tabellare sulle liste**. Confermare "dark/light ben progettati" — ora è vero **in Phase 1** e ha una matrice misurata dietro |
| 2 | `docs/product/UX.md` §2 | **Sostituire il mockup.** Oggi mostra "Your next adventure", trip e costi — **feature di Phase 2**, in inglese. Sostituire con la Camper Dashboard reale (numero dominante + `Ribbon` + badge scadenze), testi in italiano |
| 3 | `docs/product/UX.md` §3 | **Chiudere la #7**: link a questo ADR, token semantici (le due colonne chiaro/scuro), scala tipografica, **regola Fraunces (R22)**, i cinque correttivi (R11), radius, ombre, focus a doppio anello |
| 4 | `docs/product/UX.md` §3 (schermate ADR-0004) | Espandere: registro neutro, pagina non modal, doppia conferma con input, date assolute **dal server** (R23), `Ho letto` non `Accetto`, **e il gap di revoca marcato 🔴 come bloccante del rilascio** |
| 5 | `docs/product/UX.md` §4 | i18n: da "predisposizione" a **prerequisito** di R21. Aggiungere: namespace `neutral.*`/`narrative.*`, date dal server, `Intl` |
| 6 | `docs/product/UX.md` §5 | Aggiungere gli impatti design della #10 (stati sync, conflitti, indicatore di connessione) |
| 7 | `docs/product/UX.md` — **nuova §6** | **Motion e accessibilità**: durate, easing, reduced-motion con la regola "stato base = stato finale" (R20), focus a doppio anello su 6 superfici, target 44px, checklist di completamento pagina |
| 8 | `docs/product/UX.md` — **nuova §7** | **Dark mode**: le cinque leve del calore, la rampa `night-*`, l'elevazione senza ombre, contour/texture invertiti, e **la regressione `danger`/`clay` con la mitigazione R17** |
| 9 | `docs/product/ROADMAP.md` §2 Phase 0 | Sostituire "base componenti frontend (decisione #7)" con le **8 voci** di §Sequenza, **con le stime riviste per la dark mode** (0.2 e 0.8 passano a 1 g, 0.6 a 0.75 g) |
| 10 | `docs/product/ROADMAP.md` §2 Phase 1 | Aggiungere: costo UI delle tre schermate ADR-0004 (~2-3 g, oggi non stimato); **la chiusura di D4 come prerequisito di 1.3**; **il gap di revoca come criterio di uscita di fase** |
| 11 | `docs/product/FEATURES.md` §5 | Le manutenzioni tipiche da seed diventano **feature dell'MVP** (D9): lista curata, origine persistita, marcatura visibile (R24), cancellazione in blocco |
| 12 | `docs/architecture/ARCHITECTURE.md` §1 Frontend | Aggiungere: **Tailwind v4, shadcn/ui su Base UI, Lucide, i18n**. Note: **nessuna libreria di motion in Phase 1**; **nessuna CDN di terzi (R19)**; il frontend resta un bundle statico senza runtime Node |
| 13 | `docs/architecture/API-CONVENTIONS.md` | **(a)** Prerequisito dati del `Ribbon` (`ultimoIntervento`, `regolaRicorrenza`, `letturaCorrente` **opzionale**); **(b)** le date di cancellazione vengono **dal server**; **(c)** conteggio delle entità per la schermata di cancellazione; **(d)** `Money` come importo + valuta, mai stringa formattata |
| 14 | `docs/architecture/TESTING.md` §2 | Nuovi test di convenzione bloccanti: **R12** (nessun colore letterale), **R13** (nessun letterale fuori da `theme.css`), **R15** (import `domain/ ↛ ui/`), **R16** (nessun `Card`), **R22** (Fraunces solo in due classi) |
| 15 | `docs/architecture/TESTING.md` §4 | Nuovo: **contrast check sulle 47 coppie (R14)** + check di copertura del manifesto; **axe-core eseguito due volte, una per tema**; test reduced-motion (R20); test tastiera sul flusso di cancellazione; test del `Ribbon` senza `letturaCorrente` (R25) |
| 16 | `docs/architecture/SECURITY.md` §2.2 | Nota di rimando: le regole del design system sono **R11-R25** in questo ADR. In particolare **R17** (il colore non è mai l'unico segnale di distruttività) e **R23** (date dal server) hanno rilevanza di sicurezza |
| 17 | `docs/architecture/SECURITY.md` §3 | Aggiungere il **gap della revoca** (da quale interfaccia si annulla la cancellazione) come punto aperto assegnato a @sentinel + @hermes, **bloccante per il rilascio della schermata** |
| 18 | `docs/architecture/SECURITY.md` §5 | Aggiungere **R19** (nessun asset da terzi) come requisito verificato in CI, non come buona pratica |
| 19 | `docs/architecture/DEVOPS.md` | Cache header (`index.html` no-cache, asset `immutable`), font self-hosted `immutable`, **nessuna CDN di terzi**, e i tre verificatori di design nella pipeline |
| 20 | `docs/architecture/CONTEXT.md` | Nota aperta: **lettura del contachilometri** — esiste come attributo del camper? Chi la aggiorna, con quale frequenza? È il prerequisito del `Ribbon` (D4, @archimedes). Più: origine dei dati da seed |
| 21 | `docs/OPEN-DECISIONS.md` | **#7 → ✅ chiusa** con esito sintetico e link a questo ADR. Aggiungere i **vincoli derivati** su #8, #3, #9, #10. Aggiungere **due gap nuovi**: *lettura contachilometri* (@archimedes) e *interfaccia di revoca della cancellazione* (@sentinel + @hermes, 🔴 bloccante Phase 1). Promuovere il gap **i18n** a prerequisito |
| 22 | `docs/OPEN-DECISIONS.md` — rischi | **I9 "Refactor totale della UI"** → mitigato **condizionatamente**: la mitigazione vale solo se R11, R12 e R13 hanno verificatori attivi. Aggiungere un rischio nuovo: **"la matrice di contrasto a due temi degrada in silenzio"**, mitigato da R14 |
| 23 | `docs/archive/Design_Direction_Input_2026-09-24.md` | **Non modificare** — è un archivio. Gli scarti sono registrati nella tabella ❌ di questo ADR: è lì che va guardato prima di reimplementarne il §25 |

---

## Riferimenti

- Chiude la decisione **#7** di `docs/OPEN-DECISIONS.md`.
- Soddisfa il vincolo posto su @pixel da [`ADR-0004`](0004-privacy-and-erasure.md) (§Vincoli derivati): le schermate di export e cancellazione con conferma inequivocabile su grazia e irreversibilità.
- Vincolato da [`ADR-0003`](0003-auth-and-ownership.md) (same-site → nessun BFF, nessun runtime Node) e da `architecture/ARCHITECTURE.md` §1 (no SSR).
- Documenti impattati: `product/UX.md` (§1-§5, nuove §6 e §7), `product/ROADMAP.md` (§2), `product/FEATURES.md` (§5), `architecture/ARCHITECTURE.md` (§1), `architecture/API-CONVENTIONS.md`, `architecture/TESTING.md` (§2, §4), `architecture/SECURITY.md` (§2.2, §3, §5), `architecture/DEVOPS.md`, `architecture/CONTEXT.md`, `OPEN-DECISIONS.md`.
- Fonte scartata ma citata: `docs/archive/Design_Direction_Input_2026-09-24.md` — **da leggere insieme alla tabella ❌ di questo ADR, mai da solo**.
