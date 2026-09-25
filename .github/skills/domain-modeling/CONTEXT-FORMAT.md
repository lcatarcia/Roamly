# CONTEXT.md Format

Roamly's domain model document lives at `docs/architecture/CONTEXT.md`.

## Structure

```md
# Roamly Domain Context

Roamly is a camper and travel management webapp for planning trips, managing campers, tracking maintenance and documents, recording expenses, and keeping travel checklists and journal entries.

## Language

### Trips

**Trip**:
A planned or completed journey made with a camper. A Trip groups itinerary, stops, expenses, checklist items, and journal entries for a travel period.
_Avoid_: Travel, route, vacation record

**TripStop**:
A scheduled visit to a Place within a Trip, with ordering and arrival/departure information.
_Avoid_: Leg, waypoint, destination row

### Campers

**Camper**:
A recreational vehicle managed in Roamly, including its maintenance, documents, and travel usage.
_Avoid_: Vehicle, van, RV record
```

## Rules

- **Be opinionated.** Pick one canonical term and list alternatives under `_Avoid_`.
- **Keep definitions tight.** One or two sentences. Define what the concept is, not every operation it supports.
- **Only include Roamly domain terms.** General programming concepts, library names, error types, and utility patterns do not belong.
- **Group terms under module headings** when useful: Identity, Campers, Maintenance, Documents, Trips, Places, Expenses, Checklists, Journal, Intelligence.
- **Record ownership.** If a term belongs to one module but is referenced by another, say so.
- **Avoid implementation leakage.** Do not define a concept as a table, DTO, React component, or endpoint.
- **Update immediately when resolved.** Do not leave domain decisions only in chat history.

## Single vs multi-context

Roamly currently keeps the canonical domain model in one document: `docs/architecture/CONTEXT.md`. If the project later grows into multiple bounded context documents, add a context map only after an approved architecture decision.

Possible future structure:

```md
# Context Map

## Contexts

- [Campers](./campers/CONTEXT.md): owns Camper, maintenance, and camper documents.
- [Trips](./trips/CONTEXT.md): owns Trip, TripStop, itinerary planning, and trip journal links.
- [Expenses](./expenses/CONTEXT.md): owns Expense and cost reporting.

## Relationships

- **Trips -> Campers**: Trips reference a Camper by ID; Campers do not own itinerary state.
- **Trips -> Places**: Trips reference Places for stops; Places owns canonical place data.
- **Expenses -> Trips**: Expenses may reference a Trip but do not change itinerary state.
```

Do not introduce this split automatically. A multi-context split is at least MEDIUM severity and requires the orchestrator Decision Required consult.