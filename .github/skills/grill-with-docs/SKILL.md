---
name: grill-with-docs
description: "Run a focused Roamly design interview that sharpens a plan while updating domain docs and ADRs when decisions land."
---

# Grill With Docs
> User-invoked skill: run it only when the user explicitly asks for it.

Run a relentless but collaborative design interview for a Roamly idea, and update durable docs as decisions land.

Use this when a plan is too fuzzy for a PRD, issue, or implementation brief. The output is sharper requirements, clearer domain language, and, when needed, updates to `docs/architecture/CONTEXT.md` or `docs/adr`.

## Ground rules

- Ask one question at a time.
- Prefer concrete examples over abstractions.
- Use Roamly domain nouns: Camper, Trip, TripStop, MaintenanceItem, Expense, Checklist, Place, JournalEntry, Document, Intelligence insight.
- Respect the orchestrator Decision Gate from `AGENTS.md`.
- Do not choose between multiple valid MEDIUM/HIGH strategies without presenting options and a RECOMMENDED path.
- Update docs only when a durable decision has landed.
- If the discussion exposes production-readiness risk, flag it for Janus.

## Interview loop

1. Restate the current understanding in one paragraph.
2. Ask the single most load-bearing question.
3. Incorporate the answer.
4. If the answer creates a new domain term, update or propose an update to `docs/architecture/CONTEXT.md`.
5. If the answer creates an architectural decision, offer an ADR.
6. Repeat until the plan is clear enough for `to-prd`, `to-issues`, `prototype`, or implementation.

## Good questions

- Who owns this data: the user, a Camper, a Trip, or another aggregate?
- What is the smallest user-visible behavior that proves this works?
- What should happen when the Trip is archived, cancelled, or partially planned?
- Is this a backend invariant, a frontend convenience, or both?
- Is the rule enforced by validation, authorization, persistence constraints, or workflow?
- Does this need a new module seam, or does an existing vertical slice seam cover it?
- What would make Janus reject this as not production-ready?

## Documentation side effects

### Domain model

Update `docs/architecture/CONTEXT.md` when the conversation defines, renames, or sharpens a durable concept.

Examples:

- Defining the difference between a `Place` and a `TripStop`.
- Clarifying whether a `MaintenanceItem` is scheduled, completed, or both.
- Naming an `Expense` reimbursement concept.

### ADRs

Offer an ADR when the decision is durable and future agents would otherwise re-open it.

Good ADR triggers:

- Choosing SQL Server persistence shape for a module.
- Choosing backend-owned versus frontend-owned validation for an invariant.
- Rejecting a public sharing model for Trips.
- Choosing an integration boundary for Intelligence features.

Bad ADR triggers:

- Temporary priority.
- Minor naming preference.
- A decision obvious from existing conventions.

## Ending conditions

Stop when one of these is true:

- The plan is clear enough to write a PRD.
- The plan is clear enough to split into GitHub Issues.
- The next step is a prototype.
- The next step is implementation.
- A Decision Gate requires the user's explicit choice.

End with:

- Decisions made.
- Open questions.
- Docs updated or proposed.
- Recommended next skill or action.
