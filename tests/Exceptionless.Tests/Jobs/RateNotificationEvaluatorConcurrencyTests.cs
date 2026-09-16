using System.Reflection;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories.Models;
using Foundatio.Resilience;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public sealed class RateNotificationEvaluatorConcurrencyTests
{
    private const string OrganizationId = "507f1f77bcf86cd799439011";
    private const string ProjectId = "507f1f77bcf86cd799439012";
    private const string UserId = "507f1f77bcf86cd799439013";
    private const string RuleId = "507f1f77bcf86cd799439014";

    [Fact]
    public async Task RunAsync_LateBucketIncrement_IsEvaluatedAfterSettlement()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var cancellationToken = timeout.Token;
        var timeProvider = new ProxyTimeProvider();
        var eventMinute = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        timeProvider.SetUtcNow(new DateTimeOffset(eventMinute.AddSeconds(30), TimeSpan.Zero));

        using var cache = new InMemoryCacheClient(new InMemoryCacheClientOptions
        {
            TimeProvider = timeProvider
        });
        var delayedCache = DispatchProxy.Create<ICacheClient, DelayedIncrementCacheProxy>();
        var delayedCacheProxy = (DelayedIncrementCacheProxy)(object)delayedCache;
        delayedCacheProxy.Inner = cache;

        var counterService = new RateCounterService(delayedCache, timeProvider);
        var rule = new RateNotificationRule
        {
            Id = RuleId,
            OrganizationId = OrganizationId,
            ProjectId = ProjectId,
            UserId = UserId,
            Name = "One minute errors",
            IsEnabled = true,
            Signal = RateNotificationSignal.Errors,
            Subject = RateNotificationSubject.Project,
            Threshold = 1,
            Window = TimeSpan.FromMinutes(1),
            Cooldown = TimeSpan.FromMinutes(10),
            Version = 1,
            CreatedUtc = eventMinute,
            UpdatedUtc = eventMinute
        };

        var organization = new Organization
        {
            Id = OrganizationId,
            Name = "Test organization",
            HasPremiumFeatures = true
        };
        organization.Features.Add(OrganizationExtensions.RateNotificationsFeature);

        var project = new Project
        {
            Id = ProjectId,
            OrganizationId = OrganizationId,
            Name = "Test project"
        };

        var ruleRepository = DispatchProxy.Create<IRateNotificationRuleRepository, RuleRepositoryProxy>();
        ((RuleRepositoryProxy)(object)ruleRepository).Rule = rule;
        var organizationRepository = DispatchProxy.Create<IOrganizationRepository, OrganizationRepositoryProxy>();
        ((OrganizationRepositoryProxy)(object)organizationRepository).Organization = organization;
        var projectRepository = DispatchProxy.Create<IProjectRepository, ProjectRepositoryProxy>();
        ((ProjectRepositoryProxy)(object)projectRepository).Project = project;
        var notificationQueue = DispatchProxy.Create<IQueue<RateNotification>, NotificationQueueProxy>();
        var notificationQueueProxy = (NotificationQueueProxy)(object)notificationQueue;

        var resiliencePolicyProvider = new ResiliencePolicyProvider();
        var serializer = new SystemTextJsonSerializer(new System.Text.Json.JsonSerializerOptions().ConfigureExceptionlessDefaults());
        using var messageBus = new InMemoryMessageBus(new InMemoryMessageBusOptions
        {
            Serializer = serializer,
            TimeProvider = timeProvider,
            ResiliencePolicyProvider = resiliencePolicyProvider,
            LoggerFactory = NullLoggerFactory.Instance
        });
        var lockProvider = new CacheLockProvider(cache, messageBus, timeProvider, resiliencePolicyProvider, NullLoggerFactory.Instance);
        var job = new RateNotificationEvaluatorJob(
            counterService,
            ruleRepository,
            organizationRepository,
            projectRepository,
            notificationQueue,
            lockProvider,
            timeProvider,
            resiliencePolicyProvider,
            NullLoggerFactory.Instance);

        string counterKey = $"project:{ProjectId}:signal:{RateNotificationSignal.Errors}";
        Task lateIncrement = counterService.IncrementAsync(counterKey, cancellationToken);
        await delayedCacheProxy.IncrementStarted.Task.WaitAsync(cancellationToken);

        // The active-list write has completed, but the count for the event minute is still in flight.
        timeProvider.SetUtcNow(new DateTimeOffset(eventMinute.AddMinutes(1), TimeSpan.Zero));
        var firstResult = await job.RunAsync(cancellationToken);
        Assert.True(firstResult.IsSuccess, firstResult.Message);
        Assert.Empty(notificationQueueProxy.Messages);

        delayedCacheProxy.ContinueIncrement.TrySetResult();
        await lateIncrement;

        // A one-minute settlement grace should now evaluate the event minute with its real end boundary.
        timeProvider.SetUtcNow(new DateTimeOffset(eventMinute.AddMinutes(2), TimeSpan.Zero));
        var secondResult = await job.RunAsync(cancellationToken);
        Assert.True(secondResult.IsSuccess, secondResult.Message);

        var notification = Assert.Single(notificationQueueProxy.Messages);
        Assert.Equal(eventMinute, notification.WindowStartUtc);
        Assert.Equal(eventMinute.AddMinutes(1), notification.WindowEndUtc);
        Assert.Equal(1, notification.ObservedCount);
    }

    private class DelayedIncrementCacheProxy : DispatchProxy
    {
        public ICacheClient Inner { get; set; } = null!;
        public TaskCompletionSource IncrementStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ContinueIncrement { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(ICacheClient.IncrementAsync))
            {
                IncrementStarted.TrySetResult();
                return DelayIncrementAsync(targetMethod, args);
            }

            return targetMethod.Invoke(Inner, args);
        }

        private async Task<long> DelayIncrementAsync(MethodInfo targetMethod, object?[]? args)
        {
            await ContinueIncrement.Task;
            return await (Task<long>)targetMethod.Invoke(Inner, args)!;
        }
    }

    private class RuleRepositoryProxy : DispatchProxy
    {
        public RateNotificationRule Rule { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            return targetMethod.Name switch
            {
                nameof(IRateNotificationRuleRepository.GetEnabledByProjectIdAsync) => Task.FromResult(
                    new FindResults<RateNotificationRule>(
                        [new FindHit<RateNotificationRule>(Rule.Id, Rule, 1, null, null, null)], 1)),
                "PatchAsync" => Task.FromResult(true),
                _ => throw new NotSupportedException(targetMethod.Name)
            };
        }
    }

    private class OrganizationRepositoryProxy : DispatchProxy
    {
        public Organization Organization { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IOrganizationRepository.GetByIdAsync))
                return Task.FromResult<Organization?>(Organization);

            throw new NotSupportedException(targetMethod.Name);
        }
    }

    private class ProjectRepositoryProxy : DispatchProxy
    {
        public Project Project { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IProjectRepository.GetByIdAsync))
                return Task.FromResult<Project?>(Project);

            throw new NotSupportedException(targetMethod.Name);
        }
    }

    private class NotificationQueueProxy : DispatchProxy
    {
        public List<RateNotification> Messages { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IQueue<RateNotification>.EnqueueAsync))
            {
                Messages.Add((RateNotification)args![0]!);
                return Task.FromResult(Messages.Count.ToString());
            }

            throw new NotSupportedException(targetMethod.Name);
        }
    }
}
