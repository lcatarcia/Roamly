using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Roamly.Api.Common.Startup;

namespace Roamly.Model.Tests.Conventions;

/// <summary>R45: i challenge cookie producono sempre ProblemDetails sul wire, non redirect o risposte vuote.</summary>
public sealed class R45_CookieChallengeProblemDetailsTests
{
    [Theory]
    [InlineData(true, StatusCodes.Status401Unauthorized, "Autenticazione richiesta.", "unauthorized")]
    [InlineData(false, StatusCodes.Status403Forbidden, "Accesso non consentito.", "forbidden")]
    public async Task cookie_challenges_write_problem_details(
        bool loginChallenge,
        int expectedStatus,
        string expectedTitle,
        string problemType)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var services = new ServiceCollection();
        services.AddSecurity(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
        };
        await using var body = new MemoryStream();
        context.Response.Body = body;

        var scheme = new AuthenticationScheme(
            IdentityConstants.ApplicationScheme,
            IdentityConstants.ApplicationScheme,
            typeof(CookieAuthenticationHandler));
        var redirect = new RedirectContext<CookieAuthenticationOptions>(
            context,
            scheme,
            options,
            new AuthenticationProperties(),
            "/Identity/Account/Login");

        if (loginChallenge)
        {
            await options.Events.OnRedirectToLogin(redirect);
        }
        else
        {
            await options.Events.OnRedirectToAccessDenied(redirect);
        }

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(
            body,
            cancellationToken: TestContext.Current.CancellationToken);
        var root = problem.RootElement;
        Assert.Equal(expectedStatus, root.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, root.GetProperty("title").GetString());
        Assert.Equal("https://roamly.dev/problems/" + problemType, root.GetProperty("type").GetString());
        Assert.False(root.TryGetProperty("detail", out _));
        Assert.False(root.TryGetProperty("instance", out _));
        Assert.True(root.TryGetProperty("traceId", out _));
    }
}
