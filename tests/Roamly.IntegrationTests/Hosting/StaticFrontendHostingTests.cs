using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Roamly.IntegrationTests.Infrastructure;

namespace Roamly.IntegrationTests.Hosting;

[Collection(DatabaseCollection.Name)]
public sealed class StaticFrontendHostingTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task Root_and_built_asset_are_served_with_expected_cache_policy()
    {
        await using var host = await CreateHostAsync();
        using var client = host.CreateClient();

        using var root = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var rootContent = await root.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Contains("SPA shell", rootContent, StringComparison.Ordinal);
        Assert.Equal("no-store", root.Headers.CacheControl?.ToString());

        using var asset = await client.GetAsync(
            "/assets/app-abc12345.js",
            TestContext.Current.CancellationToken);
        var assetContent = await asset.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        Assert.Equal("window.roamlyBundleLoaded = true;", assetContent);
        Assert.Equal("public, max-age=31536000, immutable", asset.Headers.CacheControl?.ToString());

        using var unfingerprintedAsset = await client.GetAsync(
            "/assets/app.js",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, unfingerprintedAsset.StatusCode);
        Assert.Null(unfingerprintedAsset.Headers.CacheControl);

        using var font = await client.GetAsync(
            "/assets/inter-subset-a1b2c3d4.woff2",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, font.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", font.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Direct_client_route_serves_the_spa_shell()
    {
        await using var host = await CreateHostAsync();
        using var client = host.CreateClient();

        using var response = await client.GetAsync(
            "/email/confirm?userId=sample",
            TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("SPA shell", content, StringComparison.Ordinal);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Unknown_api_path_does_not_serve_the_spa_shell()
    {
        await using var host = await CreateHostAsync();
        using var client = host.CreateClient();

        using var response = await client.GetAsync(
            "/api/does-not-exist",
            TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("SPA shell", content, StringComparison.Ordinal);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_static_and_health_paths_do_not_serve_the_spa_shell()
    {
        await using var host = await CreateHostAsync();
        using var client = host.CreateClient();

        foreach (var path in new[] { "/assets/missing.js", "/health/unknown" })
        {
            using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("SPA shell", content, StringComparison.Ordinal);
            Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        }
    }

    private async Task<StaticFrontendHost> CreateHostAsync()
    {
        var database = await sqlServer.Phase1MigratedAsync();
        Assert.Null(database.Failure);

        return new StaticFrontendHost(database.Lease.ConnectionString);
    }

    private sealed class StaticFrontendHost : IAsyncDisposable
    {
        private static readonly string TestHmacSecret =
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        private readonly string _webRootPath = Path.Combine(
            Path.GetTempPath(),
            "roamly-static-hosting-" + Guid.NewGuid().ToString("N"));
        private readonly StaticFrontendApiFactory _factory;

        public StaticFrontendHost(string connectionString)
        {
            Directory.CreateDirectory(Path.Combine(_webRootPath, "assets"));
            File.WriteAllText(
                Path.Combine(_webRootPath, "index.html"),
                "<!doctype html><html><body>SPA shell</body></html>");
            File.WriteAllText(
                Path.Combine(_webRootPath, "assets", "app-abc12345.js"),
                "window.roamlyBundleLoaded = true;");
            File.WriteAllText(
                Path.Combine(_webRootPath, "assets", "app.js"),
                "window.roamlyUnfingerprintedBundle = true;");
            File.WriteAllText(
                Path.Combine(_webRootPath, "assets", "inter-subset-a1b2c3d4.woff2"),
                "font fixture");

            _factory = new StaticFrontendApiFactory(_webRootPath, connectionString);
        }

        public HttpClient CreateClient() => _factory.CreateClient();

        public async ValueTask DisposeAsync()
        {
            await _factory.DisposeAsync();
            Directory.Delete(_webRootPath, recursive: true);
        }

        private sealed class StaticFrontendApiFactory(
            string webRootPath,
            string connectionString) : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Testing");
                builder.UseWebRoot(webRootPath);
            }

            protected override IHost CreateHost(IHostBuilder builder)
            {
                builder.ConfigureHostConfiguration(configuration =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Roamly"] = connectionString,
                        ["RateLimiting:Login:HmacSecret"] = TestHmacSecret,
                    }));

                return base.CreateHost(builder);
            }
        }
    }
}
