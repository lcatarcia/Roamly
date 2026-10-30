using System.Reflection;
using Roamly.Api.Common.Http;
using Xunit;

namespace Roamly.Model.Tests.Conventions;

/// <summary>R47: distingue gli esiti di dominio dai contratti di protocollo dichiarati per handler.</summary>
public sealed class R47_HandlerOutcomeTests
{
    [Fact]
    public void Every_handler_returns_domain_result_or_declares_its_protocol_outcome()
    {
        var unclassifiedHandlers = FeatureHandlerDiscovery.DiscoverHandlerTypesFromAssembly()
            .Where(type => !HasDomainResultOrProtocolDeclaration(type))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(
            unclassifiedHandlers.Count == 0,
            "Ogni handler deve restituire Task<DomainResult<T>> oppure dichiarare ProtocolOutcomeHandler "
            + "con una motivazione esplicita: "
            + string.Join(", ", unclassifiedHandlers));
    }

    private static bool HasDomainResultOrProtocolDeclaration(Type handlerType)
    {
        var protocolDeclaration = handlerType.GetCustomAttribute<ProtocolOutcomeHandlerAttribute>();

        if (protocolDeclaration is not null)
        {
            return !string.IsNullOrWhiteSpace(protocolDeclaration.Reason);
        }

        var method = handlerType.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public);
        var returnType = method?.ReturnType;

        return returnType is { IsGenericType: true }
            && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            && returnType.GetGenericArguments()[0] is { IsGenericType: true } resultType
            && resultType.GetGenericTypeDefinition().Name == "DomainResult`1";
    }
}
