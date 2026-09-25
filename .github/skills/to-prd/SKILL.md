---
name: to-prd
description: "Turn the current Roamly conversation into a GitHub Issues PRD with decisions, testing seams, and ready-for-agent scope."
---

# To PRD
> User-invoked skill: run it only when the user explicitly asks for it.

This skill turns the current conversation context and codebase understanding into a product requirements document for Roamly. Do not interview the user again just to fill a template. Synthesize what is already known, identify the remaining explicit decisions, and publish the result as a GitHub Issue when the user wants it tracked.

Roamly context to preserve:

- Backend: C#, ASP.NET Core 10, EF Core, Microsoft SQL Server, REST/OpenAPI, FluentValidation, xUnit.
- Architecture: modular monolith with Vertical Slice Architecture and CQRS. A feature slice normally colocates command/query, handler, validator, response/DTO, endpoint wiring, and tests inside the owning module.
- Frontend: React, Vite, TypeScript, React Router, TanStack Query, Vitest/Testing Library, Playwright.
- Domains: Identity, Campers, Maintenance, Documents, Trips, Places, Expenses, Checklists, Journal, Intelligence.
- Docs: `docs/architecture`, `docs/product`, `docs/adr`, especially `docs/architecture/CONTEXT.md` for domain language.
- Delivery: GitHub Issues, GitHub Actions, Docker.

Apply the Roamly orchestrator rules from `AGENTS.md`: Decision Gate, Decision Severity, human-in-the-loop, and Janus as final production-readiness gate. If the PRD requires choosing between multiple valid MEDIUM or HIGH strategies, record the options and the recommended one; do not pretend the choice has already been made.

## Process

### 1. Gather existing context

Work from the current conversation, linked GitHub Issues, PRD fragments, prototypes, ADRs, and the codebase state you have already inspected. If the user gives an issue number or URL, read its body and comments.

Use Roamly's domain vocabulary. Prefer `Camper`, `Trip`, `TripStop`, `MaintenanceItem`, `Expense`, `Checklist`, `Place`, and `JournalEntry` over generic examples. Respect ADRs in the affected area; if the PRD would contradict an ADR, call that out as a decision that must be revisited.

### 2. Identify the implementation seam

Sketch the highest useful testing seam for the feature before writing the PRD. Existing seams are preferred to new ones. The fewer seams across the codebase, the better; one vertical-slice seam is usually ideal.

For Roamly, good seams are usually:

- A complete vertical slice exposed through a REST endpoint and verified through integration tests.
- A CQRS handler plus validator when the UI/API layer is not relevant to the requirement.
- A React route or flow tested through user-visible behavior when the change is frontend-only.
- A domain module interface only when there are multiple real adapters, not merely because mocking would be convenient.

Examples:

- `POST /api/trips/{tripId}/stops` creates a `TripStop`, validates route dates, updates the trip timeline, and is asserted through API/integration tests.
- A `RecordMaintenanceCompletion` command updates a `MaintenanceItem`, records expense data when present, and is asserted through handler tests plus persistence verification.
- A camper documents screen uses TanStack Query data and is asserted through Testing Library behavior, not component internals.

If several testing seams are plausible and the tradeoff is meaningful, apply the Decision Gate: present options, mark one RECOMMENDED, and wait for the user's choice before publishing.

### 3. Write the PRD

Use the template below. Keep it concrete and durable. Avoid file paths and code snippets that will go stale. Name modules, domain concepts, interfaces, commands, queries, endpoints, and behavior.

Exception: if a prototype produced a small snippet that encodes a decision more precisely than prose can, inline only the decision-rich part. Good examples are a state machine, reducer, schema shape, or validation rule. Note that it came from a prototype.

### 4. Publish to GitHub Issues

Create a GitHub Issue using the PRD title and body. Apply `ready-for-agent` when the PRD is complete enough for an implementation agent. Also apply exactly one category label: `enhancement` for new behavior or `bug` for a defect-focused PRD.

Do not create local tracker files. GitHub Issues is the issue tracker for Roamly.

## PRD template

```markdown
## Problem Statement

The problem from the user's perspective. Explain the current pain in Roamly terms: trip planning friction, camper maintenance uncertainty, missing document visibility, expense tracking gaps, checklist reliability, and so on.

## Solution

The desired user-facing outcome. Describe behavior, not implementation mechanics.

## User Stories

A numbered list of user stories. Be extensive enough to cover the feature surface.

1. As a <Roamly actor>, I want <capability>, so that <benefit>.

## Implementation Decisions

- Modules that will be built or modified.
- CQRS commands/queries or handlers that are expected to exist.
- REST/OpenAPI contract expectations.
- EF Core/data model decisions, including migration expectations.
- Frontend route, state, and TanStack Query behavior where relevant.
- Validation, authorization, and security decisions.
- Architectural tradeoffs and Decision Gate outcomes.

Do not include brittle file paths or line numbers.

## Testing Decisions

- The highest testing seam selected and why.
- Which behavior will be covered by xUnit integration/unit tests, Vitest/Testing Library tests, or Playwright E2E tests.
- Relevant prior tests or existing patterns in the codebase.
- What must not be tested because it is an implementation detail.

## Out of Scope

Explicitly list adjacent behavior that should not be built in this PRD.

## Further Notes

Open risks, assumptions, follow-up ADRs, Janus quality-gate expectations, or links to prototypes and related issues.
```
