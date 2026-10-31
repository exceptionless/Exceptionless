using System.Reflection;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Repositories.Models;
using Foundatio.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exceptionless.Tests.Services;

public class RateNotificationRuleCacheConcurrencyTests
{
    [Fact]
    public async Task InvalidateAsync_AnotherReplicaIsFilling_DoesNotRetainTheOldPlan()
    {
        using var sharedCache = new InMemoryCacheClient();
        using var messageBus = new InMemoryMessageBus();
        var workerLocks = new CacheLockProvider(sharedCache, messageBus, TimeProvider.System, new ResiliencePolicyProvider(), NullLoggerFactory.Instance);
        var apiLocks = new CacheLockProvider(sharedCache, messageBus, TimeProvider.System, new ResiliencePolicyProvider(), NullLoggerFactory.Instance);
        var repository = DispatchProxy.Create<IRateNotificationRuleRepository, PausedRepository>();
        var source = (PausedRepository)repository;
        var worker = new RateNotificationRuleCache(repository, sharedCache, workerLocks);
        var api = new RateNotificationRuleCache(repository, sharedCache, apiLocks);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        var filling = worker.GetCounterPlanAsync("project", timeout.Token);
        await source.ReadStarted.Task.WaitAsync(timeout.Token);
        source.Signal = RateNotificationSignal.CriticalErrors;

        // The mutation commits while the other replica still holds its old query result.
        var invalidating = api.InvalidateAsync("project");
        source.ContinueRead.SetResult();
        await Task.WhenAll(filling, invalidating).WaitAsync(timeout.Token);

        var plan = await worker.GetCounterPlanAsync("project", timeout.Token);
        Assert.False(plan.ProjectCounters.ContainsKey(RateNotificationSignal.Errors));
        Assert.True(plan.ProjectCounters.ContainsKey(RateNotificationSignal.CriticalErrors));
    }

    public class PausedRepository : DispatchProxy
    {
        private int _reads;
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ContinueRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public RateNotificationSignal Signal { get; set; } = RateNotificationSignal.Errors;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IRateNotificationRuleRepository.GetEnabledByProjectIdAsync))
                throw new NotSupportedException(targetMethod?.Name);

            return ReadAsync();
        }

        private async Task<FindResults<RateNotificationRule>> ReadAsync()
        {
            var rule = new RateNotificationRule
            {
                Id = "rule",
                OrganizationId = "organization",
                ProjectId = "project",
                UserId = "user",
                Name = "Error rate",
                IsEnabled = true,
                Signal = Signal,
                Subject = RateNotificationSubject.Project,
                Threshold = 10,
                Window = TimeSpan.FromMinutes(5),
                Cooldown = TimeSpan.FromMinutes(30),
                Version = 1
            };

            if (Interlocked.Increment(ref _reads) == 1)
            {
                ReadStarted.SetResult();
                await ContinueRead.Task;
            }

            return new FindResults<RateNotificationRule>([new FindHit<RateNotificationRule>(rule.Id, rule, 1, null, null, null)], 1);
        }
    }
}
