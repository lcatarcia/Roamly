# Camper × Natura × Viaggi — Frontend Design System
## Direzione creativa, UI, motion e architettura visuale per una webapp distintiva

> **Obiettivo:** trasformare una webapp funzionale per camperisti in un'esperienza digitale che faccia percepire immediatamente **strada, libertà, natura, esplorazione e vita outdoor**, evitando il look generico da SaaS, travel booking o marketplace.

---

# 1. Executive direction

La direzione che consiglio è:

**“Editorial Outdoor / Digital Camp Journal”**

Non costruirei un'interfaccia che sembri un normale gestionale con qualche foto di camper. Il prodotto dovrebbe avere una forte identità editoriale: grandi immagini ambientali, tipografia espressiva, mappe come elemento narrativo, micro-interazioni morbide e una palette ispirata a materiali naturali.

### Principi

1. **La natura è il protagonista, la UI è la cornice.**
2. **Il viaggio deve essere percepibile anche senza leggere.**
3. **La mappa non è solo una utility: è un elemento visuale.**
4. **Il camper va mostrato in contesto, non come semplice icona.**
5. **Le animazioni devono suggerire movimento, vento, strada e scoperta.**
6. **La UI deve restare funzionale anche quando diventa molto scenografica.**
7. **Niente “dashboard SaaS” piena di card indistinguibili.**
8. **Usare texture, forme organiche e fotografie con moderazione per creare riconoscibilità.**

La parola chiave è **premium outdoor**, non “rustico”.

---

# 2. Concept visivo

## Concept A — “Camp Journal”

Immagina un diario di viaggio digitale contemporaneo.

### Hero

Schermo quasi full viewport:

- fotografia/video di un camper in paesaggio;
- overlay scuro/verde molto leggero;
- titolo enorme;
- piccolo testo descrittivo;
- CTA principale;
- dati del viaggio sovrapposti alla fotografia;
- una linea/mappa che attraversa il fondo del viewport.

Esempio concettuale:

```text
┌──────────────────────────────────────────────────────────────┐
│ LOGO                         ESPLORA  VIAGGI  MAPPA      ☰  │
│                                                              │
│                                                              │
│             LA STRADA INIZIA                                │
│             DOVE FINISCE                                    │
│             L'ASFALTO.                                      │
│                                                              │
│             ┌───────────────────────┐                        │
│             │  Inizia a esplorare → │                        │
│             └───────────────────────┘                        │
│                                                              │
│      ────────╮                                               │
│              ╰──────●──────────────●──────                  │
│                  124 km          3 tappe                     │
│                                                              │
│                                      [ scroll ↓ ]             │
└──────────────────────────────────────────────────────────────┘
```

Il punto importante è che **il prodotto racconta il viaggio prima di spiegare le feature**.

---

# 3. Palette cromatica

## Palette primaria — “Forest & Sand”

Questa sarebbe la mia base.

| Token | Hex | Uso |
|---|---|---|
| `forest-950` | `#10231C` | background dark, hero |
| `forest-900` | `#173329` | superfici scure |
| `forest-700` | `#28513F` | elementi primari |
| `moss-500` | `#6F8F62` | accent naturale |
| `sage-300` | `#AFC19F` | secondary accent |
| `sand-100` | `#F3EBDD` | background principale |
| `sand-200` | `#E7DDCB` | card / borders |
| `clay-500` | `#C97952` | CTA secondaria / alert |
| `sun-400` | `#E6B85C` | highlight |
| `ink` | `#17201C` | testo |
| `white` | `#FCFBF7` | testo su dark |

### Regola 60 / 25 / 10 / 5

- **60%** sand / off-white
- **25%** forest
- **10%** sage / moss
- **5%** clay / sun

Il clay va usato come spezia, non come colore dominante.

---

# 4. Variante più radicale

Se vuoi un'estetica più “mai vista”:

## “Night Camp”

Base quasi nera:

- `#0C1411`
- `#14231C`
- `#203A2D`
- `#708C68`
- `#D9C6A5`
- `#E8A35C`
- `#F5F0E6`

Con:

- fotografie notturne;
- mappe luminose;
- linee topografiche;
- piccoli marker arancio;
- glow molto controllato;
- typography gigantesca.

È una direzione più cinematografica e tecnologica.

---

# 5. Gradienti

Evita i gradienti “SaaS” blu/viola.

Usa gradienti atmosferici.

```css
--gradient-forest:
  linear-gradient(
    135deg,
    #10231C 0%,
    #28513F 60%,
    #6F8F62 100%
  );

--gradient-sunset:
  linear-gradient(
    135deg,
    #173329 0%,
    #28513F 45%,
    #C97952 100%
  );

--gradient-paper:
  linear-gradient(
    135deg,
    #F3EBDD 0%,
    #E7DDCB 100%
  );
```

Usali soprattutto per:

- hero;
- CTA;
- overlay fotografici;
- mappe;
- stati selezionati.

Non mettere gradienti su ogni card.

---

# 6. Tipografia

## Pairing consigliato

### Display

**Fraunces**, **DM Serif Display** o **Cormorant Garamond**

Per:

- hero;
- titoli editoriali;
- numeri importanti;
- frasi manifesto.

### UI

**Inter**, **Manrope**, **DM Sans** o **Plus Jakarta Sans**

Per:

- navigation;
- form;
- bottoni;
- dati;
- card;
- mappe.

### Alternativa più tecnica

Display:

**Space Grotesk**

Body:

**Inter**

Questa rende il prodotto più contemporaneo e meno “travel magazine”.

---

# 7. Tipografia: scala

Non avere paura di usare titoli grandi.

```text
Hero title        72–120px
Page title        48–72px
Section title     32–48px
Card title        20–28px
Body              16–18px
Small             12–14px
Micro label       10–12px
```

Su mobile:

```text
Hero              44–64px
Page title        36–48px
Section           28–36px
Card              18–22px
Body              15–17px
```

### Importante

Il display type deve avere **molto respiro**.

Meglio:

> LA STRADA  
> INIZIA QUI.

che:

> Scopri i migliori percorsi per il tuo prossimo viaggio in camper.

Il secondo testo può stare sotto.

---

# 8. Navbar

Evita la classica:

`Logo | Home | Features | Pricing | Login`

Per un prodotto camper/travel preferirei:

```text
[ LOGO ]

Esplora
Viaggi
Mappa
Diario

                         [ Il mio camper ]
```

Oppure una navbar floating:

```text
           ┌─────────────────────────────────────────┐
           │ logo   Esplora   Mappa   Viaggi   ● Me  │
           └─────────────────────────────────────────┘
```

### Comportamento

All'inizio:

- trasparente;
- sopra l'hero.

Durante scroll:

- diventa `forest-950`;
- blur;
- bordo sottile;
- altezza ridotta.

Transizione:

```text
200–350ms
ease-out
```

---

# 9. Hero

Il componente più importante dell'intero sito.

## Variante 1 — Full bleed

```text
┌───────────────────────────────────────────────┐
│                                               │
│              PHOTO / VIDEO                   │
│                                               │
│     OVUNQUE                                  │
│     TI PORTI                                 │
│     LA STRADA.                               │
│                                               │
│     [ Esplora ]                               │
│                                               │
│                              42°18' N         │
│                              7°32' E          │
│                                               │
└───────────────────────────────────────────────┘
```

## Variante 2 — Split cinematic

```text
┌──────────────────────┬────────────────────────┐
│                      │                        │
│  IL PROSSIMO         │                        │
│  POSTO               │      FOTO CAMPER      │
│  NON È ANCORA        │                        │
│  SULLA MAPPA.        │                        │
│                      │                        │
│  [ Parti → ]         │                        │
│                      │                        │
└──────────────────────┴────────────────────────┘
```

Questa seconda variante è più originale e più facile da rendere responsive.

---

# 10. “Trip ribbon”

Una componente distintiva che suggerisco fortemente.

Una linea di viaggio orizzontale che compare in diverse sezioni.

```text
Roma ─────●────────●────────●──────────── Dolomiti
          │        │        │
        82km     143km     211km
```

Può diventare:

- progress indicator;
- timeline;
- route preview;
- breadcrumbs;
- journey history.

La stessa metafora visiva riappare nel prodotto.

Questo crea **brand recognition**.

---

# 11. Mappe

La mappa dovrebbe avere un trattamento grafico proprietario.

Non limitarti a incorporare una mappa standard e lasciare tutto invariato.

### Direzione

- fondo crema / verde molto chiaro;
- strade principali in verde;
- percorso utente in clay/arancio;
- punti di interesse come piccoli marker circolari;
- topografia molto leggera;
- label minimal;
- popup custom.

### Route

La traccia del viaggio può essere animata:

```text
●───────────────●───────────●
        ↘
          ●────────────●
```

Quando la route appare:

- stroke-dash animation;
- marker che segue il percorso;
- piccoli “pulse” sui punti di interesse.

Durata: **800–1600ms**.

Non ripetere l'animazione continuamente.

---

# 12. Camper come elemento UI

Una scelta che può rendere il prodotto molto riconoscibile:

**usare il camper come metafora di navigazione.**

Esempi:

### Stato viaggio

```text
🚐 ────────────────●────────────
      62%
```

### Loading

Invece del classico spinner:

```text
        🚐
───────────────
   exploring...
```

### Empty state

```text
           🚐

Nessun viaggio qui.

È il momento di
tracciare una nuova strada.

[ Crea viaggio ]
```

### Success

Il camper percorre brevemente una mini-route.

---

# 13. Card: non usare il classico SaaS grid

Evita:

```text
┌────────┐ ┌────────┐ ┌────────┐
│ Icon   │ │ Icon   │ │ Icon   │
│ Title  │ │ Title  │ │ Title  │
│ Text   │ │ Text   │ │ Text   │
└────────┘ └────────┘ └────────┘
```

È immediatamente riconoscibile come dashboard generica.

Preferisci card fotografiche / editoriali:

```text
┌──────────────────────────────┐
│                              │
│          PHOTO               │
│                              │
│  DOLOMITI                    │
│  3 giorni · 240 km           │
│                              │
│  ↗ Apri viaggio              │
└──────────────────────────────┘
```

---

# 14. Asymmetric grid

Per dare personalità:

```text
┌──────────────────────┬─────────────┐
│                      │             │
│      BIG IMAGE       │   SMALL     │
│                      │   IMAGE      │
│                      │             │
├───────────────┬──────┴─────────────┤
│               │                    │
│   CARD        │     BIG CARD       │
│               │                    │
└───────────────┴────────────────────┘
```

Non tutto deve avere la stessa dimensione.

Usa:

- 8 colonne;
- 12 colonne;
- 16 colonne per pagine editoriali.

---

# 15. Bento, ma “outdoor bento”

Il bento classico è ormai molto comune.

Puoi reinterpretarlo:

```text
┌──────────────────────────┬─────────────┐
│                          │             │
│     NEXT DESTINATION     │   WEATHER   │
│                          │             │
├───────────────┬──────────┴─────────────┤
│               │                        │
│   CAMPER      │      ROUTE MAP         │
│   PROFILE     │                        │
│               │                        │
├───────────────┴───────────────┬────────┤
│                               │        │
│        TRIP JOURNAL           │  42km  │
│                               │        │
└───────────────────────────────┴────────┘
```

Ma mantieni:

- bordi arrotondati moderati;
- immagini;
- colori naturali;
- typography editoriale.

---

# 16. Layout dashboard principale

Se la tua app è autenticata, suggerisco:

```text
┌─────────────────────────────────────────────────────┐
│ LOGO              MAPPA    VIAGGI       👤          │
├─────────────────────────────────────────────────────┤
│                                                     │
│  BUONGIORNO, MARCO                                  │
│                                                     │
│  IL PROSSIMO VIAGGIO                                │
│                                                     │
│  ┌───────────────────────────────────────────────┐  │
│  │                                               │  │
│  │          ROUTE / HERO IMAGE                  │  │
│  │                                               │  │
│  │  Roma → Dolomiti                              │  │
│  │  7 giorni · 842 km                            │  │
│  │                                               │  │
│  └───────────────────────────────────────────────┘  │
│                                                     │
│  I TUOI VIAGGI                                      │
│                                                     │
│  [card] [card] [card]                               │
│                                                     │
└─────────────────────────────────────────────────────┘
```

---

# 17. Mobile navigation

Su mobile non cercare di replicare desktop.

Bottom navigation:

```text
┌─────────────────────────────────────┐
│                                     │
│                                     │
│             CONTENT                 │
│                                     │
│                                     │
├─────────────────────────────────────┤
│  Home    Mappa    ＋    Viaggi   Io │
└─────────────────────────────────────┘
```

Il `+` centrale può essere il pulsante “Nuovo viaggio”.

Può avere forma circolare o organica.

---

# 18. Animazioni

## Filosofia

L'animazione deve comunicare:

- movimento;
- vento;
- strada;
- scoperta;
- continuità.

Non deve comunicare:

- gaming;
- fintech;
- SaaS;
- “AI sparkle”.

---

# 19. Motion system

### Micro

```text
100–180ms
```

Per:

- hover;
- opacity;
- piccoli feedback.

### Standard

```text
250–450ms
```

Per:

- card;
- menu;
- modal;
- nav;
- tabs.

### Cinematic

```text
700–1400ms
```

Per:

- hero;
- route;
- page transition;
- grandi immagini.

### Ambient

```text
4–12s
```

Per:

- vento;
- floating;
- parallax leggerissimo.

---

# 20. Easing

Preferisci curve naturali.

```css
--ease-out: cubic-bezier(.22, 1, .36, 1);
--ease-in-out: cubic-bezier(.65, 0, .35, 1);
```

Per un prodotto premium, evitare `linear` come default.

---

# 21. Hero image animation

Quando la pagina carica:

1. background leggermente zoomato;
2. image scale `1.05 → 1`;
3. overlay aumenta;
4. headline sale dal basso;
5. route line viene disegnata.

Sequenza:

```text
0ms       image
150ms     overlay
300ms     headline
450ms     subcopy
600ms     CTA
750ms     route
```

La sequenza deve essere veloce abbastanza da non sembrare una intro animation.

---

# 22. Scroll animation

Quando entra una sezione:

```text
opacity: 0 → 1
transform: translateY(24px) → 0
```

Durata:

```text
500–700ms
```

Stagger:

```text
80–120ms
```

Ma attenzione: **non animare ogni singolo elemento**.

Meglio animare gruppi.

---

# 23. Parallax

Usalo principalmente sulle immagini.

```text
scroll
  ↓

foreground  1.00x
image       0.92x
background  0.85x
```

Il parallax deve essere molto leggero.

Su mobile:

- ridurlo;
- o disattivarlo.

---

# 24. Hover card

Una travel card può:

- aumentare di 1–2%;
- spostare leggermente l'immagine;
- mostrare un piccolo arrow;
- aumentare contrasto;
- spostare il titolo di 2px.

Esempio:

```text
image scale: 1 → 1.04
card translateY: 0 → -4px
arrow translateX: 0 → 4px
```

Durata: `300ms`.

---

# 25. Scroll-driven storytelling

Questa può essere la feature visuale più forte.

Immagina una sezione:

```text
             01

      PARTENZA
          ↓

       [PHOTO]

          ↓

       02

      MONTAGNA

          ↓

       [MAP]

          ↓

       03

       ARRIVO
```

Mentre l'utente scrolla:

- la route si costruisce;
- cambia la fotografia;
- cambiano temperatura/colore;
- compaiono dati;
- il camper attraversa il percorso.

Questo trasforma la landing page in un **viaggio digitale**.

---

# 26. Texture

Usa texture quasi impercettibili:

- carta;
- grana fotografica;
- noise;
- contour lines;
- piccoli puntini.

CSS:

```css
background-image:
  radial-gradient(
    rgba(16,35,28,.035) 1px,
    transparent 1px
  );

background-size: 8px 8px;
```

La texture deve essere percepibile solo quando si guarda attentamente.

---

# 27. Contour lines

Elemento distintivo molto forte.

Usa linee topografiche come decorazione:

```text
      ╭────────────╮
   ╭──╯            ╰────╮
 ╭─╯                     ╰──╮
 │      CONTENT              │
 ╰──╮                     ╭──╯
    ╰───────╮       ╭─────╯
            ╰───────╯
```

Possono essere:

- SVG;
- background;
- maschere;
- decorazioni nelle sezioni.

Non usarle dietro testi piccoli: riducono la leggibilità.

---

# 28. Iconografia

Evita icon set troppo corporate.

Preferisci:

- stroke 1.5–2px;
- angoli leggermente morbidi;
- forme semplici;
- simboli outdoor.

Icone utili:

- camper;
- tenda;
- montagna;
- pin;
- route;
- sole;
- vento;
- acqua;
- altitudine;
- distanza;
- calendario;
- bivacco.

Le icone devono accompagnare il contenuto, non diventare illustrazioni gigantesche.

---

# 29. CTA

Primary:

```text
┌──────────────────────────┐
│  Inizia il viaggio   →   │
└──────────────────────────┘
```

Secondarie:

```text
Esplora la mappa
Scopri il percorso
Apri il diario
Continua il viaggio
```

Evita:

- Learn more
- Get started
- Discover
- Explore now

se il prodotto ha un'identità italiana.

La microcopy deve sembrare parte del viaggio.

---

# 30. Stati e microcopy

## Empty state

Non:

> No trips found.

Meglio:

> **La mappa è ancora vuota.**  
> C'è sempre una prima strada da tracciare.

CTA:

> Crea il primo viaggio →

## Loading

Non:

> Loading...

Meglio:

> **Prepariamo la prossima tappa…**

## Error

Non:

> Something went wrong.

Meglio:

> **La strada si è interrotta.**  
> Riproviamo tra un momento.

## Success

> **Tappa salvata.**  
> Il viaggio prende forma.

---

# 31. Design tokens

Organizza il frontend attorno a token.

```css
:root {
  --color-forest-950: #10231C;
  --color-forest-900: #173329;
  --color-forest-700: #28513F;

  --color-moss-500: #6F8F62;
  --color-sage-300: #AFC19F;

  --color-sand-100: #F3EBDD;
  --color-sand-200: #E7DDCB;

  --color-clay-500: #C97952;
  --color-sun-400: #E6B85C;

  --color-ink: #17201C;
  --color-white: #FCFBF7;

  --radius-sm: 10px;
  --radius-md: 18px;
  --radius-lg: 28px;
  --radius-xl: 40px;

  --shadow-soft:
    0 12px 40px rgba(16,35,28,.08);

  --shadow-floating:
    0 24px 70px rgba(16,35,28,.16);
}
```

---

# 32. Border radius

Non fare tutto `rounded-full`.

Gerarchia:

```text
buttons       999px / pill
small cards   12–16px
cards         20–28px
hero          28–40px
image blocks  24–40px
```

La forma deve ricordare oggetti fisici e morbidi, non una UI fintech.

---

# 33. Spacing

Sistema a 4/8px:

```text
4
8
12
16
24
32
48
64
80
96
128
160
```

Per le sezioni editoriali usa:

```text
desktop: 96–160px vertical
tablet: 72–112px
mobile: 56–88px
```

La sensazione premium nasce anche dallo spazio vuoto.

---

# 34. Immagini

## Direzione fotografica

Cerca immagini con:

- camper piccoli rispetto al paesaggio;
- persone viste da dietro;
- luce golden hour;
- strade secondarie;
- montagne;
- boschi;
- laghi;
- coste;
- dettagli del camper;
- mani / mappe / tazze / porte aperte.

Evita fotografie troppo stock:

- persona sorridente davanti al camper;
- camper parcheggiato frontalmente;
- posa pubblicitaria;
- cielo perfetto e saturazione eccessiva.

Il camper dovrebbe spesso essere **parte della scena**, non il soggetto isolato.

---

# 35. Image treatment

Usa:

```text
contrast: leggermente ridotto
saturation: naturale
grain: leggerissimo
overlay: forest
```

Per immagini hero:

```css
.hero-image {
  filter: saturate(.92) contrast(.96);
}
```

Non applicare filtri aggressivi.

---

# 36. Video

Se hai video:

- 5–12 secondi;
- loop;
- no audio;
- movimenti lenti;
- camera su strada;
- drone molto lento;
- camper in movimento.

Il video deve essere **ambientale**, non un commercial.

Fallback obbligatorio:

```text
poster image
```

---

# 37. Layout pagina “Scopri”

Struttura suggerita:

```text
HERO

↓

EDITORIAL INTRO

“Non cerchiamo destinazioni.
Cerchiamo strade che meritano di essere percorse.”

↓

FEATURED JOURNEY

big image + route

↓

DESTINATIONS

asymmetric grid

↓

MAP EXPERIENCE

full-width map

↓

JOURNAL

photos + notes

↓

CTA

“Dove vuoi andare domani?”
```

---

# 38. Layout pagina “Viaggio”

```text
┌─────────────────────────────────────────────┐
│ ← Tutti i viaggi                            │
│                                             │
│ DOLOMITI                                    │
│ 7 giorni · 842 km                           │
│                                             │
│ [hero photo]                                │
│                                             │
├───────────────────────┬─────────────────────┤
│                       │                     │
│ TIMELINE              │ MAP                 │
│                       │                     │
│ ● Partenza            │       route         │
│ │                     │                     │
│ ● Lago                 │                     │
│ │                     │                     │
│ ● Passo                │                     │
│                       │                     │
└───────────────────────┴─────────────────────┘
```

Desktop: split view.

Mobile:

```text
hero
↓
trip summary
↓
map
↓
timeline
↓
journal
```

---

# 39. Timeline

La timeline deve essere una componente proprietaria.

```text
●─────────────
│
│  ROMA
│  08:30
│
│  82 km
│
●─────────────
│
│  LAGO
│  12:40
│
●─────────────
│
│  MONTAGNA
│  18:20
```

Usa il colore clay per lo stato attivo.

---

# 40. Weather UI

Se l'app usa meteo, evita il classico widget:

```text
☀ 24°
```

Fallo diventare parte del viaggio:

```text
┌────────────────────────────┐
│ DOMANI                     │
│                            │
│  ☀ 24°                     │
│  vento 8 km/h              │
│                            │
│  Perfetto per partire.     │
└────────────────────────────┘
```

La UI può cambiare leggermente con:

- sole;
- pioggia;
- neve;
- notte.

Ma senza trasformare il prodotto in una dashboard meteo.

---

# 41. Responsive

## Desktop

```text
max-width: 1440px
content: 1200–1320px
```

## Tablet

```text
768–1199px
```

## Mobile

```text
< 768px
```

### Mobile priority

1. hero;
2. CTA;
3. viaggio corrente;
4. mappa;
5. prossima tappa;
6. diario;
7. informazioni secondarie.

Non cercare di comprimere desktop in mobile.

---

# 42. Accessibility

L'estetica non deve compromettere l'accessibilità.

Obbligatorio:

- contrasto WCAG;
- focus states;
- keyboard navigation;
- reduced motion;
- alt text;
- target touch >= 44px;
- testo non inserito dentro immagini senza alternativa;
- mappa con alternativa testuale.

### Reduced motion

```css
@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    animation-duration: .01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: .01ms !important;
    scroll-behavior: auto !important;
  }
}
```

---

# 43. Performance

Essenziale per una webapp ricca di immagini.

### Images

- WebP/AVIF;
- responsive `srcset`;
- lazy loading sotto fold;
- priority loading hero;
- dimensioni esplicite;
- placeholder blur.

### Video

- poster;
- preload metadata;
- lazy load;
- fallback statico.

### Animazioni

Preferire:

```text
transform
opacity
```

Evitare animazioni continue su:

```text
width
height
top
left
```

quando non necessarie.

---

# 44. Stack frontend consigliato

Se sei backend-oriented e vuoi una base solida:

### Framework

**Next.js**

### Styling

**Tailwind CSS**

### Animation

**Motion / Framer Motion**

### Scroll

**Motion + IntersectionObserver**

Per storytelling molto avanzato:

**GSAP + ScrollTrigger**

### Map

A seconda del prodotto:

- Mapbox;
- MapLibre;
- Leaflet.

Per un'estetica custom, MapLibre/Mapbox sono particolarmente interessanti perché permettono di personalizzare molto il map style.

### Icons

Lucide.

### UI primitives

Radix UI se servono componenti accessibili.

---

# 45. Regola importante per le librerie

Non trasformare il progetto in:

```text
Tailwind
+
shadcn
+
Framer Motion
+
GSAP
+
three.js
+
Lottie
+
5 icon libraries
+
3 map libraries
```

La complessità cresce rapidamente.

Una base più disciplinata:

```text
Next.js
Tailwind
Radix
Lucide
Motion
MapLibre
```

e aggiungere GSAP solo se serve davvero per una specifica esperienza.

---

# 46. “Skills” / capability che darei all'agente coding

Se stai usando un coding agent, gli darei capacità/moduli espliciti come:

### `design-system`

Responsabile di:

- token;
- colori;
- typography;
- spacing;
- radius;
- shadows;
- breakpoints.

### `motion-system`

Responsabile di:

- entrance;
- hover;
- page transitions;
- scroll;
- reduced motion.

### `travel-map`

Responsabile di:

- route;
- marker;
- itinerary;
- map theme;
- selected destination.

### `trip-storytelling`

Responsabile di:

- timeline;
- route progression;
- chapter;
- photos;
- travel statistics.

### `responsive-layout`

Responsabile di:

- mobile;
- tablet;
- desktop;
- container;
- grid.

### `accessibility`

Responsabile di:

- keyboard;
- focus;
- contrast;
- reduced motion;
- semantic HTML.

### `performance`

Responsabile di:

- images;
- video;
- lazy loading;
- bundle;
- animations.

---

# 47. Prompt architetturale per il coding agent

Puoi passargli questo principio:

```text
Build the frontend as a premium outdoor travel experience,
not as a generic SaaS dashboard.

Visual identity:
- forest green
- warm sand
- moss
- clay orange
- editorial typography
- large atmospheric photography
- subtle topographic textures

UX principles:
- route and journey are recurring visual metaphors
- maps are first-class UI elements
- cards should feel editorial rather than SaaS-like
- use asymmetric layouts
- use generous whitespace
- animations should suggest movement, wind and exploration
- avoid excessive glassmorphism
- avoid purple/blue SaaS gradients
- avoid generic dashboard grids
- avoid excessive rounded cards

Motion:
- 200–400ms micro interactions
- 500–800ms section reveals
- 800–1600ms cinematic route animations
- subtle image parallax
- support prefers-reduced-motion

Accessibility:
- WCAG-conscious contrast
- keyboard navigation
- visible focus
- semantic HTML
- reduced motion support

Performance:
- AVIF/WebP
- responsive images
- lazy loading
- transform/opacity animations
- lightweight client-side JavaScript

The interface should feel like:
a digital travel journal + modern map + premium outdoor magazine.
```

---

# 48. Component architecture

Una possibile struttura:

```text
components/
│
├── layout/
│   ├── SiteHeader
│   ├── MobileNav
│   ├── PageContainer
│   └── Section
│
├── travel/
│   ├── TripCard
│   ├── TripHero
│   ├── TripTimeline
│   ├── TripStats
│   ├── RouteRibbon
│   ├── DestinationCard
│   └── TravelJournal
│
├── map/
│   ├── TravelMap
│   ├── RouteLine
│   ├── MapMarker
│   └── MapPopup
│
├── outdoor/
│   ├── TopographicPattern
│   ├── WeatherCard
│   ├── CamperMarker
│   └── TerrainDivider
│
├── motion/
│   ├── Reveal
│   ├── ParallaxImage
│   ├── RouteDraw
│   └── PageTransition
│
└── ui/
    ├── Button
    ├── Badge
    ├── Modal
    ├── Sheet
    ├── Input
    └── Tabs
```

---

# 49. Page transition

Una transizione interessante:

### Partenza

La pagina vecchia viene leggermente sfocata.

### Route

Una linea clay attraversa lo schermo.

### Arrivo

La nuova pagina emerge.

Molto breve:

```text
350–650ms
```

Non deve diventare una splash screen.

---

# 50. Idee “wow”

## 1. Route becomes navigation

La route del viaggio diventa anche:

- breadcrumb;
- progress bar;
- timeline;
- decorative element.

Un'unica metafora in tutto il prodotto.

---

## 2. Camper cursor

Desktop only.

Un piccolo marker/camper può sostituire il cursore **solo in alcune hero experience**, non in tutta l'app.

Al passaggio su una CTA:

```text
🚐 → →
```

Usarlo globalmente sarebbe probabilmente troppo.

---

## 3. Dynamic atmosphere

La UI può cambiare leggermente in base al contesto del viaggio:

```text
mare       → sand / blue-green
montagna   → forest / sage
deserto    → sand / clay
notte      → forest-black / warm yellow
```

La struttura resta identica; cambia l'atmosfera.

---

## 4. Digital campfire

Per una sezione community/journal:

- grande cerchio centrale;
- immagini degli utenti intorno;
- contenuti che orbitano;
- glow molto sottile.

Metafora:

**“ci si incontra attorno al fuoco.”**

---

## 5. Passport / journey stamps

Ogni luogo visitato genera un piccolo “stamp”.

```text
┌──────────────┐
│  DOLOMITI    │
│      ★       │
│   2026       │
└──────────────┘
```

Diventa una collezione personale.

---

# 51. Cosa evitare

### ❌ Glassmorphism ovunque

È già molto abusato.

### ❌ Gradienti viola/blu

Comunicano SaaS/AI, non outdoor.

### ❌ Dashboard piena di card

Riduce l'identità.

### ❌ Animare tutto

Il risultato sembra un template showcase.

### ❌ Parallax aggressivo

Su mobile è fastidioso e costoso.

### ❌ Foto stock palesemente commerciali

Distruggono autenticità.

### ❌ Troppi colori

Outdoor non significa verde ovunque.

### ❌ Font “rustici”

Niente font western / hand-written / campeggio anni '70 come font principale.

### ❌ Icone gigantesche

L'outdoor deve emergere da immagini, mappe e layout.

---

# 52. Design direction finale

Se dovessi sintetizzare l'intero prodotto in una frase:

> **“Un diario di viaggio cinematografico costruito intorno a una mappa.”**

La UI dovrebbe far percepire:

```text
NATURA
   ↓
STRADA
   ↓
MOVIMENTO
   ↓
SCOPERTA
   ↓
MEMORIA
```

e non:

```text
DATABASE
   ↓
FORM
   ↓
DASHBOARD
```

---

# 53. Priorità di implementazione

Non implementerei tutto contemporaneamente.

## Phase 1 — Foundation

- design tokens;
- typography;
- navbar;
- buttons;
- cards;
- responsive grid;
- colors;
- spacing.

## Phase 2 — Brand identity

- hero;
- photography;
- contour lines;
- route ribbon;
- trip cards;
- editorial sections.

## Phase 3 — Motion

- reveal;
- hover;
- hero entrance;
- route animation;
- parallax leggero.

## Phase 4 — Map

- custom style;
- routes;
- markers;
- itinerary;
- responsive behavior.

## Phase 5 — Advanced experience

- scroll storytelling;
- dynamic atmosphere;
- journey stamps;
- camper interactions;
- cinematic transitions.

---

# 54. La mia composizione consigliata

Se vuoi una direzione forte e coerente, partirei da questa combinazione:

### Visual

**Forest + Sand + Clay**

### Typography

**Fraunces + Inter**

### Layout

**Editorial asymmetric grid**

### Hero

**Full-bleed cinematic image**

### Navigation

**Floating transparent → solid on scroll**

### Signature element

**Animated route ribbon**

### Map

**Custom muted topographic map**

### Motion

**Soft, slow, physical**

### Cards

**Photography-first**

### Texture

**Subtle paper/noise + contour lines**

### Mobile

**Bottom navigation + central “Nuovo viaggio”**

### Brand metaphor

**The route**

---

# 55. Checklist finale per l'agente

Prima di considerare una pagina completa, verifica:

```text
[ ] La pagina comunica viaggio senza dover leggere tutto?
[ ] Il verde non domina eccessivamente?
[ ] Esiste una gerarchia tipografica forte?
[ ] Le immagini sembrano editoriali?
[ ] La mappa ha un trattamento proprietario?
[ ] La route è una metafora ricorrente?
[ ] Le card non sembrano componenti SaaS standard?
[ ] Il layout usa spazio negativo?
[ ] Le animazioni sono poche ma memorabili?
[ ] Mobile è progettato, non semplicemente adattato?
[ ] Reduced motion è supportato?
[ ] Focus state e contrasto sono corretti?
[ ] Hero e immagini sono ottimizzati?
[ ] Nessuna animazione causa layout shift?
[ ] La UI rimane utilizzabile senza le animazioni?
```

---

# 56. Direzione conclusiva

Il rischio principale del progetto non è “fare una UI brutta”.

È fare una UI **troppo simile a tutte le altre**.

Per distinguerti, non servono 30 animazioni o una tecnologia spettacolare.

Servono **3–4 elementi proprietari ripetuti con disciplina**:

1. **la route come elemento grafico;**
2. **la mappa come spazio narrativo;**
3. **la fotografia outdoor come superficie primaria;**
4. **una palette forest/sand/clay con typography editoriale.**

Se questi quattro elementi diventano il linguaggio comune di landing, dashboard, mappe, viaggio, profilo e mobile navigation, la webapp inizierà ad avere una vera identità.

**Obiettivo estetico:**  
*non “un'app per camper”.*

**Obiettivo:**  
*“un posto digitale in cui viene voglia di partire.”*
