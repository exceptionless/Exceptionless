using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Exceptionless.Web.Mcp;
using Foundatio.Parsers.LuceneQueries;
using Foundatio.Parsers.LuceneQueries.Nodes;
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
        Assert.Equal("(project:a AND (tag:b AND (status:open OR type:error)))", GetQueryGrouping(new LuceneQueryParser().Parse(SavedViewFilterDefinitions.ToFilter(definitions)!)));
    }

    [Theory]
    [InlineData("""[{"type":"keyword","value":"type:error"},{"type":"tag","value":["critical"]}]""", "keyword", "\"type:error OR type:log\"")]
    [InlineData("""[{"type":"keyword","value":"type:error OR type:log"},{"type":"tag","value":["old"]}]""", "tag", """["critical"]""")]
    public void Set_ExistingChip_KeepsOtherCriteriaOutsideOrSearch(string originalDefinitions, string type, string value)
    {
        // Arrange
        var definitions = SavedViewFilterDefinitions.Parse(originalDefinitions, null);

        // Act
        SavedViewFilterDefinitions.Set(definitions, type, JsonNode.Parse(value));
        string? filter = SavedViewFilterDefinitions.ToFilter(definitions);
        var query = new LuceneQueryParser().Parse(filter!);

        // Assert
        Assert.Equal("tag:critical type:error OR type:log", filter);
        Assert.Equal("(tag:critical AND (type:error OR type:log))", GetQueryGrouping(query));
    }

    [Theory]
    [InlineData("[]", "tag:critical")]
    [InlineData("""["d"]""", "project:d tag:critical")]
    public void Set_DuplicateProjectChips_ReplacesEveryProjectRestriction(string projects, string expectedFilter)
    {
        // Arrange
        var definitions = SavedViewFilterDefinitions.Parse("""[{"type":"project","value":["a","b"]},{"type":"tag","value":["critical"],"hidden":true},{"type":"project","value":["b","c"]}]""", null);
        var originalTag = definitions[1]!.DeepClone();

        // Act
        SavedViewFilterDefinitions.Set(definitions, "project", JsonNode.Parse(projects));

        // Assert
        Assert.Equal(expectedFilter, SavedViewFilterDefinitions.ToFilter(definitions));
        Assert.Equal(2, definitions.Count);
        Assert.True(JsonNode.DeepEquals(originalTag, definitions[1]));
    }

    [Fact]
    public void Set_DuplicateTermChips_PreservesOtherTerms()
    {
        // Arrange
        var definitions = SavedViewFilterDefinitions.Parse("""[{"type":"string","term":"user","value":"old"},{"type":"string","term":"machine","value":"host","hidden":true},{"type":"string","term":"user","value":"older"}]""", null);
        var originalMachine = definitions[1]!.DeepClone();

        // Act
        SavedViewFilterDefinitions.Set(definitions, "string", JsonValue.Create("new"), term: "user");

        // Assert
        Assert.Equal("user:new machine:host", SavedViewFilterDefinitions.ToFilter(definitions));
        Assert.Equal(2, definitions.Count);
        Assert.True(JsonNode.DeepEquals(originalMachine, definitions[1]));
    }

    [Fact]
    public void Set_TimeOnly_KeepsExistingQueryGrouping()
    {
        // Arrange
        var definitions = SavedViewFilterDefinitions.Parse("""[{"type":"keyword","value":"type:error OR type:log"},{"type":"tag","value":["critical"]},{"type":"date","term":"date","value":"[now-7d TO now]"}]""", null);
        string? originalFilter = SavedViewFilterDefinitions.ToFilter(definitions);

        // Act
        SavedViewFilterDefinitions.Set(definitions, "date", JsonValue.Create("[now-1d TO now]"), term: "date");

        // Assert
        Assert.Equal(originalFilter, SavedViewFilterDefinitions.ToFilter(definitions));
        Assert.Equal("[now-1d TO now]", definitions[2]!["value"]!.GetValue<string>());
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

    private static string GetQueryGrouping(IQueryNode? node)
    {
        if (node is not GroupNode group)
            return node?.ToString() ?? String.Empty;

        if (group.Right is null)
            return GetQueryGrouping(group.Left);

        string operation = group.Operator == GroupOperator.Or ? "OR" : "AND";
        return $"({GetQueryGrouping(group.Left)} {operation} {GetQueryGrouping(group.Right)})";
    }
}
