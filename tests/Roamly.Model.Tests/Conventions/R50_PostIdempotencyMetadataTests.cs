using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Startup;

namespace Roamly.Model.Tests.Conventions;

/// <summary>
/// R50: ogni endpoint POST registrato dichiara esplicitamente una policy d'idempotenza.
/// Mappa gli endpoint di produzione senza costruire o avviare un host HTTP.
/// </summary>
public sealed class R50_PostIdempotencyMetadataTests
{
    [Fact]
    public void every_post_endpoint_has_an_explicit_idempotency_policy()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Roamly"] = "Server=fake;Database=fake;TrustServerCertificate=True",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddRouting();
        services.AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource());
        services.AddRoamly(configuration);

        using var provider = services.BuildServiceProvider();

        var routeBuilder = new TestEndpointRouteBuilder(provider);
        routeBuilder.MapRoamlyEndpoints();

        var posts = routeBuilder.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
                    .Contains(HttpMethods.Post, StringComparer.OrdinalIgnoreCase) == true)
            .ToList();

        Assert.NotEmpty(posts);

        var missingPolicies = posts
            .Where(endpoint => endpoint.Metadata.GetMetadata<IdempotencyPolicy>() is null)
            .Select(endpoint =>
            {
                var path = endpoint is RouteEndpoint routeEndpoint
                    ? routeEndpoint.RoutePattern.RawText
                    : endpoint.DisplayName;
                return path ?? "endpoint senza nome";
            })
            .ToList();

        Assert.True(
            missingPolicies.Count == 0,
            "Endpoint POST senza IdempotencyPolicy: " + string.Join(", ", missingPolicies));

        var expenseCreate = Assert.Single(posts, endpoint =>
            endpoint is RouteEndpoint route
            && route.RoutePattern.RawText == "/api/v1/campers/{camperId:guid}/expenses");
        Assert.Equal(
            IdempotencyMode.IdentityBased,
            expenseCreate.Metadata.GetMetadata<IdempotencyPolicy>()?.Mode);

        AssertIdentityBased(
            posts,
            "/api/v1/trips/{tripId:guid}/checklists");
        AssertIdentityBased(
            posts,
            "/api/v1/trips/{tripId:guid}/checklists/{checklistId:guid}/items");
    }

    private static void AssertIdentityBased(IReadOnlyCollection<Endpoint> posts, string route)
    {
        var endpoint = Assert.Single(posts, candidate =>
            candidate is RouteEndpoint routeEndpoint
            && routeEndpoint.RoutePattern.RawText == route);
        Assert.Equal(
            IdempotencyMode.IdentityBased,
            endpoint.Metadata.GetMetadata<IdempotencyPolicy>()?.Mode);
    }

    private sealed class TestEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;

        public ICollection<EndpointDataSource> DataSources { get; } = [];

        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }
}
