# Good and Bad Tests

## Good tests

Good tests verify behavior through public interfaces.

```csharp
// GOOD: exercises the same endpoint and vertical slice used by the app.
[Fact]
public async Task Creating_a_maintenance_item_makes_it_visible_on_the_camper_timeline()
{
    var app = new RoamlyApiFactory();
    var client = app.CreateAuthenticatedClient(TestUsers.Owner);

    var create = await client.PostAsJsonAsync("/api/campers/van-1/maintenance", new
    {
        title = "Replace leisure battery",
        dueOn = "2026-11-15",
        odometerKm = 84200
    });

    create.EnsureSuccessStatusCode();

    var timeline = await client.GetFromJsonAsync<CamperTimelineResponse>(
        "/api/campers/van-1/timeline");

    timeline!.Items.Should().Contain(i =>
        i.Kind == "Maintenance" &&
        i.Title == "Replace leisure battery" &&
        i.DueOn == new DateOnly(2026, 11, 15));
}
```

Characteristics:

- Tests behavior users or API callers care about.
- Uses public seams: HTTP API, command/query dispatcher, domain method, or UI interaction.
- Survives internal refactors inside handlers, validators, repositories, and EF mappings.
- Describes WHAT happens, not HOW it is implemented.
- Uses one logical behavior per test, even if several assertions describe that behavior.

For frontend behavior, use user-facing queries and interactions:

```tsx
// GOOD: verifies the visible Trip flow, not component state.
it("shows the saved stop in the itinerary", async () => {
  render(<TripEditor tripId="trip-1" />);

  await user.click(screen.getByRole("button", { name: /add stop/i }));
  await user.type(screen.getByLabelText(/place/i), "Cortina");
  await user.click(screen.getByRole("button", { name: /save/i }));

  expect(await screen.findByText("Cortina")).toBeInTheDocument();
});
```

## Bad tests

Bad tests verify implementation details.

```csharp
// BAD: coupled to internal collaboration, not observable behavior.
[Fact]
public async Task Handler_calls_repository_save_once()
{
    var repository = Substitute.For<IMaintenanceRepository>();
    var handler = new CreateMaintenanceItemHandler(repository);

    await handler.Handle(new CreateMaintenanceItemCommand(...), CancellationToken.None);

    await repository.Received(1).SaveAsync(Arg.Any<MaintenanceItem>(), Arg.Any<CancellationToken>());
}
```

Red flags:

- Mocking Roamly's own handlers, repositories, validators, or services when a real in-process version is cheap.
- Testing private methods or internal helper classes directly.
- Asserting call counts, call order, or exact EF Core method chains.
- Reading database tables directly when the API/query can verify the result.
- Test names that describe HOW the code works instead of WHAT the product does.

```csharp
// BAD: bypasses the application interface to verify persistence details.
[Fact]
public async Task CreateTrip_inserts_row_in_Trips_table()
{
    await SendAsync(new CreateTripCommand(...));

    var row = await Db.Trips.SingleAsync(t => t.Title == "Dolomites weekend");
    row.Should().NotBeNull();
}

// GOOD: verifies the behavior through the query/API that callers use.
[Fact]
public async Task Created_trip_can_be_opened_from_the_trip_list()
{
    var created = await TripsApi.CreateTripAsync("Dolomites weekend");

    var list = await TripsApi.GetMyTripsAsync();

    list.Should().Contain(t => t.Id == created.Id && t.Title == "Dolomites weekend");
}
```

## Useful assertion style

Prefer assertions that explain the business rule:

```csharp
result.Errors.Should().Contain(e =>
    e.PropertyName == "Stops" &&
    e.ErrorMessage.Contains("at least one stop"));
```

Avoid assertions that merely mirror implementation structure:

```csharp
handler.Dependencies.Count.Should().Be(3);
```