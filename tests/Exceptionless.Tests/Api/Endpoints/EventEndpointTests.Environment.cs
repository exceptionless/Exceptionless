using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Repositories.Models;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public partial class EventEndpointTests
{
    [Theory]
    [InlineData("null", null, "environment")]
    [InlineData("{\"region\":\"west\"}", null, "environment")]
    [InlineData("{\"region\":\"west\"}", null, "Environment")]
    [InlineData("\"west\"", "west", "environment")]
    [InlineData("\" Production \"", "Production", "environment")]
    [InlineData("\" Production \"", "Production", "ENVIRONMENT")]
    [InlineData("\"   \"", null, "environment")]
    [InlineData("\"bad\\nenvironment\"", null, "environment")]
    public async Task PostEvent_RootEnvironment_StoresValidNamesAndPreservesOtherValuesAsCustomData(string environment, string? expected, string propertyName)
    {
        // Arrange
        var content = new Dictionary<string, object?>
        {
            ["type"] = "log",
            ["message"] = "Legacy environment metadata",
            ["reference_id"] = "legacy-environment-reference",
            [propertyName] = JsonSerializer.Deserialize<JsonElement>(environment)
        };

        // Act
        await SendRequestAsync(request => request.Post()
            .AsTestOrganizationClientUser().AppendPath("events")
            .Content(content).StatusCodeShouldBeAccepted());

        await GetService<EventPostsJob>().RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        var events = await SendRequestAsAsync<List<PersistentEvent>>(request => request
            .AsTestOrganizationUser().AppendPath("events")
            .QueryString("filter", "reference:legacy-environment-reference").StatusCodeShouldBeOk());

        // Assert
        var ev = Assert.Single(Assert.IsType<List<PersistentEvent>>(events));
        Assert.Equal(expected, ev.Environment);
        Assert.NotNull(ev.Data);
        if (expected is not null)
        {
            Assert.False(ev.Data.ContainsKey("environment"));
        }
        else
        {
            Assert.True(JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(environment), JsonSerializer.SerializeToElement(ev.Data["environment"])));
        }
        Assert.False(ev.Data.ContainsKey("environment1"));
    }

    [Theory]
    [InlineData(" Production ", "Production", "environment")]
    [InlineData(" Production ", "Production", "Environment")]
    [InlineData("preview-42", "preview-42", "environment")]
    [InlineData("   ", null, "environment")]
    [InlineData("bad\nenvironment", null, "environment")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklm", null, "environment")]
    public async Task GetSubmitEvent_EnvironmentParameter_StoresValidNamesAndPreservesOtherValuesAsCustomData(string environment, string? expected, string propertyName)
    {
        // Arrange is provided by the theory data.

        // Act
        await SendRequestAsync(request => request
            .AsTestOrganizationClientUser().AppendPaths("events", "submit")
            .QueryString("message", "GET environment submission")
            .QueryString("reference", "get-environment-reference")
            .QueryString(propertyName, environment).StatusCodeShouldBeOk());

        await GetService<EventPostsJob>().RunAsync(TestCancellationToken);
        await RefreshDataAsync();
        var storedEvents = (await _eventRepository.GetAllAsync()).Documents;

        // Assert
        var ev = Assert.Single(storedEvents,
            item => item.ReferenceId == "get-environment-reference");
        Assert.Equal(expected, ev.Environment);
        Assert.NotNull(ev.Data);
        if (expected is not null)
        {
            Assert.False(ev.Data.ContainsKey(propertyName));
        }
        else
        {
            Assert.Equal(environment, ev.Data[propertyName]);
        }
        Assert.False(ev.Data.ContainsKey("environment1"));
    }

    [Fact]
    public async Task GetStacks_EnvironmentFilter_ScopesUserCountsAndTheirCache()
    {
        // Arrange
        await CreateDataAsync(data =>
        {
            var first = data.Event().FreeProject().Type(Event.KnownTypes.Error).Mutate(ev => { ev.Environment = "production"; ev.SetUserIdentity("production-0"); });
            for (int i = 1; i < 12; i++)
            {
                string identity = $"production-{i}";
                data.Event().FreeProject().Type(Event.KnownTypes.Error).Stack(first).Mutate(ev => { ev.Environment = "production"; ev.SetUserIdentity(identity); });
            }
            data.Event().FreeProject().Type(Event.KnownTypes.Error).Stack(first).Mutate(ev => { ev.Environment = "staging"; ev.SetUserIdentity("staging-user"); });
            data.Event().FreeProject().Type(Event.KnownTypes.Log).Mutate(ev => { ev.Environment = "production"; ev.SetUserIdentity("unaffected-production-user"); });
            data.Event().FreeProject().Type(Event.KnownTypes.Error).Stack(first).Mutate(ev => { ev.Environment = "development"; ev.SetUserIdentity("development-user"); });
            data.Event().FreeProject().Type(Event.KnownTypes.Log).Mutate(ev => { ev.Environment = "development"; ev.SetUserIdentity("unaffected-development-user"); });
            data.Event().FreeProject().Type(Event.KnownTypes.Error).Stack(first).Mutate(ev => ev.SetUserIdentity("unspecified-user"));
            data.Event().FreeProject().Type(Event.KnownTypes.Log).Mutate(ev => ev.SetUserIdentity("unaffected-unspecified-user"));
        });

        var cases = new[]
        {
            ("environment:production", 12, 13), ("environment:staging", 1, 1), ("environment:production", 12, 13),
            ("environment:(production OR staging)", 13, 14), ("_missing_:environment", 1, 2), ("_exists_:environment", 14, 16),
            ("(_missing_:environment OR environment:production)", 13, 15), ("", 15, 18)
        };

        // Act
        var results = new List<(int Expected, int TotalUsers, List<StackSummaryModel>? Stacks)>();
        foreach (var (filter, expected, totalUsers) in cases)
        {
            var stacks = await SendRequestAsAsync<List<StackSummaryModel>>(request => request
                .AsFreeOrganizationUser().AppendPath("events").QueryString("mode", "stack_frequent")
                .QueryString("filter", $"type:error {filter}").StatusCodeShouldBeOk());
            results.Add((expected, totalUsers, stacks));
        }

        // Assert
        foreach (var (expected, totalUsers, stacks) in results)
        {
            var stack = Assert.Single(Assert.IsType<List<StackSummaryModel>>(stacks));
            Assert.Equal(expected, stack.Total);
            Assert.Equal(expected, stack.Users);
            Assert.Equal(totalUsers, stack.TotalUsers);
        }
    }

    [Fact]
    public async Task GetStacks_EnvironmentFilter_CountsUsersOnlyForTheStackProjects()
    {
        // Arrange
        await CreateDataAsync(data =>
        {
            data.Event().FreeProject().Type(Event.KnownTypes.Error).Mutate(ev => { ev.Environment = "production"; ev.SetUserIdentity("stack-user"); });
            // More busy projects than the project terms aggregation returns by default.
            for (int project = 0; project < 11; project++)
            {
                string projectId = $"{project:D24}";
                for (int user = 0; user < 3; user++)
                {
                    string identity = $"other-{project}-{user}";
                    data.Event().Organization(SampleDataService.FREE_ORG_ID).Project(projectId).Type(Event.KnownTypes.Log)
                        .Mutate(ev => { ev.Environment = "production"; ev.SetUserIdentity(identity); });
                }
            }
        });

        // Act
        var stacks = await SendRequestAsAsync<List<StackSummaryModel>>(request => request
            .AsFreeOrganizationUser().AppendPath("events").QueryString("mode", "stack_frequent")
            .QueryString("filter", "type:error environment:production").StatusCodeShouldBeOk());

        // Assert
        var stack = Assert.Single(Assert.IsType<List<StackSummaryModel>>(stacks));
        Assert.Equal(1, stack.Users);
        Assert.Equal(1, stack.TotalUsers);
    }

    [Theory]
    [InlineData("environment")]
    [InlineData("Environment")]
    public async Task GetSubmitEvent_RepeatedEnvironments_PreservesCollectionAsCustomData(string propertyName)
    {
        // Arrange is provided by the theory data.

        // Act
        await SendRequestAsync(request => request
            .AsTestOrganizationClientUser().AppendPaths("events", "submit")
            .QueryString("message", "Repeated GET environments")
            .QueryString("reference", "repeated-environment-reference")
            .QueryStrings(propertyName, new[] { " Production ", "staging" }).StatusCodeShouldBeOk());

        await GetService<EventPostsJob>().RunAsync(TestCancellationToken);
        await RefreshDataAsync();
        var storedEvents = (await _eventRepository.GetAllAsync()).Documents;

        // Assert
        var ev = Assert.Single(storedEvents,
            item => item.ReferenceId == "repeated-environment-reference");
        Assert.Null(ev.Environment);
        Assert.NotNull(ev.Data);
        Assert.Contains(propertyName, ev.Data.Keys, StringComparer.Ordinal);
        Assert.Equal(new[] { " Production ", "staging" }, JsonSerializer.SerializeToElement(ev.Data[propertyName]).EnumerateArray().Select(value => value.GetString()));
        Assert.False(ev.Data.ContainsKey("environment1"));
    }

    [Fact]
    public async Task GetEvents_EnvironmentOnFreePlan_FiltersEventsSummariesAndStacks()
    {
        // Arrange
        await CreateDataAsync(data =>
        {
            var production = data.Event().FreeProject().Mutate(ev => ev.Environment = " Production ");
            data.Event().FreeProject().Stack(production).Mutate(ev => ev.Environment = "production");
            data.Event().FreeProject().Stack(production).Mutate(ev => ev.Environment = "staging");
            data.Event().FreeProject().Stack(production);
            data.Event().TestProject().Mutate(ev => ev.Environment = "private-environment");
        });

        // Act
        var events = await SendRequestAsAsync<List<PersistentEvent>>(request => request
            .AsFreeOrganizationUser().AppendPath("events")
            .QueryString("filter", "environment:PRODUCTION").StatusCodeShouldBeOk());

        var summaries = await SendRequestAsAsync<List<EventSummaryModel>>(request => request
            .AsFreeOrganizationUser().AppendPath("events").QueryString("mode", "summary")
            .QueryString("filter", "environment:production").StatusCodeShouldBeOk());

        var stacks = await SendRequestAsAsync<List<StackSummaryModel>>(request => request
            .AsFreeOrganizationUser().AppendPath("events").QueryString("mode", "stack_frequent")
            .QueryString("filter", "environment:production").StatusCodeShouldBeOk());

        var missing = await SendRequestAsAsync<List<PersistentEvent>>(request => request
            .AsFreeOrganizationUser().AppendPath("events")
            .QueryString("filter", "_missing_:environment").StatusCodeShouldBeOk());

        var count = await SendRequestAsAsync<CountResult>(request => request
            .AsFreeOrganizationUser().AppendPaths("events", "count")
            .QueryString("aggregations", "terms:(environment~100)").StatusCodeShouldBeOk());

        // Assert
        Assert.Equal(new[] { "Production", "production" }, Assert.IsType<List<PersistentEvent>>(events).Select(ev => ev.Environment).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "Production", "production" }, Assert.IsType<List<EventSummaryModel>>(summaries).Select(ev => ev.Environment).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(2, Assert.Single(Assert.IsType<List<StackSummaryModel>>(stacks)).Total);
        Assert.Null(Assert.Single(Assert.IsType<List<PersistentEvent>>(missing)).Environment);
        Assert.NotNull(count);
        Assert.Equal(4, count.Total);
        Assert.Equal(new[] { "production", "staging" }, count.Aggregations.Terms<string>("terms_environment")!.Buckets.Select(bucket => bucket.Key).Order().ToArray());
        Assert.Equal(2, count.Aggregations.Terms<string>("terms_environment")!.Buckets.Single(bucket => bucket.Key == "production").Total);
    }
}
