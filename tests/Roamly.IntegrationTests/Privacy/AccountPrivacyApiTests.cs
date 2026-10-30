using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roamly.Api.Common.Security;
using Roamly.Common;
using Roamly.Common.Storage;
using Roamly.Domain.Abstractions;
using Roamly.Domain.Entities;
using Roamly.Domain.Enums;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Persistence;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Privacy;

[Collection(DatabaseCollection.Name)]
public sealed class AccountPrivacyApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Password = "Test!Password123";
    private const string Email = "privacy-owner@example.test";
    private const string PasswordAuthenticatedAtUtcClaim = "roamly:password-authenticated-at-utc";
    private static readonly DateTimeOffset ClockNow = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _otherOwnerId = Guid.NewGuid();
    private readonly FixedTimeProvider _clock = new(ClockNow);
    private readonly TestPerAccountRateLimiter _rateLimiter = new();
    private static readonly byte[] JournalImageBytes = [0xFF, 0xD8, 0xFF, 0xD9];
    private Guid _ownerJournalEntryId;
    private Guid _ownerJournalAttachmentId;
    private string? _freshnessClaimOverride;
    private bool _removeFreshnessClaim;
    private bool _duplicateFreshnessClaim;
    private bool _forceSecurityStampValidation;
    private int _securityStampRefreshCount;
    private RoamlyApiFactory _factory = null!;
    private RoamlyClient _client = null!;
    private DatabaseLease _database = null!;

    public async ValueTask InitializeAsync()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        _database = migrated.Lease;
        await _database.ResetAsync(TestContext.Current.CancellationToken);

        _factory = new RoamlyApiFactory(
            _database.ConnectionString,
            journalImagesAzuriteConnectionString: sqlServer.JournalImageStorageConnectionString,
            configureTestServices: services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_clock);
                services.RemoveAll<IPerAccountRateLimiter>();
                services.AddSingleton<IPerAccountRateLimiter>(_rateLimiter);
                services.PostConfigure<CookieAuthenticationOptions>(
                    IdentityConstants.ApplicationScheme,
                    options =>
                    {
                        var previous = options.Events.OnSigningIn;
                        options.Events.OnSigningIn = async context =>
                        {
                            if (previous is not null)
                            {
                                await previous(context);
                            }

                            OverrideFreshnessClaim(context.Principal);
                        };

                        var previousValidation = options.Events.OnValidatePrincipal;
                        options.Events.OnValidatePrincipal = async context =>
                        {
                            if (_forceSecurityStampValidation)
                            {
                                await SecurityStampValidator.ValidatePrincipalAsync(context);
                            }

                            if (previousValidation is not null)
                            {
                                await previousValidation(context);
                            }
                        };
                    });
                services.PostConfigure<SecurityStampValidatorOptions>(options =>
                {
                    // Forza la validazione Identity sulla successiva richiesta HTTP autenticata,
                    // rendendo osservabile il callback senza dipendere da tempi reali di polling.
                    options.ValidationInterval = TimeSpan.Zero;
                    var previous = options.OnRefreshingPrincipal;
                    options.OnRefreshingPrincipal = async context =>
                    {
                        Interlocked.Increment(ref _securityStampRefreshCount);
                        if (previous is not null)
                        {
                            await previous(context);
                        }
                    };
                });
            });
        _client = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        await CreateUserAsync(_ownerId, Email);
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
    public async Task export_streams_a_model_driven_zip_with_only_the_authenticated_owners_data()
    {
        await CreateUserAsync(_otherOwnerId, "other-owner@example.test");
        await SeedOwnedApplicationDataAsync(_ownerId, "owner-a-private-marker");
        await SeedOwnedApplicationDataAsync(_otherOwnerId, "owner-b-private-marker");
        using var login = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var response = await _client.GetAsync("/api/v1/me/export", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("roamly-export.zip", response.Content.Headers.ContentDisposition?.ToString());
        Assert.DoesNotContain(Email, response.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);

        await using var responseStream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var archive = new ZipArchive(responseStream, ZipArchiveMode.Read, leaveOpen: true);
        var manifestEntry = Assert.Single(archive.Entries, entry => entry.FullName == "manifest.json");
        using var manifestStream = manifestEntry.Open();
        using var manifest = await JsonDocument.ParseAsync(
            manifestStream,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, manifest.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.Equal(ClockNow.UtcDateTime, manifest.RootElement.GetProperty("generatedAtUtc").GetDateTime());

        var runtimeEntityTypes = GetOwnedRuntimeEntityTypes();
        var expectedNames = runtimeEntityTypes
            .Select(entityType => entityType.ClrType.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var expectedPaths = expectedNames.Select(name => $"entities/{name}.json")
            .Append($"attachments/{_ownerJournalEntryId:D}/{_ownerJournalAttachmentId:D}")
            .Append("manifest.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedPaths, archive.Entries.Select(entry => entry.FullName)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray());

        var actualCounts = new Dictionary<string, long>(StringComparer.Ordinal);
        var exportedRecords = new List<string>();
        foreach (var entityType in runtimeEntityTypes)
        {
            var entityName = entityType.ClrType.Name;
            var entry = Assert.Single(archive.Entries, item => item.FullName == $"entities/{entityName}.json");
            using var entityStream = entry.Open();
            using var entities = await JsonDocument.ParseAsync(
                entityStream,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(JsonValueKind.Array, entities.RootElement.ValueKind);
            var records = entities.RootElement.EnumerateArray().ToArray();
            Assert.Single(records);
            actualCounts.Add(entityName, records.LongLength);
            foreach (var record in records)
            {
                AssertJsonObjectMatchesEntityType(record, entityType);
                Assert.Equal(_ownerId, record.GetProperty("ownerId").GetGuid());
                var recordText = record.GetRawText();
                Assert.DoesNotContain(_otherOwnerId.ToString("D"), recordText, StringComparison.OrdinalIgnoreCase);
                exportedRecords.Add(recordText);
            }
        }

        var allExportedData = string.Join('\n', exportedRecords);
        Assert.Contains("owner-a-private-marker", allExportedData, StringComparison.Ordinal);
        Assert.DoesNotContain("owner-b-private-marker", allExportedData, StringComparison.Ordinal);
        Assert.DoesNotContain(
            archive.Entries.Select(entry => entry.FullName),
            name => name.Contains("Identity", StringComparison.Ordinal)
                || name.Contains("ErasureReceipt", StringComparison.Ordinal));

        var journalImagePath = $"attachments/{_ownerJournalEntryId:D}/{_ownerJournalAttachmentId:D}";
        var exportedImage = Assert.Single(archive.Entries, entry => entry.FullName == journalImagePath);
        await using (var imageStream = exportedImage.Open())
        using (var imageBytes = new MemoryStream())
        {
            await imageStream.CopyToAsync(imageBytes, TestContext.Current.CancellationToken);
            Assert.Equal(JournalImageBytes, imageBytes.ToArray());
        }

        var manifestFiles = manifest.RootElement.GetProperty("files");
        var files = manifestFiles.EnumerateArray().ToArray();
        Assert.Equal(expectedNames.Append("JournalEntryAttachmentBlob").OrderBy(name => name, StringComparer.Ordinal),
            files
            .Select(file => file.GetProperty("entityType").GetString()!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray());
        foreach (var file in files)
        {
            var entityName = file.GetProperty("entityType").GetString()!;
            if (entityName == "JournalEntryAttachmentBlob")
            {
                Assert.Equal("attachments/{JournalEntryId}/{AttachmentId}", file.GetProperty("path").GetString());
                Assert.Equal(1, file.GetProperty("count").GetInt64());
                continue;
            }

            Assert.Equal($"entities/{entityName}.json", file.GetProperty("path").GetString());
            Assert.Equal(actualCounts[entityName], file.GetProperty("count").GetInt64());
            Assert.Equal(1, actualCounts[entityName]);
        }
    }

    [Fact]
    public async Task export_succeeds_at_the_exact_five_minute_freshness_boundary()
    {
        using var login = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        _clock.Advance(TimeSpan.FromMinutes(5));

        using var response = await _client.GetAsync("/api/v1/me/export", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task export_requires_a_password_login_no_older_than_five_minutes()
    {
        using var login = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        _clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));

        using var response = await _client.GetAsync("/api/v1/me/export", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Null(response.Content.Headers.ContentDisposition);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/recent-password-required",
            problem.RootElement.GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("future")]
    [InlineData("duplicate")]
    public async Task export_rejects_missing_malformed_future_or_duplicate_password_claim(string claimCase)
    {
        if (claimCase == "missing")
        {
            _removeFreshnessClaim = true;
        }
        else if (claimCase == "duplicate")
        {
            _duplicateFreshnessClaim = true;
        }
        else
        {
            _freshnessClaimOverride = claimCase == "future"
                ? ClockNow.AddSeconds(1).UtcDateTime.ToString("O")
                : "not-a-utc-timestamp";
        }

        using var login = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var response = await _client.GetAsync("/api/v1/me/export", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Content.Headers.ContentDisposition);
    }

    [Fact]
    public async Task identity_stamp_refresh_preserves_the_original_password_time_without_extending_freshness()
    {
        _forceSecurityStampValidation = true;
        using var login = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        _clock.Advance(TimeSpan.FromMinutes(5));
        using var profile = await _client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.True(
            Volatile.Read(ref _securityStampRefreshCount) > 0,
            "La richiesta HTTP al boundary deve eseguire il callback di refresh del security stamp.");
        _clock.Advance(TimeSpan.FromSeconds(1));

        using var response = await _client.GetAsync("/api/v1/me/export", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task export_rate_limit_returns_problem_details_before_zip_streaming()
    {
        using var login = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        _rateLimiter.RejectExports = true;

        using var response = await _client.GetAsync("/api/v1/me/export", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Content.Headers.ContentDisposition);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal("https://roamly.dev/problems/too-many-attempts",
            problem.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task deletion_preview_counts_only_the_current_owner_and_marks_counts_informational()
    {
        await CreateUserAsync(_otherOwnerId, "other-owner@example.test");
        await SeedOwnedApplicationDataAsync(_ownerId, "owner-a-preview-marker");
        await SeedOwnedApplicationDataAsync(_otherOwnerId, "owner-b-preview-marker");
        using var login = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var response = await _client.GetAsync(
            "/api/v1/me/deletion/preview",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var preview = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(ClockNow.UtcDateTime, preview.RootElement.GetProperty("generatedAtUtc").GetDateTime());
        var runtimeEntityNames = GetOwnedRuntimeEntityTypes()
            .Select(entityType => entityType.ClrType.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var counts = preview.RootElement.GetProperty("entityCounts").EnumerateArray().ToArray();
        Assert.Equal(runtimeEntityNames, counts
            .Select(item => item.GetProperty("entityType").GetString()!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray());
        Assert.All(counts, item => Assert.Equal(1, item.GetProperty("count").GetInt64()));
        Assert.All(counts, item => Assert.True(item.GetProperty("count").GetInt64() > 0));
        Assert.Equal(counts.Sum(item => item.GetProperty("count").GetInt64()),
            preview.RootElement.GetProperty("totalCount").GetInt64());
        Assert.Equal((long)runtimeEntityNames.Length, preview.RootElement.GetProperty("totalCount").GetInt64());
        Assert.DoesNotContain("owner-b-preview-marker", preview.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task deletion_status_is_limited_to_state_and_timestamps_and_limited_session_cannot_preview_or_export()
    {
        var requestedAtUtc = ClockNow.AddDays(-2).UtcDateTime;
        await CreateUserAsync(
            _otherOwnerId,
            "limited-owner@example.test",
            requestedAtUtc,
            requestedAtUtc.AddDays(30));
        await AddCamperAsync(_otherOwnerId, "Non restituire questo record");
        using var login = await _client.LoginAsync(
            "limited-owner@example.test",
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var status = await _client.GetAsync("/api/v1/me/deletion", TestContext.Current.CancellationToken);
        using var preview = await _client.GetAsync(
            "/api/v1/me/deletion/preview",
            TestContext.Current.CancellationToken);
        using var export = await _client.GetAsync("/api/v1/me/export", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var statusBody = JsonDocument.Parse(await status.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal("scheduled", statusBody.RootElement.GetProperty("status").GetString());
        Assert.Equal(requestedAtUtc, statusBody.RootElement.GetProperty("deletionRequestedAtUtc").GetDateTime());
        Assert.Equal(requestedAtUtc.AddDays(30),
            statusBody.RootElement.GetProperty("deletionScheduledForUtc").GetDateTime());
        Assert.Equal(3, statusBody.RootElement.EnumerateObject().Count());
        Assert.DoesNotContain("Non restituire", statusBody.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("count", statusBody.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        await AssertRestrictedSessionProblemAsync(preview);
        await AssertRestrictedSessionProblemAsync(export);

        using var revoked = await _client.DeleteWithCsrfAsync(
            "/api/v1/me/deletion",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        using var statusAfterRevocation = await _client.GetAsync(
            "/api/v1/me/deletion",
            TestContext.Current.CancellationToken);
        using var previewAfterRevocation = await _client.GetAsync(
            "/api/v1/me/deletion/preview",
            TestContext.Current.CancellationToken);
        using var exportAfterRevocation = await _client.GetAsync(
            "/api/v1/me/export",
            TestContext.Current.CancellationToken);
        using var logout = await _client.PostJsonAsync(
            "/api/v1/auth/logout",
            new { },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, statusAfterRevocation.StatusCode);
        await AssertRestrictedSessionProblemAsync(previewAfterRevocation);
        await AssertRestrictedSessionProblemAsync(exportAfterRevocation);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task deletion_status_returns_no_content_when_there_is_no_active_request()
    {
        using var login = await _client.LoginAsync(Email, Password, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var response = await _client.GetAsync("/api/v1/me/deletion", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    private async Task CreateUserAsync(
        Guid userId,
        string email,
        DateTime? deletionRequestedAtUtc = null,
        DateTime? deletionScheduledForUtc = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAtUtc = ClockNow.AddDays(-30).UtcDateTime,
            DeletionRequestedAtUtc = deletionRequestedAtUtc,
            DeletionScheduledForUtc = deletionScheduledForUtc,
        }, Password);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private async Task AddCamperAsync(Guid ownerId, string name)
    {
        await using var db = CreateOwnerContext(ownerId);
        db.Campers.Add(new Camper
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = name,
            VehicleKind = VehicleKind.Coachbuilt,
            Dimensions = new Roamly.Domain.ValueObjects.Dimensions(7400, 2300, 2900),
            CreatedAtUtc = ClockNow.UtcDateTime,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedOwnedApplicationDataAsync(Guid ownerId, string marker)
    {
        await using var db = CreateOwnerContext(ownerId);
        var camperId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var savedPlaceId = Guid.NewGuid();
        var maintenanceItemId = Guid.NewGuid();
        var journalEntryId = Guid.NewGuid();
        var journalAttachmentId = Guid.NewGuid();
        var createdAtUtc = ClockNow.UtcDateTime;
        db.Campers.Add(new Camper
        {
            Id = camperId,
            OwnerId = ownerId,
            Name = $"{marker}-camper",
            VehicleKind = VehicleKind.Coachbuilt,
            Dimensions = new Roamly.Domain.ValueObjects.Dimensions(7400, 2300, 2900),
            CreatedAtUtc = createdAtUtc,
        });
        db.Equipment.Add(new Equipment
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            CamperId = camperId,
            Name = $"{marker}-equipment",
            Category = EquipmentCategory.Solar,
            Notes = $"{marker}-equipment-note",
            CreatedAtUtc = createdAtUtc,
        });
        db.MaintenanceItems.Add(new MaintenanceItem
        {
            Id = maintenanceItemId,
            OwnerId = ownerId,
            CamperId = camperId,
            Name = $"{marker}-maintenance",
            Category = MaintenanceCategory.Engine,
            IntervalKm = 5000,
            Origin = MaintenanceOrigin.User,
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
        });
        db.MaintenanceLogs.Add(new MaintenanceLog
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            MaintenanceItemId = maintenanceItemId,
            PerformedOnUtc = DateOnly.FromDateTime(createdAtUtc),
            Notes = $"{marker}-maintenance-log",
            CreatedAtUtc = createdAtUtc,
        });
        db.OdometerReadings.Add(new OdometerReading
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            CamperId = camperId,
            ReadingKm = 12345,
            TakenOnUtc = DateOnly.FromDateTime(createdAtUtc),
            Source = OdometerSource.Manual,
            CreatedAtUtc = createdAtUtc,
        });
        db.Trips.Add(new Trip
        {
            Id = tripId,
            OwnerId = ownerId,
            CamperId = camperId,
            Name = $"{marker}-trip",
            CreatedAtUtc = createdAtUtc,
        });
        var checklistId = Guid.NewGuid();
        db.Checklists.Add(new Checklist
        {
            Id = checklistId,
            OwnerId = ownerId,
            TripId = tripId,
            Name = $"{marker}-checklist",
            Kind = ChecklistKind.Departure,
            Origin = MaintenanceOrigin.User,
            CreatedAtUtc = createdAtUtc,
        });
        db.ChecklistItems.Add(new ChecklistItem
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            ChecklistId = checklistId,
            Text = $"{marker}-checklist-item",
            SortOrder = 1,
            CreatedAtUtc = createdAtUtc,
        });
        db.Expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            CamperId = camperId,
            TripId = tripId,
            Amount = new Roamly.Domain.ValueObjects.Money(42.125m, "EUR"),
            Category = ExpenseCategory.Fuel,
            IncurredOnUtc = DateOnly.FromDateTime(createdAtUtc),
            Description = $"{marker}-expense",
            CreatedAtUtc = createdAtUtc,
        });
        db.SavedPlaces.Add(new SavedPlace
        {
            Id = savedPlaceId,
            OwnerId = ownerId,
            Name = $"{marker}-place",
            Category = SavedPlaceCategory.Aire,
            CreatedAtUtc = createdAtUtc,
        });
        db.TripStops.Add(new TripStop
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            TripId = tripId,
            SavedPlaceId = savedPlaceId,
            SequenceNo = 1,
            Name = $"{marker}-stop",
            CreatedAtUtc = createdAtUtc,
        });
        db.JournalEntries.Add(new JournalEntry
        {
            Id = journalEntryId,
            OwnerId = ownerId,
            TripId = tripId,
            EntryOnUtc = DateOnly.FromDateTime(createdAtUtc),
            Title = $"{marker}-journal-title",
            Body = $"{marker}-journal-body",
            CreatedAtUtc = createdAtUtc,
        });
        var storageKey =
            $"{ownerId:D}/journal-entry-attachment/{journalEntryId:D}/{journalAttachmentId:D}";
        db.JournalEntryAttachments.Add(new JournalEntryAttachment
        {
            Id = journalAttachmentId,
            OwnerId = ownerId,
            JournalEntryId = journalEntryId,
            StorageKey = storageKey,
            ContentType = "image/jpeg",
            SizeBytes = JournalImageBytes.Length,
            CreatedAtUtc = createdAtUtc,
        });
        db.JournalAttachmentDeletionOutbox.Add(new JournalAttachmentDeletionOutboxMessage
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            StorageKey =
                $"{ownerId:D}/journal-entry-attachment/{journalEntryId:D}/{Guid.NewGuid():D}",
            CreatedAtUtc = createdAtUtc,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var scope = _factory.Services.CreateAsyncScope();
        var imageStorage = scope.ServiceProvider.GetRequiredService<IJournalImageStorage>();
        await using var image = new MemoryStream(JournalImageBytes, writable: false);
        await imageStorage.UploadAsync(
            storageKey,
            image,
            "image/jpeg",
            TestContext.Current.CancellationToken);
        if (ownerId == _ownerId)
        {
            _ownerJournalEntryId = journalEntryId;
            _ownerJournalAttachmentId = journalAttachmentId;
        }
    }

    private IEntityType[] GetOwnedRuntimeEntityTypes()
    {
        using var db = CreateOwnerContext(_ownerId);
        return db.Model.GetEntityTypes()
            .Where(entityType => typeof(IOwnedResource).IsAssignableFrom(entityType.ClrType))
            .OrderBy(entityType => entityType.ClrType.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertJsonObjectMatchesEntityType(JsonElement json, ITypeBase entityType)
    {
        Assert.Equal(JsonValueKind.Object, json.ValueKind);
        var expectedPropertyNames = entityType.GetProperties()
            .Select(property => JsonNamingPolicy.CamelCase!.ConvertName(property.Name))
            .Concat(entityType.GetComplexProperties()
                .Select(property => JsonNamingPolicy.CamelCase!.ConvertName(property.Name)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var actualPropertyNames = json.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedPropertyNames, actualPropertyNames);

        foreach (var complexProperty in entityType.GetComplexProperties())
        {
            var propertyName = JsonNamingPolicy.CamelCase!.ConvertName(complexProperty.Name);
            var complexValue = json.GetProperty(propertyName);
            if (complexValue.ValueKind != JsonValueKind.Null)
            {
                AssertJsonObjectMatchesEntityType(complexValue, complexProperty.ComplexType);
            }
        }
    }

    private static async Task AssertRestrictedSessionProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Content.Headers.ContentDisposition);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(403, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Accesso non consentito.", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "https://roamly.dev/problems/restricted-session",
            problem.RootElement.GetProperty("type").GetString());
    }

    private RoamlyDbContext CreateOwnerContext(Guid ownerId)
    {
        var options = new DbContextOptionsBuilder<RoamlyDbContext>()
            .UseSqlServer(_database.ConnectionString)
            .Options;
        return new RoamlyDbContext(options, new TestCurrentUser(ownerId));
    }

    private sealed class TestCurrentUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
        public bool IsAuthenticated => true;
    }

    private void OverrideFreshnessClaim(ClaimsPrincipal? principal)
    {
        if (!_removeFreshnessClaim && _freshnessClaimOverride is null && !_duplicateFreshnessClaim)
        {
            return;
        }

        var identity = Assert.IsType<ClaimsIdentity>(principal?.Identity);
        foreach (var claim in identity.FindAll(PasswordAuthenticatedAtUtcClaim).ToArray())
        {
            identity.RemoveClaim(claim);
        }

        if (!_removeFreshnessClaim)
        {
            var claimValue = _freshnessClaimOverride
                ?? ClockNow.UtcDateTime.ToString("O");
            identity.AddClaim(new Claim(
                PasswordAuthenticatedAtUtcClaim,
                claimValue));
            if (_duplicateFreshnessClaim)
            {
                identity.AddClaim(new Claim(PasswordAuthenticatedAtUtcClaim, claimValue));
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }

    private sealed class TestPerAccountRateLimiter : IPerAccountRateLimiter
    {
        public bool RejectExports { get; set; }

        public bool TryAcquire(string accountKey) =>
            !RejectExports || !accountKey.StartsWith("account-export:", StringComparison.Ordinal);
    }
}
