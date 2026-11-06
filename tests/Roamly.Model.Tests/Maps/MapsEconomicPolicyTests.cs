using Roamly.Infrastructure.Maps;

namespace Roamly.Model.Tests.Maps;

public sealed class MapsEconomicPolicyTests
{
    [Fact]
    public void Incomplete_default_configuration_denies_even_a_zero_cost_observation()
    {
        var policy = new MapsEconomicPolicy();
        Assert.Equal(MapsAdmissionReason.NotConfigured, policy.Validate(null, new DateTime(2026, 11, 6)));
    }
}
