using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Roamly.Infrastructure.Storage;

namespace Roamly.Model.Tests.Privacy;

public sealed class JournalImageStorageSettingsTests
{
    [Fact]
    public void Azurite_is_rejected_outside_development_and_testing()
    {
        var configuration = CreateConfiguration();

        Assert.Throws<InvalidOperationException>(
            () => JournalImageStorageSettings.From(configuration, CreateEnvironment(Environments.Production)));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Azurite_is_allowed_in_development_and_testing(string environmentName)
    {
        var settings = JournalImageStorageSettings.From(
            CreateConfiguration(),
            CreateEnvironment(environmentName));

        Assert.Equal("Azurite", settings.Provider);
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JournalImages:Provider"] = "Azurite",
                ["JournalImages:Azurite:ConnectionString"] = "UseDevelopmentStorage=true",
            })
            .Build();

    private static TestHostEnvironment CreateEnvironment(string environmentName) =>
        new TestHostEnvironment { EnvironmentName = environmentName };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = nameof(JournalImageStorageSettingsTests);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
