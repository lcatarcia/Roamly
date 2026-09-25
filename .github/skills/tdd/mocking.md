# When to Mock

Mock at system boundaries only.

Good candidates:

- External APIs: maps/geocoding, weather, email, storage, AI services, payment providers.
- Time and randomness: `TimeProvider`, deterministic IDs, seeded randomness.
- File system or blob storage when documents are involved.
- Remote services that Roamly does not own.

Usually do not mock:

- Roamly handlers, validators, domain services, EF Core configurations, or internal repositories.
- Classes/modules you control and can run in process.
- MediatR-style orchestration when the slice test can use the real dispatcher.
- React hooks/components that can be exercised through UI behavior.

Use a test database before a database mock when persistence behavior matters. EF Core query translation, transactions, concurrency tokens, cascade rules, and SQL Server differences are not proven by mocking `DbSet`.

## Designing for mockability

At true boundaries, design narrow interfaces that say what Roamly needs.

**1. Accept dependencies, do not create them internally**

```csharp
public sealed class PlanTripHandler(
    IRoutePlanner routePlanner,
    TimeProvider clock,
    RoamlyDbContext db)
    : IRequestHandler<PlanTripCommand, PlanTripResponse>
{
    public async Task<PlanTripResponse> Handle(
        PlanTripCommand command,
        CancellationToken cancellationToken)
    {
        var route = await routePlanner.PlanAsync(
            command.Stops.Select(s => s.PlaceId),
            cancellationToken);

        // Persist itinerary using db...
    }
}
```

Avoid constructing SDK clients, reading clocks, or generating IDs directly inside business logic:

```csharp
// Hard to test and hard to replace.
var client = new ExternalMapsClient(configuration["Maps:Key"]);
var now = DateTimeOffset.UtcNow;
```

**2. Prefer Roamly-specific ports over generic clients**

Use an interface named after the business capability:

```csharp
public interface IRoutePlanner
{
    Task<RoutePlan> PlanAsync(
        IEnumerable<PlaceId> orderedStops,
        CancellationToken cancellationToken);
}
```

This is better than injecting a generic HTTP client wrapper that forces each test to know URLs, headers, and JSON shapes.

**3. Use fakes for owned behavior**

If Roamly owns both sides of a seam, use an in-memory fake or test adapter rather than a mocking framework. Fakes express behavior and reduce brittle call-count assertions.

```csharp
public sealed class FakeRoutePlanner : IRoutePlanner
{
    public RoutePlan NextPlan { get; set; } = RoutePlan.Empty;

    public Task<RoutePlan> PlanAsync(
        IEnumerable<PlaceId> orderedStops,
        CancellationToken cancellationToken)
        => Task.FromResult(NextPlan);
}
```

**4. Keep mocks at the edge of the test**

A test may replace `IRoutePlanner`; it should not replace `CreateTripHandler`, `CreateTripValidator`, `TripRepository`, and `RoamlyDbContext` all at once. If many internal mocks are required, the design seam is probably wrong.