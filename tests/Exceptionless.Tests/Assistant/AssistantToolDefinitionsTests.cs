using System.Runtime.CompilerServices;
using System.Text.Json;
using Exceptionless.Web.Assistant;
using Exceptionless.Web.Mcp;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantToolDefinitionsTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Theory]
    [InlineData("/stack/production-errors", "get_stack", "stackId", true)]
    [InlineData("/stack/production-errors", "get_stack_events", "stackId", true)]
    [InlineData("/event/production-logs", "get_event", "eventId", true)]
    [InlineData("/stack/537650f3b77efe23a47914aa", "get_stack", "stackId", false)]
    [InlineData("/stack/537650f3b77efe23a47914aa/event/537650f3b77efe23a47914ab", "get_event", "eventId", false)]
    public void Create_PageRoute_OnlySuppliesDefaultsForResourceIds(string path, string toolName, string propertyName, bool required)
    {
        // Arrange
        var tools = (ExceptionlessMcpTools)RuntimeHelpers.GetUninitializedObject(typeof(ExceptionlessMcpTools));
        var request = new AssistantChatRequest([new AssistantChatMessage("user", "Inspect this page.")], Path: path);

        // Act
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(AssistantToolDefinitions.Create(tools, request)));
        var schema = document.RootElement.EnumerateArray()
            .Single(tool => tool.GetProperty("function").GetProperty("name").GetString() == toolName)
            .GetProperty("function").GetProperty("parameters");

        // Assert
        bool isRequired = schema.TryGetProperty("required", out var properties)
            && properties.EnumerateArray().Any(property => property.GetString() == propertyName);
        Assert.Equal(required, isRequired);
        string description = schema.GetProperty("properties").GetProperty(propertyName).GetProperty("description").GetString()!;
        Assert.Equal(!required, description.Contains("Defaults to the current page", StringComparison.Ordinal));
    }
}
