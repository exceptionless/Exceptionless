using Exceptionless.Core.Jobs;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
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
        var definitions = GetService<ICustomFieldDefinitionRepository>();
        var definition = await definitions.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "DatabaseVersion", "keyword");
        await RefreshDataAsync();

        await PostRawEventAsync("database-version-production", """"DatabaseVersion":"4.90"""");
        await PostRawEventAsync("database-version-development", """"DatabaseVersion":"4.90 build 1234 30-Aug-2024"""");
        await GetService<EventPostsJob>().RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        var production = Assert.Single((await _eventRepository.GetByReferenceIdAsync(
            SampleDataService.TEST_PROJECT_ID, "database-version-production")).Documents);
        var development = Assert.Single((await _eventRepository.GetByReferenceIdAsync(
            SampleDataService.TEST_PROJECT_ID, "database-version-development")).Documents);
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
        var definitions = GetService<ICustomFieldDefinitionRepository>();
        var definition = await definitions.AddFieldAsync(
            nameof(PersistentEvent), SampleDataService.TEST_ORG_ID, "DatabaseVersionNumeric", "double");
        await RefreshDataAsync();

        await PostRawEventAsync("database-version-numeric", """"DatabaseVersionNumeric":"4.90"""");
        await PostRawEventAsync("database-version-nonnumeric", """"DatabaseVersionNumeric":"4.90 build 1234 30-Aug-2024"""");
        await GetService<EventPostsJob>().RunAsync(TestCancellationToken);
        await RefreshDataAsync();

        var numeric = Assert.Single((await _eventRepository.GetByReferenceIdAsync(
            SampleDataService.TEST_PROJECT_ID, "database-version-numeric")).Documents);
        var nonnumeric = Assert.Single((await _eventRepository.GetByReferenceIdAsync(
            SampleDataService.TEST_PROJECT_ID, "database-version-nonnumeric")).Documents);
        Assert.Equal("4.90", Assert.IsType<string>(numeric.Data!["DatabaseVersionNumeric"]));
        Assert.Equal(4.9d, Assert.IsType<double>(numeric.Idx![definition.GetIdxName()]));
        Assert.Equal("4.90 build 1234 30-Aug-2024", Assert.IsType<string>(nonnumeric.Data!["DatabaseVersionNumeric"]));
        Assert.False(nonnumeric.Idx?.ContainsKey(definition.GetIdxName()) ?? false);
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
