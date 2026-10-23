using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roamly.Common.Email;
using Roamly.Infrastructure.Identity;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Authentication;

[Collection(DatabaseCollection.Name)]
public sealed class ResendEmailConfirmationApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string Email = "unconfirmed@example.test";
    private const string Password = "Test!Password123";
    private readonly Guid _userId = Guid.NewGuid();
    private readonly RecordingEmailSender _emailSender = new();
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
            Environments.Development,
            services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(_emailSender);
            });
        _client = new RoamlyClient(_factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
        await CreateUserAsync(_userId, Email, confirmed: false);
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
    public async Task development_resend_sends_a_new_token_but_returns_a_generic_response()
    {
        using var blockedLogin = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, blockedLogin.StatusCode);

        using var response = await _client.PostJsonAsync(
            "/api/v1/auth/email/resend-confirmation",
            new { email = Email },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthenticationMessageResponse>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(
            "Se la richiesta e' valida, riceverai un'email di conferma.",
            body.Message);

        var sent = Assert.Single(_emailSender.Messages);
        Assert.Equal(Email, sent.Recipient);
        Assert.StartsWith($"UserId: {_userId}\nToken: ", sent.Body);
        var tokenLine = sent.Body
            .Split('\n')
            .Single(line => line.StartsWith("Token: ", StringComparison.Ordinal));
        var token = tokenLine["Token: ".Length..].TrimEnd('\r');
        Assert.NotEmpty(token);
        var responseBody = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain(token, responseBody);
        Assert.DoesNotContain(_userId.ToString(), responseBody);

        using var confirmation = await _client.PostJsonAsync(
            "/api/v1/auth/email/confirm",
            new { userId = _userId, token },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);

        using var login = await _client.LoginAsync(
            Email,
            Password,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task unknown_confirmed_and_unconfirmed_addresses_have_identical_responses()
    {
        const string confirmedEmail = "confirmed@example.test";
        await CreateUserAsync(Guid.NewGuid(), confirmedEmail, confirmed: true);

        using var unconfirmed = await _client.PostJsonAsync(
            "/api/v1/auth/email/resend-confirmation",
            new { email = Email },
            TestContext.Current.CancellationToken);
        using var unknown = await _client.PostJsonAsync(
            "/api/v1/auth/email/resend-confirmation",
            new { email = "unknown@example.test" },
            TestContext.Current.CancellationToken);
        using var confirmed = await _client.PostJsonAsync(
            "/api/v1/auth/email/resend-confirmation",
            new { email = confirmedEmail },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, unconfirmed.StatusCode);
        Assert.Equal(unconfirmed.StatusCode, unknown.StatusCode);
        Assert.Equal(unconfirmed.StatusCode, confirmed.StatusCode);
        var genericBody = await unconfirmed.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.Equal(genericBody, await unknown.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Equal(genericBody, await confirmed.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Single(_emailSender.Messages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("non-e-un-indirizzo")]
    public async Task invalid_email_is_rejected_without_sending_a_message(string email)
    {
        using var response = await _client.PostJsonAsync(
            "/api/v1/auth/email/resend-confirmation",
            new { email },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_emailSender.Messages);
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    public async Task resend_endpoint_is_not_mapped_outside_development(string environmentName)
    {
        await using var factory = new RoamlyApiFactory(_database.ConnectionString, environmentName);
        using var client = new RoamlyClient(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));

        using var response = await client.PostJsonAsync(
            "/api/v1/auth/email/resend-confirmation",
            new { email = Email },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task resend_endpoint_remains_protected_by_the_global_csrf_middleware()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/email/resend-confirmation",
            new { email = Email },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_emailSender.Messages);
    }

    private async Task CreateUserAsync(Guid userId, string email, bool confirmed)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            EmailConfirmed = confirmed,
            CreatedAtUtc = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
        }, Password);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        private readonly List<SentEmail> _messages = [];

        public IReadOnlyList<SentEmail> Messages
        {
            get
            {
                lock (_messages)
                {
                    return _messages.ToArray();
                }
            }
        }

        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
        {
            lock (_messages)
            {
                _messages.Add(new SentEmail(recipient, subject, body));
            }

            return Task.CompletedTask;
        }
    }

    private sealed record SentEmail(string Recipient, string Subject, string Body);
    private sealed record AuthenticationMessageResponse(string Message);
}
