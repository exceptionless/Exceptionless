using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public partial class EventEndpointTests
{
    [Fact]
    public async Task PostEvent_DecimalLikeVersionConfiguredAsKeyword_PreservesExactTextThroughHttpAndElasticsearch()
    {
        var definition = await CreateEventCustomFieldAsync("DatabaseVersion", "keyword");

        await PostRawEventAsync("database-version-production", """"DatabaseVersion":"4.90"""");
        await PostRawEventAsync("database-version-development", """"DatabaseVersion":"4.90 build 1234 30-Aug-2024"""");
        await GetService<EventPostsJob>().RunUntilEmptyAsync(TestCancellationToken);
        await RefreshDataAsync();

        var production = await GetEventByReferenceIdWithCustomFieldsAsync("database-version-production");
        var development = await GetEventByReferenceIdWithCustomFieldsAsync("database-version-development");
        Assert.Equal("4.90", Assert.IsType<string>(production.Data!["DatabaseVersion"]));
        Assert.Equal("4.90", Assert.IsType<string>(production.Idx![definition.GetIdxName()]));
        Assert.Equal("4.90 build 1234 30-Aug-2024", Assert.IsType<string>(development.Data!["DatabaseVersion"]));
        Assert.Equal("4.90 build 1234 30-Aug-2024", Assert.IsType<string>(development.Idx![definition.GetIdxName()]));

        Assert.Equal(1, await CountVersionMatchesAsync("DatabaseVersion", "\"4.90\""));
        Assert.Equal(0, await CountVersionMatchesAsync("DatabaseVersion", "4.9"));
    }

    [Fact]
    public async Task PostEvent_DecimalLikeVersionConfiguredAsDouble_IsNumericAndSkipsDevelopmentText()
    {
        var definition = await CreateEventCustomFieldAsync("DatabaseVersionNumeric", "double");

        await PostRawEventAsync("database-version-numeric", """"DatabaseVersionNumeric":"4.90"""");
        await PostRawEventAsync("database-version-nonnumeric", """"DatabaseVersionNumeric":"4.90 build 1234 30-Aug-2024"""");
        await GetService<EventPostsJob>().RunUntilEmptyAsync(TestCancellationToken);
        await RefreshDataAsync();

        var numeric = await GetEventByReferenceIdWithCustomFieldsAsync("database-version-numeric");
        var nonnumeric = await GetEventByReferenceIdWithCustomFieldsAsync("database-version-nonnumeric");
        Assert.Equal("4.90", Assert.IsType<string>(numeric.Data!["DatabaseVersionNumeric"]));
        Assert.Equal(4.9d, Assert.IsType<double>(numeric.Idx![definition.GetIdxName()]));
        Assert.Equal("4.90 build 1234 30-Aug-2024", Assert.IsType<string>(nonnumeric.Data!["DatabaseVersionNumeric"]));
        Assert.False(nonnumeric.Idx?.ContainsKey(definition.GetIdxName()) ?? false);
    }

    private async Task<CustomFieldDefinition> CreateEventCustomFieldAsync(string name, string indexType)
    {
        var options = GetService<AppOptions>().CustomFieldOptions;
        var result = await GetService<EventCustomFieldService>().CreateFieldAsync(
            SampleDataService.TEST_ORG_ID,
            name,
            indexType,
            options.MaxFieldsPerOrganization,
            options.MaxLifetimeFieldsPerOrganization,
            cancellationToken: TestCancellationToken);

        Assert.Equal(EventCustomFieldService.CreateFieldStatus.Created, result.Status);
        await RefreshDataAsync();
        return Assert.IsType<CustomFieldDefinition>(result.Definition);
    }

    private async Task<PersistentEvent> GetEventByReferenceIdWithCustomFieldsAsync(string referenceId)
    {
        // Search a bounded test-project page and filter the raw _source value in memory. This avoids
        // coupling the ingestion regression to the separate reference-id search helper while still
        // proving the event, Data, and managed Idx values round-trip through Elasticsearch.
        var events = await _eventRepository.FindAsync(
            query => query.Project(SampleDataService.TEST_PROJECT_ID),
            options => options.PageLimit(1000).Include(
                eventDocument => eventDocument.ReferenceId,
                eventDocument => eventDocument.Data,
                eventDocument => eventDocument.Idx));
        return Assert.Single(events.Documents, eventDocument => eventDocument.ReferenceId == referenceId);
    }

    private Task PostRawEventAsync(string referenceId, string customDataProperty)
    {
        string payload = $$"""
        {
          "type": "log",
          "message": "custom field raw ingestion",
          "reference_id": "{{referenceId}}",
          "data": {
            {{customDataProperty}}
          }
        }
        """;

        return SendRequestAsync(request => request
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(payload, "application/json")
            .StatusCodeShouldBeAccepted());
    }

    private async Task<long> CountVersionMatchesAsync(string fieldName, string value)
    {
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID)
            ?? throw new InvalidOperationException("Test organization was not found.");
        var result = await _eventRepository.CountAsync(query => query
            .AppFilter(new AppFilter(organization))
            .FilterExpression($"data.{fieldName}:{value}"));
        return result.Total;
    }
}
