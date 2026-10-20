using System.Text.Json;
using Exceptionless.Web.Assistant;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantDsmlToolCallParserTests
{
    [Fact]
    public void TryParse_ToolCallsBlock_ReturnsLeadingTextAndTypedArguments()
    {
        const string content = """
            The 24-hour window came back empty, so let me check the last 7 days.

            <｜DSML｜tool_calls>
            <｜DSML｜invoke name="search_stacks">
            <｜DSML｜parameter name="filter" string="true">type:error</｜DSML｜parameter>
            <｜DSML｜parameter name="last" string="true">7d</｜DSML｜parameter>
            <｜DSML｜parameter name="limit" string="false">10</｜DSML｜parameter>
            <｜DSML｜parameter name="sort">-occurrences</｜DSML｜parameter>
            </｜DSML｜invoke>
            </｜DSML｜tool_calls>
            """;

        Assert.True(AssistantDsmlToolCallParser.TryParse(content, out string leadingText, out var toolCalls));

        Assert.Equal("The 24-hour window came back empty, so let me check the last 7 days.", leadingText);
        var toolCall = Assert.Single(toolCalls);
        Assert.Equal("search_stacks", toolCall.Name);
        using var arguments = JsonDocument.Parse(toolCall.Arguments);
        Assert.Equal("type:error", arguments.RootElement.GetProperty("filter").GetString());
        Assert.Equal("7d", arguments.RootElement.GetProperty("last").GetString());
        Assert.Equal(10, arguments.RootElement.GetProperty("limit").GetInt32());
        // A value without a string attribute that is not JSON stays literal text.
        Assert.Equal("-occurrences", arguments.RootElement.GetProperty("sort").GetString());
    }

    [Fact]
    public void TryParse_AsciiPipesAndParallelCalls_ReturnsCallsInOrder()
    {
        const string content = """
            <|DSML|function_calls>
            <|DSML|invoke name="list_projects">
            </|DSML|invoke>
            <|DSML|invoke name="suggest_followups">
            <|DSML|parameter name="actions" string="false">[{"label":"Billing API","prompt":"Search Billing API"}]</|DSML|parameter>
            </|DSML|invoke>
            </|DSML|function_calls>
            """;

        Assert.True(AssistantDsmlToolCallParser.TryParse(content, out string leadingText, out var toolCalls));

        Assert.Empty(leadingText);
        Assert.Collection(toolCalls,
            call =>
            {
                Assert.Equal("list_projects", call.Name);
                Assert.Equal("{}", call.Arguments);
            },
            call =>
            {
                Assert.Equal("suggest_followups", call.Name);
                using var arguments = JsonDocument.Parse(call.Arguments);
                Assert.Equal("Billing API", arguments.RootElement.GetProperty("actions")[0].GetProperty("label").GetString());
            });
    }

    [Theory]
    [InlineData("<|DSML|tool_calls>")]
    [InlineData("""<｜DSML｜tool_calls><｜DSML｜invoke name="suggest_followups">""")]
    [InlineData("""<｜DSML｜invoke name="search_stacks"><｜DSML｜parameter name="filter" string="true">type:error</｜DSML｜invoke>""")]
    [InlineData("""<｜DSML｜invoke name="search_stacks">stray text</｜DSML｜invoke>""")]
    [InlineData("""<｜DSML｜invoke name="list_projects"></｜DSML｜invoke> I will also <｜DSML｜invoke name="search_stacks"></｜DSML｜invoke>""")]
    [InlineData("No tool calls here.")]
    public void TryParse_IncompleteOrMixedMarkup_ReturnsFalse(string content)
    {
        Assert.False(AssistantDsmlToolCallParser.TryParse(content, out _, out var toolCalls));
        Assert.Empty(toolCalls);
    }
}
