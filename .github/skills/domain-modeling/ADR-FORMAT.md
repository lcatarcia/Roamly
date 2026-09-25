# ADR Format

ADRs live in `docs/adr/` and use sequential numbering: `0001-slug.md`, `0002-slug.md`, and so on.

Create the `docs/adr/` directory lazily only when the first ADR is needed. Do not create ADRs just to document ordinary implementation details.

## Template

```md
# {Short title of the decision}

{1-3 sentences: what is the context, what did we decide, and why.}
```

That is enough for many ADRs. The value is recording that a decision was made and why, not filling sections for ceremony.

## Optional sections

Include these only when they add genuine value:

- **Status** frontmatter: `proposed`, `accepted`, `deprecated`, or `superseded by ADR-NNNN`.
- **Considered Options**: when rejected alternatives are worth remembering.
- **Consequences**: when downstream effects are non-obvious.
- **Links**: GitHub Issues, PRs, or related ADRs that explain the decision context.

## Numbering

Scan `docs/adr/` for the highest existing number and increment by one. Keep numbers stable; do not renumber old ADRs.

## When to offer an ADR

All three conditions must be true:

1. **Hard to reverse**: changing later would be meaningful work.
2. **Surprising without context**: a future engineer would ask why this path was chosen.
3. **Real trade-off**: there were genuine alternatives and one was selected for specific reasons.

If a decision is easy to reverse, skip it. If it is obvious, skip it. If there was no real alternative, the code or docs can speak for themselves.

## What qualifies in Roamly

- **Architectural shape**: modular monolith boundaries, vertical slice conventions, CQRS style, or where cross-cutting behavior lives.
- **Module ownership**: for example, whether `Place` is owned by Places and referenced by Trips, or embedded inside trip planning.
- **Integration patterns between modules**: direct query, domain event, background job, or API boundary.
- **Technology choices with lock-in**: SQL Server, EF Core migration strategy, auth provider, document storage, queue, hosting model, or AI provider.
- **Boundary and scope decisions**: explicit no-s are valuable, such as "Expenses reference Trips by ID but do not own itinerary state".
- **Deliberate deviations**: anything a future maintainer might "fix" back to the default unless the reason is recorded.
- **Constraints not visible in code**: compliance, performance targets, offline assumptions, cost limits, or partner API contracts.
- **Rejected alternatives when non-obvious**: for example, choosing REST + OpenAPI over GraphQL for a specific Roamly reason.

## Orchestrator severity

Many ADR-worthy decisions are MEDIUM or HIGH severity. Before accepting them, use the orchestrator's **Decision Required** format with 4+ options and a recommendation. The ADR records the approved decision after the user chooses.