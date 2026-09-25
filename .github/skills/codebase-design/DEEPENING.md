# Deepening

How to deepen a cluster of shallow Roamly modules safely, given its dependencies. Assumes the vocabulary in [SKILL.md](SKILL.md): **module**, **interface**, **seam**, **adapter**, **depth**, and **leverage**.

## Dependency categories

Classify the dependencies of the candidate module before changing the seam. The category determines how the deepened module is tested.

### 1. In-process

Pure computation or in-memory state with no I/O. Always deepenable. Merge the behavior and test through the new public interface directly.

Roamly examples:

- Calculating trip duration from `TripStop` dates.
- Computing maintenance due status from odometer and dates.
- Summing expenses by category.
- Building checklist completion state.

No adapter is needed. If the logic is scattered across helpers, move it behind one domain method or slice-level policy.

### 2. Local-substitutable

Dependencies that have realistic local stand-ins. Deepenable if the stand-in catches the same class of bugs.

Roamly examples:

- EF Core with a test database or containerized SQL Server when query translation matters.
- Local file/blob storage fake for document uploads.
- In-memory message queue when behavior is not transport-specific.

The deep module is tested with the stand-in running in the test suite. Keep the seam internal unless production and test genuinely need different adapters.

### 3. Remote but owned: ports and adapters

Services Roamly owns across a process or network boundary. Define a port at the seam, keep business logic in the deep module, and inject the transport adapter.

Recommendation shape:

> Define a port at the seam, implement an HTTP or queue adapter for production and an in-memory adapter for testing, so the trip planning logic sits in one deep module even though the transport varies.

Examples:

- A future internal Intelligence service that proposes itinerary improvements.
- A background worker that generates document previews.
- A notification worker that sends maintenance reminders.

### 4. True external: mock or fake at the edge

Third-party services Roamly does not control. The deepened module takes the external dependency as an injected port; tests provide a fake or mock adapter.

Examples:

- Maps/geocoding providers.
- Weather APIs.
- Email/SMS providers.
- AI APIs.
- Payment providers if marketplace features appear later.

## Seam discipline

- **One adapter means a hypothetical seam. Two adapters means a real one.** A single implementation hidden behind an interface is often just indirection.
- **Internal seams vs external seams.** A deep module may use private seams for its implementation or tests; do not expose them to callers just because tests use them.
- **Do not create generic service layers by default.** A vertical slice can be the deep module.
- **Prefer domain language over technical forwarding.** `ScheduleMaintenanceItem` is deeper than `MaintenanceRepository.Insert` plus `MaintenanceService.Validate` plus `MaintenanceMapper.Map`.

## Testing strategy: replace, do not layer

- Old unit tests on shallow helpers become waste once tests at the deepened interface exist. Delete or rewrite them.
- Write new tests at the deep module's public seam.
- Assert observable outcomes: API responses, query results, domain state, emitted events, visible UI.
- Avoid tests that know the internal class split.
- If a test must change when only implementation changes, it is testing past the interface.

## Practical Roamly workflow

1. Pick one cluster causing pain: repeated trip date validation, duplicate expense totals, scattered document access checks, or hard-to-test maintenance rules.
2. Name the intended deep module in domain language.
3. Classify dependencies using the categories above.
4. Sketch the smallest interface that gives callers useful leverage.
5. Use TDD at the new seam before moving code.
6. Remove shallow pass-through modules and obsolete tests after behavior is covered.
7. If the change is MEDIUM/HIGH severity, consult the user through the orchestrator Decision Required format before implementation.