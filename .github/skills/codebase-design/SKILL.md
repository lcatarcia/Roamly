---
name: codebase-design
description: Roamly vocabulary for designing deep modules and clean seams. Use when designing or improving module interfaces, vertical slice boundaries, test seams, domain/API/data seams, or when another skill needs deep-module language.
---

# Codebase Design

Design **deep modules**: a lot of behavior behind a small interface, placed at a clean seam, testable through that interface.

In Roamly, a module can be a vertical slice, domain service, EF Core adapter, API endpoint group, React route, TanStack Query hook, or a whole bounded module such as Trips, Campers, Maintenance, Documents, Places, Expenses, Checklists, Journal, Intelligence, or Identity.

## Glossary

- **Module**: anything with an interface and an implementation: class, function, feature slice, package, UI component, or module boundary.
- **Interface**: everything a caller must know to use the module: signature, request/response shape, invariants, error modes, authorization assumptions, performance expectations, and ordering guarantees.
- **Depth**: leverage at the interface: how much useful behavior callers get per concept they must learn.
- **Seam**: a place where behavior can change without editing callers. In Roamly this is often an endpoint contract, command/query, domain method, port, or UI component boundary.
- **Adapter**: concrete implementation that satisfies an interface at a seam: EF Core repository/query, HTTP client, file storage, map provider, in-memory fake, or frontend API client.
- **Leverage**: the amount of policy, validation, orchestration, or persistence hidden behind a small concept.

## Deep vs shallow

**Deep module** = small interface + substantial behavior behind it. Good.

Example: `CreateTripCommand` accepts the traveller's intent once, validates dates and camper ownership, persists the Trip and TripStops, and returns a stable response.

**Shallow module** = large interface + thin pass-through implementation. Avoid.

Example: `TripService`, `TripValidatorService`, `TripRepository`, and `TripMapper` each exposing nearly the same fields while only forwarding data to the next layer.

When designing an interface, ask:

- Can I reduce the number of entry points?
- Can I simplify parameters into Roamly concepts such as `TripId`, `CamperId`, `DateRange`, or `Money`?
- Can I hide ordering, validation, persistence, retries, or mapping inside?
- Can callers verify outcomes without knowing EF Core, SQL, or internal handler structure?

## Principles

- **The interface is the test surface.** Callers and tests should cross the same seam.
- **One adapter means a hypothetical seam. Two adapters means a real one.** Do not add ports just to satisfy a pattern.
- **Accept dependencies, do not create them.** Testable design uses dependency injection at real seams.
- **Return results, do not hide outcomes in side effects.** Make success, validation failures, and IDs visible.
- **Keep vertical slices cohesive.** Do not split a feature across generic services unless reuse is real.
- **Name with the domain model.** Prefer terms from `docs/architecture/CONTEXT.md`.

## Roamly agent alignment

Codebase design is owned by **Archimedes**. Implementation follows through **Solomon**; EF Core/data trade-offs involve **Oracle**; API contracts involve **Hermes**; final production readiness is validated by **Janus**.

Any MEDIUM or HIGH design choice requires the orchestrator's **Decision Required** consult format with the user. Examples: changing module boundaries, introducing a new port, adopting a library, moving behavior between backend and frontend, changing persistence strategy, or reshaping public API contracts. LOW design cleanup can proceed directly when it preserves behavior.