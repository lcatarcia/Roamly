using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Roamly.Common.Email;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Authentication;

[Collection(DatabaseCollection.Name)]
public sealed class RegistrationApiTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private const string DevelopmentEmail = "development-registration@example.test";
    private const string ProductionEmail = "production-registration@example.test";
    private const string Password = "Test!Password123";
    private readonly RecordingEmailSender _developmentEmailSender = new();
    private RoamlyApiFactory _developmentFactory = null!;
    private RoamlyClient _developmentClient = null!;
    private DatabaseLease _database = null!;

    public async ValueTask InitializeAsync()
    {
        var migrated = await sqlServer.ApplicationMigratedAsync();
        Assert.Null(migrated.Failure);
        _database = migrated.Lease;
        await _database.ResetAsync(TestContext.Current.CancellationToken);

        _developmentFactory = CreateFactory(Environments.Development, _developmentEmailSender);
        _developmentClient = CreateClient(_developmentFactory);
    }

    public async ValueTask DisposeAsync()
    {
        _developmentClient?.Dispose();
        if (_developmentFactory is not null)
        {
            await _developmentFactory.DisposeAsync();
        }
    }

    [Fact]
    public async Task production_registration_endpoint_is_not_mapped()
    {
        var productionEmailSender = new RecordingEmailSender();
        await using var factory = CreateFactory(Environments.Production, productionEmailSender);
        using var client = CreateClient(factory);

        using var response = await client.PostJsonAsync(
            "/api/v1/auth/register",
            new { email = ProductionEmail, password = Password, displayName = "Prod" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(productionEmailSender.Messages);
    }

    [Fact]
    public async Task development_registration_still_sends_the_confirmation_message()
    {
        using var response = await _developmentClient.PostJsonAsync(
            "/api/v1/auth/register",
            new { email = DevelopmentEmail, password = Password, displayName = "Dev" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var message = Assert.Single(_developmentEmailSender.Messages);
        Assert.Equal(DevelopmentEmail, message.Recipient);
        Assert.Contains("UserId: ", message.Body);
        Assert.Contains("Token: ", message.Body);
    }

    private RoamlyApiFactory CreateFactory(string environmentName, RecordingEmailSender emailSender)
    {
        return new RoamlyApiFactory(
            _database.ConnectionString,
            environmentName,
            services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(emailSender);
            });
    }

    private static RoamlyClient CreateClient(RoamlyApiFactory factory)
    {
        return new RoamlyClient(factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        }));
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

        public Task SendAsync(
            string recipient,
            string subject,
            string body,
            CancellationToken cancellationToken)
        {
            lock (_messages)
            {
                _messages.Add(new SentEmail(recipient, subject, body));
            }

            return Task.CompletedTask;
        }
    }

    private sealed record SentEmail(string Recipient, string Subject, string Body);
}
