using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Roamly.Api.Common.Identity;
using Roamly.Api.Common.Startup;
using Roamly.Infrastructure.Identity;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Authentication;

[Collection(DatabaseCollection.Name)]
public sealed class ProvisionedUserLoginApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Email = "provisioned@example.test";
    private const string Password = "Test!Password123";
    private static readonly DateTimeOffset ProvisionedAtUtc =
        new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private readonly TestProvisioningConsole _console = new();
    private readonly FixedTimeProvider _clock = new(ProvisionedAtUtc);
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
            "Testing",
            services =>
            {
                services.RemoveAll<IProvisioningConsole>();
                services.AddSingleton<IProvisioningConsole>(_console);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_clock);
            });
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
    public async Task unconfirmed_provisioned_user_can_login_without_being_marked_confirmed()
    {
        _console.SetInput(Email, Password, Password);
        Assert.True(await OneShotJobDispatcher.RunIfRequestedAsync(
            ["--job", "provision-user"],
            _factory.Services,
            TestContext.Current.CancellationToken));

        using var response = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var profile = await _client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        using var profileJson = JsonDocument.Parse(
            await profile.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(profileJson.RootElement.GetProperty("mustChangePassword").GetBoolean());

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(Email);
        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);
        Assert.True(user.RequiresPasswordChange);
        Assert.Equal(ProvisionedAtUtc.UtcDateTime, user.CreatedAtUtc);
        Assert.DoesNotContain(Password, string.Join(Environment.NewLine, _console.Output));
    }

    [Fact]
    public async Task unconfirmed_user_without_provisioning_marker_cannot_login()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = Email,
            Email = Email,
            EmailConfirmed = false,
            CreatedAtUtc = ProvisionedAtUtc.UtcDateTime,
        };
        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

        using var response = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var accountResponse = await _client.GetAsync(
            "/api/v1/me",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, accountResponse.StatusCode);
    }

    [Fact]
    public async Task ten_failed_passwords_remain_limited_after_five_minutes_until_account_window_expires()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = "limited@example.test",
            Email = "limited@example.test",
            EmailConfirmed = true,
            CreatedAtUtc = ProvisionedAtUtc.UtcDateTime,
        };
        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var failedLogin = await _client.LoginAsync(
                user.Email!,
                "Wrong!Password123",
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);
        }

        _clock.Advance(TimeSpan.FromMinutes(6));

        await using (var lockoutScope = _factory.Services.CreateAsyncScope())
        {
            var lockoutManager = lockoutScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var userAfterLockoutReset = await lockoutManager.FindByIdAsync(user.Id.ToString());
            Assert.NotNull(userAfterLockoutReset);
            Assert.True((await lockoutManager.SetLockoutEndDateAsync(userAfterLockoutReset, null)).Succeeded);
            Assert.True((await lockoutManager.ResetAccessFailedCountAsync(userAfterLockoutReset)).Succeeded);
        }

        using var limitedLogin = await _client.LoginAsync(
            user.Email!,
            "Wrong!Password123",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, limitedLogin.StatusCode);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var currentUser = await verificationManager.FindByIdAsync(user.Id.ToString());
        Assert.NotNull(currentUser);
        Assert.False(await verificationManager.IsLockedOutAsync(currentUser));
        Assert.Equal(0, await verificationManager.GetAccessFailedCountAsync(currentUser));
    }

    [Fact]
    public async Task mismatched_password_confirmation_creates_no_account()
    {
        _console.SetInput(Email, Password, "Different!Password123");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OneShotJobDispatcher.RunIfRequestedAsync(
                ["--job", "provision-user"],
                _factory.Services,
                TestContext.Current.CancellationToken));

        Assert.Contains("non corrispondono", exception.Message);
        await AssertUserDoesNotExistAsync();
    }

    [Fact]
    public async Task duplicate_email_fails_without_replacing_the_existing_account()
    {
        _console.SetInput(Email, Password, Password);
        await OneShotJobDispatcher.RunIfRequestedAsync(
            ["--job", "provision-user"],
            _factory.Services,
            TestContext.Current.CancellationToken);
        _console.SetInput(Email, "Another!Password123", "Another!Password123");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OneShotJobDispatcher.RunIfRequestedAsync(
                ["--job", "provision-user"],
                _factory.Services,
                TestContext.Current.CancellationToken));

        Assert.Contains("Impossibile creare l'account", exception.Message);
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existingUser = await userManager.FindByEmailAsync(Email);
        Assert.NotNull(existingUser);
        Assert.True(await userManager.CheckPasswordAsync(existingUser, Password));
        Assert.False(await userManager.CheckPasswordAsync(existingUser, "Another!Password123"));
    }

    [Fact]
    public async Task provisioning_at_account_limit_is_rejected_before_reading_credentials()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        for (var index = 0; index < 3; index++)
        {
            var account = new ApplicationUser
            {
                UserName = $"existing-{index}@example.test",
                Email = $"existing-{index}@example.test",
                EmailConfirmed = true,
                CreatedAtUtc = ProvisionedAtUtc.UtcDateTime,
            };
            var result = await userManager.CreateAsync(account, Password);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        _console.SetInput(Email, Password, Password);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OneShotJobDispatcher.RunIfRequestedAsync(
                ["--job", "provision-user"],
                _factory.Services,
                TestContext.Current.CancellationToken));

        Assert.Contains("Limite massimo di 3 account", exception.Message);
        Assert.Equal(0, _console.EmailReadCount);
        Assert.Equal(0, _console.SecretReadCount);
        Assert.Equal(3, await userManager.Users.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task provisioning_requires_interactive_input_and_does_not_accept_password_arguments()
    {
        _console.IsInteractive = false;
        var nonInteractive = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OneShotJobDispatcher.RunIfRequestedAsync(
                ["--job", "provision-user"],
                _factory.Services,
                TestContext.Current.CancellationToken));
        Assert.Contains("console interattiva", nonInteractive.Message);

        _console.IsInteractive = true;
        var passwordArgument = await Assert.ThrowsAsync<ArgumentException>(() =>
            OneShotJobDispatcher.RunIfRequestedAsync(
                ["--job", "provision-user", Password],
                _factory.Services,
                TestContext.Current.CancellationToken));
        Assert.Contains("Uso job non valido", passwordArgument.Message);
        await AssertUserDoesNotExistAsync();
    }

    [Fact]
    public async Task account_erasure_remains_available_through_the_existing_job_dispatch()
    {
        Assert.True(await OneShotJobDispatcher.RunIfRequestedAsync(
            ["--job", "account-erasure"],
            _factory.Services,
            TestContext.Current.CancellationToken));
    }

    private async Task AssertUserDoesNotExistAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await userManager.FindByEmailAsync(Email));
    }

    private sealed class TestProvisioningConsole : IProvisioningConsole
    {
        private Queue<string> _inputs = new();
        private readonly List<string> _output = [];

        public bool IsInteractive { get; set; } = true;

        public IReadOnlyList<string> Output => _output;

        public int EmailReadCount { get; private set; }

        public int SecretReadCount { get; private set; }

        public string? ReadEmail()
        {
            EmailReadCount++;
            return _inputs.Dequeue();
        }

        public string ReadSecret(string prompt)
        {
            SecretReadCount++;
            return _inputs.Dequeue();
        }

        public void WriteLine(string message) => _output.Add(message);

        public void SetInput(params string[] inputs)
        {
            _inputs = new Queue<string>(inputs);
            _output.Clear();
            EmailReadCount = 0;
            SecretReadCount = 0;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
