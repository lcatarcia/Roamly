# ADR-0008 — Chiave primaria: `(OwnerId, Id)` composita e clusterizzata, `Guid` generato dal dominio

- **Stato:** Accettato
- **Data:** 2026-09-25
- **Owner:** @oracle
- **Decisore:** utente
- **Severità:** HIGH (database + breaking change irreversibile dopo la prima migration)
- **Chiude:** decisione *"Tipo della chiave primaria"* (`CONTEXT.md` §3.8) · gap *"`UNIQUE (Id, OwnerId)` su ogni tabella-padre"*
- **Emenda:** ADR-0003 (R4) e ADR-0004 (§Impatto sullo schema → *Sul tipo della chiave primaria*), che rinviavano entrambi a questa decisione

---

## Contesto

`Id` era l'ultima lacuna aperta del domain model, ed è l'unica con costo di inversione **ALTO**. Non è una scelta di tipo di colonna: è la scelta della **forma** della chiave, che si moltiplica su 21 foreign key, su ogni indice non clusterizzato di ogni tabella e su 6 vincoli di unicità.

### Perché qui conta più che in un progetto normale

In un progetto qualunque la PK è un dettaglio: `int IDENTITY` o `Guid`, si decide in trenta secondi e non se ne parla più. Su Roamly tre proprietà dello schema, già decise in ADR-0003 e ADR-0004, rendono la scelta strutturale:

1. **`OwnerId` è la prima colonna di ogni indice** (ADR-0003, R1/R2) e ogni accesso è owner-scoped. Non esiste in Roamly una query che attraversi owner diversi: il query filter nominato `"OwnerScope"` lo impedisce. **Sei query su sette del catalogo reale iniziano da `WHERE OwnerId = @me`; la settima anche.** Un sistema in cui il 100% degli accessi condivide lo stesso prefisso deve avere quel prefisso nella clustering key — altrimenti paga un key lookup per ogni riga di ogni pagina di ogni schermata.
2. **Tutte le 21 FK figlio→padre sono composite e includono `OwnerId`** (ADR-0003, R4). Questo non è negoziabile: è ciò che rende impossibile, a livello di motore, agganciare un figlio al padre di un altro utente.
3. Da (2) discendeva un requisito che `CONTEXT.md` §5.3 dichiarava **"non negoziabile"**: se la PK è `Id` da solo, una FK su `(OwnerId, ParentId)` ha bisogno di un vincolo di unicità **`UNIQUE (Id, OwnerId)`** su ogni tabella-padre — 6 indici aggiuntivi, più una **regola operativa da ricordare per sempre**: *"ogni volta che un'entità diventa padre, la chiave alternativa va aggiunta nella stessa migration"*.

Il punto (3) è il vero contenuto di questo ADR. **Quel requisito non era non negoziabile: era una conseguenza dell'aver scelto `PK(Id)`.** SQL Server richiede che le colonne referenziate da una FK siano le colonne chiave di *un* vincolo di unicità — PK **oppure** UNIQUE, indifferentemente. Se la PK **è già** `(OwnerId, Id)`, la FK referenzia direttamente la PK e non serve nient'altro.

> **La domanda non era "quale tipo per `Id`". Era "quale chiave".** Rispondendo alla seconda, la prima si semplifica e un requisito documentato come permanente **cessa di esistere**.

### Vincoli reali

Sviluppatore singolo, nessuna review esterna, nessun DBA, nessun ambiente di carico. Il costo che conta non è lo storage né la CPU: è **il numero di regole che una persona deve ricordare tra sei mesi**. Una soluzione che aggiunge un indice per tabella e una regola operativa perde contro una che li toglie entrambi, anche a parità di performance.

### Sequenza

Il costo di inversione è **ZERO oggi** (una riga di convenzione in `OnModelCreating`) e **ALTO dopo la prima migration**. Non esiste una seconda occasione: per questo la decisione è stata chiusa prima della migration iniziale e non dopo un benchmark.

---

## Fatti verificati, non verificati e falsi

Come in ADR-0003 e ADR-0004, questa tabella è il contenuto informativo principale. Nulla di ciò che segue è stato misurato su Roamly, e ciò che non è verificato è marcato come tale.

### ✅ Verificati — documentazione primaria o sorgente letto

| Fatto | Fonte |
|---|---|
| SQL Server **non** confronta `uniqueidentifier` byte per byte da sinistra a destra: lo confronta in **5 gruppi**, con i **byte 10-15 come criterio più significativo** e i primi tre gruppi in ordine di byte **invertito** | SQLServerScience / Brent Ozar, *How SQL Server Stores a GUID*; Stack Overflow, *SQL Server GUID sort algorithm. Why?* |
| **`Guid.CreateVersion7()` non produce inserimenti sequenziali** in una colonna `uniqueidentifier` su SQL Server | DBA.SE, *GUID v4 vs GUID v7 as SQL Server PK*; Conrad Akunga, *GuidV7 Considerations for SQL Server*; `dotnet/SqlClient` **discussion #2999** (*Add API to correctly insert GuidV7 to SQL Server (BigEndian)*), **tuttora aperta** |
| `Guid.CreateVersion7()` esiste da .NET 9 e implementa RFC 9562: timestamp a 48 bit nei **primi 6 byte**, big-endian | Microsoft Learn, `System.Guid.CreateVersion7`; RFC 9562 §5.7 |
| `SequentialGuidValueGenerator` di EF Core scrive un contatore nei byte **8-15**, con i byte **alti in 10-15** → è sequenziale **nell'ordinamento di SQL Server** | **sorgente letto**: `dotnet/efcore`, `src/EFCore/ValueGeneration/SequentialGuidValueGenerator.cs`, branch `main` |
| È il **default** del provider SQL Server per proprietà `Guid` chiave con `ValueGeneratedOnAdd` | Microsoft Learn, *SQL Server Value Generation* |
| Una FK può referenziare indifferentemente una **PRIMARY KEY o un vincolo UNIQUE** | Microsoft Learn, `CREATE TABLE` / `FOREIGN KEY` |
| La **clustering key è inclusa in ogni indice non clusterizzato** | Microsoft Learn, *Clustered and Nonclustered Indexes Described* |
| `NEWSEQUENTIALID()` è utilizzabile **solo come `DEFAULT`** di colonna, è sequenziale **solo dall'ultimo avvio di Windows**, e **Microsoft ne sconsiglia esplicitamente l'uso quando la privacy è un problema** (il valore successivo è indovinabile; deriva da `UuidCreateSequential`, che incorpora il MAC address) | Microsoft Learn, *NEWSEQUENTIALID (Transact-SQL)* — testo letterale |
| Su Azure SQL / failover la sequenza di `NEWSEQUENTIALID()` produce cluster di sequenze, non una progressione unica | Microsoft Q&A, *Using newsequentialID for GUID columns in Azure SQL DB* |
| **EF Core 10 non introduce value generation UUID v7 nativa** | Microsoft Learn, EF Core 10 What's New; assenza verificata nel sorgente di `SequentialGuidValueGenerator` |
| EF Core **non applica value generation per convenzione** a una proprietà che fa parte di una **chiave composita** | comportamento documentato; è il motivo di R28 |

### ⚠️ Non verificati — dichiarati come tali, mai spacciati per misure

| Affermazione | Come va trattata |
|---|---|
| **EF Core 10 genera `REFERENCES (OwnerId, Id)` nell'ordine delle colonne della PK** | 🔴 **il punto empirico più importante di questo ADR.** Richiede il database reale. Verificato dal test dello schema completo (`DATA.md` §2.2), lo stesso che serve per l'errore 1785 → **non aggiunge un prerequisito**. Piano B pronto: vedi §*Piano B* |
| "~35% di indici più grandi con GUID v7 su SQL Server" | **evidenza di terzi** (gist `sdrapkin/dotnet-guid-sequential-key-sql`), misurata su quel banco di prova, **non su Roamly**. Il **meccanismo** è verificato, la **magnitudine** no. Con la decisione adottata diventa irrilevante, perché `Id` non è più il primo criterio di clustering |
| Il profilo di volume (≈400 righe/utente/anno, ~2,25 M righe a 1000 utenti × 5 anni) | **derivato da assunzioni dichiarate** su `FEATURES.md`, non da dati. Roamly ha zero utenti |
| "il `DELETE` ordinato dell'erasure è sotto il secondo" | **inferenza da ordine di grandezza** (~2.000 righe per utente a 5 anni). Confidenza molto alta, misura assente |
| Frammentazione reale dentro la regione di un singolo owner con id COMB | misurabile solo con `sys.dm_db_index_physical_stats` su dati reali. **Non serve per decidere**; serve per il trigger T2 |
| Latch contention sulle hot page di coda per owner | non misurata. Irrilevante al profilo di scrittura di Roamly, reale in assoluto |

### ❌ Smentiti

| Affermazione | Verdetto |
|---|---|
| «GUID v7 e `NEWSEQUENTIALID()` sono la stessa via di mezzo» (testo precedente di `CONTEXT.md` §3.8) | ❌ **falso, confermato.** Sono due opzioni distinte, e una delle due **non funziona** su SQL Server |
| «la causa del problema di v7 è il layout little-endian di `System.Guid`» (spiegazione allegata alla ❌ in §3.8) | ❌ **impreciso, e va corretto.** `Guid.CreateVersion7()` produce byte **conformi all'RFC**. La causa è **l'ordinamento di confronto di `uniqueidentifier` in SQL Server**. Vedi §*La verifica su GUID v7* per perché la differenza conta |
| «il default di EF Core per i `Guid` è un GUID casuale» | ❌ **falso.** È `SequentialGuidValueGenerator`, ordinato **per SQL Server** (sorgente letto) |
| «`UNIQUE (Id, OwnerId)` su ogni tabella-padre è un requisito non negoziabile» (`CONTEXT.md` §5.3) | ❌ **smentito da questo ADR.** È non negoziabile **solo se** la PK è `Id` da solo. Con `PK(OwnerId, Id)` **scompare** |
| «`ON DELETE SET NULL` è la via d'uscita per le FK opzionali» | ❌ già smentito in ADR-0004: `SET NULL` **conta come percorso** ai fini dell'errore 1785. Confermato, e reso verificabile da R30 |

---

## La verifica su GUID v7 — per esteso

`CONTEXT.md` §3.8 conteneva già una ❌ su `Guid.CreateVersion7()`. **La smentita è confermata e va mantenuta. La causa indicata era sbagliata e va corretta.** Questa sezione esiste perché propagare una causa sbagliata porterebbe a cercare la soluzione nel posto sbagliato.

### Come SQL Server ordina `uniqueidentifier`

SQL Server confronta i 16 byte di un `uniqueidentifier` in **cinque gruppi**, e l'ordine di significatività dei gruppi è **inverso** rispetto alla rappresentazione testuale:

| Priorità di confronto | Gruppo in `aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee` | Byte (indice nell'array .NET) | Ordine interno |
|---|---|---|---|
| **1ª — la più significativa** | `eeeeeeeeeeee` | 10-15 | come memorizzati |
| 2ª | `dddd` | 8-9 | come memorizzati |
| 3ª | `cccc` | 6-7 | **invertito** |
| 4ª | `bbbb` | 4-5 | **invertito** |
| **5ª — la meno significativa** | `aaaaaaaa` | 0-3 | **invertito** |

### Dove RFC 9562 mette il timestamp

RFC 9562 §5.7 colloca il timestamp Unix a 48 bit di UUID v7 nei **primi 6 byte**, in ordine big-endian. Cioè: **esattamente nei gruppi che SQL Server confronta per ultimi** (priorità 4ª e 5ª), e per di più con l'ordine dei byte **invertito** all'interno di ciascun gruppo.

Conseguenza: nell'ordinamento di SQL Server un GUID v7 è dominato dai byte 10-15, che in v7 sono **casuali**. Il timestamp degenera a tie-break meno significativo. **L'ordinamento risultante è indistinguibile da quello di un GUID casuale**, e i page split sono quelli di `NEWID()`.

### Perché la causa corretta cambia la conclusione operativa

Se la causa fosse il layout di `System.Guid`, la soluzione sarebbe *aspettare una versione di .NET che lo sistemi*. Non lo è, e nessuna lo sistemerà: l'ordinamento di `uniqueidentifier` è una scelta **di SQL Server**, precedente all'RFC e mai allineata ad esso. La soluzione, se mai servisse, sta **nel tipo di colonna** (`binary(16)` con byte in ordine big-endian), non nel runtime. La discussione **#2999** aperta nel repo ufficiale di `dotnet/SqlClient` — che chiede un'API per inserire correttamente un GuidV7 in big-endian — è la prova più forte che il problema è reale e non risolto: se lo fosse, non esisterebbe.

### `SequentialGuidValueGenerator` fa l'opposto, ed è corretto

Il generatore di EF Core parte da `Guid.NewGuid()` e **sovrascrive i byte 8-15** con un contatore inizializzato a `DateTime.UtcNow.Ticks`, disponendo i byte **più alti** del contatore nei byte **10-15**:

```
guidBytes[10..15] = counterBytes[7,6,5,4,3,2]   // ticks, byte alti  → 1º criterio di sort SQL Server
guidBytes[08..09] = counterBytes[1,0]           // ticks, byte bassi  → 2º criterio
guidBytes[00..07] = casuali da Guid.NewGuid()   // → criteri 3º/4º/5º, i meno significativi
```

**È l'opposto esatto di UUID v7, ed è la cosa giusta su SQL Server.** Questo fatto chiude la questione della generazione senza bisogno di misurare: si legge nel sorgente.

*Limite dichiarato:* il contatore è **per-istanza** (di fatto per-processo). Con più processi, o dopo un riavvio, la monotonicità è **approssimativa** — riparte dai ticks correnti, quindi in avanti, ma due processi possono interfogliarsi entro la stessa finestra temporale. Effetto pratico: page split occasionali, non frammentazione sistematica. Con la clustering key che inizia da `OwnerId`, l'effetto è ulteriormente ridotto.

---

## Opzioni considerate

### Opzione 1 — `PK(Id)` clusterizzata + 6 × `UNIQUE (Id, OwnerId)` — lo status quo implicito

È la forma che EF Core produce da sola, ed è quella che i documenti descrivevano senza averla mai scelta.

**Pro:** zero pensiero, `Id` unico globalmente garantito, `FindAsync(id)` a un valore.

**Contro:** **nessuna owner-locality** — le righe di un utente sono sparse su tutto il B-tree, ogni query owner-scoped è un seek su NCI seguito da key lookup riga per riga; il `DELETE` dell'erasure tocca righe sparse su molte pagine; le 6 alternate key restano tutte, con la loro regola operativa da ricordare; la frammentazione dipende interamente dal generatore di `Id`.

**Verdetto: la peggiore delle tre forme su questo schema**, proprio perché è quella che nessuno sceglie esplicitamente.

### Opzione 2 — `PK(Id)` non clusterizzata + clustered `(OwnerId, CreatedAtUtc, Id)` + 6 alternate key — proposta di @archimedes

L'intuizione è giusta — `OwnerId` deve stare nella clustering key — ma è portata a metà strada.

**Pro:** owner-locality; ordinamento cronologico nativo per owner; `Id` unico globalmente garantito da constraint.

**Contro, tutti strutturali:**
- clustering key di **40 byte** (16 + 7 + 16 + overhead), replicata in **ogni** indice non clusterizzato della tabella;
- **la PK su `Id` resta `Id` da solo** → le 6 alternate key `UNIQUE (Id, OwnerId)` **restano necessarie**, e ciascuna porta con sé anche i 40 byte di clustering key: ~72 byte di chiave per riga di indice;
- la PK non clusterizzata è essa stessa un secondo indice da mantenere (16 + 40 byte per riga);
- **`CreatedAtUtc` non è la colonna su cui si filtra**: le query reali filtrano per `CamperId`/`TripId` e ordinano per `ReadAtUtc`, `SpentOn`, `NextDueAtUtc` — campi di **dominio**, non l'audit field;
- la regola operativa di `CONTEXT.md` §5.3 sopravvive per sempre.

**Verdetto: strettamente dominata dall'opzione 5.** Ottiene lo stesso beneficio fisico pagando **due indici in più per tabella-padre**.

### Opzione 3 — `bigint IDENTITY` + PK composita `(OwnerId, Id)`

**Pro:** chiave più stretta (8 byte invece di 16), ~80 MB in meno sul profilo stimato, clustering perfetto, id leggibile nei log.

**Contro:** (1) **FK eterogenee** `(Guid, bigint)` su 21 FK — due famiglie di chiavi da tenere a mente per sempre; (2) `Id` **non noto prima del `SaveChanges`** → l'aggregato non può comporre in memoria il proprio grafo di figli; (3) un `int 42` è **plausibile in ogni tabella**: un bug che passa un `Trip.Id` dove ci si aspetta un `Camper.Id` trova una riga valida; (4) oracolo di enumerazione in URL.

**Verdetto: no.** I motivi decisivi sono (1) e (2) — **non** l'enumerazione, vedi §*Enumerazione degli id in URL*.

### Opzione 4 — `Guid` v7 client-side, oppure `NEWSEQUENTIALID()` server-side

**GUID v7:** respinto per il meccanismo documentato sopra. Peggio del semplice non funzionare: *sembra* la scelta moderna e corretta, quindi sarebbe un errore che non verrebbe mai rivisto.

**`NEWSEQUENTIALID()`:** respinto per quattro motivi indipendenti, ciascuno sufficiente:
1. **l'id non è noto prima dell'`INSERT`** — serve `OUTPUT INSERTED.Id`, il batching di EF diventa più fragile e meno leggibile nei log, e il dominio non può costruire il grafo prima;
2. è un **`DEFAULT` di colonna**, quindi strutturalmente incompatibile con una PK composita generata dal dominio;
3. la sequenza **si resetta al riavvio dell'host**, e su Azure SQL / failover produce cluster di sequenze;
4. 🔴 **Microsoft avverte esplicitamente di non usarlo quando la privacy è un problema**: il valore successivo è indovinabile e il GUID deriva da `UuidCreateSequential`, che incorpora il MAC address della macchina. Questo collide frontalmente con ADR-0004 e con lo spirito di R7.

**Varianti minori respinte:** `NEWID()` come default (id non noto prima dell'insert, e pessimo se clusterizzato su `Id`); `binary(16)` con v7 big-endian (l'unico modo di avere v7 davvero sequenziale su SQL Server, al prezzo di un `ValueConverter` con inversione di byte **su ogni proprietà chiave e ogni FK**, dove un errore produce un bug di ordinamento silenzioso che nessun test funzionale vede — riconsiderare solo al trigger T3); Snowflake-like su `bigint` (richiede worker-id, gestione del clock skew e del riavvio: infrastruttura operativa in un progetto senza operations); chiave naturale composita tipo `(OwnerId, PlateNumber)` (la targa è **modificabile**, spesso **assente**, ed è l'identificativo di un bene registrato: metterla in URL è peggio di un `int`; e `Expense`, `OdometerReading`, `JournalEntry` non hanno alcuna chiave naturale).

### Opzione 5 — PK composita `(OwnerId, Id)` CLUSTERED, `Guid` COMB generato dal dominio ⭐ **SCELTA**

**Pro:**
- **nessuna alternate key su nessuna tabella**: le FK `(OwnerId, ParentId)` referenziano direttamente la PK → **6 indici UNIQUE in meno** e una **regola operativa eliminata**;
- **owner-locality by construction**: ogni pagina contiene righe di un solo utente (salvo la pagina di confine). Beneficio su **ogni** query del sistema, perché ogni query è owner-scoped;
- clustering key di **32 byte** contro i 40 dell'opzione 2, replicata in ogni NCI;
- il `DELETE` dell'erasure diventa un **range seek contiguo** invece di righe sparse;
- rende **strutturalmente impossibile** dimenticare `OwnerId` in un accesso per chiave: non esiste un `Find(id)` che funzioni senza owner. **R2 diventa una proprietà della forma dello schema**, non solo del query filter;
- `Id` noto alla costruzione dell'aggregato → batching intatto, `201 Created` + `Location` senza rilettura, test deterministici.

**Contro, accettati e dichiarati:** `Id` non è unico globalmente a livello di constraint; ogni `FindAsync` richiede due valori; la value generation va dichiarata esplicitamente (EF non la applica per convenzione alle chiavi composite); `ORDER BY CreatedAtUtc` non è gratuito senza un indice dedicato.

**Complessità: bassa — inferiore allo status quo documentato.** È l'unica opzione che **riduce** il numero di regole da ricordare.

---

## Decisione

> **`Id` è di tipo `Guid` (`uniqueidentifier`). Ogni entità owned ha chiave primaria COMPOSITA `(OwnerId, Id)`, CLUSTERED. Il valore di `Id` è generato lato client, nel dominio, con algoritmo COMB sequenziale nell'ordinamento di SQL Server. Nessuna chiave alternata `UNIQUE (Id, OwnerId)` su nessuna tabella. `CreatedAtUtc` resta fuori dalla clustering key. L'unicità globale di `Id` non è garantita da alcun vincolo di database, e non serve.**

La topologia delle FK di ADR-0004 **non cambia di una riga**: le 21 FK restano identiche, con le stesse azioni referenziali, e la dimostrazione anti-1785 resta valida integralmente, perché dipende dalle **azioni** (`CASCADE` / `NO ACTION`), non dalle chiavi referenziate.

---

## Configurazione EF Core — la parte che si copia

> Scritta per essere copiata così com'è nella prima migration.

### 1. Il generatore di id — `Roamly.Common`

```csharp
namespace Roamly.Common;

public interface IIdGenerator
{
    Guid NewId();
}

/// <summary>
/// GUID sequenziali nell'ORDINAMENTO DI CONFRONTO DI SQL SERVER (COMB).
/// SQL Server confronta uniqueidentifier dando priorita' ai byte 10-15: il
/// timestamp va scritto li', non nei primi byte. Per la stessa ragione
/// Guid.CreateVersion7() (RFC 9562: timestamp nei byte 0-5) NON e' sequenziale
/// su SQL Server — vedi ADR-0008.
/// Algoritmo allineato a Microsoft.EntityFrameworkCore.ValueGeneration.SequentialGuidValueGenerator.
/// </summary>
public sealed class SequentialGuidGenerator : IIdGenerator
{
    private long _counter = DateTime.UtcNow.Ticks;

    public Guid NewId()
    {
        var guid = Guid.NewGuid();
        var counter = Interlocked.Increment(ref _counter);

        Span<byte> c = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(c, counter);

        Span<byte> b = stackalloc byte[16];
        guid.TryWriteBytes(b);

        // byte alti del contatore nei byte 10-15 = 1º criterio di sort di SQL Server
        b[8]  = c[1]; b[9]  = c[0];
        b[10] = c[7]; b[11] = c[6]; b[12] = c[5];
        b[13] = c[4]; b[14] = c[3]; b[15] = c[2];

        return new Guid(b);
    }
}
```

Registrazione: `services.AddSingleton<IIdGenerator, SequentialGuidGenerator>();`
Nei test: un `DeterministicIdGenerator` che restituisce una sequenza fissa. **È l'unica ragione per cui questo sta dietro un'interfaccia.**

> ⚠️ **Non usare `new SequentialGuidValueGenerator().Next(null!)` come scorciatoia.** Funziona — il metodo ignora l'`EntityEntry` — ma è un dettaglio implementativo non garantito dal contratto pubblico. Si copiano le venti righe, con il commento che ne cita l'origine.

### 2. Convenzione per ogni entità owned

```csharp
// IOwnedResource : { Guid Id { get; } Guid OwnerId { get; } DateTime CreatedAtUtc { get; } ... }

private static void ConfigureOwnedResource<T>(EntityTypeBuilder<T> b) where T : class, IOwnedResource
{
    // PK COMPOSITA, OwnerId per primo, CLUSTERED.
    // E' questa riga che elimina le 6 alternate key: le FK composite
    // referenziano direttamente la PK.
    // IsClustered(true) e' il default di SQL Server, ma va reso ESPLICITO:
    // qui e' una decisione, non un default subito.
    b.HasKey(e => new { e.OwnerId, e.Id }).IsClustered(true);

    // EF NON applica value generation per convenzione a una proprieta' che fa
    // parte di una chiave composita. L'id e' assegnato dall'aggregato tramite
    // IIdGenerator: EF lo persiste e basta.
    b.Property(e => e.Id).ValueGeneratedNever();
    b.Property(e => e.OwnerId).ValueGeneratedNever();

    b.Property(e => e.CreatedAtUtc).HasColumnType("datetime2(3)").IsRequired();
    b.Property(e => e.UpdatedAtUtc).HasColumnType("datetime2(3)");

    // OwnerId -> AspNetUsers: NO ACTION senza eccezioni (ADR-0004).
    b.HasOne<ApplicationUser>()
     .WithMany()
     .HasForeignKey(e => e.OwnerId)
     .OnDelete(DeleteBehavior.NoAction);

    // Query filter nominato, l'unico del sistema (R2, ADR-0003).
    b.HasQueryFilter("OwnerScope", e => e.OwnerId == _currentUser.Id);
}
```

### 3. Relazione padre-figlio — la forma da replicare 21 volte

```csharp
// Camper -> MaintenanceItem: CASCADE lungo la gerarchia di dominio
modelBuilder.Entity<MaintenanceItem>(b =>
{
    ConfigureOwnedResource(b);

    b.HasOne(e => e.Camper)
     .WithMany(c => c.MaintenanceItems)
     .HasForeignKey(e => new { e.OwnerId, e.CamperId })
     // Referenzia la PK. NESSUN HasAlternateKey, NESSUN HasPrincipalKey.
     .OnDelete(DeleteBehavior.Cascade);

    b.HasIndex(e => new { e.OwnerId, e.CamperId })
     .IncludeProperties(e => new { e.NextDueAtUtc, e.NextDueKm });
});

// FK opzionale verso un'entita' gia' raggiungibile per un'altra strada:
// NO ACTION obbligatorio, altrimenti errore 1785 (ADR-0004)
modelBuilder.Entity<Expense>(b =>
{
    ConfigureOwnedResource(b);

    b.HasOne(e => e.Camper).WithMany()
     .HasForeignKey(e => new { e.OwnerId, e.CamperId })
     .OnDelete(DeleteBehavior.Cascade);        // obbligatorio

    b.HasOne(e => e.Trip).WithMany()
     .HasForeignKey(e => new { e.OwnerId, e.TripId })
     .OnDelete(DeleteBehavior.NoAction);       // opzionale — MAI SetNull
});
```

### 4. Cosa NON scrivere — vietato, e verificato da R26-R31

```csharp
b.HasAlternateKey(e => new { e.Id, e.OwnerId });                // non serve piu'
b.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");  // Opzione 4
b.Property(e => e.Id).HasDefaultValueSql("NEWID()");            // id non noto pre-insert
b.Property(e => e.Id).HasValueGenerator<GuidV7Generator>();     // falso amico
.OnDelete(DeleteBehavior.SetNull);                              // conta come cascade path
```

### 5. Gli indici non clusterizzati che servono davvero

Proposta **minima**, da rivedere quando le query esisteranno davvero. `DATA.md` §2: *"sulle query reali, non per sicurezza"*.

| Tabella | Indice | Query servita |
|---|---|---|
| `MaintenanceItem` | `(OwnerId, CamperId) INCLUDE (NextDueAtUtc, NextDueKm)` | dashboard, prossime manutenzioni |
| `OdometerReading` | `(OwnerId, CamperId, ReadAtUtc DESC)` | `Ribbon`, ultima lettura |
| `Expense` | `(OwnerId, CamperId, SpentOn) INCLUDE (Amount, Currency, Category)` | cost management per periodo |
| `Expense` | `(OwnerId, TripId)` filtrato `WHERE TripId IS NOT NULL` | costo per viaggio |
| `SavedPlace` | `(OwnerId, Latitude, Longitude)` | prossimità (già in `DATA.md` §2.1) |
| `TripStop` | `(OwnerId, TripId, SequenceNumber)` | itinerario |
| `MaintenanceLog` | `(OwnerId, MaintenanceItemId, PerformedAtUtc DESC)` | storico interventi |

> Le FK composite figlio→padre **non** hanno bisogno di un indice dedicato quando uno di questi le copre a prefisso — ed è così per costruzione, perché iniziano tutte da `(OwnerId, <ParentId>)`. Un indice per-FK creato "per sicurezza" sarebbe un duplicato.

---

## Regole invarianti — R26-R33

> Numerazione in continuità con **R1-R7** (`SECURITY.md` §2.2), **R8-R10** (ADR-0004) e **R11-R25** (ADR-0007). **R1-R25 non vengono toccate.**

Criterio, invariato: *una regola che si applica "ricordandosene" non è una regola, è un proposito. La differenza è che la prima rompe la build.*

Senza questi verificatori, la prima entità aggiunta tra sei mesi prenderà `PK(Id)` per convenzione EF e nessuno se ne accorgerà finché la FK composita non si rifiuterà di costruirsi — a migration già applicate.

| # | Regola | Verificata da |
|---|---|---|
| **R26** | **PK composita e clusterizzata.** Ogni entity type che implementa `IOwnedResource` ha una chiave primaria di **esattamente due proprietà**, nell'ordine **`[OwnerId, Id]`**, dichiarata **`IsClustered(true)`** | test di convenzione sul modello EF (**bloccante**): itera su `DbContext.Model`, legge `FindPrimaryKey().Properties` e fallisce se il conteggio ≠ 2, se l'ordine è diverso, o se l'annotazione `SqlServer:Clustered` non è `true` |
| **R27** | **Nessuna alternate key.** Nessuna entità owned dichiara `HasAlternateKey`, e nessun indice `UNIQUE (Id, OwnerId)` esiste nello schema | test di convenzione (**bloccante**): `entityType.GetKeys()` deve contenere **una sola** chiave, che è la PK. È la regola che impedisce al requisito eliminato di rientrare dalla finestra |
| **R28** | **Id generato dal dominio.** `Id` e `OwnerId` hanno `ValueGenerated == Never`; nessuna proprietà chiave ha un default SQL (`NEWID()`, `NEWSEQUENTIALID()`) né un `ValueGenerator` registrato. Ogni `Id` proviene da `IIdGenerator` | test di convenzione (**bloccante**): verifica `ValueGenerated`, assenza di `GetDefaultValueSql()` e assenza di `GetValueGeneratorFactory()` su tutte le proprietà chiave |
| **R29** | **FK composite verso la PK.** Ogni FK tra due entità owned è composita, ha **`OwnerId` come prima colonna**, e il suo `PrincipalKey` **è la PK** del principal — mai una alternate key | test di convenzione (**bloccante**): per ogni `GetForeignKeys()` verso un `IOwnedResource`, `Properties[0].Name == "OwnerId"` e `PrincipalKey.IsPrimaryKey() == true` |
| **R30** | **Nessun `SetNull` / `SetDefault`.** Nessuna FK del modello usa `DeleteBehavior.SetNull` o `SetDefault`: entrambe contano come cascade path ai fini dell'errore 1785 e rompono la dimostrazione di ADR-0004 | test di convenzione (**bloccante**). Formalizza una regola finora solo scritta in `CONTEXT.md` §5.2 |
| **R31** | **Il generatore di id è iniettato, mai istanziato.** Nessun tipo di dominio chiama `Guid.NewGuid()`, `Guid.CreateVersion7()` o `new Guid(...)` per produrre un `Id`: l'unica sorgente è `IIdGenerator` | lint / analyzer (**bloccante**): regex su `src/**/Domain/**` che vieta quelle chiamate fuori da `Roamly.Common.SequentialGuidGenerator`. **`Guid.CreateVersion7()` è vietato ovunque nella persistenza**, con il messaggio che rimanda a questo ADR |
| **R32** | **`CreatedAtUtc` fuori dalla clustering key.** La clustering key è **`(OwnerId, Id)` e nient'altro**. `CreatedAtUtc` può comparire **solo** in indici non clusterizzati, e solo se una query esistente lo usa | coperta da R26 per la parte bloccante. La seconda metà è una regola di review: **un indice nuovo va giustificato con la query che lo usa**, riferita per nome in `DATA.md` |
| **R33** | **Lo schema generato è verificato sul database reale.** Il test dello schema completo (`DATA.md` §2.2) verifica, oltre all'assenza dell'errore 1785, che ogni clausola `REFERENCES` generata elenchi le colonne **nell'ordine della PK** `(OwnerId, Id)` | integration test su database reale (**bloccante in CI, non a ogni salvataggio**): applica tutte le migration e interroga `sys.foreign_key_columns` confrontando `referenced_column_id` con l'ordine della PK. **Dipende dalla decisione #6.** Se fallisce → §*Piano B* |

**R27 è la regola che protegge il contenuto di questo ADR.** R26 e R29 descrivono la forma di oggi; R27 impedisce che tra un anno qualcuno, davanti a un errore di FK, "risolva" aggiungendo un `HasAlternateKey` e riporti dentro il requisito che questa decisione ha eliminato, senza che nessuno se ne accorga.

---

## Piano B — se EF Core 10 generasse le colonne di `REFERENCES` in un ordine diverso

Il solo punto empirico non verificato è se EF Core 10 emetta `REFERENCES Camper (OwnerId, Id)` nell'ordine della PK, oppure riordini le colonne secondo l'ordine di dichiarazione della FK. **Non blocca la decisione**, perché la verifica è R33 e vive dentro un test già previsto per l'errore 1785.

**Se R33 fallisce**, nell'ordine:

1. **Tentativo 1 — riordinare la FK.** Dichiarare `HasForeignKey(e => new { e.OwnerId, e.CamperId })` con `HasPrincipalKey(p => new { p.OwnerId, p.Id })` esplicito. Se l'ordine segue il `PrincipalKey` dichiarato, la questione è chiusa senza cambiare nulla di strutturale.
2. **Tentativo 2 — forzare l'ordine delle colonne della PK** con `HasKey(...).HasName(...)` e l'ordine di dichiarazione delle proprietà nell'entity type. Costo: una convenzione in più, nessun impatto sul modello.
3. **Fallback strutturale — `PK(Id)` NON clusterizzata + `UNIQUE CLUSTERED (OwnerId, Id)`.** Le FK referenziano il vincolo UNIQUE invece della PK. **È funzionalmente equivalente alla decisione** — stessa owner-locality, stessa clustering key a 32 byte, stesso `DELETE` locale — al prezzo di **un indice in più per tabella** (la PK non clusterizzata su `Id`). In cambio si ottiene, gratis, la garanzia di unicità globale di `Id`.

> Il fallback (3) **non è un ripiego su una soluzione peggiore in astratto**: è la stessa architettura con un indice in più. Va adottato con una riga in `ConfigureOwnedResource`, e R26/R27 vanno emendate di conseguenza con un ADR successivo, non modificando questo.

---

## Conseguenze

### Sulla dimensione del database — **la misura meno decisiva delle quattro**

> ⚠️ **Questo paragrafo va letto con la sua conclusione in mano, non con i suoi numeri.** È stato calcolato perché era stato chiesto, e il risultato è che **non deve entrare nel ragionamento decisionale**. Presentare il delta con troppa precisione rischia di farlo sembrare importante: non lo è.

Sul profilo stimato — ⚠️ assunzioni dichiarate, non dati — di ≈400 righe per utente per anno, 1000 utenti × 5 anni ≈ **2,25 milioni di righe**:

| Configurazione | Byte di chiave per riga (tabella + NCI) | Delta |
|---|---|---|
| **Decisione — `Guid`, PK `(OwnerId, Id)` clustered** | ~158 B | — |
| Opzione 2 — PK NC su `Id` + clustered 40 B + 6 AK | ~234 B (padri) / ~178 B (figli) | **+70-80 MB** |
| Opzione 3 — `bigint` IDENTITY | ~122 B | **−80 MB** |

**Il `Guid` costa circa 80 MB in più del `bigint` su 2,25 milioni di righe — cioè ~35 byte per riga.** Su Azure SQL Basic/S0 non muove il costo di un centesimo, e le righe di `Expense` e `JournalEntry` con `nvarchar` liberi domineranno la dimensione del database molto prima delle chiavi. **Chiunque decida la chiave primaria di Roamly su questo numero sta ottimizzando la cosa sbagliata.** Il numero è qui per essere archiviato, non per essere usato.

### Sulla cancellazione in ordine topologico (ADR-0004)

| Aspetto | `PK(Id)` clusterizzata su `Id` | **`PK(OwnerId, Id)` clusterizzata** |
|---|---|---|
| Individuazione delle righe | seek su NCI + **key lookup riga per riga** | **range seek contiguo** sul clustered |
| Pagine toccate | righe sparse su tutto il B-tree | poche pagine contigue |
| Verifica `COUNT(*) = 0` di **R9** | seek su NCI | seek sul clustered, praticamente gratuito |
| Indici da mantenere nel `DELETE` | clustered + N NCI + **6 alternate key** | clustered + N NCI |
| Lock | possibile escalation su pagine **condivise con altri utenti** | il range è già dell'utente → **nessun blocco su righe altrui** |

⚠️ In termini di **tempo** la differenza è irrilevante: ~2.000 righe per un utente a 5 anni, il `DELETE` completo è sotto il secondo in entrambe le forme (inferenza, non misura). **Il beneficio vero non è la velocità, è l'isolamento**: la cancellazione di un utente tocca un range di pagine suo. Per un impianto privacy che deve poter girare in qualsiasi momento senza degradare il servizio, è la proprietà giusta.

> Resta invariato ciò che ADR-0004 dichiara: **cancellare `AspNetUsers` non cancella niente** (tutte le FK verso il principal sono `NO ACTION`). Nessuna scelta di chiave cambia questo. La cancellazione è e resta applicativa.

### Sull'enumerazione degli id in URL — **l'argomento più debole, e quello più forte**

Va detto con precisione, perché sia @archimedes sia la formulazione originale della domanda davano peso alla ragione sbagliata arrivando comunque alla conclusione giusta.

**L'oracolo di enumerazione è l'argomento PIÙ DEBOLE a favore del `Guid`.** Cosa espone davvero un `int` sequenziale in `/api/v1/campers/{id}`:
- ✅ **non** viola R7: la risposta per una risorsa altrui è un `404` uniforme, indistinguibile da "non esiste";
- ⚠️ espone un **oracolo di volume**: creando due risorse a distanza di una settimana si misura quante ne sono state create nel frattempo nell'intero sistema. Informazione di business, modesta;
- ⚠️ espone un **oracolo temporale**: un id basso significa "utente vecchio".

**L'argomento PIÙ FORTE è un altro, e non compare in nessun documento precedente: un `int 42` è plausibile in ogni tabella.** Un bug che passa un `Trip.Id` dove ci si aspetta un `Camper.Id` trova una **riga valida**, e se il controllo di ownership fallisse su un solo percorso restituirebbe il dato sbagliato senza alcun sintomo. **Con un `Guid` lo stesso identico bug muore in un `404`.** Il `Guid` non previene l'errore: ne rende il fallimento rumoroso invece che silenzioso, che è l'unica cosa che conta su un progetto con un solo paio di occhi.

**Cosa il `Guid` NON compra, da dire chiaramente:** non è un controllo di sicurezza. ADR-0003 è esplicito — la protezione è il query filter + l'interceptor + il `404` uniforme. Un `Guid` finito in un log, in un referrer o in uno screenshot è esattamente esposto quanto un `int`. **Il `Guid` riduce l'informazione incidentale, non l'autorizzazione.**

⚠️ **Costo specifico del COMB, dichiarato qui invece di essere scoperto dopo:** un id sequenziale contiene l'istante di creazione in chiaro nei byte 10-15. Chi possiede due id può dedurne ordine e distanza temporale. I byte 0-7 restano 64 bit casuali, quindi il brute force resta impraticabile. Su Roamly `CreatedAtUtc` è comunque esposto nell'API e nell'export: **il timestamp non è un'informazione che l'id aggiunge.** Accettato.

### Cosa diventa più facile

Ogni query owner-scoped legge pagine contigue; `R2` è garantita dalla forma della chiave e non solo dal filtro; sei indici e una regola operativa spariscono; l'`Id` esiste alla costruzione dell'aggregato, quindi factory, test e `201 Created` si scrivono senza round-trip.

### Cosa diventa più difficile

`FindAsync` richiede due valori — mitigato da un'extension `ByIdForOwner(id)` che legge `OwnerId` da `ICurrentUser`: è un attrito del primo giorno, non quotidiano. `ORDER BY CreatedAtUtc DESC` per un owner richiede un sort o un indice dedicato.

---

## Costo di inversione

| Cambiamento | Momento | Costo |
|---|---|---|
| Adottare questa decisione | **oggi, nessuna migration** | **ZERO** — una riga di convenzione in `OnModelCreating` |
| Da `PK(Id)` a `PK(OwnerId, Id)` | dopo la prima migration, con dati | 🔴 **ALTO** — drop delle 21 FK, drop e ricreazione di ogni PK e di ogni clustered index, ricostruzione di tutti gli NCI, downtime proporzionale alla tabella più grande. **Non è una migration EF automatica** |
| Da `Guid` a `bigint` | dopo la prima migration | 🔴 **MOLTO ALTO** — riscrittura di ogni riga, ogni FK, ogni id già esposto in URL e negli export. Praticamente: si rifà il database |
| Passare al fallback §*Piano B* | prima della migration, su fallimento di R33 | 🟢 **BASSO** — una riga in `ConfigureOwnedResource` |
| Cambiare **solo** la strategia di generazione dell'`Id` | in qualsiasi momento | 🟢 **BASSO** — le righe vecchie restano, le nuove sono generate diversamente. Unica conseguenza: un salto di ordinamento e qualche page split. **È l'unico pezzo rivedibile senza dolore** |
| Aggiungere `UNIQUE(Id)` su una singola tabella | quando servisse | 🟢 **BASSO** — una migration |

**Conseguenza di sequenza: questa decisione andava chiusa prima della prima migration, e non c'era una seconda occasione.**

---

## Tradeoff negativi accettati

1. **`Id` non è unico globalmente a livello di constraint.** Lo è probabilisticamente (i 64 bit casuali del COMB, più la separazione per owner). Se servisse per un'integrazione esterna, si aggiunge un `UNIQUE(Id)` **su quella tabella**: costo di una migration, non di un ridisegno.
2. **Ogni accesso per chiave richiede due valori.** Mitigato, non eliminato.
3. **Hot page in scrittura per owner.** Con clustered su `(OwnerId, …)` tutte le scritture di un utente vanno nella stessa regione di pagine → latch contention **per utente** a molti scrittori concorrenti. Irrilevante al profilo di Roamly, **reale in assoluto**: è il vero costo di questa scelta, e nessun documento lo nominava.
4. **~80 MB in più rispetto a `bigint`** su 2,25 M righe. Accettato consapevolmente, e vedi l'avvertenza sopra sul peso da dare a questo numero.
5. **Il COMB rivela l'istante di creazione.** Accettato: `CreatedAtUtc` è già esposto.
6. **Chiavi larghe nei join.** Join poco profondi (2-3 livelli) su volumi minuscoli. Accettato.
7. **`ORDER BY CreatedAtUtc` non gratuito** dove non c'è indice. Accettato: l'indice si aggiunge quando la query esiste, non prima.
8. **Unicità globale e owner-locality sono in tensione.** È stata scelta la seconda. Se emergesse un requisito forte di unicità globale, la risposta è il fallback §*Piano B*, non l'Opzione 2.

---

## Trigger di revisione pre-approvati

| # | Trigger | Azione |
|---|---|---|
| **T1** | Compare una **risorsa condivisa tra utenti** (community, Phase 6: `PublicPlace`, viaggi pubblici) | La PK composita **non si applica** a entità non-owned: avranno `PK(Id)` semplice, deciso lì. **Non invalida questa decisione**, e R26 si applica solo a `IOwnedResource` |
| **T2** | Un'entità owned supera **10⁶ righe per singolo utente** | Rimisurare con `sys.dm_db_index_physical_stats`: a quella scala la frammentazione dentro la regione dell'owner diventa visibile |
| **T3** | Si valuta una **migrazione a PostgreSQL** | L'ordinamento di `uuid` in PostgreSQL è bytewise: lì il COMB diventa **controproducente** e `Guid.CreateVersion7()` diventa la scelta giusta. Cambio di generatore, costo BASSO |
| **T4** | Si introduce **replica read-only, sharding o multi-tenant fisico** | `OwnerId` come prima colonna diventa anche la shard key: la decisione **si rafforza** |
| **T5** | `AccountErasureJob` supera **5 secondi** o causa lock escalation misurata | Riaprire con batch `DELETE TOP (n)`. **Non prima** |
| **T6** | **R33 fallisce** sul database reale | §*Piano B*, nell'ordine indicato |
| **T7** | EF Core introduce value generation per convenzione sulle **chiavi composite** | Semplificare R28. Cosmetico |
| **T8** | Serve un `Id` **globalmente univoco** per un'integrazione esterna | `UNIQUE(Id)` sulla singola tabella interessata, oppure il fallback §*Piano B* se il requisito è generale |

---

## Cosa NON si fa

| Non si fa | Perché | Trigger che lo rende obbligatorio |
|---|---|---|
| **`UNIQUE (Id, OwnerId)` su ogni tabella-padre** | non serve più: le FK referenziano la PK. **Vietato da R27** | mai, per come è formulato. Il caso singolo è T8 |
| **`UNIQUE(Id)` globale** | sarebbe esattamente l'indice che questa decisione elimina | T8 |
| **`CreatedAtUtc` nella clustering key** | le query reali ordinano per campi di **dominio**, non per l'audit field | mai: si aggiunge un NCI, non si tocca la clustering key |
| **`NEWSEQUENTIALID()`** | id non noto pre-insert, incompatibile con PK composita, **avviso privacy esplicito di Microsoft** | mai |
| **`Guid.CreateVersion7()` in `uniqueidentifier`** | non è sequenziale nell'ordinamento di SQL Server | T3 (PostgreSQL), dove diventa la scelta **giusta** |
| **`binary(16)` con v7 big-endian** | `ValueConverter` con inversione di byte su ogni chiave e ogni FK; un errore produce un bug di ordinamento silenzioso | T2 + T3 insieme |
| **Benchmark di frammentazione prima della migration** | nessuna affermazione su cui poggia la decisione richiede una misura: sono tutte documentate o leggibili nel sorgente | T2 |

---

## Checklist di propagazione — file per file

> **@oracle non ha modificato nessun file oltre a questo ADR.** La propagazione è a carico dell'utente, e @argus sta lavorando in parallelo su altri documenti.

| File | Sezione | Modifica |
|---|---|---|
| **`architecture/CONTEXT.md`** | **§3.8** | 🔴 → ✅ **chiusa.** Sostituire con l'esito + link a questo ADR. **Mantenere la ❌ su GUID v7 — è confermata — correggendone la causa**: è l'ordinamento di confronto di `uniqueidentifier` in SQL Server, **non** il layout di `System.Guid`. Nessuna versione futura di .NET lo risolverà |
| **`architecture/CONTEXT.md`** | **§2.0** | riga `Id`: *"PK — tipo non ancora deciso 🔴"* → **"parte della PK composita `(OwnerId, Id)` CLUSTERED, `Guid` sequenziale generato dal dominio"**. **Eliminare la riga "Chiave alternativa"** e il box ⚠️ sotto la tabella |
| **`architecture/CONTEXT.md`** | **§5.3** | 🔴 **sezione da eliminare**, o da riscrivere come *"requisito superato dalla PK composita"* con la cronistoria. Sparisce anche la **regola operativa** finale sulle alternate key |
| `architecture/CONTEXT.md` | §5.1 | **nessuna modifica alle 21 FK.** Aggiungere una nota: le FK referenziano la **PK**, non una alternate key |
| `architecture/CONTEXT.md` | §5.2 | la riga su `SET NULL` diventa **R30**, con verificatore |
| `architecture/CONTEXT.md` | §2.6 / §3.11 | aggiungere **R33** al test dello schema completo (ordine delle colonne in `REFERENCES`) |
| `architecture/CONTEXT.md` | §4 glossario | aggiungere: **COMB**, **clustering key**, **`IIdGenerator`** |
| **`architecture/DATA.md`** | §2 | aggiungere alle regole di persistenza: **PK composita `(OwnerId, Id)` clusterizzata su ogni entità owned**; **nessuna alternate key**. **Riscrivere il terzo bullet** di *"Regole introdotte dal domain model"* (⚠️ alternate key) come **risolto** |
| `architecture/DATA.md` | §2.1 | l'indice `(OwnerId, Latitude, Longitude)` resta valido; nota: **ora è un NCI con clustering key da 32 byte** |
| `architecture/DATA.md` | §2.2 | aggiungere il caso **R33** al test dello schema completo, accanto al 1785 |
| `architecture/DATA.md` | §6 | grafo di cancellazione: aggiungere che il `DELETE` per owner è un **range seek locale**, e che il beneficio è l'**isolamento**, non la velocità |
| `architecture/DATA.md` | **§7 (nuova)** | **gli indici non clusterizzati** della tabella in §*Configurazione EF Core → 5*, con la query che ciascuno serve |
| **`architecture/TESTING.md`** | verificatori | aggiungere **R26-R32** ai test di convenzione bloccanti (famiglia R1-R25) e **R33** agli integration test |
| `architecture/TESTING.md` | §4 | rafforzare la nota sulla **#6**: anche R33 richiede un database reale. **Non aggiunge un prerequisito** — si appoggia a quello già dichiarato da `DATA.md` §2.2 |
| `architecture/API-CONVENTIONS.md` | id in URL | `{id}` è un `Guid`; la lookup è **sempre** `(OwnerId, Id)`; `404` uniforme invariato |
| **`adr/0003-auth-and-ownership.md`** | **R4** | **non si modifica l'ADR** (`ADR-FORMAT.md`): va letto insieme a questo. Emendamento: le FK composite referenziano la **PK composita**, non una alternate key. **R4 si rafforza** — non esiste accesso per chiave senza `OwnerId` |
| **`adr/0004-privacy-and-erasure.md`** | *Sul tipo della chiave primaria* / R9 | **non si modifica**: questo ADR **chiude** il rinvio. Nota: la cancellazione per owner è un'operazione **locale** sul clustered; la topologia delle FK e la dimostrazione anti-1785 restano valide **integralmente** |
| `adr/0001-use-sql-server.md` | emendamenti | nota: la forma della chiave **dipende dall'ordinamento di `uniqueidentifier` di SQL Server** → **nuovo trigger di riesame** in caso di cambio di database (**T3**) |
| **`OPEN-DECISIONS.md`** | gap *"Tipo della chiave primaria"* | ⬜ → ✅ **chiuso**, con esito sintetico e link a questo ADR |
| **`OPEN-DECISIONS.md`** | gap *"`UNIQUE (Id, OwnerId)` su ogni tabella-padre"* | ⬜ *"da quantificare"* → ✅ **chiuso: il requisito non esiste più.** Non si quantifica ciò che non si paga |
| `OPEN-DECISIONS.md` | decisione **#6** (DB di test) | nota di sequenza: **R33** è un'ulteriore verifica che richiede il DB reale, **senza aggiungere un prerequisito** |
| `OPEN-DECISIONS.md` | rischi | aggiungere ✅ *"Rework sulla forma della chiave"* → mitigato |

---

## Riferimenti

Documenti impattati: `architecture/CONTEXT.md` (§2.0, §2.6, §3.8, §3.11, §4, §5.1, §5.2, §5.3), `architecture/DATA.md` (§2, §2.1, §2.2, §6, §7 nuova), `architecture/TESTING.md` (§4, verificatori), `architecture/API-CONVENTIONS.md`, `OPEN-DECISIONS.md`.

ADR correlati: [`0001`](0001-use-sql-server.md) (SQL Server, e il suo ordinamento di `uniqueidentifier`), [`0003`](0003-auth-and-ownership.md) (R1-R7, FK composite, `404` uniforme), [`0004`](0004-privacy-and-erasure.md) (R8-R10, topologia delle FK, errore 1785), [`0007`](0007-design-system.md) (R11-R25, continuità della numerazione).

Fonti esterne citate: RFC 9562 §5.7 · Microsoft Learn (`NEWSEQUENTIALID`, `System.Guid.CreateVersion7`, *SQL Server Value Generation*, *Clustered and Nonclustered Indexes Described*, `CREATE TABLE`/`FOREIGN KEY`, EF Core 10 What's New) · `dotnet/efcore` `src/EFCore/ValueGeneration/SequentialGuidValueGenerator.cs` (`main`) · `dotnet/SqlClient` discussion **#2999** · SQLServerScience / Brent Ozar *How SQL Server Stores a GUID* · DBA.SE *GUID v4 vs GUID v7 as SQL Server PK* · Conrad Akunga *GuidV7 Considerations for SQL Server* · gist `sdrapkin/dotnet-guid-sequential-key-sql` (⚠️ evidenza di terzi, non misura di Roamly).
