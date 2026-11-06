using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Roamly.Api.Features.Identity.Consents;
using Roamly.Api.Features.Maps;
using Roamly.Common;
using Roamly.Domain.Entities;
using Roamly.Infrastructure.Identity;
using Roamly.Infrastructure.Maps;
using Roamly.Infrastructure.Persistence;

namespace Roamly.IntegrationTests.Maps;

public sealed partial class MapsEconomicGateTests
{
    private static readonly ConsentPurposeDefinition SyntheticPurpose = new("synthetic-render", "v1", "synthetic-text", true);
    private sealed record TestUser(Guid Id) : ICurrentUser
    {
        public bool IsAuthenticated => true;
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("missing")]
    [InlineData("revoked")]
    [InlineData("old-version")]
    [InlineData("password-change")]
    [InlineData("deleting")]
    [InlineData("absent-account")]
    public async Task Invalid_account_or_consent_is_denied_before_any_economic_observation(string scenario)
    {
        var user = new TestUser(Guid.NewGuid());
        await using var db = CreateUserDb(user);
        if (scenario != "absent-account")
            await SeedIdentityAsync(db, user, scenario);
        var observer = new FakeObserver((_, _, _, _) => throw new InvalidOperationException("Must not observe"));
        var definition = scenario == "draft" ? ConsentPurposeDefinition.AzureMapsDraft : SyntheticPurpose;
        var service = Admission(db, definition, observer);
        var result = await service.CheckAsync(Ct);
        Assert.NotEqual(MapsAdmissionReason.Admitted, result);
        Assert.Equal(0, observer.Calls);
        Assert.False(await db.MapsEconomicStates.AnyAsync(Ct));
    }

    [Theory]
    [InlineData(false, MapsAdmissionReason.Admitted)]
    [InlineData(true, MapsAdmissionReason.ConsentRequired)]
    public async Task Synthetic_consent_allows_only_local_economic_admission_and_is_rechecked_after_observation(
        bool revoke, MapsAdmissionReason expected)
    {
        var user = new TestUser(Guid.NewGuid());
        await using var db = CreateUserDb(user);
        await SeedIdentityAsync(db, user, "valid");
        var observer = new FakeObserver(async (_, _, _, _) =>
        {
            if (revoke)
            {
                await using var other = CreateUserDb(user);
                await new RecordConsentHandler(other, SyntheticPurpose, _clock).HandleAsync(
                    SyntheticPurpose.Purpose, "revoke", new(Guid.NewGuid(), "v1", "synthetic-text"), Ct);
            }
            return Evidence();
        });
        Assert.Equal(expected, await Admission(db, SyntheticPurpose, observer).CheckAsync(Ct));
        Assert.Equal(1, observer.Calls);
    }

    private RoamlyDbContext CreateUserDb(ICurrentUser user) => new(new DbContextOptionsBuilder<RoamlyDbContext>()
        .UseSqlServer(_database.ConnectionString).Options, user);

    private MapsAdmissionService Admission(RoamlyDbContext db, ConsentPurposeDefinition definition, IMapsCostObserver observer) =>
        new(db, definition, new GetConsentHandler(definition, db), Gate(db, observer),
            NullLogger<MapsAdmissionService>.Instance);

    private async Task SeedIdentityAsync(RoamlyDbContext db, TestUser user, string scenario)
    {
        db.Users.Add(new ApplicationUser
        {
            Id = user.Id, UserName = "synthetic", NormalizedUserName = "SYNTHETIC",
            RequiresPasswordChange = scenario == "password-change",
            DeletionRequestedAtUtc = scenario == "deleting" ? _clock.GetUtcNow().UtcDateTime : null,
        });
        await db.SaveChangesAsync(Ct);
        if (scenario == "missing" || scenario == "draft") return;
        db.ConsentTransitions.Add(new ConsentTransition
        {
            OwnerId = user.Id, Id = Guid.NewGuid(), Purpose = SyntheticPurpose.Purpose,
            Version = scenario == "old-version" ? "old" : "v1", TextReference = "synthetic-text",
            Action = scenario == "revoked" ? "revoke" : "grant", Revision = 1,
            CreatedAtUtc = _clock.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(Ct);
        db.ChangeTracker.Clear();
    }
}
