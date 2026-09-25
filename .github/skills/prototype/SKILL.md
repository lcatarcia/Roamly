---
name: prototype
description: "Build a Roamly throwaway prototype for logic, state, API shape, or React UI variants before committing to implementation."
---

# Prototype
> User-invoked skill: run it only when the user explicitly asks for it.

A prototype is throwaway code that answers a question. The question decides the shape.

For Roamly there are two branches:

- **Logic/state/API question** -> [LOGIC.md](LOGIC.md). Build a runnable console prototype, usually C# with `dotnet run`, or a small Node script when the question is frontend-specific TypeScript logic.
- **UI/layout question** -> [UI.md](UI.md). Build several radically different React + Vite + TypeScript variants on one route, switchable by URL search param and a floating bottom bar.

The prototype is not production code. It exists to learn quickly, then be deleted or absorbed into a real vertical slice.

## Pick a branch

Identify the question from the user's prompt, surrounding code, or issue:

- "Does this Trip state model handle reorder and cancellation?" -> logic.
- "Can a MaintenanceItem recurrence rule represent annual service and odometer-based service?" -> logic.
- "What should the camper dashboard look like?" -> UI.
- "Should the expense capture flow be a wizard or inline editor?" -> UI.
- "What should this API request/response shape feel like?" -> logic, unless the concern is visual.

If ambiguous and the user is unavailable, default to the branch closest to the surrounding code: backend/domain module -> logic; page/component -> UI. State the assumption at the top of the prototype.

## Rules for both branches

1. **Throwaway from day one.** Name files/routes so a reader knows they are prototypes. Keep them close enough to the real area for context, but visibly separate from production code.
2. **One command to run.** Use existing tooling. Prefer `dotnet run` for C# console prototypes and existing package scripts for frontend/Node prototypes.
3. **No persistence by default.** State lives in memory. If persistence is the question, use a clearly marked scratch store and never real production data.
4. **Skip polish.** No tests, no broad error handling, no reusable framework. The point is speed of learning.
5. **Surface the state.** After every action or variant switch, show enough state for the user to understand what changed.
6. **Respect the Decision Gate.** If the prototype reveals several valid MEDIUM/HIGH strategies, present options rather than choosing silently.
7. **Delete or absorb when done.** Keep the answer, not the prototype shell.

## Roamly examples

Good prototype questions:

- Can a TripStop timeline state machine handle insert, reorder, skip, and overlap prevention?
- What is the simplest command shape for recording a MaintenanceItem completion plus optional Expense?
- Does an Expense split model need per-person allocation now, or is category-level enough?
- Should the camper overview prioritize maintenance alerts, documents, or upcoming trips?
- Which checklist execution UI makes missed items hardest to overlook?

Bad prototype questions:

- "Build the feature quickly and maybe keep it." That is implementation.
- "Add tests around the prototype." That is no longer a prototype.
- "Try all possible future extension points." That is overengineering.

## Capture the answer

When the prototype answers the question, record the decision somewhere durable:

- GitHub Issue comment.
- PRD or implementation issue.
- ADR if architecture-significant.
- Commit message if the prototype is immediately absorbed.
- A short `NOTES.md` next to the prototype only if the user is unavailable and a verdict is still needed.

The captured answer should include:

- The question.
- What was learned.
- Which option won, if any.
- What should be built for real.
- What should be deleted.

Do not leave variant components, console shells, or scratch data behind after the real implementation lands.
