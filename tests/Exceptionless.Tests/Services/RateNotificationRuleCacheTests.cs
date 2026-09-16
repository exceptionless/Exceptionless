using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class RateNotificationRuleCacheTests : IntegrationTestsBase
{
    private readonly RateNotificationRuleCache _cache;
    private readonly IRateNotificationRuleRepository _repository;
    private readonly OrganizationService _organizationService;

    public RateNotificationRuleCacheTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _cache = GetService<RateNotificationRuleCache>();
        _repository = GetService<IRateNotificationRuleRepository>();
        _organizationService = GetService<OrganizationService>();
    }

    [Fact]
    public async Task GetCounterPlanAsync_MoreThanOnePage_LoadsEveryEnabledRule()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        var rules = Enumerable.Range(0, 501).Select(index => new RateNotificationRule
        {
            OrganizationId = TestConstants.OrganizationId,
            ProjectId = TestConstants.ProjectId,
            UserId = TestConstants.UserId,
            Name = $"Rule {index}",
            IsEnabled = true,
            Signal = RateNotificationSignal.Errors,
            Subject = RateNotificationSubject.Stack,
            StackId = ObjectId.GenerateNewId().ToString(),
            Threshold = 10,
            Window = TimeSpan.FromMinutes(5),
            Cooldown = TimeSpan.FromMinutes(30),
            Version = 1,
            CreatedUtc = now,
            UpdatedUtc = now
        }).ToList();
        await _repository.AddAsync(rules, o => o.ImmediateConsistency());

        // Act
        var plan = await _cache.GetCounterPlanAsync(TestConstants.ProjectId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(501, plan.RuleCount);
        Assert.Equal(501, plan.StackCounters.Count);
    }

    [Fact]
    public async Task RemoveProjectRateNotificationRulesAsync_InvalidatesCompiledPlan()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        await _repository.AddAsync(new RateNotificationRule
        {
            OrganizationId = TestConstants.OrganizationId,
            ProjectId = TestConstants.ProjectId,
            UserId = TestConstants.UserId,
            Name = "Rule to remove",
            IsEnabled = true,
            Signal = RateNotificationSignal.Errors,
            Subject = RateNotificationSubject.Project,
            Threshold = 10,
            Window = TimeSpan.FromMinutes(5),
            Cooldown = TimeSpan.FromMinutes(30),
            Version = 1,
            CreatedUtc = now,
            UpdatedUtc = now
        }, o => o.ImmediateConsistency());
        var ct = TestContext.Current.CancellationToken;
        Assert.True((await _cache.GetCounterPlanAsync(TestConstants.ProjectId, ct)).HasCounters);

        // Act
        await _organizationService.RemoveProjectRateNotificationRulesAsync(TestConstants.OrganizationId, TestConstants.ProjectId);
        var plan = await _cache.GetCounterPlanAsync(TestConstants.ProjectId, ct);

        // Assert
        Assert.False(plan.HasCounters);
    }

    [Fact]
    public async Task SaveAsync_RuleChanged_InvalidatesCompiledPlan()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        var rule = await _repository.AddAsync(CreateRule(TestConstants.ProjectId, TestConstants.UserId, now), o => o.ImmediateConsistency());
        var ct = TestContext.Current.CancellationToken;
        var initialPlan = await _cache.GetCounterPlanAsync(TestConstants.ProjectId, ct);
        Assert.True(initialPlan.ProjectCounters.ContainsKey(RateNotificationSignal.Errors));

        // Act
        rule.Signal = RateNotificationSignal.CriticalErrors;
        await _repository.SaveAsync(rule, o => o.ImmediateConsistency());
        var updatedPlan = await _cache.GetCounterPlanAsync(TestConstants.ProjectId, ct);

        // Assert
        Assert.False(updatedPlan.ProjectCounters.ContainsKey(RateNotificationSignal.Errors));
        Assert.True(updatedPlan.ProjectCounters.ContainsKey(RateNotificationSignal.CriticalErrors));
    }

    [Fact]
    public async Task RemoveUserRateNotificationRulesAsync_RemovesOnlyOwnedRulesAndInvalidatesPlans()
    {
        // Arrange
        string otherProjectId = ObjectId.GenerateNewId().ToString();
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        await _repository.AddAsync([
            CreateRule(TestConstants.ProjectId, TestConstants.UserId, now),
            CreateRule(otherProjectId, TestConstants.UserId2, now)
        ], o => o.ImmediateConsistency());
        var ct = TestContext.Current.CancellationToken;
        Assert.True((await _cache.GetCounterPlanAsync(TestConstants.ProjectId, ct)).HasCounters);
        Assert.True((await _cache.GetCounterPlanAsync(otherProjectId, ct)).HasCounters);

        // Act
        await _organizationService.RemoveUserRateNotificationRulesAsync(TestConstants.OrganizationId, TestConstants.UserId);

        // Assert
        Assert.Empty((await _repository.GetByOrganizationIdAndUserIdAsync(TestConstants.OrganizationId, TestConstants.UserId)).Documents);
        Assert.Single((await _repository.GetByOrganizationIdAndUserIdAsync(TestConstants.OrganizationId, TestConstants.UserId2)).Documents);
        Assert.False((await _cache.GetCounterPlanAsync(TestConstants.ProjectId, ct)).HasCounters);
        Assert.True((await _cache.GetCounterPlanAsync(otherProjectId, ct)).HasCounters);
    }

    [Fact]
    public async Task RemoveRateNotificationRulesAsync_RemovesOrganizationRulesAndInvalidatesEveryPlan()
    {
        // Arrange
        string otherProjectId = ObjectId.GenerateNewId().ToString();
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        await _repository.AddAsync([
            CreateRule(TestConstants.ProjectId, TestConstants.UserId, now),
            CreateRule(otherProjectId, TestConstants.UserId2, now)
        ], o => o.ImmediateConsistency());
        var ct = TestContext.Current.CancellationToken;
        Assert.True((await _cache.GetCounterPlanAsync(TestConstants.ProjectId, ct)).HasCounters);
        Assert.True((await _cache.GetCounterPlanAsync(otherProjectId, ct)).HasCounters);

        // Act
        await _organizationService.RemoveRateNotificationRulesAsync(TestConstants.OrganizationId);

        // Assert
        Assert.Empty((await _repository.GetByOrganizationIdAsync(TestConstants.OrganizationId)).Documents);
        Assert.False((await _cache.GetCounterPlanAsync(TestConstants.ProjectId, ct)).HasCounters);
        Assert.False((await _cache.GetCounterPlanAsync(otherProjectId, ct)).HasCounters);
    }

    private static RateNotificationRule CreateRule(string projectId, string userId, DateTime now)
    {
        return new RateNotificationRule
        {
            OrganizationId = TestConstants.OrganizationId,
            ProjectId = projectId,
            UserId = userId,
            Name = "Lifecycle rule",
            IsEnabled = true,
            Signal = RateNotificationSignal.Errors,
            Subject = RateNotificationSubject.Project,
            Threshold = 10,
            Window = TimeSpan.FromMinutes(5),
            Cooldown = TimeSpan.FromMinutes(30),
            Version = 1,
            CreatedUtc = now,
            UpdatedUtc = now
        };
    }
}
