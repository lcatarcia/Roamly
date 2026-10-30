using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Api.Common.Security;
using Roamly.Infrastructure.Identity;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Authentication;

[Collection(DatabaseCollection.Name)]
public sealed class ForcedPasswordChangeApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Email = "password-change@example.test";
    private const string NormalEmail = "normal-account@example.test";
    private const string Password = "Test!Password123";
    private static readonly DateTime CreatedAtUtc = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private RoamlyApiFactory _factory = null!;
    private RoamlyClient _client = null!;
    private DatabaseLease _database = null!;

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
    public async Task password_change_required_account_can_read_status_but_cannot_use_product_api()
    {
        await CreateUserAsync(Email, requiresPasswordChange: true);
        using var login = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginProfile = JsonDocument.Parse(
            await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(loginProfile.RootElement.TryGetProperty("mustChangePassword", out _));

        using var status = await _client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        using var product = await _client.GetAsync("/api/v1/campers", TestContext.Current.CancellationToken);
        using var deletionStatus = await _client.GetAsync(
            "/api/v1/me/deletion",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, product.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deletionStatus.StatusCode);
        using var deletionProblem = JsonDocument.Parse(
            await deletionStatus.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/password-change-required",
            deletionProblem.RootElement.GetProperty("type").GetString());
        using var problem = JsonDocument.Parse(
            await product.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/password-change-required",
            problem.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task password_change_required_account_is_blocked_from_protected_endpoints()
    {
        var account = await CreateUserAsync(Email, requiresPasswordChange: true);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, account.Id.ToString())],
            authenticationType: "integration-test"));
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute()),
            "protected-test-endpoint"));
        var nextCalled = false;
        var middleware = new AccountSessionAuthorizationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task password_change_required_account_cannot_create_product_resources()
    {
        await CreateUserAsync(Email, requiresPasswordChange: true);
        using var login = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var create = await _client.PostJsonAsync(
            "/api/v1/campers",
            new { id = Guid.NewGuid(), name = "Camper bloccato", vehicleKind = 1 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        using var changed = await _client.PostJsonAsync(
            "/api/v1/auth/password/change",
            new { currentPassword = Password, newPassword = "A_New!Password456" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        using var campers = await _client.GetAsync("/api/v1/campers", TestContext.Current.CancellationToken);
        using var page = JsonDocument.Parse(
            await campers.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, campers.StatusCode);
        Assert.Empty(page.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task me_reports_that_the_initial_password_must_be_changed()
    {
        await CreateUserAsync(Email, requiresPasswordChange: true);
        using var login = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var response = await _client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        using var profile = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(profile.RootElement.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task changing_initial_password_clears_restriction_and_updates_password()
    {
        await CreateUserAsync(Email, requiresPasswordChange: true);
        using var login = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var changed = await _client.PostJsonAsync(
            "/api/v1/auth/password/change",
            new { currentPassword = Password, newPassword = "A_New!Password456" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        using var profile = await _client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        using var profileJson = JsonDocument.Parse(
            await profile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var product = await _client.GetAsync("/api/v1/campers", TestContext.Current.CancellationToken);

        Assert.False(profileJson.RootElement.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, product.StatusCode);

        using var newPasswordClient = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        using var newPasswordLogin = await newPasswordClient.LoginAsync(
            Email,
            "A_New!Password456",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);

        using var oldPasswordLogin = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task wrong_current_password_does_not_clear_the_change_requirement()
    {
        await CreateUserAsync(Email, requiresPasswordChange: true);
        using var login = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var response = await _client.PostJsonAsync(
            "/api/v1/auth/password/change",
            new { currentPassword = "Wrong!Password123", newPassword = "A_New!Password456" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            "https://roamly.dev/problems/invalid-reauthentication",
            problem.RootElement.GetProperty("type").GetString());

        using var profile = await _client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        using var profileJson = JsonDocument.Parse(
            await profile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(profileJson.RootElement.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task current_password_cannot_be_reused_to_clear_the_change_requirement()
    {
        await CreateUserAsync(Email, requiresPasswordChange: true);
        using var login = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var response = await _client.PostJsonAsync(
            "/api/v1/auth/password/change",
            new { currentPassword = Password, newPassword = Password },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(
            problem.RootElement.GetProperty("errors").GetProperty("newPassword").EnumerateArray(),
            error => error.GetString() == "La nuova password deve essere diversa da quella attuale.");

        using var profile = await _client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        using var profileJson = JsonDocument.Parse(
            await profile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var product = await _client.GetAsync("/api/v1/campers", TestContext.Current.CancellationToken);

        Assert.True(profileJson.RootElement.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, product.StatusCode);
    }

    [Fact]
    public async Task password_policy_error_is_reported_on_new_password_and_keeps_restriction()
    {
        await CreateUserAsync(Email, requiresPasswordChange: true);
        using var login = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var response = await _client.PostJsonAsync(
            "/api/v1/auth/password/change",
            new { currentPassword = Password, newPassword = "short" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(
            "newPassword",
            problem.RootElement.GetProperty("errors").EnumerateObject().Select(property => property.Name));

        using var product = await _client.GetAsync("/api/v1/campers", TestContext.Current.CancellationToken);
        using var profile = await _client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        using var profileJson = JsonDocument.Parse(
            await profile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.Forbidden, product.StatusCode);
        Assert.True(profileJson.RootElement.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task ordinary_account_remains_unrestricted_and_reports_no_required_change()
    {
        await CreateUserAsync(NormalEmail, requiresPasswordChange: false);
        using var login = await _client.LoginAsync(
            NormalEmail,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var profile = await _client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        using var profileJson = JsonDocument.Parse(
            await profile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var product = await _client.GetAsync("/api/v1/campers", TestContext.Current.CancellationToken);

        Assert.False(profileJson.RootElement.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, product.StatusCode);
    }

    [Fact]
    public async Task password_change_required_account_can_logout()
    {
        await CreateUserAsync(Email, requiresPasswordChange: true);
        using var login = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var logout = await _client.PostJsonAsync(
            "/api/v1/auth/logout",
            new { },
            TestContext.Current.CancellationToken);
        using var profile = await _client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, profile.StatusCode);
    }

    private async Task<ApplicationUser> CreateUserAsync(string email, bool requiresPasswordChange)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            RequiresPasswordChange = requiresPasswordChange,
            CreatedAtUtc = CreatedAtUtc,
        };
        var result = await userManager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
        return user;
    }
}
