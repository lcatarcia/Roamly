# Design It Twice

When the user wants to explore alternative interfaces for a chosen deepening candidate, use a design-it-twice process. Your first interface idea is unlikely to be the best.

This uses the vocabulary in [SKILL.md](SKILL.md): **module**, **interface**, **seam**, **adapter**, **depth**, and **leverage**.

## Process

### 1. Frame the problem space

Before producing alternatives, write a user-facing explanation of the problem space for the chosen candidate:

- The behavior the new interface must support.
- The constraints it must satisfy: authorization, validation, performance, compatibility, UI/API contracts, persistence, and observability.
- The dependencies it relies on and their category from [DEEPENING.md](DEEPENING.md).
- The current pain: duplication, shallow pass-throughs, hard tests, hidden coupling, or unclear ownership.
- A rough code sketch to ground the constraints. This is not a proposal; it only makes the problem concrete.

Example frame:

```csharp
// Constraint sketch only: this is not the chosen design.
public Task<TripPlanResult> PlanTripAsync(
    CamperId camperId,
    IReadOnlyList<PlaceId> stops,
    DateRange travelWindow,
    CancellationToken cancellationToken);
```

### 2. Generate at least two genuinely different designs

Produce multiple designs that optimize for different values. If the change is MEDIUM or HIGH severity, these become the options in the orchestrator's **Decision Required** format.

Useful design angles:

1. **Minimize the interface**: 1-3 entry points, maximum leverage per entry point.
2. **Optimize for the common caller**: make the default Roamly workflow trivial.
3. **Optimize for extension**: support future modules such as Intelligence, Journal, or Checklists without leaking internals.
4. **Ports and adapters**: isolate remote or third-party dependencies cleanly.
5. **Compatibility-first**: keep current API contracts stable and deepen behind them.

Each design should include:

- Interface: types, methods, parameters, invariants, ordering, error modes, and performance expectations.
- Usage example showing how a caller uses it.
- What the implementation hides behind the seam.
- Dependency and adapter strategy from [DEEPENING.md](DEEPENING.md).
- Testing strategy at the interface.
- Trade-offs: where leverage is high, where it is thin, what becomes harder.

### 3. Compare designs

Compare sequentially so the user can absorb them. Contrast by:

- **Depth**: how much behavior sits behind the interface.
- **Locality**: where future changes concentrate.
- **Seam placement**: who knows about persistence, transport, UI state, and third-party APIs.
- **Testability**: whether tests can verify behavior without implementation details.
- **Roamly fit**: whether the design respects Vertical Slice Architecture, module boundaries, domain language, and current conventions.

### 4. Recommend, then wait when required

Give a clear recommendation. If a hybrid is strongest, say exactly which pieces combine well.

For LOW decisions, proceed after stating the recommendation. For MEDIUM/HIGH decisions, stop at the orchestrator **Decision Required** consult: at least 4 concrete options, one marked recommended, pros/cons, impact, complexity, and "Altro / proposta custom". The recommendation does not authorize execution; the user decides.