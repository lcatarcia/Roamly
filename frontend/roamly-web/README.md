# Roamly Web

Frontend React + TypeScript + Vite di Roamly. Il login usa il backend locale
tramite il proxy Vite; i cookie di sessione Secure richiedono il certificato
HTTPS di sviluppo.

## Avvio locale

1. Avvia l'API Roamly in HTTPS sulla porta `7187`.
2. Installa le dipendenze gia' dichiarate nel lockfile con `npm ci`.
3. Avvia Vite con `npm run dev` e apri l'indirizzo HTTPS mostrato nel terminale.
4. Verifica il login reale dalla pagina `/`.

Comandi utili: `npm run build`, `npm run lint` e `npm run design:check`.

## Catalogo i18n

Le stringhe UI italiane vivono nell'unico catalogo tipizzato
`src/i18n/catalog.ts`. Ogni chiave appartiene a `neutral.*` per contenuti
funzionali, errori e privacy, oppure a `narrative.*` per la microcopy narrativa.
Usa `t('namespace.chiave')`: TypeScript controlla la chiave e
`design:r21` verifica catalogo, riferimenti, superfici funzionali e testi JSX
inline. Numeri e date del prototipo sono formattati con `Intl`.

## Prototipo statico del Ribbon

Con Vite in Development apri `/__dev/ribbon` per ispezionare il prototipo
orizzontale e verticale e cambiare tema dal toggle nell'header. Il route viene
registrato solo in Development e non fa parte della build di produzione.
I valori sono quelli dimostrativi del diagramma ADR-0007: non arrivano da API,
non rappresentano dati reali e la geometria non calcola una misura di
contachilometri. Il prerequisito di dominio D4 resta fuori da questo prototipo.
