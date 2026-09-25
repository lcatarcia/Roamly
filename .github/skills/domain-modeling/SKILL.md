---
name: domain-modeling
description: Build and sharpen Roamly's domain model. Use when the user wants to pin down domain terminology, update docs/architecture/CONTEXT.md, record an ADR, clarify bounded contexts, or when another skill needs domain vocabulary.
---

# Domain Modeling

Actively build and sharpen Roamly's domain model. Challenge terms, stress-test them with scenarios, and write down glossary entries and decisions as soon as they crystallize.

## File structure

```text
/
|-- docs/
|   |-- architecture/
|   |   `-- CONTEXT.md          # Roamly ubiquitous language and module context
|   `-- adr/                    # architectural decisions
|       |-- 0001-...md
|       `-- 0002-...md
```

Roamly's canonical domain model document is `docs/architecture/CONTEXT.md`. Do not create a root `CONTEXT.md` unless the project structure is explicitly changed by the user.

## During sessions

- **Challenge against the glossary**: when the user uses a term that conflicts with `docs/architecture/CONTEXT.md`, call it out.
- **Sharpen fuzzy language**: replace vague words like "item", "record", "thing", or "entry" with canonical terms such as `Camper`, `Trip`, `TripStop`, `MaintenanceItem`, `Document`, `Expense`, `Checklist`, `Place`, or `JournalEntry`.
- **Update the context document inline**: when a term is resolved and belongs in the domain, update `docs/architecture/CONTEXT.md` immediately.
- **Offer ADRs sparingly**: create ADRs only for decisions that are hard to reverse, surprising without context, and the result of a real trade-off.
- **Separate glossary from specification**: the context document names concepts and relationships; it is not a scratch pad, requirements doc, or implementation design.

## Roamly domain discipline

- Use business language before technical language.
- Define what a term is, not the database table that stores it.
- Prefer stable module terms over UI labels when they differ.
- Record avoid-list synonyms so future agents do not reintroduce ambiguity.
- Keep module ownership visible: Identity, Campers, Maintenance, Documents, Trips, Places, Expenses, Checklists, Journal, and Intelligence.
- When a term crosses modules, document ownership and reference rules.

## Roamly agent alignment

Domain modeling and architectural decisions are owned by **Archimedes**. Implementation uses **Solomon**, data implications involve **Oracle**, API naming involves **Hermes**, and final consistency is checked by **Janus**.

Any MEDIUM or HIGH modeling decision requires the orchestrator's **Decision Required** consult format with the user. Examples: splitting a bounded context, changing aggregate ownership, introducing domain events, changing identity/authorization terminology, or choosing between competing module boundaries.

Use [ADR-FORMAT.md](ADR-FORMAT.md) for decisions and [CONTEXT-FORMAT.md](CONTEXT-FORMAT.md) for glossary structure.