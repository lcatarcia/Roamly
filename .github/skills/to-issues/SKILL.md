---
name: to-issues
description: "Break a Roamly PRD or plan into GitHub Issues using complete tracer-bullet vertical slices."
---

# To Issues
> User-invoked skill: run it only when the user explicitly asks for it.

Break a plan, PRD, or loose implementation strategy into independently grabbable GitHub Issues. Each issue should be a thin but complete vertical slice that can be picked up by a Copilot coding agent or a human without rereading the whole planning conversation.

Roamly's unit of work maps naturally to a vertical slice: command/query, handler, validator, response/DTO, endpoint/OpenAPI, EF Core persistence, frontend route or component, and tests colocated around a narrow behavior in the owning module.

Use GitHub Issues only. Do not create local issue-tracker files or GitLab variants.

## Process

### 1. Gather context

Work from the current conversation, PRD, design notes, prototype verdicts, and linked GitHub Issues. If the user passes an issue reference, read the full issue body and comments first.

Respect:

- `docs/architecture/CONTEXT.md` for domain terms.
- ADRs under `docs/adr/`.
- `AGENTS.md` and the Roamly orchestrator Decision Gate.
- Existing vertical-slice/CQRS conventions.

Use domain nouns in issue titles: Camper, Trip, TripStop, MaintenanceItem, Expense, Checklist, Place, JournalEntry, Document, or Intelligence insight.

### 2. Explore the current codebase when needed

Explore enough to understand the current implementation and where the slice should land. Look for prefactoring that would make the feature easier:

> Make the change easy, then make the easy change.

A prefactor issue is valid only if it is independently valuable and demonstrably reduces risk for later slices. It must still be verifiable on its own.

### 3. Draft tracer-bullet slices

Each slice must deliver a narrow complete path through all relevant layers. Avoid horizontal issues such as "create database tables", "add API endpoints", or "build UI" unless the source plan is itself only about that layer.

Vertical-slice rules:

- Each slice delivers one user-visible or externally verifiable behavior.
- Backend slices include validation, authorization, persistence, API/OpenAPI impact, and tests where relevant.
- Frontend slices include route/state/query behavior and tests where relevant.
- A completed slice is demoable or verifiable on its own.
- Dependencies are explicit and minimal.
- MEDIUM/HIGH strategic choices are not hidden inside an issue; they are called out as Decision Gate items.

Examples of good Roamly slices:

- "Create a trip stop and show it on the trip timeline" rather than "Add TripStop table".
- "Record maintenance completion with optional expense" rather than "Build maintenance service".
- "Upload a camper document and list it on the camper profile" rather than "Implement documents module".

### 4. Quiz the user before publishing

Present the proposed breakdown as a numbered list. For each issue show:

- **Title**: short and domain-specific.
- **Blocked by**: issue titles or "None".
- **User stories covered**: source user-story numbers if present.
- **Slice surface**: backend, frontend, tests, docs, or all of them.

Ask:

- Does the granularity feel right?
- Are dependencies correct?
- Should any slices be merged or split?
- Are any MEDIUM/HIGH decisions still unresolved?

Iterate until approved. If the user is unavailable and the task requires autonomous drafting, stop at a draft list rather than publishing ambiguous work.

### 5. Publish GitHub Issues

Create issues in dependency order so blockers have real issue numbers before dependent issues are created. Apply:

- `ready-for-agent` when the issue is fully specified for agent implementation.
- Exactly one category label: `enhancement` or `bug`.

Do not close or modify the parent PRD issue unless the user explicitly asks.

## Issue body template

```markdown
## Parent

<Reference to the parent GitHub Issue or PRD issue. Omit if none.>

## What to build

A concise end-to-end description of this vertical slice. Describe behavior rather than layer-by-layer tasks.

Avoid brittle file paths and code snippets. Exception: if a prototype produced a compact decision artifact, inline only the decision-rich part and say it came from a prototype.

## Acceptance criteria

- [ ] Criterion 1, externally verifiable.
- [ ] Criterion 2, including validation/error behavior where relevant.
- [ ] Criterion 3, including test or documentation expectations where relevant.

## Blocked by

- <GitHub Issue reference>

Or: None - can start immediately.

## Notes for the agent

- Relevant Roamly module/domain terms.
- Required Decision Gate outcome, if already decided.
- Testing seam to prefer.
- Out-of-scope adjacent behavior.
```
