using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Common;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Trips;

[Collection(DatabaseCollection.Name)]
public sealed class TripEstimatesApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Email = "trip-estimates@example.test";
    private const string Password = "Test!Password123";
    private readonly Guid _ownerId = Guid.NewGuid();
    private RoamlyApiFactory _factory = null!;
    private RoamlyClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        await migrated.Lease.ResetAsync(TestContext.Current.CancellationToken);

        _factory = new RoamlyApiFactory(migrated.Lease.ConnectionString);
        _client = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));

        await CreateUserAsync();
        using var login = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task traveller_can_set_manual_trip_estimates_and_read_them_back()
    {
        var trip = await CreateTripAsync();
        using var createdBody = JsonDocument.Parse(trip.CreatedBody);
        Assert.Equal(JsonValueKind.Null, createdBody.RootElement.GetProperty("estimatedDistanceKm").ValueKind);
        Assert.Equal(JsonValueKind.Null, createdBody.RootElement.GetProperty("estimatedDurationMinutes").ValueKind);

        using var initial = await _client.GetAsync(trip.Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var etag = HttpEtagAssertions.StrongRowVersion(initial.Headers.ETag?.ToString());

        using var updated = await SendPutAsync(trip.Path, etag, new
        {
            name = "Weekend",
            plannedStartOnUtc = (DateOnly?)null,
            plannedEndOnUtc = (DateOnly?)null,
            estimatedDistanceKm = "12345.67",
            estimatedDurationMinutes = 987,
            stops = new[] { new { name = "Bologna" } },
        });

        Assert.True(
            updated.StatusCode == HttpStatusCode.OK,
            await updated.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var updatedEtag = HttpEtagAssertions.StrongRowVersion(updated.Headers.ETag?.ToString());
        Assert.NotEqual(etag, updatedEtag);
        using var updatedBody = JsonDocument.Parse(await updated.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal("12345.67", updatedBody.RootElement.GetProperty("estimatedDistanceKm").GetString());
        Assert.Equal(987, updatedBody.RootElement.GetProperty("estimatedDurationMinutes").GetInt32());

        using var readBack = await _client.GetAsync(trip.Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, readBack.StatusCode);
        Assert.Equal(updatedEtag, readBack.Headers.ETag?.ToString());
        using var readBody = JsonDocument.Parse(await readBack.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal("12345.67", readBody.RootElement.GetProperty("estimatedDistanceKm").GetString());
        Assert.Equal(987, readBody.RootElement.GetProperty("estimatedDurationMinutes").GetInt32());
    }

    [Fact]
    public async Task trip_put_clears_estimates_when_null_or_omitted()
    {
        var trip = await CreateTripAsync();
        using var initial = await _client.GetAsync(trip.Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var etag = HttpEtagAssertions.StrongRowVersion(initial.Headers.ETag?.ToString());

        using var set = await SendPutAsync(trip.Path, etag, ReplacementWithEstimates("123.45", 600));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        etag = HttpEtagAssertions.StrongRowVersion(set.Headers.ETag?.ToString());

        var beforeClearEtag = etag;
        using var cleared = await SendPutAsync(trip.Path, etag, ReplacementWithEstimates(null, null));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        etag = HttpEtagAssertions.StrongRowVersion(cleared.Headers.ETag?.ToString());
        Assert.NotEqual(beforeClearEtag, etag);
        await AssertEstimatesAreNullAsync(cleared);

        using var distanceOnly = await SendPutAsync(trip.Path, etag, ReplacementWithEstimates("987.65", null));
        Assert.Equal(HttpStatusCode.OK, distanceOnly.StatusCode);
        etag = HttpEtagAssertions.StrongRowVersion(distanceOnly.Headers.ETag?.ToString());
        using (var distanceBody = JsonDocument.Parse(await distanceOnly.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken)))
        {
            Assert.Equal("987.65", distanceBody.RootElement.GetProperty("estimatedDistanceKm").GetString());
            Assert.Equal(JsonValueKind.Null, distanceBody.RootElement.GetProperty("estimatedDurationMinutes").ValueKind);
        }

        using var durationOnly = await SendPutAsync(trip.Path, etag, ReplacementWithEstimates(null, 1200));
        Assert.Equal(HttpStatusCode.OK, durationOnly.StatusCode);
        etag = HttpEtagAssertions.StrongRowVersion(durationOnly.Headers.ETag?.ToString());
        using (var durationBody = JsonDocument.Parse(await durationOnly.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken)))
        {
            Assert.Equal(JsonValueKind.Null, durationBody.RootElement.GetProperty("estimatedDistanceKm").ValueKind);
            Assert.Equal(1200, durationBody.RootElement.GetProperty("estimatedDurationMinutes").GetInt32());
        }

        using var omitted = await SendPutAsync(trip.Path, etag, new
        {
            name = "Weekend",
            plannedStartOnUtc = (DateOnly?)null,
            plannedEndOnUtc = (DateOnly?)null,
            stops = new[] { new { name = "Bologna" } },
        });
        Assert.Equal(HttpStatusCode.OK, omitted.StatusCode);
        Assert.NotEqual(etag, HttpEtagAssertions.StrongRowVersion(omitted.Headers.ETag?.ToString()));
        await AssertEstimatesAreNullAsync(omitted);

        using var readBack = await _client.GetAsync(trip.Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, readBack.StatusCode);
        await AssertEstimatesAreNullAsync(readBack);
    }

    [Fact]
    public async Task trip_put_rejects_estimates_outside_supported_range_or_precision_without_mutation()
    {
        var trip = await CreateTripAsync();
        using var initial = await _client.GetAsync(trip.Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var etag = HttpEtagAssertions.StrongRowVersion(initial.Headers.ETag?.ToString());
        using var set = await SendPutAsync(trip.Path, etag, ReplacementWithEstimates("50.25", 300));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        etag = HttpEtagAssertions.StrongRowVersion(set.Headers.ETag?.ToString());

        using var validBoundary = await SendPutAsync(
            trip.Path,
            etag,
            ReplacementWithEstimates("9999999999999999.99", int.MaxValue));
        Assert.Equal(
            HttpStatusCode.OK,
            validBoundary.StatusCode);
        var boundaryEtag = HttpEtagAssertions.StrongRowVersion(validBoundary.Headers.ETag?.ToString());
        Assert.NotEqual(etag, boundaryEtag);
        using var boundaryRead = await _client.GetAsync(trip.Path, TestContext.Current.CancellationToken);
        using var boundaryBody = JsonDocument.Parse(await boundaryRead.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        var exactBoundary = boundaryBody.RootElement.GetProperty("estimatedDistanceKm").GetString();
        Assert.Equal("9999999999999999.99", exactBoundary);
        using var boundaryEdit = await SendPutAsync(
            trip.Path,
            boundaryEtag,
            ReplacementWithEstimates(exactBoundary, int.MaxValue));
        Assert.Equal(HttpStatusCode.OK, boundaryEdit.StatusCode);
        boundaryEtag = HttpEtagAssertions.StrongRowVersion(boundaryEdit.Headers.ETag?.ToString());

        var invalidRequests = new (object Body, string Field, bool HasValidationErrors)[]
        {
            (ReplacementWithEstimates("-0.01", 300), "estimatedDistanceKm", true),
            (ReplacementWithEstimates("1.001", 300), "estimatedDistanceKm", true),
            (ReplacementWithEstimates("10000000000000000", 300), "estimatedDistanceKm", true),
            (new
            {
                name = "Weekend",
                estimatedDistanceKm = 123.45m,
                stops = new[] { new { name = "Bologna" } },
            }, "estimatedDistanceKm", false),
            (ReplacementWithEstimates("50.25", -1), "estimatedDurationMinutes", true),
            (new
            {
                name = "Weekend",
                estimatedDurationMinutes = 2147483648L,
                stops = new[] { new { name = "Bologna" } },
            }, "estimatedDurationMinutes", false),
        };

        foreach (var (body, field, hasValidationErrors) in invalidRequests)
        {
            using var response = await SendPutAsync(trip.Path, etag, body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            if (hasValidationErrors)
            {
                using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
                    TestContext.Current.CancellationToken));
                Assert.True(
                    problem.RootElement.GetProperty("errors").TryGetProperty(field, out _),
                    problem.RootElement.GetRawText());
            }
        }

        using var unchanged = await _client.GetAsync(trip.Path, TestContext.Current.CancellationToken);
        Assert.Equal(boundaryEtag, unchanged.Headers.ETag?.ToString());
        using var bodyAfterFailures = JsonDocument.Parse(await unchanged.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(
            "9999999999999999.99",
            bodyAfterFailures.RootElement.GetProperty("estimatedDistanceKm").GetString());
        Assert.Equal(int.MaxValue, bodyAfterFailures.RootElement.GetProperty("estimatedDurationMinutes").GetInt32());
    }

    [Fact]
    public async Task trip_put_persists_reads_and_clears_the_optional_fuel_price_money()
    {
        var trip = await CreateTripAsync();
        using var createdBody = JsonDocument.Parse(trip.CreatedBody);
        Assert.Equal(JsonValueKind.Null, createdBody.RootElement.GetProperty("fuelPricePerLiter").ValueKind);

        using var initial = await _client.GetAsync(trip.Path, TestContext.Current.CancellationToken);
        var etag = HttpEtagAssertions.StrongRowVersion(initial.Headers.ETag?.ToString());
        using var updated = await SendPutAsync(
            trip.Path,
            etag,
            ReplacementWithFuelPrice(new { amount = 1.8790m, currency = "EUR" }));

        Assert.True(
            updated.StatusCode == HttpStatusCode.OK,
            await updated.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var updatedEtag = HttpEtagAssertions.StrongRowVersion(updated.Headers.ETag?.ToString());
        Assert.NotEqual(etag, updatedEtag);
        using var updatedBody = JsonDocument.Parse(await updated.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        var money = updatedBody.RootElement.GetProperty("fuelPricePerLiter");
        Assert.Equal(1.8790m, money.GetProperty("amount").GetDecimal());
        Assert.Equal("EUR", money.GetProperty("currency").GetString());

        using var readBack = await _client.GetAsync(trip.Path, TestContext.Current.CancellationToken);
        Assert.Equal(updatedEtag, readBack.Headers.ETag?.ToString());
        using var readBody = JsonDocument.Parse(await readBack.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(1.8790m, readBody.RootElement.GetProperty("fuelPricePerLiter")
            .GetProperty("amount").GetDecimal());

        using var cleared = await SendPutAsync(
            trip.Path,
            updatedEtag,
            ReplacementWithFuelPrice(null));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        var clearedEtag = HttpEtagAssertions.StrongRowVersion(cleared.Headers.ETag?.ToString());
        Assert.NotEqual(updatedEtag, clearedEtag);
        using var clearedBody = JsonDocument.Parse(await cleared.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(JsonValueKind.Null, clearedBody.RootElement.GetProperty("fuelPricePerLiter").ValueKind);

        using var restored = await SendPutAsync(
            trip.Path,
            clearedEtag,
            ReplacementWithFuelPrice(new { amount = 1.9m, currency = "CHF" }));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        var restoredEtag = HttpEtagAssertions.StrongRowVersion(restored.Headers.ETag?.ToString());
        using var omitted = await SendPutAsync(trip.Path, restoredEtag, ReplacementWithFuelPrice(null, false));
        Assert.Equal(HttpStatusCode.OK, omitted.StatusCode);
        using var omittedBody = JsonDocument.Parse(await omitted.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(JsonValueKind.Null, omittedBody.RootElement.GetProperty("fuelPricePerLiter").ValueKind);
    }

    [Fact]
    public async Task trip_put_rejects_invalid_fuel_prices_without_mutating_the_trip()
    {
        var trip = await CreateTripAsync();
        using var initial = await _client.GetAsync(trip.Path, TestContext.Current.CancellationToken);
        var etag = HttpEtagAssertions.StrongRowVersion(initial.Headers.ETag?.ToString());
        using var minimum = await SendPutAsync(
            trip.Path,
            etag,
            ReplacementWithFuelPrice(new { amount = 0.0001m, currency = "EUR" }));
        Assert.Equal(HttpStatusCode.OK, minimum.StatusCode);
        etag = HttpEtagAssertions.StrongRowVersion(minimum.Headers.ETag?.ToString());

        using var maximum = await SendPutAsync(
            trip.Path,
            etag,
            ReplacementWithFuelPrice(new { amount = 999999999999999.9999m, currency = "EUR" }));
        Assert.Equal(HttpStatusCode.OK, maximum.StatusCode);
        etag = HttpEtagAssertions.StrongRowVersion(maximum.Headers.ETag?.ToString());
        using var maximumBody = JsonDocument.Parse(await maximum.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(
            999999999999999.9999m,
            maximumBody.RootElement.GetProperty("fuelPricePerLiter").GetProperty("amount").GetDecimal());

        using var set = await SendPutAsync(
            trip.Path,
            etag,
            ReplacementWithFuelPrice(new { amount = 1.5m, currency = "EUR" }));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        etag = HttpEtagAssertions.StrongRowVersion(set.Headers.ETag?.ToString());

        var invalidPrices = new object[]
        {
            new { currency = "EUR" },
            new { amount = (decimal?)null, currency = "EUR" },
            new { amount = 0m, currency = "EUR" },
            new { amount = -1m, currency = "EUR" },
            new { amount = 1.12345m, currency = "EUR" },
            new { amount = 1000000000000000m, currency = "EUR" },
            new { amount = 1.5m, currency = "eur" },
            new { amount = 1.5m, currency = "EU" },
        };

        foreach (var price in invalidPrices)
        {
            using var invalid = await SendPutAsync(
                trip.Path,
                etag,
                ReplacementWithFuelPrice(price));
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        using var unchanged = await _client.GetAsync(trip.Path, TestContext.Current.CancellationToken);
        Assert.Equal(etag, unchanged.Headers.ETag?.ToString());
        using var unchangedBody = JsonDocument.Parse(await unchanged.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(1.5m, unchangedBody.RootElement.GetProperty("fuelPricePerLiter")
            .GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task sql_rejects_a_trip_fuel_price_with_only_one_value()
    {
        var trip = await CreateTripAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RoamlyDbContext>();

        var failure = await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [Trips] SET [FuelPricePerLiterAmount] = {1.5m}, [FuelPricePerLiterCurrency] = {(string?)null} WHERE [OwnerId] = {_ownerId} AND [Id] = {trip.Id}",
                TestContext.Current.CancellationToken));

        Assert.Equal(547, failure.Number);
    }

    private async Task CreateUserAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(new ApplicationUser
        {
            Id = _ownerId,
            UserName = Email,
            Email = Email,
            EmailConfirmed = true,
            CreatedAtUtc = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc),
        }, Password);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private async Task<TripHandle> CreateTripAsync()
    {
        var camperId = NewId();
        using var camper = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id = camperId,
            name = "Il Grigio",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, camper.StatusCode);

        var tripId = NewId();
        using var created = await _client.PostJsonAsync(
            $"/api/v1/campers/{camperId:D}/trips",
            new { id = tripId, name = "Weekend", stops = new[] { new { name = "Bologna" } } },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return new TripHandle(
            tripId,
            $"/api/v1/trips/{tripId:D}",
            await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    private async Task<HttpResponseMessage> SendPutAsync(string path, string etag, object body)
    {
        using var csrf = await _client.GetAsync("/api/v1/csrf-token", TestContext.Current.CancellationToken);
        var token = await csrf.Content.ReadFromJsonAsync<CsrfTokenResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(token);

        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add("X-XSRF-TOKEN", token.Token);
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Guid NewId() => new SequentialGuidGenerator(TimeProvider.System).NewId();

    private static object ReplacementWithEstimates(string? distanceKm, int? durationMinutes) =>
        new
        {
            name = "Weekend",
            plannedStartOnUtc = (DateOnly?)null,
            plannedEndOnUtc = (DateOnly?)null,
            estimatedDistanceKm = distanceKm,
            estimatedDurationMinutes = durationMinutes,
            stops = new[] { new { name = "Bologna" } },
        };

    private static Dictionary<string, object?> ReplacementWithFuelPrice(object? price, bool includePrice = true)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = "Weekend",
            ["plannedStartOnUtc"] = null,
            ["plannedEndOnUtc"] = null,
            ["estimatedDistanceKm"] = "123.45",
            ["estimatedDurationMinutes"] = 600,
            ["stops"] = new[] { new { name = "Bologna" } },
        };
        if (includePrice)
        {
            body["fuelPricePerLiter"] = price;
        }

        return body;
    }

    private static async Task AssertEstimatesAreNullAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("estimatedDistanceKm").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("estimatedDurationMinutes").ValueKind);
    }

    private sealed record CsrfTokenResponse(string Token);

    private sealed record TripHandle(Guid Id, string Path, string CreatedBody);
}
