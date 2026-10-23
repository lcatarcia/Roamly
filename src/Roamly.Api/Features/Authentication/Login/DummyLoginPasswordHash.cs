using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Roamly.Infrastructure.Identity;

namespace Roamly.Api.Features.Authentication.Login;

/// <summary>Verifica la password contro un hash fittizio generato una sola volta per host.</summary>
internal sealed class DummyLoginPasswordHash
{
    private static readonly ApplicationUser DummyUser = new();
    private readonly Lazy<string> _hash;
    private readonly IServiceScopeFactory _scopeFactory;

    public DummyLoginPasswordHash(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
        _hash = new Lazy<string>(CreateHash, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public void Verify(IPasswordHasher<ApplicationUser> passwordHasher, string password)
    {
        ArgumentNullException.ThrowIfNull(passwordHasher);
        _ = passwordHasher.VerifyHashedPassword(DummyUser, _hash.Value, password);
    }

    private string CreateHash()
    {
        using var scope = _scopeFactory.CreateScope();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
        var oneTimePassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return passwordHasher.HashPassword(DummyUser, oneTimePassword);
    }
}
