using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Mail;
using Foundatio.Queues;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Jobs;

public class RateNotificationsJobTests : IntegrationTestsBase
{
    private readonly RateNotificationsJob _job;
    private readonly NullMailer _mailer;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IQueue<RateNotification> _queue;
    private readonly IRateNotificationRuleRepository _ruleRepository;
    private readonly IUserRepository _userRepository;

    public RateNotificationsJobTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _job = GetService<RateNotificationsJob>();
        _mailer = Assert.IsType<NullMailer>(GetService<IMailer>());
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _queue = GetService<IQueue<RateNotification>>();
        _ruleRepository = GetService<IRateNotificationRuleRepository>();
        _userRepository = GetService<IUserRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await _queue.DeleteQueueAsync();
        _mailer.RateNotifications.Clear();

        await GetService<SampleDataService>().CreateDataAsync();
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.Features.Add(OrganizationExtensions.RateNotificationsFeature);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());

        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        user.IsEmailAddressVerified = true;
        user.VerifyEmailAddressToken = null;
        user.VerifyEmailAddressTokenExpiration = default;
        await _userRepository.SaveAsync(user, o => o.ImmediateConsistency().Cache());
    }

    [Fact]
    public async Task RunAsync_EnabledValidRule_SendsNotification()
    {
        // Arrange
        var (rule, notification) = await CreateRuleAndNotificationAsync();
        await _queue.EnqueueAsync(notification);

        // Act
        var result = await _job.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        var call = Assert.Single(_mailer.RateNotifications);
        Assert.Equal(rule.Id, call.RuleId);
        Assert.Equal(notification.ObservedCount, call.ObservedCount);
    }

    [Fact]
    public async Task RunAsync_FeatureDisabled_SkipsNotification()
    {
        // Arrange
        var (_, notification) = await CreateRuleAndNotificationAsync();
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.Features.Remove(OrganizationExtensions.RateNotificationsFeature);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());
        await _queue.EnqueueAsync(notification);

        // Act
        var result = await _job.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(_mailer.RateNotifications);
    }

    [Fact]
    public async Task RunAsync_PayloadDoesNotMatchRule_SkipsNotification()
    {
        // Arrange
        var (_, notification) = await CreateRuleAndNotificationAsync();
        notification.ProjectId = SampleDataService.FREE_PROJECT_ID;
        await _queue.EnqueueAsync(notification);

        // Act
        var result = await _job.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(_mailer.RateNotifications);
    }

    [Fact]
    public async Task RunAsync_SubjectKeyDoesNotMatchRule_SkipsNotification()
    {
        // Arrange
        var (_, notification) = await CreateRuleAndNotificationAsync();
        notification.SubjectKey = $"project:{SampleDataService.FREE_PROJECT_ID}";
        await _queue.EnqueueAsync(notification);

        // Act
        var result = await _job.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(_mailer.RateNotifications);
    }

    [Fact]
    public async Task EnqueueAsync_DuplicateEvaluation_EnqueuesOnce()
    {
        // Arrange
        var (_, notification) = await CreateRuleAndNotificationAsync();

        // Act
        await _queue.EnqueueAsync(notification);
        await _queue.EnqueueAsync(notification);

        // Assert
        var stats = await _queue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Enqueued);
    }

    private async Task<(RateNotificationRule Rule, RateNotification Notification)> CreateRuleAndNotificationAsync()
    {
        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);
        Assert.Contains(SampleDataService.TEST_ORG_ID, user.OrganizationIds);
        Assert.True(user.IsEmailAddressVerified);
        Assert.True(user.EmailNotificationsEnabled);

        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);

        var now = TimeProvider.GetUtcNow().UtcDateTime;
        var rule = await _ruleRepository.AddAsync(new RateNotificationRule
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            ProjectId = project.Id,
            UserId = user.Id,
            Name = "Delivery test",
            IsEnabled = true,
            Signal = RateNotificationSignal.Errors,
            Subject = RateNotificationSubject.Project,
            Threshold = 5,
            Window = TimeSpan.FromMinutes(5),
            Cooldown = TimeSpan.FromMinutes(30),
            Version = 1,
            CreatedUtc = now,
            UpdatedUtc = now
        }, o => o.ImmediateConsistency());

        return (rule, new RateNotification
        {
            RuleId = rule.Id,
            RuleVersion = rule.Version,
            OrganizationId = rule.OrganizationId,
            ProjectId = rule.ProjectId,
            UserId = rule.UserId,
            SubjectKey = $"project:{rule.ProjectId}",
            WindowStartUtc = now.AddMinutes(-5),
            WindowEndUtc = now,
            ObservedCount = 10,
            Threshold = rule.Threshold
        });
    }
}
