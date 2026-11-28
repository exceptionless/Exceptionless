using Elastic.Clients.Elasticsearch;
using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Services.BulkOperations;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Validation;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public partial class BulkOperationEndpointTests
{
    [Fact]
    public async Task Execute_PartialStackSearch_RetriesThePageBeforeFixingAnyStacks()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (stacks, _) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Message("first stack").Date(now.AddMinutes(-2));
            d.Event().TestProject().Message("second stack").Date(now.AddMinutes(-1));
        });
        var created = await CreateOperationAsync(StackFilterRequest("stacks.mark-fixed", ""));
        await using var searchNode = WebApplication.CreateBuilder().Build();
        searchNode.Urls.Add("http://127.0.0.1:0");
        searchNode.MapMethods("/", [HttpMethods.Head], (HttpResponse response) =>
        {
            response.Headers["X-Elastic-Product"] = "Elasticsearch";
            return TypedResults.Ok();
        });
        searchNode.MapPost("/{index}/_search", (HttpResponse response) =>
        {
            response.Headers["X-Elastic-Product"] = "Elasticsearch";
            return TypedResults.Json(new
            {
                took = 1,
                timed_out = false,
                _shards = new { total = 2, successful = 1, skipped = 0, failed = 1 },
                hits = new { hits = Array.Empty<object>() },
                aggregations = new Dictionary<string, object>
                {
                    ["composite#stack_ids"] = new { buckets = new[] { new { key = new { stack_id = stacks[0].Id }, doc_count = 1 } } }
                }
            });
        });
        await searchNode.StartAsync(TestCancellationToken);
        var services = GetService<IServiceProvider>();
        var repository = ActivatorUtilities.CreateInstance<SearchNodeEventRepository>(services, new Uri(searchNode.Urls.Single()));
        var partialResolver = ActivatorUtilities.CreateInstance<StackBulkSelectionResolver>(services, repository);

        // Act
        await RunStackOperationAsync(created.Id, 2, partialResolver);
        var interrupted = await _taskService.GetAsync(created.Id);
        var afterPartialPage = await GetStacksAsync(stacks);
        await RunStackOperationAsync(created.Id, 2);
        var finished = await GetOperationAsync(created.Id);
        var afterRetry = await GetStacksAsync(stacks);

        // Assert
        Assert.NotNull(interrupted);
        Assert.Equal(BackgroundTaskStatus.Running, interrupted.Status);
        Assert.Equal(0, interrupted.Processed);
        Assert.Null(interrupted.Bulk!.Cursor);
        Assert.Equal(1, interrupted.Bulk.RetryCount);
        Assert.All(afterPartialPage.Values, stack => Assert.Equal(StackStatus.Open, stack.Status));
        Assert.Contains(Log.LogEntries, entry => entry.Exception is DocumentException error && error.Message.Contains("1 of 2 shards failed", StringComparison.Ordinal));
        Assert.Equal(BackgroundTaskStatus.Completed, finished.Status);
        Assert.Equal(2, finished.Succeeded);
        Assert.All(afterRetry.Values, stack => Assert.Equal(StackStatus.Fixed, stack.Status));
    }

    [Fact]
    public async Task Create_StackFilterSelection_AppliesTheActionToEveryMatchingStackAcrossBatches()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (stacks, _) = await CreateDataAsync(d =>
        {
            for (int i = 0; i < 5; i++)
            {
                d.Event().TestProject().Status(StackStatus.Open).Date(now.AddMinutes(-i - 1)).Create(1);
            }

            d.Event().TestProject().Status(StackStatus.Open).Date(now.AddDays(-20));
            d.Event().TestProject().Status(StackStatus.Fixed).Date(now.AddHours(-1));
            d.Event().FreeProject().Status(StackStatus.Open).Date(now.AddHours(-1));
        });
        var matching = stacks.Where(s => s.OrganizationId is SampleDataService.TEST_ORG_ID && s.Status is StackStatus.Open).Take(5).ToList();
        var request = StackFilterRequest("stacks.change-status", "status:open", ("status", "ignored"));

        // Act
        var created = await CreateOperationAsync(request);
        await RunStackOperationAsync(created.Id, batchSize: 2);
        var finished = await GetOperationAsync(created.Id);
        var after = await GetStacksAsync(stacks);

        // Assert
        Assert.Equal("stacks.change-status", created.Type);
        Assert.Equal(5, created.Total);
        Assert.StartsWith("Mark 5 stacks matching filter as ignored", created.Description);
        Assert.Equal(BackgroundTaskStatus.Completed, finished.Status);
        Assert.Equal(5, finished.Succeeded);
        Assert.Equal(0, finished.Failed);
        Assert.All(matching, s => Assert.Equal(StackStatus.Ignored, after[s.Id].Status));
        Assert.Equal(StackStatus.Open, after[stacks.Single(s => s.OrganizationId is SampleDataService.FREE_ORG_ID).Id].Status);
        Assert.Equal(StackStatus.Fixed, after[stacks.Single(s => s.Status is StackStatus.Fixed).Id].Status);
    }

    [Fact]
    public async Task Create_StackIdSelection_MarkFixedMatchesTheSingleEndpointForVersionAndState()
    {
        // Arrange
        var (stacks, _) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Status(StackStatus.Snoozed).MutateStack(s => s.SnoozeUntilUtc = TimeProvider.GetUtcNow().UtcDateTime.AddDays(1));
            d.Event().TestProject().Status(StackStatus.Snoozed).MutateStack(s => s.SnoozeUntilUtc = TimeProvider.GetUtcNow().UtcDateTime.AddDays(1));
        });
        var (bulk, single) = (stacks[0], stacks[1]);

        // Act
        var created = await CreateOperationAsync(StackIdRequest("stacks.mark-fixed", [bulk.Id], ("version", "1.2.3")));
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPath($"stacks/{single.Id}/mark-fixed").QueryString("version", "1.2.3").StatusCodeShouldBeOk());
        var after = await GetStacksAsync(stacks);

        // Assert
        Assert.Equal(BackgroundTaskStatus.Completed, (await GetOperationAsync(created.Id)).Status);
        Assert.Equal("Mark 1 selected stacks as fixed in 1.2.3", created.Description);
        Assert.Equal(StackStatus.Fixed, after[bulk.Id].Status);
        Assert.Equal("1.2.3", after[bulk.Id].FixedInVersion);
        Assert.Null(after[bulk.Id].SnoozeUntilUtc);
        AssertSameStackState(after[single.Id], after[bulk.Id]);
    }

    [Fact]
    public async Task Create_StackIdSelection_SnoozeMatchesTheSingleEndpoint()
    {
        // Arrange
        var (stacks, _) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Status(StackStatus.Fixed).MutateStack(s => { s.DateFixed = TimeProvider.GetUtcNow().UtcDateTime; s.FixedInVersion = "1.0.0"; });
            d.Event().TestProject().Status(StackStatus.Fixed).MutateStack(s => { s.DateFixed = TimeProvider.GetUtcNow().UtcDateTime; s.FixedInVersion = "1.0.0"; });
        });
        var (bulk, single) = (stacks[0], stacks[1]);
        var until = TimeProvider.GetUtcNow().UtcDateTime.AddDays(1);

        // Act
        await CreateOperationAsync(StackIdRequest("stacks.snooze", [bulk.Id], ("snooze_until_utc", until.ToString("o"))));
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPath($"stacks/{single.Id}/mark-snoozed").QueryString("snoozeUntilUtc", until.ToString("o")).StatusCodeShouldBeOk());
        var after = await GetStacksAsync(stacks);

        // Assert
        Assert.Equal(StackStatus.Snoozed, after[bulk.Id].Status);
        Assert.Equal(until, after[bulk.Id].SnoozeUntilUtc);
        Assert.Null(after[bulk.Id].FixedInVersion);
        Assert.Null(after[bulk.Id].DateFixed);
        AssertSameStackState(after[single.Id], after[bulk.Id]);
    }

    [Theory]
    [InlineData("open")]
    [InlineData("ignored")]
    [InlineData("discarded")]
    public async Task Create_StackIdSelection_ChangeStatusMatchesTheSingleEndpoint(string status)
    {
        // Arrange
        var (stacks, _) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Status(StackStatus.Fixed).MutateStack(s => { s.DateFixed = TimeProvider.GetUtcNow().UtcDateTime; s.FixedInVersion = "1.0.0"; });
            d.Event().TestProject().Status(StackStatus.Fixed).MutateStack(s => { s.DateFixed = TimeProvider.GetUtcNow().UtcDateTime; s.FixedInVersion = "1.0.0"; });
        });
        var (bulk, single) = (stacks[0], stacks[1]);

        // Act
        await CreateOperationAsync(StackIdRequest("stacks.change-status", [bulk.Id], ("status", status)));
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPath($"stacks/{single.Id}/change-status").QueryString("status", status).StatusCodeShouldBeOk());
        var after = await GetStacksAsync(stacks);

        // Assert
        Assert.Equal(status, after[bulk.Id].Status.ToString().ToLowerInvariant());
        Assert.Null(after[bulk.Id].FixedInVersion);
        Assert.Null(after[bulk.Id].DateFixed);
        AssertSameStackState(after[single.Id], after[bulk.Id]);
    }

    [Fact]
    public async Task Create_StackFilterSelection_DeleteRemovesTheStacksTheSameWayAsTheSingleEndpointAndTheirEventsWithTheCleanupJob()
    {
        // Arrange
        var now = TimeProvider.GetUtcNow();
        var (stacks, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Date(now.AddHours(-1)).Create(2);
            d.Event().TestProject().Date(now.AddHours(-1));
            d.Event().TestProject().Date(now.AddDays(-20));
        });
        var outside = events.Single(e => e.Date < now.AddDays(-10));
        var single = stacks.First(s => !String.Equals(s.Id, outside.StackId, StringComparison.Ordinal));
        var matching = stacks.Where(s => !String.Equals(s.Id, outside.StackId, StringComparison.Ordinal) && !String.Equals(s.Id, single.Id, StringComparison.Ordinal)).ToList();
        var request = StackFilterRequest("stacks.delete", "");

        // Act
        await SendRequestAsync(r => r.Delete().AsTestOrganizationUser().AppendPath($"stacks/{single.Id}").StatusCodeShouldBeAccepted());
        await RefreshDataAsync();
        var created = await CreateOperationAsync(request);
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        await RefreshDataAsync();
        var stackRepository = GetService<IStackRepository>();
        var deletedBulk = await stackRepository.GetByIdsAsync(matching.Select(s => s.Id).ToArray(), o => o.IncludeSoftDeletes().Cache(false));
        var deletedSingle = await stackRepository.GetByIdAsync(single.Id, o => o.IncludeSoftDeletes().Cache(false));
        var untouched = await stackRepository.GetByIdAsync(outside.StackId, o => o.IncludeSoftDeletes().Cache(false));
        await GetService<CleanupDataJob>().RunAsync(TestCancellationToken);
        await RefreshDataAsync();
        var remainingEvents = await _eventRepository.GetByIdsAsync(events.Select(e => e.Id).ToArray(), o => o.Cache(false));

        // Assert
        Assert.Equal(1, created.Total);
        Assert.Equal("Delete 1 stacks matching filter", created.Description);
        Assert.Equal(BackgroundTaskStatus.Completed, (await GetOperationAsync(created.Id)).Status);
        Assert.Equal(matching.Select(s => s.Id).Order(), deletedBulk.Select(s => s.Id).Order());
        Assert.All(deletedBulk, s => Assert.True(s.IsDeleted));
        Assert.True(deletedSingle!.IsDeleted);
        Assert.False(untouched!.IsDeleted);
        Assert.Equal([outside.Id], remainingEvents.Select(e => e.Id).ToArray());
    }

    [Fact]
    public async Task Create_StackIdSelectionWithAnotherOrganizationOrProjectsStack_LeavesThatStackAndCountsItFailed()
    {
        // Arrange
        var (stacks, _) = await CreateDataAsync(d =>
        {
            d.Event().TestProject();
            d.Event().TestProject().Project(SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID);
            d.Event().FreeProject();
        });
        var own = stacks.Single(s => s.ProjectId is SampleDataService.TEST_PROJECT_ID);
        var otherProject = stacks.Single(s => s.ProjectId is SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID);
        var foreign = stacks.Single(s => s.OrganizationId is SampleDataService.FREE_ORG_ID);

        // Act
        var created = await CreateOperationAsync(StackIdRequest("stacks.delete", [own.Id, otherProject.Id, foreign.Id]) with { ProjectId = SampleDataService.TEST_PROJECT_ID });
        await _workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        var finished = await GetOperationAsync(created.Id);
        var after = await GetStacksAsync(stacks);

        // Assert
        Assert.Equal(1, finished.Succeeded);
        Assert.Equal(2, finished.Failed);
        Assert.True(after.TryGetValue(own.Id, out var removed) is false || removed.IsDeleted);
        Assert.False(after[otherProject.Id].IsDeleted);
        Assert.False(after[foreign.Id].IsDeleted);
    }

    [Fact]
    public async Task Create_StackActionForAnotherOrganizationOrWithoutASession_IsRejectedAndStartsNothing()
    {
        // Arrange
        var request = StackFilterRequest("stacks.delete", "");

        // Act
        await SendRequestAsync(r => r.Post().AsFreeOrganizationUser().AppendPath("bulk-operations").Content(request).StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.Post().AppendPath("bulk-operations").Content(request).StatusCodeShouldBeUnauthorized());

        // Assert
        Assert.Empty(await _taskService.GetByOrganizationAsync(SampleDataService.TEST_ORG_ID));
    }

    [Fact]
    public async Task Create_StackActionsOtherThanDelete_AreNotDestructiveAndDoNotConflict()
    {
        // Arrange
        await CreateDataAsync(d => d.Event().TestProject());
        var delete = await CreateOperationAsync(StackFilterRequest("stacks.delete", ""));

        // Act
        await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPath("bulk-operations")
            .Content(StackFilterRequest("stacks.change-status", "", ("status", "ignored"))).StatusCodeShouldBeAccepted());
        await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPath("bulk-operations")
            .Content(StackFilterRequest("stacks.delete", "")).StatusCodeShouldBeConflict());

        // Assert
        Assert.Equal(BackgroundTaskStatus.Queued, (await GetOperationAsync(delete.Id)).Status);
    }

    [Fact]
    public async Task Create_InvalidStackRequests_AreRejectedWithoutStartingAnything()
    {
        // Arrange
        string soon = TimeProvider.GetUtcNow().UtcDateTime.AddMinutes(2).ToString("o");
        string later = TimeProvider.GetUtcNow().UtcDateTime.AddDays(1).ToString("o");
        var requests = new[]
        {
            StackFilterRequest("stacks.mark-fixed", "", ("version", "not a version")),
            StackFilterRequest("stacks.mark-fixed", "", ("unknown", "1.0.0")),
            StackFilterRequest("stacks.snooze", ""),
            StackFilterRequest("stacks.snooze", "", ("snooze_until_utc", "tomorrow-ish")),
            StackFilterRequest("stacks.snooze", "", ("snooze_until_utc", soon)),
            StackFilterRequest("stacks.snooze", "", ("snooze_until_utc", later), ("other", "x")),
            StackFilterRequest("stacks.change-status", ""),
            StackFilterRequest("stacks.change-status", "", ("status", "regressed")),
            StackFilterRequest("stacks.change-status", "", ("status", "snoozed")),
            StackFilterRequest("stacks.change-status", "", ("status", "fixed")),
            StackFilterRequest("stacks.change-status", "", ("status", "bogus")),
            StackFilterRequest("stacks.delete", "", ("force", "true")),
            StackFilterRequest("stacks.delete", "status:(open"),
            StackFilterRequest("stacks.delete", "", time: "not a time range"),
            StackFilterRequest("stacks.explode", "")
        };

        // Act
        foreach (var request in requests)
        {
            await SendRequestAsync(r => r.Post().AsTestOrganizationUser().AppendPath("bulk-operations").Content(request).StatusCodeShouldBeUnprocessableEntity());
        }

        // Assert
        Assert.Empty(await _taskService.GetByOrganizationAsync(SampleDataService.TEST_ORG_ID));
    }

    [Fact]
    public async Task Create_StackActionWithPremiumFilterOnFreePlan_RequiresUpgrade()
    {
        // Arrange
        var request = StackFilterRequest("stacks.change-status", "tag:premium-tag", ("status", "ignored")) with { OrganizationId = SampleDataService.FREE_ORG_ID };

        // Act
        await SendRequestAsync(r => r.Post().AsFreeOrganizationUser().AppendPath("bulk-operations").Content(request).StatusCodeShouldBeUpgradeRequired());

        // Assert
        Assert.Empty(await _taskService.GetByOrganizationAsync(SampleDataService.FREE_ORG_ID));
    }

    private sealed class SearchNodeEventRepository(ExceptionlessElasticConfiguration configuration, AppOptions options, MiniValidationValidator validator, Uri searchNode)
        : EventRepository(configuration, options, validator)
    {
        protected override async Task<SearchRequestDescriptor<PersistentEvent>> CreateSearchDescriptorAsync(IRepositoryQuery query, ICommandOptions options)
        {
            var search = await base.CreateSearchDescriptorAsync(query, options);
            return search.RequestConfiguration(request => request.ForceNode(searchNode));
        }
    }

    private static NewBulkOperation StackFilterRequest(string type, string filter, params (string Key, string Value)[] parameters)
        => StackFilterRequest(type, filter, "[now-7d TO now]", parameters);

    private static NewBulkOperation StackFilterRequest(string type, string filter, string time, params (string Key, string Value)[] parameters)
    {
        return FilterRequest(SampleDataService.TEST_ORG_ID, null, filter, time) with
        {
            Type = type,
            Parameters = parameters.Length is 0 ? null : parameters.ToDictionary(p => p.Key, p => p.Value)
        };
    }

    private static NewBulkOperation StackIdRequest(string type, List<string> ids, params (string Key, string Value)[] parameters)
    {
        return new NewBulkOperation
        {
            Type = type,
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Selection = new NewBulkOperationSelection { Kind = BulkOperationSelectionKind.Ids, Ids = ids },
            Parameters = parameters.Length is 0 ? null : parameters.ToDictionary(p => p.Key, p => p.Value)
        };
    }

    private async Task<Dictionary<string, Stack>> GetStacksAsync(IEnumerable<Stack> stacks)
    {
        await RefreshDataAsync();
        var stackRepository = GetService<IStackRepository>();
        var loaded = await stackRepository.GetByIdsAsync(stacks.Select(s => s.Id).ToArray(), o => o.IncludeSoftDeletes().Cache(false));
        return loaded.ToDictionary(s => s.Id);
    }

    private static void AssertSameStackState(Stack expected, Stack actual)
    {
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.FixedInVersion, actual.FixedInVersion);
        Assert.Equal(expected.DateFixed.HasValue, actual.DateFixed.HasValue);
        Assert.Equal(expected.SnoozeUntilUtc, actual.SnoozeUntilUtc);
        Assert.Equal(expected.IsDeleted, actual.IsDeleted);
        Assert.Equal(expected.IsNewNotificationPending, actual.IsNewNotificationPending);
        Assert.Equal(expected.IsRegressionNotificationPending, actual.IsRegressionNotificationPending);
    }

    private Task RunStackOperationAsync(string operationId, int batchSize, StackBulkSelectionResolver? stackResolver = null)
    {
        stackResolver ??= GetService<StackBulkSelectionResolver>();
        var lockProvider = GetService<ILockProvider>();
        var handler = new BulkOperationWorkItemHandler(_bulkOperationService, _taskService, [stackResolver],
            new BulkOperationOptions { BatchSize = batchSize, RetryDelay = TimeSpan.Zero }, lockProvider, TimeProvider, Log);
        var workItem = new BulkOperationWorkItem { TaskId = operationId, OrganizationId = SampleDataService.TEST_ORG_ID };
        var context = new WorkItemContext(workItem, "test-job", EmptyLock.Empty, TestCancellationToken, static (_, _) => Task.CompletedTask);

        return handler.HandleItemAsync(context);
    }
}
