using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Roamly.Api.Common.Ownership;
using Roamly.Api.Features.Identity.Consents;
using Roamly.Common;
using Roamly.Infrastructure.Persistence;

namespace Roamly.IntegrationTests.Privacy;

public sealed partial class ConsentRegistryApiTests
{
    private readonly AccountLockBarrier _locks = new();

    [Theory]
    [InlineData("grants", "revocations", false)]
    [InlineData("revocations", "grants", true)]
    public async Task Concurrent_transitions_are_ordered_by_the_real_account_lock(
        string firstAction, string secondAction, bool granted)
    {
        await UseSyntheticDefinitionAsync();
        _locks.Arm();
        var first = PostAsync("synthetic-render", firstAction, NewId());
        await _locks.WaitForFirstLockAsync();
        var second = PostAsync("synthetic-render", secondAction, NewId());
        try
        {
            await _locks.WaitForSecondAttemptAsync();
            Assert.False(second.IsCompleted);
        }
        finally
        {
            _locks.Release();
        }

        using var firstResponse = await first;
        using var secondResponse = await second;
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(1, (await firstResponse.Content.ReadFromJsonAsync<Receipt>(Ct))!.Revision);
        Assert.Equal(2, (await secondResponse.Content.ReadFromJsonAsync<Receipt>(Ct))!.Revision);
        var state = await ReadStateAsync();
        Assert.Equal(2, state.Revision);
        Assert.Equal(granted, state.IsGranted);
    }

    [Theory]
    [InlineData("grants", HttpStatusCode.OK)]
    [InlineData("revocations", HttpStatusCode.Conflict)]
    public async Task Concurrent_identical_ids_replay_or_conflict_without_adding_an_event(
        string secondAction, HttpStatusCode expected)
    {
        await UseSyntheticDefinitionAsync();
        var id = NewId();
        _locks.Arm();
        var first = PostAsync("synthetic-render", "grants", id);
        await _locks.WaitForFirstLockAsync();
        var second = PostAsync("synthetic-render", secondAction, id);
        try
        {
            await _locks.WaitForSecondAttemptAsync();
            Assert.False(second.IsCompleted);
        }
        finally
        {
            _locks.Release();
        }

        using var created = await first;
        using var replayed = await second;
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(expected, replayed.StatusCode);
        Assert.Equal(1, (await ReadStateAsync()).Revision);
    }

    [Fact]
    public async Task Erasure_wins_against_an_inflight_write_and_removes_the_entire_history()
    {
        await UseSyntheticDefinitionAsync();
        using var grant = await PostAsync("synthetic-render", "grants", NewId());
        using var revoke = await PostAsync("synthetic-render", "revocations", NewId());
        Assert.Equal(HttpStatusCode.Created, grant.StatusCode);
        Assert.Equal(HttpStatusCode.Created, revoke.StatusCode);
        await SetAccountRestrictedAsync(passwordChange: false);
        await using var jobScope = _factory.Services.CreateAsyncScope();
        _locks.Arm();
        var erasure = jobScope.ServiceProvider.GetRequiredService<AccountErasureJob>().RunOnceAsync(Ct);
        await _locks.WaitForFirstLockAsync();
        var write = RecordDirectlyAsync(NewId());
        try
        {
            await _locks.WaitForSecondAttemptAsync();
            Assert.False(write.IsCompleted);
        }
        finally
        {
            _locks.Release();
        }

        await erasure;
        Assert.Equal(RecordConsentOutcome.AccountRestricted, (await write).Outcome);
        await using var verification = _factory.Services.CreateAsyncScope();
        var db = verification.ServiceProvider.GetRequiredService<RoamlyDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Id == _ownerId, Ct));
        var userScope = verification.ServiceProvider.GetRequiredService<ICurrentUserScope>();
        Assert.False(await userScope.RunAsUserAsync(_ownerId, "verify consent erasure",
            () => db.ConsentTransitions.AnyAsync(Ct)));
        var receipt = await db.ErasureReceipts.AsNoTracking().SingleAsync(Ct);
        using var counts = JsonDocument.Parse(receipt.DeletedRowCounts);
        Assert.Equal(2, counts.RootElement.GetProperty("ConsentTransition").GetInt64());
        Assert.DoesNotContain(_ownerId.ToString("D"), JsonSerializer.Serialize(receipt), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_write_that_wins_the_lock_is_retained_until_erasure_and_blocks_concurrent_deletion_scheduling()
    {
        await UseSyntheticDefinitionAsync();
        _locks.Arm();
        var write = PostAsync("synthetic-render", "grants", NewId());
        await _locks.WaitForFirstLockAsync();
        var scheduling = _client.PostJsonAsync("/api/v1/me/deletion", new { password = Password }, Ct);
        try
        {
            await _locks.WaitForSecondAttemptAsync();
            Assert.False(scheduling.IsCompleted);
        }
        finally
        {
            _locks.Release();
        }

        using var created = await write;
        using var scheduled = await scheduling;
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, scheduled.StatusCode);
        Assert.Equal(RecordConsentOutcome.AccountRestricted, (await RecordDirectlyAsync(NewId())).Outcome);
        await SetAccountRestrictedAsync(passwordChange: false);
        await using var jobScope = _factory.Services.CreateAsyncScope();
        await jobScope.ServiceProvider.GetRequiredService<AccountErasureJob>().RunOnceAsync(Ct);
        var db = jobScope.ServiceProvider.GetRequiredService<RoamlyDbContext>();
        var receipt = await db.ErasureReceipts.AsNoTracking().SingleAsync(Ct);
        using var counts = JsonDocument.Parse(receipt.DeletedRowCounts);
        Assert.Equal(1, counts.RootElement.GetProperty("ConsentTransition").GetInt64());
        Assert.False(await db.Users.AnyAsync(u => u.Id == _ownerId, Ct));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handler_rechecks_restrictions_even_when_request_passed_the_outer_gate(bool passwordChange)
    {
        await UseSyntheticDefinitionAsync();
        await SetAccountRestrictedAsync(passwordChange);
        Assert.Equal(RecordConsentOutcome.AccountRestricted, (await RecordDirectlyAsync(NewId())).Outcome);
    }

    private async Task<RecordConsentResult> RecordDirectlyAsync(Guid id)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userScope = scope.ServiceProvider.GetRequiredService<ICurrentUserScope>();
        return await userScope.RunAsUserAsync(_ownerId, "exercise inflight consent request",
            () => scope.ServiceProvider.GetRequiredService<RecordConsentHandler>().HandleAsync(
                "synthetic-render", "grant", new(id, "test-v1", "synthetic-text-1"), Ct));
    }

    private sealed class AccountLockBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _firstLocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondAttempt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private DbCommand? _first;
        private int _armed;

        public void Arm() => Volatile.Write(ref _armed, 1);
        public Task WaitForFirstLockAsync() => _firstLocked.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
        public Task WaitForSecondAttemptAsync() => _secondAttempt.Task.WaitAsync(TimeSpan.FromSeconds(20), Ct);
        public void Release()
        {
            Volatile.Write(ref _armed, 0);
            _release.TrySetResult();
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _armed) == 1
                && command.CommandText.Contains("UPDLOCK, HOLDLOCK", StringComparison.Ordinal)
                && Interlocked.CompareExchange(ref _first, command, null) is not null)
            {
                _secondAttempt.TrySetResult();
            }

            return ValueTask.FromResult(result);
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(command, _first))
            {
                _firstLocked.TrySetResult();
                await _release.Task.WaitAsync(TimeSpan.FromSeconds(25), cancellationToken);
            }

            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _armed) == 1 && _first is not null
                && command.CommandText.Contains("UPDATE", StringComparison.Ordinal)
                && command.CommandText.Contains("[AspNetUsers]", StringComparison.Ordinal))
            {
                _secondAttempt.TrySetResult();
            }

            return ValueTask.FromResult(result);
        }
    }
}
