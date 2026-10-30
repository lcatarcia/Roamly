using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Campers;

[Collection(DatabaseCollection.Name)]
public sealed class CamperProfileApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Password = "Test!Password123";
    private readonly Guid _ownerId = Guid.NewGuid();
    private RoamlyApiFactory _factory = null!;
    private RoamlyClient _client = null!;
    private DatabaseLease _database = null!;
    private Guid _camperId;

    public async ValueTask InitializeAsync()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        _database = migrated.Lease;
        await _database.ResetAsync(TestContext.Current.CancellationToken);

        _factory = new RoamlyApiFactory(_database.ConnectionString);
        _client = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));

        await CreateUserAsync(_ownerId, "profile-owner@example.test");
        using var login = await _client.LoginAsync(
            "profile-owner@example.test",
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        _camperId = await CreateCamperAsync();
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
    public async Task owner_can_read_and_update_the_complete_technical_profile_with_etag()
    {
        using var read = await _client.GetAsync(
            CamperPath,
            TestContext.Current.CancellationToken);

        Assert.True(
            read.StatusCode == HttpStatusCode.OK,
            await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var initialEtag = HttpEtagAssertions.StrongRowVersion(read.Headers.ETag?.ToString());
        var initial = await read.Content.ReadFromJsonAsync<CamperProfileResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(initial);
        Assert.Equal("Il Grigio", initial.Name);
        Assert.Null(initial.Brand);
        Assert.Null(initial.LengthMm);

        using var weakUpdate = await SendJsonAsync(
            HttpMethod.Put,
            CamperPath,
            HttpEtagAssertions.AsWeak(initialEtag),
            FullProfile());
        Assert.Equal(HttpStatusCode.PreconditionFailed, weakUpdate.StatusCode);

        using var update = await SendJsonAsync(HttpMethod.Put, CamperPath, initialEtag, FullProfile());

        Assert.True(
            update.StatusCode == HttpStatusCode.OK,
            await update.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.NotEqual(initialEtag, HttpEtagAssertions.StrongRowVersion(update.Headers.ETag?.ToString()));
        var updated = await update.Content.ReadFromJsonAsync<CamperProfileResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal("Laika", updated.Brand);
        Assert.Equal("Kreos", updated.Model);
        Assert.Equal((short)2022, updated.Year);
        Assert.Equal("AB123CD", updated.PlateNumber);
        Assert.Equal(0, updated.VehicleKind);
        Assert.Equal(7350, updated.LengthMm);
        Assert.Equal(2320, updated.WidthMm);
        Assert.Equal(2990, updated.HeightMm);
        Assert.Equal(3100, updated.KerbWeightKg);
        Assert.Equal(3500, updated.MaxWeightKg);
        Assert.Equal(120, updated.FreshWaterL);
        Assert.Equal(100, updated.GreyWaterL);
        Assert.Equal(90, updated.FuelTankL);
        Assert.Equal(20, updated.GasKg);
        Assert.Equal(0, updated.FuelKind);
        Assert.Equal(12.5m, updated.AverageConsumptionLPer100Km);

        using var stale = await SendJsonAsync(HttpMethod.Put, CamperPath, initialEtag, FullProfile());
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("application/problem+json", stale.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task profile_update_requires_if_match_and_rejects_invalid_technical_values()
    {
        using var missing = await SendJsonAsync(HttpMethod.Put, CamperPath, null, FullProfile());
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);

        using var read = await _client.GetAsync(CamperPath, TestContext.Current.CancellationToken);
        var etag = read.Headers.ETag?.ToString();
        Assert.NotNull(etag);
        using var invalid = await SendJsonAsync(
            HttpMethod.Put,
            CamperPath,
            etag,
            new
            {
                name = "Il Grigio",
                brand = "Laika",
                model = "Kreos",
                year = 2022,
                plateNumber = "AB123CD",
                vehicleKind = 0,
                lengthMm = 0,
                widthMm = 2320,
                heightMm = 2990,
                kerbWeightKg = 3100,
                maxWeightKg = 3000,
                freshWaterL = 120,
                greyWaterL = 100,
                fuelTankL = 90,
                gasKg = 20,
                fuelKind = 0,
                averageConsumptionLPer100Km = 12.5,
            });

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var problem = await JsonDocument.ParseAsync(
            await invalid.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("lengthMm", out _), errors.GetRawText());

        using var inconsistentWeights = await SendJsonAsync(
            HttpMethod.Put,
            CamperPath,
            etag,
            new
            {
                name = "Il Grigio",
                vehicleKind = 1,
                kerbWeightKg = 3500,
                maxWeightKg = 3000,
            });
        using var weightProblem = await JsonDocument.ParseAsync(
            await inconsistentWeights.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(
            weightProblem.RootElement.GetProperty("errors").TryGetProperty("maxWeightKg", out _),
            weightProblem.RootElement.GetRawText());
    }

    [Fact]
    public async Task camper_profile_is_not_readable_or_mutable_by_another_owner()
    {
        var otherOwnerId = Guid.NewGuid();
        const string otherEmail = "profile-other-owner@example.test";
        await CreateUserAsync(otherOwnerId, otherEmail);
        using var otherClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        using var login = await otherClient.LoginAsync(otherEmail, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var read = await otherClient.GetAsync(CamperPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        using var update = await SendJsonAsync(otherClient, HttpMethod.Put, CamperPath, "W/\"stale\"", FullProfile());
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        await using var context = CreateOwnerContext(_ownerId);
        Assert.Equal("Il Grigio", (await context.Campers.SingleAsync(
            camper => camper.Id == _camperId,
            TestContext.Current.CancellationToken)).Name);
    }

    [Fact]
    public async Task owner_can_parse_manual_specifications_and_save_detailed_measurements_map()
    {
        using var parseResponse = await SendJsonAsync(
            HttpMethod.Post,
            $"{CamperPath}/manual/parse",
            null,
            new
            {
                sourceName = "Manuale Adria Twin 540 SP Axess 2025",
                rawText = """
                    MANUALE TECNICO E OPERATIVO
                    ADRIA TWIN 540 SP AXESS
                    Base Citroën Jumper • Prima immatricolazione 2025
                    Pagina 3
                    Elemento
                    Riferimento
                    Nota
                    Lunghezza esterna
                    5.413 mm
                    Scheda Adria 540 SP; alcune schede riportano circa
                    5.410 mm.
                    Larghezza esterna
                    2.050 mm
                    La larghezza con specchi aperti è maggiore.
                    Altezza esterna
                    2.580–2.590 mm
                    Verificare accessori montati sul tetto.
                    Passo
                    3.450 mm
                    Confermare con i documenti del telaio.
                    Altezza interna
                    1.900 mm
                    Quota nominale di gamma.
                    Letto posteriore
                    1.960 × 1.340–1.160 mm
                    Letto trasversale rastremato; verificare materasso
                    reale.
                    Frigorifero
                    84 L
                    Dato di gamma; leggere la targhetta.
                    Acqua pulita
                    100 L
                    Capacità nominale pubblicata.
                    Acque grigie
                    70 L
                    Capacità nominale pubblicata.
                    Riscaldamento
                    Truma a gas
                    Identificare modello esatto sulla targhetta.
                    https://ch-it.adria-mobil.com/campervans/twin
                    """,
            });

        Assert.Equal(HttpStatusCode.OK, parseResponse.StatusCode);
        var parsed = await parseResponse.Content.ReadFromJsonAsync<CamperManualParseResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(parsed);
        Assert.Equal("Adria", parsed.Brand);
        Assert.Equal("Twin 540 SP Axess", parsed.Model);
        Assert.Equal((short)2025, parsed.Year);
        Assert.Equal(1, parsed.VehicleKind);
        Assert.Equal(0, parsed.FuelKind);
        Assert.Equal(5413, parsed.LengthMm);
        Assert.Equal(2050, parsed.WidthMm);
        Assert.Equal(2580, parsed.HeightMm);
        Assert.Equal(100, parsed.FreshWaterL);
        Assert.Equal(70, parsed.GreyWaterL);
        Assert.Equal("https://ch-it.adria-mobil.com/campervans/twin", parsed.SourceUrl);
        Assert.Equal(10, parsed.Measurements.Count);
        Assert.Contains(parsed.Measurements, item => item.Id == "rear-bed" && item.Value == "1.960 × 1.340–1.160 mm");

        using var read = await _client.GetAsync(CamperPath, TestContext.Current.CancellationToken);
        var etag = read.Headers.ETag?.ToString();
        Assert.NotNull(etag);

        using var update = await SendJsonAsync(
            HttpMethod.Put,
            CamperPath,
            etag,
            new
            {
                name = "Adria",
                brand = parsed.Brand,
                model = parsed.Model,
                year = parsed.Year,
                plateNumber = "GY301FG",
                vehicleKind = parsed.VehicleKind,
                lengthMm = parsed.LengthMm,
                widthMm = parsed.WidthMm,
                heightMm = parsed.HeightMm,
                freshWaterL = parsed.FreshWaterL,
                greyWaterL = parsed.GreyWaterL,
                fuelTankL = 90,
                gasKg = 22,
                fuelKind = parsed.FuelKind,
                manualSourceName = parsed.SourceName,
                manualSourceUrl = parsed.SourceUrl,
                manualMeasurements = parsed.Measurements,
            });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var saved = await update.Content.ReadFromJsonAsync<CamperProfileResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal("Manuale Adria Twin 540 SP Axess 2025", saved.ManualSourceName);
        Assert.Equal("https://ch-it.adria-mobil.com/campervans/twin", saved.ManualSourceUrl);
        Assert.NotNull(saved.ManualMeasurements);
        Assert.Equal(10, saved.ManualMeasurements!.Count);
    }

    private string CamperPath => $"/api/v1/campers/{_camperId:D}";

    private async Task<Guid> CreateCamperAsync()
    {
        var id = NewCamperId();
        using var response = await _client.PostJsonAsync("/api/v1/campers", new
        {
            id,
            name = "Il Grigio",
            vehicleKind = 1,
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return id;
    }

    private async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string? etag, object body)
    {
        return await SendJsonAsync(_client, method, path, etag, body);
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        RoamlyClient client,
        HttpMethod method,
        string path,
        string? etag,
        object body)
    {
        var csrf = await client.GetAsync("/api/v1/csrf-token", TestContext.Current.CancellationToken);
        var token = await csrf.Content.ReadFromJsonAsync<CsrfTokenResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(token);
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Roamly-Request", "1");
        request.Headers.Add("X-XSRF-TOKEN", token.Token);
        if (etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task CreateUserAsync(Guid userId, string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAtUtc = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
        }, Password);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private RoamlyDbContext CreateOwnerContext(Guid ownerId)
    {
        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .Options;
        return new RoamlyDbContext(options, new TestCurrentUser(ownerId));
    }

    private static object FullProfile() => new
    {
        name = "Il Grigio",
        brand = "Laika",
        model = "Kreos",
        year = 2022,
        plateNumber = "AB123CD",
        vehicleKind = 0,
        lengthMm = 7350,
        widthMm = 2320,
        heightMm = 2990,
        kerbWeightKg = 3100,
        maxWeightKg = 3500,
        freshWaterL = 120,
        greyWaterL = 100,
        fuelTankL = 90,
        gasKg = 20,
        fuelKind = 0,
        averageConsumptionLPer100Km = 12.5,
    };

    private sealed record CsrfTokenResponse(string Token);
    private sealed record CamperProfileResponse(
        Guid Id,
        string Name,
        string? Brand,
        string? Model,
        short? Year,
        string? PlateNumber,
        int VehicleKind,
        int? LengthMm,
        int? WidthMm,
        int? HeightMm,
        int? KerbWeightKg,
        int? MaxWeightKg,
        int? FreshWaterL,
        int? GreyWaterL,
        int? FuelTankL,
        int? GasKg,
        int? FuelKind,
        decimal? AverageConsumptionLPer100Km,
        string? ManualSourceName = null,
        string? ManualSourceUrl = null,
        IReadOnlyList<CamperManualMeasurementDto>? ManualMeasurements = null);

    private sealed record CamperManualMeasurementDto(
        string Id,
        string Name,
        string Value,
        string Zone,
        string View,
        int PositionX,
        int PositionY,
        string? Description = null,
        int? Page = null);

    private sealed record CamperManualParseResponse(
        string SourceName,
        string? SourceUrl,
        string? Brand,
        string? Model,
        short? Year,
        int? VehicleKind,
        int? FuelKind,
        int? LengthMm,
        int? WidthMm,
        int? HeightMm,
        int? KerbWeightKg,
        int? MaxWeightKg,
        int? FreshWaterL,
        int? GreyWaterL,
        int? FuelTankL,
        int? GasKg,
        decimal? AverageConsumptionLPer100Km,
        IReadOnlyList<CamperManualMeasurementDto> Measurements,
        IReadOnlyList<string> Warnings);

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
        public bool IsAuthenticated => true;
    }

    private static Guid NewCamperId() => new SequentialGuidGenerator(TimeProvider.System).NewId();
}
