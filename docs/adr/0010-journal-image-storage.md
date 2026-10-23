# ADR-0010 — Storage delle immagini del Journal

- **Stato:** Accettato
- **Data:** 2026-10-09
- **Owner:** @vulcan
- **Decisore:** utente
- **Severità:** HIGH (privacy, storage esterno, cancellazione, rete)
- **Chiude:** decisione #8 di `OPEN-DECISIONS.md`
- **Emenda:** F5 di ADR-0004 per i blob del Journal; i requisiti restano invariati per Azure SQL

---

## Contesto

Il Journal Phase 2 può contenere foto personali e geolocalizzate. I byte non devono entrare nel database SQL da 1 GB; devono invece essere esportabili e cancellabili insieme ai dati dell'utente. Le regole R1-R10 si fermano al confine del database, quindi l'accesso e l'erasure dello storage devono soddisfare F1-F7 di ADR-0004.

Il progetto è personale e deve contenere costi e complessità. Il target cloud già scelto è Azure Container Apps e Azure SQL in Italy North, ma non sono state create risorse Blob.

## Opzioni considerate

### Opzione A — Journal testuale, senza foto

Evita storage, file lifecycle e l'esposizione di metadati fotografici, ma non soddisfa l'interesse espresso per caricare immagini.

### Opzione B — Azure Blob Standard, LRS, Hot

Si integra con il target Azure e separa i binari dal database. Richiede identità del servizio, configurazione verificata e flussi espliciti di export, riconciliazione e cancellazione.

### Opzione C — AWS S3 o altro object storage

Object storage maturo, ma introduce un secondo provider cloud, una diversa regione e un ulteriore insieme di identità, costi e policy di retention.

### Opzione D — Binari nel database o filesystem dell'applicazione

Il database aumenta di dimensione e costo; il filesystem dell'app non è durevole né condiviso in un ambiente containerizzato e non offre le proprietà di prefix-delete richieste.

## Decisione

1. Le foto appartengono a una sola `JournalEntry` tramite la nuova entità owned `JournalEntryAttachment`. `Document` resta una funzione distinta della Phase 4.
2. In produzione si userà Azure Blob Standard, LRS, Hot in Italy North; in Development e nei test si userà Azurite tramite Docker. Container privato, accesso anonimo disabilitato, endpoint di rete pubblico ma accesso del backend autenticato con managed identity e permessi minimi.
3. L'upload passa in streaming dall'API a Blob; il browser non riceve credenziali di scrittura. Il backend verifica ownership, tipo e dimensione. I download usano URL firmati read-only, limitati al blob e validi per 5 minuti, emessi solo dopo verifica owner-scoped.
4. Si accettano JPEG, PNG e WebP, massimo 10 MiB per file e 10 file per voce. Si usano i pacchetti ufficiali `Azure.Storage.Blobs` e `Azure.Identity`, open source e senza licenza commerciale o chiave di build; non si aggiunge una libreria di elaborazione immagini.
5. Gli originali sono conservati senza trasformazione: gli EXIF restano, incluse eventuali coordinate GPS. L'utente ha accettato esplicitamente questo rischio per evitare una dipendenza e un flusso di licenza aggiuntivi. Si accetta inoltre l'assenza di scansione antimalware nell'uso personale attuale.
6. Le immagini sono incluse come file nello ZIP dell'export. Le chiavi rispettano `{ownerId}/journal-entry-attachment/{journalEntryId}/{attachmentId}`.
7. Per rispettare F4-F5, non si mantengono soft delete, versioning, snapshot recuperabili o backup Blob applicativi. La configurazione effettiva dell'account deve essere verificata prima del primo upload cloud. Per Azure SQL restano approvati PITR di 7 giorni e replay delle erasure dopo un restore.
8. La riconciliazione F2 rileva blob senza riga e righe senza blob. L'erasure F7 esegue uno sweep idempotente del prefisso dell'owner; la `ErasureReceipt` non viene emessa finché lo sweep non è confermato.

## Motivazione

Azure Blob è coerente con il provider già scelto e mantiene i binari fuori da Azure SQL. LRS Hot evita una seconda regione e costi di retrieval per immagini consultate nel Journal. L'accesso via API riduce l'esposizione di credenziali al client; il SAS di download è limitato all'oggetto e a breve durata.

I trade-off accettati sono espliciti: LRS non protegge dalla perdita dell'intero datacenter; la cancellazione accidentale di un blob non è recuperabile; gli EXIF possono rivelare coordinate precise; senza scansione antimalware si accetta un rischio maggiore nell'elaborazione e nella conservazione di file ostili. Rivalutare scansione e conservazione EXIF prima di consentire altri utenti o registrazione pubblica.

## Conseguenze

- `JournalEntryAttachment` entra nel modello completo e runtime Phase 2, nell'export model-driven e nel grafo di erasure.
- Upload, download, eliminazione, export, riconciliazione e account erasure devono coordinare SQL e Blob senza presumere una transazione distribuita.
- Azurite verifica il contratto locale, ma non prova le impostazioni effettive dell'account Azure. Le proprietà effettive dell'account e del Blob service sono state verificate il 2026-10-20 prima di un test d'accettazione con dati sintetici; il test non verifica versioning, retention o backup e non sostituisce il controllo separato della configurazione.
- Nessun account o risorsa Azure, container o deployment viene creato o autorizzato da questo ADR. Il provisioning Blob è stato autorizzato separatamente dall'utente il 2026-10-20; l'app non è stata pubblicata.

## Riferimenti

`docs/OPEN-DECISIONS.md` #8; `docs/architecture/CONTEXT.md` §2.3, §2.5 e §5; `docs/architecture/SECURITY.md` §5; `docs/adr/0004-privacy-and-erasure.md` F1-F7; `docs/product/ROADMAP.md` Phase 2.
