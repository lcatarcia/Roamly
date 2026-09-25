# Formato e processo ADR

Stato: attivo · Ultimo aggiornamento: 2026-09-24

---

## Quando si scrive un ADR

Si scrive un ADR quando una decisione:

- è difficile o costosa da invertire;
- vincola più di una feature o più di un layer;
- ha severità **MEDIUM** o **HIGH** secondo la DECISION SEVERITY dell'orchestratore;
- rifiuta un'alternativa ragionevole (il rifiuto è il contenuto informativo).

Non si scrive un ADR per scelte locali a una slice.

---

## Processo

1. La decisione viene aperta in `docs/OPEN-DECISIONS.md`.
2. L'agente owner presenta le opzioni all'utente nel formato **Decision Required** (opzioni + raccomandazione + tradeoff).
3. L'utente decide. Nessun agente chiude da solo una decisione MEDIUM/HIGH.
4. L'owner scrive l'ADR, aggiorna il documento di riferimento in `docs/` e segna la decisione come chiusa.
5. @janus verifica la coerenza tra ADR e documentazione.

Un ADR **non si modifica** dopo l'accettazione: si **supera** con un nuovo ADR che dichiara `Supersedes: ADR-000X`, e il vecchio passa a `Superseded by: ADR-000Y`.

---

## Numerazione

`NNNN-titolo-in-kebab-case.md`, numerazione progressiva, mai riusata.

---

## Template

```markdown
# ADR-NNNN — Titolo

- **Stato:** Proposto | Accettato | Superato da ADR-NNNN
- **Data:** YYYY-MM-DD
- **Owner:** @agente
- **Decisore:** utente
- **Severità:** LOW | MEDIUM | HIGH

## Contesto

Qual è il problema, quali vincoli reali esistono (competenze, budget, tempo, licenze).

## Opzioni considerate

### Opzione A — ...
Pro / Contro.

### Opzione B — ...
Pro / Contro.

## Decisione

Cosa è stato scelto, in una frase.

## Motivazione

Perché. Includere i tradeoff **sfavorevoli** accettati: un ADR che presenta la scelta come priva di svantaggi non è utile.

## Conseguenze

Cosa diventa più facile, cosa diventa più difficile, cosa va rivisto se la decisione cambia.

## Riferimenti

Documenti in `docs/` impattati e aggiornati.
```

---

## ADR previsti

| File | Copre | Decisione |
|---|---|---|
| `0001-use-sql-server.md` | SQL Server + libreria spatial, con rifiuto motivato di PostGIS | #1 |
| `0002-modular-monolith.md` | Modular monolith + Vertical Slice + CQRS logico | — |
| `0003-auth-and-ownership.md` | Autenticazione e regola owner-scoped | #2 |
| `0004-api-conventions.md` | Error model, paginazione, versioning, concorrenza | — |
| `0005-geo-data-strategy.md` | Provider routing e fonti dati geografiche | #3 |
