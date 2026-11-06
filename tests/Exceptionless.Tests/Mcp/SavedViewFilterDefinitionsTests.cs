using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Exceptionless.Web.Mcp;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Mcp;

public sealed class SavedViewFilterDefinitionsTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Fact]
    public void ToFilter_ContractCases_MatchesTheApp()
    {
        // Arrange
        var cases = JsonNode.Parse(File.ReadAllText(GetContractPath()))!.AsArray();

        // Act
        var results = cases.Select(item => (
            Name: item!["name"]!.GetValue<string>(),
            Expected: item["filter"]!.GetValue<string>(),
            Actual: SavedViewFilterDefinitions.ToFilter(SavedViewFilterDefinitions.Parse(item["definitions"]!.ToJsonString(), null)) ?? String.Empty)).ToList();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, result => Assert.True(result.Expected == result.Actual, $"{result.Name}: expected '{result.Expected}' but got '{result.Actual}'."));
    }

    [Fact]
    public void IsRawFilter_ContractCases_ChipsAgreeWithTheirFilter()
    {
        // Arrange
        var cases = JsonNode.Parse(File.ReadAllText(GetContractPath()))!.AsArray();

        // Act
        var rawCases = cases
            .Where(item => SavedViewFilterDefinitions.IsRawFilter(item!["definitions"]!.ToJsonString(), item["filter"]!.GetValue<string>()))
            .Select(item => item!["name"]!.GetValue<string>())
            .ToList();

        // Assert
        Assert.Empty(rawCases);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(null, "project:a", true)]
    [InlineData("[]", "project:a", true)]
    [InlineData("not json", "project:a", true)]
    [InlineData("""[{"type":"status","value":["open"]}]""", "status:fixed", true)]
    [InlineData("""[{"type":"status","value":["open"]}]""", "status:open", false)]
    [InlineData("""[{"type":"status","value":["open"]}]""", null, false)]
    public void IsRawFilter_View_OnlyWhenChipsCannotRepresentTheFilter(string? definitions, string? filter, bool expected)
    {
        // Act
        bool result = SavedViewFilterDefinitions.IsRawFilter(definitions, filter);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Parse_ClearedFilterWithOldChips_DoesNotBringThemBack()
    {
        // Arrange
        const string definitions = """[{"type":"status","value":["open"]},{"type":"date","term":"date","value":"[now-7d TO now]"}]""";

        // Act
        var cleared = SavedViewFilterDefinitions.Parse(definitions, String.Empty);
        var chipsOnly = SavedViewFilterDefinitions.Parse(definitions, null);

        // Assert
        Assert.Null(SavedViewFilterDefinitions.ToFilter(cleared));
        Assert.Equal("date", cleared.Single()!["type"]!.GetValue<string>());
        Assert.Equal("status:open", SavedViewFilterDefinitions.ToFilter(chipsOnly));
    }

    [Fact]
    public void Set_NewChip_GoesBeforeTheSearchSoAnOrCannotWidenIt()
    {
        // Arrange
        var definitions = new JsonArray();
        SavedViewFilterDefinitions.Set(definitions, "keyword", JsonValue.Create("status:open OR type:error"));

        // Act
        SavedViewFilterDefinitions.Set(definitions, "project", new JsonArray("a"));
        SavedViewFilterDefinitions.Set(definitions, "tag", new JsonArray("b"));

        // Assert
        Assert.Equal("project:a tag:b status:open OR type:error", SavedViewFilterDefinitions.ToFilter(definitions));
    }

    [Fact]
    public void Set_ExistingChip_KeepsItsPositionAndOtherChips()
    {
        // Arrange
        var definitions = SavedViewFilterDefinitions.Parse("""[{"type":"version","term":"version","value":"1.0.0"},{"type":"status","value":["open"],"hidden":true},{"type":"tag","value":["a"]}]""", null);

        // Act
        SavedViewFilterDefinitions.Set(definitions, "status", new JsonArray("fixed"));

        // Assert
        Assert.Equal("version:1.0.0 status:fixed tag:a", SavedViewFilterDefinitions.ToFilter(definitions));
        Assert.False(definitions[1]!.AsObject().ContainsKey("hidden"));
    }

    [Theory]
    [InlineData(null, "status:open", "status:open")]
    [InlineData("not json", "status:open", "status:open")]
    [InlineData("""{"type":"tag"}""", "status:open", "status:open")]
    [InlineData(null, null, null)]
    public void Parse_NoUsableDefinitions_KeepsTheFilterAsASearch(string? definitions, string? filter, string? expected)
    {
        // Act
        var result = SavedViewFilterDefinitions.Parse(definitions, filter);

        // Assert
        Assert.Equal(expected, SavedViewFilterDefinitions.ToFilter(result));
    }

    [Fact]
    public void Parse_ChipsDisagreeWithFilter_KeepsTheFilterAndTimeRange()
    {
        // Arrange
        const string definitions = """[{"type":"date","term":"date","value":"[now-7d TO now]"},{"type":"tag","value":["a"]}]""";

        // Act
        var result = SavedViewFilterDefinitions.Parse(definitions, "status:open");

        // Assert
        Assert.Equal("status:open", SavedViewFilterDefinitions.ToFilter(result));
        Assert.Contains(result, definition => definition!["type"]!.GetValue<string>() == "date");
        Assert.DoesNotContain(result, definition => definition!["type"]!.GetValue<string>() == "tag");
    }

    private static string GetContractPath([CallerFilePath] string testFilePath = "")
        => Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(testFilePath)!,
            "../../../src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/filter-definition-contract.json"));
}
