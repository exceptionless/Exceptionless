using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Pipeline;

public class UpdateRateCountersActionIntegrationTests : IntegrationTestsBase
{
    private const string CounterKey = $"project:{SampleDataService.TEST_PROJECT_ID}:signal:Errors";

    private readonly UpdateRateCountersAction _action;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly RateCounterService _counterService;
    private readonly IRateNotificationRuleRepository _ruleRepository;

    public UpdateRateCountersActionIntegrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
        _counterService = GetService<RateCounterService>();
        _ruleRepository = GetService<IRateNotificationRuleRepository>();
        _action = new UpdateRateCountersAction(
            GetService<RateNotificationRuleCache>(),
            _counterService,
            GetService<AppOptions>(),
            GetService<ILoggerFactory>());
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.Features.Add(OrganizationExtensions.RateNotificationsFeature);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());

        var user = await GetService<IUserRepository>().GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        await _ruleRepository.AddAsync(new RateNotificationRule
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = SampleDataService.TEST_PROJECT_ID,
            UserId = user.Id,
            Name = "Pipeline test",
            IsEnabled = true,
            Signal = RateNotificationSignal.Errors,
            Subject = RateNotificationSubject.Project,
            Threshold = 1,
            Window = TimeSpan.FromMinutes(5),
            Cooldown = TimeSpan.FromMinutes(5),
            Version = 1,
            CreatedUtc = TimeProvider.GetUtcNow().UtcDateTime,
            UpdatedUtc = TimeProvider.GetUtcNow().UtcDateTime
        }, o => o.ImmediateConsistency());
    }

    [Fact]
    public async Task ProcessAsync_EligibleEvent_IncrementsCounter()
    {
        var context = await CreateContextAsync();

        await _action.ProcessAsync(context);

        Assert.Equal(1, await GetCurrentBucketCountAsync());
    }

    [Fact]
    public async Task ProcessAsync_FeatureDisabled_DoesNotIncrementCounter()
    {
        var context = await CreateContextAsync();
        context.Organization.Features.Remove(OrganizationExtensions.RateNotificationsFeature);

        await _action.ProcessAsync(context);

        Assert.Equal(0, await GetCurrentBucketCountAsync());
    }

    [Fact]
    public async Task ProcessAsync_StackDisallowsNotifications_DoesNotIncrementCounter()
    {
        var context = await CreateContextAsync();
        context.Stack!.Status = StackStatus.Fixed;

        await _action.ProcessAsync(context);

        Assert.Equal(0, await GetCurrentBucketCountAsync());
    }

    [Fact]
    public async Task ProcessAsync_BotMarkedRequest_DoesNotIncrementCounter()
    {
        var context = await CreateContextAsync();
        context.Event.Data = new DataDictionary
        {
            [Event.KnownDataKeys.RequestInfo] = new RequestInfo
            {
                Data = new DataDictionary { [RequestInfo.KnownDataKeys.IsBot] = true }
            }
        };

        await _action.ProcessAsync(context);

        Assert.Equal(0, await GetCurrentBucketCountAsync());
    }

    private async Task<EventContext> CreateContextAsync()
    {
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        var project = await GetService<IProjectRepository>().GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(organization);
        Assert.NotNull(project);

        var stack = GetService<StackData>().GenerateStack(generateId: true, organizationId: organization.Id, projectId: project.Id);
        var ev = new PersistentEvent
        {
            StackId = stack.Id,
            Type = Event.KnownTypes.Error
        };

        return new EventContext(ev, organization, project) { Stack = stack };
    }

    private Task<long> GetCurrentBucketCountAsync()
    {
        var now = TimeProvider.GetUtcNow().UtcDateTime;
        var minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
        return _counterService.SumBucketsAsync(CounterKey, minute, minute.AddMinutes(1), TestContext.Current.CancellationToken);
    }
}
