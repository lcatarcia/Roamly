---
name: tdd
description: Test-driven development for Roamly. Use when the user wants to build features or fix bugs test-first, mentions red-green-refactor, needs xUnit or Vitest coverage, or wants integration tests for vertical slices.
---

# Test-Driven Development

## Philosophy

**Core principle**: tests verify behavior through public interfaces, not implementation details. In Roamly, the most valuable tests exercise vertical slices, HTTP endpoints, domain services, validators, or React user flows exactly as callers use them. The implementation can move between handler, validator, EF Core query, and helper classes; the test should keep describing the same capability.

**Good tests** are integration-style where practical. Backend tests should prefer an ASP.NET Core test host, a real DI container, EF Core with a test database provider, and requests that pass through the same Command/Query + Handler + Validator path used in production. Frontend tests should prefer Testing Library interactions over component internals. They describe what Roamly does: "a traveller can schedule a maintenance reminder for a camper", not "CreateMaintenanceHandler calls repository.Save".

**Bad tests** are coupled to implementation. They mock internal collaborators, test private methods, assert on handler call order, or query tables directly when a public API can verify the outcome. The warning sign: the test fails after an internal refactor although the behavior is unchanged.

See [tests.md](tests.md) for examples and [mocking.md](mocking.md) for mocking guidelines.

## Roamly testing surface

Prefer the highest seam that is still fast and deterministic:

1. **Backend vertical slice**: HTTP request through ASP.NET Core test host into the feature slice.
2. **Application slice**: Command/Query sent through the MediatR-style dispatcher with real validators and handler dependencies.
3. **Domain object or service**: direct public method when behavior is pure and meaningful without infrastructure.
4. **Frontend user flow**: React Testing Library for component behavior; Playwright for browser-level journeys.

Use lower seams only when the higher seam is too slow, unavailable, or unable to isolate the behavior.

## Anti-pattern: horizontal slices

**Do not write all tests first, then all implementation.** This is horizontal slicing: treating RED as "write all tests" and GREEN as "write all code".

This produces weak tests:

- Tests written in bulk describe imagined behavior rather than learned behavior.
- They lock in request/response shapes before the feature is understood.
- They become insensitive to real regressions and oversensitive to refactors.
- They encourage implementing a pile of code before any feedback arrives.

**Correct approach**: vertical tracer bullets. One behavior test -> one minimal implementation -> repeat. Each cycle responds to what the previous cycle taught you.

```text
Wrong:
  RED:   all Trip tests, all Expense tests, all validation tests
  GREEN: implement everything at once

Right:
  RED/GREEN: traveller can create a Trip with one TripStop
  RED/GREEN: invalid TripStop dates are rejected
  RED/GREEN: created Trip appears in the itinerary query
```

## Workflow

### 1. Planning

Before writing code:

- [ ] Read `docs/architecture/CONTEXT.md` and relevant ADRs.
- [ ] Confirm the public interface: endpoint, command/query, UI route, or domain method.
- [ ] List behaviors in user language, not implementation steps.
- [ ] Prioritize critical paths, invariants, and regression risks.
- [ ] Identify the right seam for each behavior.
- [ ] Decide whether the change is LOW, MEDIUM, or HIGH severity.

You cannot test everything. Spend coverage on Roamly behavior that matters: camper ownership, trip dates and ordering, expense totals, maintenance due rules, document access, checklist completion, and authorization boundaries.

### 2. Tracer bullet

Write one failing test for one behavior.

```csharp
[Fact]
public async Task Traveller_can_create_trip_with_first_stop()
{
    var app = new RoamlyApiFactory();
    var client = app.CreateAuthenticatedClient(userId: TestUsers.Lorenzo);

    var response = await client.PostAsJsonAsync("/api/trips", new
    {
        camperId = TestCampers.VanOne,
        title = "Dolomites weekend",
        startsOn = "2026-10-02",
        stops = new[]
        {
            new { placeId = TestPlaces.Cortina, arrivesOn = "2026-10-02" }
        }
    });

    response.StatusCode.Should().Be(HttpStatusCode.Created);

    var itinerary = await client.GetFromJsonAsync<TripDetailsResponse>(
        response.Headers.Location!);

    itinerary!.Title.Should().Be("Dolomites weekend");
    itinerary.Stops.Should().ContainSingle(s => s.PlaceName == "Cortina");
}
```

Then write the minimum vertical slice to pass: request, validator, handler, persistence, response mapping, and endpoint registration only as needed for this behavior.

### 3. Incremental loop

For each remaining behavior:

```text
RED:   Write the next behavior test -> it fails for the right reason.
GREEN: Add the smallest production change -> it passes.
REPEAT: Move to the next behavior.
```

Rules:

- One behavior at a time.
- Keep the test at the same public seam where possible.
- Do not anticipate future validators, fields, endpoints, or UI states.
- Keep assertions focused on observable outcomes.
- Prefer builders/fixtures that speak Roamly language over generic test setup.

### 4. Refactor

After all tests pass, improve the design:

- [ ] Remove duplication in tests and production code.
- [ ] Deepen modules: move complexity behind smaller interfaces.
- [ ] Simplify validators and request/response contracts.
- [ ] Revisit EF Core queries for N+1 risks or over-fetching.
- [ ] Run tests after each meaningful refactor.

**Never refactor while RED.** Get to GREEN first.

## Roamly agent alignment

Testing is owned by **Argus**. Implementation concerns discovered while testing are shared with **Solomon**; architecture or seam problems go to **Archimedes**; data issues go to **Oracle**; final production readiness goes through **Janus**.

For MEDIUM or HIGH decisions, use the orchestrator's **Decision Required** format with the user before proceeding. Examples: changing the test database strategy, introducing a new test container dependency, changing slice boundaries, or altering authorization behavior. LOW decisions such as naming, local fixture cleanup, or adding an obvious missing edge case can be handled directly.

## Checklist per cycle

```text
[ ] Test describes Roamly behavior, not handler internals.
[ ] Test crosses a public seam.
[ ] Test can fail for the specific behavior under development.
[ ] Production code is minimal for this test.
[ ] No speculative endpoint fields, services, or abstractions were added.
[ ] Relevant backend/frontend tests pass.
```