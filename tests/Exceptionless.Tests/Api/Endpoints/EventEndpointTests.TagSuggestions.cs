using System.Net;
using System.Text.Json;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public partial class EventEndpointTests
{
    [Fact]
    public async Task TagSuggestions_TruncatedAndCompleteResults_ExposeCompletenessMetadata()
    {
        // Arrange
        await CreateDataAsync(d =>
        {
            for (int i = 0; i < 260; i++)
                d.Event().TestProject().Tag($"suggestion-{i:D3}");
        });

        // Act
        using var truncated = await GetTagSuggestionsAsync("terms:(tags~251)");
        using var complete = await GetTagSuggestionsAsync("terms:(tags~251 @include:/suggestion-259/)");

        // Assert
        var truncatedAggregate = truncated.RootElement.GetProperty("aggregations").GetProperty("terms_tags");
        Assert.Equal(251, truncatedAggregate.GetProperty("items").GetArrayLength());
        Assert.Equal("bucket", truncatedAggregate.GetProperty("data").GetProperty("@type").GetString());
        Assert.True(truncatedAggregate.GetProperty("data").GetProperty("SumOtherDocCount").GetInt64() > 0);

        var completeAggregate = complete.RootElement.GetProperty("aggregations").GetProperty("terms_tags");
        Assert.Single(completeAggregate.GetProperty("items").EnumerateArray());
        var data = completeAggregate.GetProperty("data");
        Assert.Equal("bucket", data.GetProperty("@type").GetString());
        Assert.False(data.TryGetProperty("SumOtherDocCount", out _));
        Assert.False(data.TryGetProperty("DocCountErrorUpperBound", out _));
    }

    [Theory]
    [InlineData("Mixed.Case+Tag", "terms:(tags~251 @include:/.*[mM][iI][xX][eE][dD]\\\\.[cC][aA][sS][eE]\\\\+[tT][aA][gG].*/)")]
    [InlineData("path/segment", "terms:(tags~251 @include:/.*path\\/segment.*/)")]
    public async Task TagSuggestions_IncludePattern_MatchesLiteralTagOnly(string tag, string aggregation)
    {
        // Arrange
        await CreateDataAsync(d =>
        {
            d.Event().TestProject().Tag(tag, "unrelated");
            d.Event().FreeProject().Tag(tag);
        });

        // Act
        using var result = await GetTagSuggestionsAsync(aggregation);

        // Assert
        var bucket = Assert.Single(result.RootElement.GetProperty("aggregations").GetProperty("terms_tags").GetProperty("items").EnumerateArray());
        Assert.Equal(tag, bucket.GetProperty("key").GetString());
        Assert.Equal(1, bucket.GetProperty("total").GetInt64());
    }

    [Fact]
    public async Task TagSuggestions_AllTime_IncludesOlderRetainedTags()
    {
        // Arrange
        await CreateDataAsync(d => d.Event().TestProject().Date(TimeProvider.GetUtcNow().AddDays(-3)).Tag("older-retained-tag"));

        // Act
        using var result = await GetTagSuggestionsAsync("terms:(tags~251 @include:/older-retained-tag/)");

        // Assert
        var bucket = Assert.Single(result.RootElement.GetProperty("aggregations").GetProperty("terms_tags").GetProperty("items").EnumerateArray());
        Assert.Equal("older-retained-tag", bucket.GetProperty("key").GetString());
    }

    [Fact]
    public async Task TagSuggestions_FreeOrganization_PreservesPremiumRestriction()
    {
        // Arrange
        const string aggregation = "terms:(tags~251)";

        // Act
        using var response = await SendRequestAsync(r => r
            .AsFreeOrganizationUser()
            .ExpectedStatus(HttpStatusCode.UpgradeRequired)
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "events", "count")
            .QueryString("time", "all")
            .QueryString("aggregations", aggregation));

        // Assert
        Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
    }

    [Fact]
    public async Task TagSuggestions_OtherOrganization_DoesNotExposeTags()
    {
        // Arrange
        const string aggregation = "terms:(tags~251)";

        // Act
        using var response = await SendRequestAsync(r => r
            .AsFreeOrganizationUser()
            .ExpectedStatus(HttpStatusCode.NotFound)
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "events", "count")
            .QueryString("time", "all")
            .QueryString("aggregations", aggregation));

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<JsonDocument> GetTagSuggestionsAsync(string aggregation)
    {
        using var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "events", "count")
            .QueryString("time", "all")
            .QueryString("aggregations", aggregation)
            .StatusCodeShouldBeOk());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
