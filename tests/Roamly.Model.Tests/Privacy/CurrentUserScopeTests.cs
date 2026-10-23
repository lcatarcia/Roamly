using Roamly.Api.Common.Ownership;

namespace Roamly.Model.Tests.Privacy;

/// <summary>Lo scope worker non ha un'identita' implicita e non espone mai Guid.Empty come owner.</summary>
public sealed class CurrentUserScopeTests
{
    [Fact]
    public async Task System_scope_has_no_user_and_user_scope_requires_an_explicit_nonempty_id()
    {
        var scope = new CurrentUserScope();
        Assert.False(scope.IsRunningAsSystem);
        Assert.False(scope.IsRunningAsUser);
        Assert.Throws<InvalidOperationException>(() => _ = scope.UserId);

        await scope.RunAsSystemAsync("poll", () =>
        {
            Assert.True(scope.IsRunningAsSystem);
            Assert.False(scope.IsRunningAsUser);
            Assert.Throws<InvalidOperationException>(() => _ = scope.UserId);
            return Task.FromResult(true);
        });
        Assert.False(scope.IsRunningAsSystem);

        var userId = Guid.NewGuid();
        await scope.RunAsUserAsync(userId, "owner sweep", () =>
        {
            Assert.False(scope.IsRunningAsSystem);
            Assert.True(scope.IsRunningAsUser);
            Assert.Equal(userId, scope.UserId);
            return Task.FromResult(true);
        });
        Assert.False(scope.IsRunningAsUser);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            scope.RunAsUserAsync(Guid.Empty, "invalid", () => Task.FromResult(true)));
    }
}
