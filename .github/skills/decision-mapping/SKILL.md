---
name: decision-mapping
description: "Turn a loose Roamly idea into a compact map of research, prototype, and discussion tickets, then resolve them one at a time."
---

# Decision Mapping
> User-invoked skill: run it only when the user explicitly asks for it.

Use this when a loose Roamly idea has too much fog of war for a PRD or implementation issue. The skill creates a compact decision map and drives one decision ticket at a time to resolution.

The map is for multi-session planning. If the initial grilling reveals no real fog of war, skip the map and move to `to-prd`, `to-issues`, `prototype`, or implementation.

## Decision Gate alignment

This skill complements the Roamly orchestrator Decision Gate:

- LOW decisions can often be resolved inline.
- MEDIUM decisions require user consultation.
- HIGH decisions require consultation and explicit confirmation.
- Multiple valid strategies must be presented as 4+ options with one marked RECOMMENDED and a final "Other / custom proposal" option.

The decision map records open decisions; it does not authorize autonomous choices.

## The decision map

The decision map is one compact Markdown file, tracked with the project when the user wants durable planning. It is the canonical artifact. Every session resolving a ticket should load the whole map, so keep it concise and link assets instead of duplicating them.

Suggested location:

- Near related planning docs under `docs/product` or a project planning area the repo already uses.
- Do not create an index or README just for the map.

## Structure

Each ticket is a numbered section:

```markdown
# Decision Map: <feature or initiative>

## #1: Choose Trip Sharing Scope

Blocked by: None
Type: Research | Prototype | Discuss
Severity: LOW | MEDIUM | HIGH
Status: Open | Answered | Superseded

### Question

<question-here>

### Options

1. <Option A>
   - Pros:
   - Cons:
   - Impact:
   - Complexity:

2. <Option B> - RECOMMENDED
   - Rationale:
   - Pros:
   - Cons:
   - Impact:
   - Complexity:

3. <Option C>
   - Pros:
   - Cons:
   - Impact:
   - Complexity:

4. <Option D>
   - Pros:
   - Cons:
   - Impact:
   - Complexity:

5. Other / custom proposal

### Answer

<answer-here once resolved>

### Assets

- <links to research notes, prototypes, ADRs, or issues>
```

Each ticket should fit in one agent session.

## Ticket types

### Research

Use when knowledge outside the immediate code context is required:

- Microsoft/ASP.NET Core behavior.
- EF Core and SQL Server tradeoffs.
- GitHub Actions or Docker constraints.
- Third-party API behavior.
- Existing docs or ADR history.

Creates a short markdown summary as an asset, linked from the map.

### Prototype

Use when the decision depends on feel or behavior:

- Logic/state/API shape -> use `prototype` [LOGIC.md](../prototype/LOGIC.md).
- UI layout/interaction -> use `prototype` [UI.md](../prototype/UI.md).

Creates throwaway code as an asset and records only the answer in the map.

### Discuss

Use when the decision is mostly product/domain/architecture clarification. Use `grill-with-docs` to ask one question at a time and update `CONTEXT.md` or ADRs when decisions land.

## Fog of war

The map is deliberately incomplete beyond the frontier. Do not pretend to know every future ticket. Resolve the current frontier, then add newly discovered tickets with correct dependencies.

A map is done when the remaining path is clear enough for:

- a PRD,
- a set of GitHub Issues,
- a prototype verdict,
- or direct implementation.

## Invocation modes

### Bootstrap

The user gives a loose idea.

1. Run `grill-with-docs` enough to expose open decisions.
2. Identify trivially decidable points and resolve them inline.
3. Create a new decision map with the current frontier.
4. Stop. Map-building is one session's work; do not also resolve the tickets unless the user explicitly asks.

Bootstrap output:

- Map path.
- Ticket list.
- Recommended first ticket.
- Any Decision Gate items that require explicit user choice.

### Resume

The user gives a map path and ticket number.

1. Load the whole map.
2. Resolve the requested ticket using Research, Prototype, or Discuss.
3. Record the answer in that ticket.
4. Add newly discovered tickets and dependencies.
5. Mark invalidated tickets Superseded or update/delete them.
6. Stop.

Because other agents may resolve tickets in parallel, re-read the map immediately before editing and preserve unrelated changes.

## Roamly examples

Good tickets:

- `#1: Should Trip sharing be private link, invited users, or public discovery?` - Discuss, HIGH.
- `#2: Can Maintenance recurrence be represented without a scheduler service?` - Prototype, MEDIUM.
- `#3: Which SQL shape supports Expense splits without overengineering?` - Research, MEDIUM.
- `#4: What camper dashboard layout best surfaces urgent work?` - Prototype, LOW/MEDIUM depending on scope.

Bad tickets:

- `#1: Build trips module` - too large and not a decision.
- `#2: Add database table` - implementation step, not decision mapping.
- `#3: Make UI nice` - vague and not answerable.

## Skipping the map

If grilling reveals no unresolved multi-session decisions, say so and recommend the next action:

- Use `to-prd` for a durable product spec.
- Use `to-issues` to split an approved plan.
- Use `prototype` for one remaining logic/UI question.
- Implement directly if the slice is small and decisions are settled.
