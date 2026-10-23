using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Api.Common.Http;
using Roamly.Api.Common.Startup;
using Roamly.Domain.Entities;

namespace Roamly.Model.Tests.Conventions;

/// <summary>R49: i confini di concorrenza degli update indipendenti hanno rowversion.</summary>
public sealed class R49_TripAggregateConcurrencyTests(ModelFixture model)
{
    [Fact]
    public void rowversion_etags_are_strong_and_reject_weak_tokens()
    {
        byte[] rowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

        var etag = RowVersionEtag.Format(rowVersion);

        Assert.Equal("\"AQIDBAUGBwg\"", etag);
        Assert.True(RowVersionEtag.TryParse(etag, out var parsed));
        Assert.Equal(rowVersion, parsed);
        Assert.False(RowVersionEtag.TryParse($"W/{etag}", out _));
    }

    [Fact]
    public void trip_is_the_concurrency_boundary_and_trip_stop_has_no_independent_token()
    {
        var trip = Assert.IsAssignableFrom<IEntityType>(model.FullSchemaModel.FindEntityType(typeof(Trip)));
        var rowVersion = trip.FindProperty(nameof(Trip.RowVersion));
        Assert.NotNull(rowVersion);
        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);

        var stop = Assert.IsAssignableFrom<IEntityType>(model.FullSchemaModel.FindEntityType(typeof(TripStop)));
        Assert.Null(stop.FindProperty("RowVersion"));
        Assert.NotNull(model.ApplicationModel.FindEntityType(typeof(Trip)));
        Assert.NotNull(model.ApplicationModel.FindEntityType(typeof(TripStop)));
    }

    [Fact]
    public void checklist_children_share_the_trip_concurrency_boundary()
    {
        var trip = Assert.IsAssignableFrom<IEntityType>(model.ApplicationModel.FindEntityType(typeof(Trip)));
        var checklist = Assert.IsAssignableFrom<IEntityType>(
            model.ApplicationModel.FindEntityType(typeof(Checklist)));
        var item = Assert.IsAssignableFrom<IEntityType>(
            model.ApplicationModel.FindEntityType(typeof(ChecklistItem)));
        Assert.NotNull(trip.FindProperty(nameof(Trip.RowVersion)));
        Assert.Null(checklist.FindProperty("RowVersion"));
        Assert.Null(item.FindProperty("RowVersion"));
    }

    [Fact]
    public void expense_is_an_independent_concurrency_boundary_for_put_and_delete()
    {
        var expense = Assert.IsAssignableFrom<IEntityType>(model.FullSchemaModel.FindEntityType(typeof(Expense)));
        var rowVersion = expense.FindProperty(nameof(Expense.RowVersion));
        Assert.NotNull(rowVersion);
        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        Assert.NotNull(model.ApplicationModel.FindEntityType(typeof(Expense)));

        var (provider, sources) = MapEndpoints();
        using (provider)
        {
            var endpoints = sources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
            Assert.Contains(endpoints, endpoint => endpoint.RoutePattern.RawText == "/api/v1/expenses/{expenseId:guid}"
                && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(
                    HttpMethods.Put, StringComparer.OrdinalIgnoreCase) == true);
            Assert.Contains(endpoints, endpoint => endpoint.RoutePattern.RawText == "/api/v1/expenses/{expenseId:guid}"
                && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(
                    HttpMethods.Delete, StringComparer.OrdinalIgnoreCase) == true);
        }
    }

    [Fact]
    public void trip_stop_update_is_exposed_only_through_the_root_trip_put()
    {
        var (provider, sources) = MapEndpoints();
        using (provider)
        {
            var updates = sources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(endpoint => endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods
                    .Contains(HttpMethods.Put, StringComparer.OrdinalIgnoreCase) == true)
                .ToArray();

            Assert.Contains(updates, endpoint => endpoint.RoutePattern.RawText == "/api/v1/trips/{tripId:guid}");
            Assert.DoesNotContain(
                updates,
                endpoint => endpoint.RoutePattern.RawText?.Contains("stops", StringComparison.Ordinal) == true);
        }
    }

    private static (ServiceProvider Provider, ICollection<EndpointDataSource> Sources) MapEndpoints()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Roamly"] = "Server=fake;Database=fake;TrustServerCertificate=True",
                ["RateLimiting:Login:HmacSecret"] = Convert.ToBase64String(new byte[32]),
            })
            .Build();
        var services = new ServiceCollection();
        services.AddRouting();
        services.AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource());
        services.AddRoamly(configuration);
        var provider = services.BuildServiceProvider();
        var routeBuilder = new TestEndpointRouteBuilder(provider);
        routeBuilder.MapRoamlyEndpoints();
        return (provider, routeBuilder.DataSources);
    }

    private sealed class TestEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = [];
        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(ServiceProvider);
    }
}
