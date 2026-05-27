using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Models.Data;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Controllers;

[Collection("EventQueue")]
public class StackControllerTests : IntegrationTestsBase
{
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IQueue<EventPost> _eventQueue;
    private readonly IQueue<WorkItemData> _workItemQueue;

    public StackControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _stackRepository = GetService<IStackRepository>();
        _eventRepository = GetService<IEventRepository>();
        _eventQueue = GetService<IQueue<EventPost>>();
        _workItemQueue = GetService<IQueue<WorkItemData>>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await _eventQueue.DeleteQueueAsync();
        await _workItemQueue.DeleteQueueAsync();

        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task CanSearchByNonPremiumFields()
    {
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.False(stack.IsFixed());

        var result = await SendRequestAsAsync<IReadOnlyCollection<Stack>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("stacks")
            .QueryString("f", "status:fixed")
            .StatusCodeShouldBeOk());

        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1.0.0")]
    [InlineData("1.0.0.0")]
    public async Task CanMarkFixed(string? version)
    {
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.False(stack.IsFixed());

        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{stack.Id}/mark-fixed")
            .QueryStringIf(() => !String.IsNullOrEmpty(version), "version", version)
            .StatusCodeShouldBeOk());

        stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.True(stack.IsFixed());
    }

    private async Task<PersistentEvent> SubmitErrorEventAsync()
    {
        const string message = "simple string";

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(new Event
            {
                Message = message,
                Type = Event.KnownTypes.Error,
                Data = new DataDictionary {{ Event.KnownDataKeys.SimpleError, new SimpleError {
                        Message = message,
                        Type = "Error",
                        StackTrace = "test",
                    } }}
            })
            .StatusCodeShouldBeAccepted());

        var stats = await _eventQueue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Enqueued);
        Assert.Equal(0, stats.Completed);

        var processEventsJob = GetService<EventPostsJob>();
        await processEventsJob.RunAsync();
        await RefreshDataAsync();

        stats = await _eventQueue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Completed);

        var ev = (await _eventRepository.GetAllAsync()).Documents.Single();
        Assert.Equal(message, ev.Message);
        return ev;
    }

    [Theory]
    [InlineData("ErrorStack")]
    [InlineData("Stack")]
    public async Task CanMarkFixedWithJsonDocument(string propertyName)
    {
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.False(stack.IsFixed());

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("stacks/mark-fixed")
            .Content(new Dictionary<string, string> { { propertyName, stack.Id } })
            .StatusCodeShouldBeOk());

        stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.True(stack.IsFixed());
    }

    [Theory]
    [InlineData("ErrorStack")]
    [InlineData("Stack")]
    public async Task CanAddLinkWithJsonDocument(string propertyName)
    {
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.Empty(stack.References);

        string testUrl = "https://localhost/123";
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPath("stacks/add-link")
            .Content(new Dictionary<string, string> { { propertyName, stack.Id }, { "Link", testUrl } })
            .StatusCodeShouldBeOk());

        stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.Single(stack.References);
        Assert.Contains(testUrl, stack.References);
    }

    [Fact]
    public async Task ChangeStatusAsync_ToFixed_SetsDateFixed()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/change-status")
            .QueryString("status", "Fixed")
            .StatusCodeShouldBeOk());

        // Assert
        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.Equal(StackStatus.Fixed, stack.Status);
        Assert.NotNull(stack.DateFixed);
    }

    [Fact]
    public async Task ChangeStatusAsync_ToOpen_ClearsFixedFields()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/mark-fixed")
            .StatusCodeShouldBeOk());

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/change-status")
            .QueryString("status", "Open")
            .StatusCodeShouldBeOk());

        // Assert
        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.Equal(StackStatus.Open, stack.Status);
        Assert.Null(stack.DateFixed);
        Assert.Null(stack.FixedInVersion);
    }

    [Fact]
    public async Task ChangeStatusAsync_ToRegressed_ReturnsBadRequest()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/change-status")
            .QueryString("status", "Regressed")
            .StatusCodeShouldBeBadRequest());
    }

    [Fact]
    public async Task ChangeStatusAsync_ToSnoozed_ReturnsBadRequest()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);
        var stackBefore = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stackBefore);

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/change-status")
            .QueryString("status", "Snoozed")
            .StatusCodeShouldBeBadRequest());

        // Assert — status must not have changed
        var stackAfter = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stackAfter);
        Assert.Equal(stackBefore.Status, stackAfter.Status);
    }

    [Fact]
    public Task ChangeStatusAsync_WithNonExistentStack_ReturnsNotFound()
    {
        // Arrange & Act
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("stacks/000000000000000000000000/change-status")
            .QueryString("status", "Fixed")
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public async Task DeleteAsync_ExistingStack_ReturnsAccepted()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}")
            .StatusCodeShouldBeAccepted());
    }

    [Fact]
    public Task DeleteAsync_NonExistentStack_ReturnsNotFound()
    {
        // Arrange & Act
        return SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPath("stacks/000000000000000000000000")
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public async Task GetAll_WithDateRangeFilter_ReturnsOnlyMatchingStacks()
    {
        // Arrange
        var utcNow = TimeProvider.GetUtcNow();
        var (stacks, _) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Date(utcNow.AddDays(-1));
            d.Event().TestProject().Date(utcNow.AddDays(-3));
        });

        Assert.Equal(2, stacks.Count);
        var recentStack = stacks.Single(s => s.LastOccurrence >= utcNow.AddDays(-2).UtcDateTime);
        var oldStack = stacks.Single(s => s.LastOccurrence < utcNow.AddDays(-2).UtcDateTime);

        // Act
        var result = await SendRequestAsAsync<IReadOnlyCollection<Stack>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("stacks")
            .QueryString("time", "[now-2d TO now]")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        Assert.Contains(result, s => String.Equals(s.Id, recentStack.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(result, s => String.Equals(s.Id, oldStack.Id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetAll_WithNoFilter_ReturnsAllStacks()
    {
        // Arrange
        var utcNow = TimeProvider.GetUtcNow();
        await CreateDataAsync(d =>
        {
            d.Event().TestProject().Date(utcNow);
            d.Event().TestProject().Date(utcNow.AddHours(-1));
        });

        // Act
        var result = await SendRequestAsAsync<IReadOnlyCollection<Stack>>(r => r
            .AsGlobalAdminUser()
            .AppendPath("stacks")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAsync_ExistingStack_ReturnsStack()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        // Act
        var stack = await SendRequestAsAsync<Stack>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("stacks", ev.StackId)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(stack);
        Assert.Equal(ev.StackId, stack.Id);
        Assert.Equal(SampleDataService.TEST_ORG_ID, stack.OrganizationId);
        Assert.Equal(SampleDataService.TEST_PROJECT_ID, stack.ProjectId);
    }

    [Fact]
    public Task GetAsync_NonExistentStack_ReturnsNotFound()
    {
        // Arrange & Act
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("stacks", "000000000000000000000000")
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public async Task GetByOrganizationAsync_ExistingOrganization_ReturnsStacks()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        // Act
        var result = await SendRequestAsAsync<IReadOnlyCollection<Stack>>(r => r
            .AsGlobalAdminUser()
            .AppendPath($"organizations/{SampleDataService.TEST_ORG_ID}/stacks")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, s => Assert.Equal(SampleDataService.TEST_ORG_ID, s.OrganizationId));
    }

    [Fact]
    public Task GetByOrganizationAsync_NonExistentOrganization_ReturnsNotFound()
    {
        // Arrange & Act
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("organizations/000000000000000000000000/stacks")
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public async Task GetByProjectAsync_ExistingProject_ReturnsStacks()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        // Act
        var result = await SendRequestAsAsync<IReadOnlyCollection<Stack>>(r => r
            .AsGlobalAdminUser()
            .AppendPath($"projects/{SampleDataService.TEST_PROJECT_ID}/stacks")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, s => Assert.Equal(SampleDataService.TEST_PROJECT_ID, s.ProjectId));
    }

    [Fact]
    public Task GetByProjectAsync_NonExistentProject_ReturnsNotFound()
    {
        // Arrange & Act
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPath("projects/000000000000000000000000/stacks")
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public async Task MarkCriticalAsync_ExistingStack_SetsOccurrencesAreCritical()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/mark-critical")
            .StatusCodeShouldBeOk());

        // Assert
        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.True(stack.OccurrencesAreCritical);
    }

    [Fact]
    public Task MarkCriticalAsync_NonExistentStack_ReturnsNotFound()
    {
        // Arrange & Act
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("stacks/000000000000000000000000/mark-critical")
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public async Task MarkNotCriticalAsync_CriticalStack_ClearsOccurrencesAreCritical()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/mark-critical")
            .StatusCodeShouldBeOk());

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/mark-critical")
            .StatusCodeShouldBeNoContent());

        // Assert
        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.False(stack.OccurrencesAreCritical);
    }

    [Fact]
    public async Task RemoveLinkAsync_ExistingLink_RemovesReference()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        string testUrl = "https://github.com/issue/123";
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/add-link")
            .Content(new ValueFromBody<string>(testUrl))
            .StatusCodeShouldBeOk());

        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.Contains(testUrl, stack.References);

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/remove-link")
            .Content(new ValueFromBody<string>(testUrl))
            .StatusCodeShouldBeNoContent());

        // Assert
        stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.DoesNotContain(testUrl, stack.References);
    }

    [Fact]
    public async Task RemoveLinkAsync_EmptyUrl_ReturnsBadRequest()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/remove-link")
            .Content(new ValueFromBody<string>(""))
            .StatusCodeShouldBeBadRequest());
    }

    [Fact]
    public async Task PromoteAsync_NonPremiumOrganization_ReturnsUpgradeRequired()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        var organizationRepository = GetService<IOrganizationRepository>();
        var organization = await organizationRepository.GetByIdAsync(ev.OrganizationId);
        Assert.NotNull(organization);
        organization.HasPremiumFeatures = false;
        await organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());

        // Act & Assert
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/promote")
            .StatusCodeShouldBeUpgradeRequired());
    }

    [Fact]
    public async Task PromoteAsync_PremiumOrganizationWithoutPromoteHooks_ReturnsNotImplemented()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        // Act & Assert
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/promote")
            .ExpectedStatus(System.Net.HttpStatusCode.NotImplemented));
    }

    [Fact]
    public async Task PromoteAsync_WithEnabledPromoteHook_QueuesWebHookNotification()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);

        var webHookRepository = GetService<IWebHookRepository>();
        var webHookNotificationQueue = GetService<IQueue<WebHookNotification>>();
        await webHookRepository.AddAsync(new WebHook
        {
            OrganizationId = ev.OrganizationId,
            ProjectId = ev.ProjectId,
            Url = "https://example.com/promote",
            Version = WebHook.KnownVersions.Version2,
            EventTypes = [WebHook.KnownEventTypes.StackPromoted],
            IsEnabled = true
        }, o => o.ImmediateConsistency());

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/promote")
            .StatusCodeShouldBeOk());

        // Assert
        var stats = await webHookNotificationQueue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Enqueued);
    }

    [Fact]
    public Task PromoteAsync_NonExistentStack_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("stacks/000000000000000000000000/promote")
            .StatusCodeShouldBeNotFound());
    }

    [Fact]
    public async Task SnoozeAsync_WithValidFutureDate_SetsSnoozeStatus()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);
        var snoozeUntilUtc = TimeProvider.GetUtcNow().AddDays(1);

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/mark-snoozed")
            .QueryString("snoozeUntilUtc", snoozeUntilUtc.ToString("o"))
            .StatusCodeShouldBeOk());

        // Assert
        var stack = await _stackRepository.GetByIdAsync(ev.StackId);
        Assert.NotNull(stack);
        Assert.Equal(StackStatus.Snoozed, stack.Status);
        Assert.NotNull(stack.SnoozeUntilUtc);
    }

    [Fact]
    public async Task SnoozeAsync_WithPastDate_ReturnsBadRequest()
    {
        // Arrange
        var ev = await SubmitErrorEventAsync();
        Assert.NotNull(ev.StackId);
        var pastDateUtc = TimeProvider.GetUtcNow().AddMinutes(-10);

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath($"stacks/{ev.StackId}/mark-snoozed")
            .QueryString("snoozeUntilUtc", pastDateUtc.ToString("o"))
            .StatusCodeShouldBeBadRequest());
    }

    [Fact]
    public Task SnoozeAsync_NonExistentStack_ReturnsNotFound()
    {
        // Arrange
        var snoozeUntilUtc = TimeProvider.GetUtcNow().AddDays(1);

        // Act
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPath("stacks/000000000000000000000000/mark-snoozed")
            .QueryString("snoozeUntilUtc", snoozeUntilUtc.ToString("o"))
            .StatusCodeShouldBeNotFound());
    }
}
