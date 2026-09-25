# Logic Prototype

Use a logic prototype when the question is about business rules, state transitions, data shape, command/query contract, or API behavior. It should be a tiny runnable console app or script that lets the user push a model through cases that are hard to reason about on paper.

For Roamly, prefer:

- A small C# console app runnable with `dotnet run` when the question belongs to backend/domain logic.
- A single-file C# program when that is enough.
- A small Node/TypeScript script only when the logic belongs to frontend state, route behavior, or TanStack Query orchestration.

Do not introduce a new runtime just for the prototype.

## When this is the right shape

- "Can TripStop dates be reordered without invalid states?"
- "Does the MaintenanceItem recurrence model handle time-based and odometer-based schedules?"
- "What should the `RecordExpense` command accept?"
- "Can a Checklist run represent skipped items and later completion?"
- "What should the Intelligence insight lifecycle be?"

If the question is visual, use [UI.md](UI.md).

## Process

### 1. State the question

At the top of the prototype file, write one paragraph:

- What model is being explored.
- What question it answers.
- What should be learned before deleting it.

Example:

```csharp
// PROTOTYPE - throw away after deciding the TripStop reorder rules.
// Question: can a trip timeline allow inserting a stop between two existing stops
// while preventing date overlap and preserving a useful validation message?
```

### 2. Pick the smallest runtime

Use the project's existing tooling.

Backend/domain examples:

- `dotnet new console` only if a tiny project is useful.
- A single C# file in an existing scratch/prototype folder if the repo already supports it.
- `dotnet run --project <prototype-project>` as the run command.

Frontend logic examples:

- A small TypeScript/Node script using existing package tooling.
- No new package manager and no new dependency unless absolutely necessary.

### 3. Isolate the logic behind a portable interface

The useful part should be liftable into production later. The terminal shell is disposable.

Good shapes:

- A pure reducer: `(state, action) => state`.
- An explicit state machine with legal transitions.
- A small set of pure functions over plain data.
- A small class/module with a clear method surface if it genuinely owns state.

Keep it pure: no database calls, no `Console.WriteLine` in the logic, no HTTP calls, no dependency injection framework.

Example C# shape:

```csharp
public record TripTimeline(IReadOnlyList<TripStopDraft> Stops);
public record AddStop(string PlaceName, DateOnly Arrival, DateOnly Departure);

public static class TripTimelineRules
{
    public static Result<TripTimeline> Apply(TripTimeline state, AddStop action)
    {
        // prototype logic only
    }
}
```

### 4. Build the smallest console UI

Render one stable frame after every action.

Frame order:

1. Current state, pretty-printed and diff-friendly.
2. Derived values or validation messages.
3. Keyboard or line commands.

Use native ANSI formatting if useful. Avoid dependencies unless already present.

Behavior:

1. Initialize in-memory state.
2. Read one key or line.
3. Dispatch to the pure logic.
4. Re-render the full frame.
5. Loop until quit.

Commands should match the domain:

```text
[a] add trip stop
[r] reorder stop
[x] remove stop
[v] violate date rule
[q] quit
```

### 5. Make it runnable in one command

The user should not need to remember paths.

Acceptable examples:

- `dotnet run --project prototypes\TripTimelinePrototype`
- `npm run prototype:trip-timeline`
- `pnpm prototype:expense-split`

If the repo has no suitable task runner, put the command at the top of the prototype file and repeat it in the handoff message.

### 6. Hand it over

Give the run command and the question it answers. The user should drive it. Interesting feedback usually sounds like:

- "That state should not be possible."
- "That validation message needs the Place name."
- "We do not need that branch after all."

Add actions as the question evolves, but resist generalizing beyond the question.

### 7. Capture the answer

When done, keep the answer and delete or absorb the prototype.

Capture:

- Winning state model or command shape.
- Rejected alternatives.
- Any Decision Gate outcome.
- Whether an ADR, PRD, or GitHub Issue should be updated.

## Anti-patterns

- Adding tests to the prototype.
- Wiring to the real SQL Server database.
- Adding dependency injection, logging, or production error handling.
- Generalizing for future cases not in the question.
- Letting the console shell become production code.
- Keeping the prototype after the real vertical slice is implemented.
