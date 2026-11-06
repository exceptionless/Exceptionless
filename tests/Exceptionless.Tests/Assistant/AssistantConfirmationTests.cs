using Exceptionless.Web.Assistant;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantConfirmationTests(ITestOutputHelper output) : TestWithLoggingBase(output)
{
    [Theory]
    [InlineData(AssistantConfirmation.Prompt, true)]
    [InlineData("yes", true)]
    [InlineData("YES!", true)]
    [InlineData("Yes, delete it.", true)]
    [InlineData("ok", true)]
    [InlineData("delete it", true)]
    [InlineData("no", false)]
    [InlineData("yesterday's errors", false)]
    [InlineData("ok, what does that view show?", false)]
    [InlineData("thanks", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsConfirmation_UserMessage_OnlyAcceptsAClearYes(string? message, bool expected)
    {
        // Act
        bool result = AssistantConfirmation.IsConfirmation(message);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("delete_saved_view", "{}", true)]
    [InlineData("create_saved_view", "{\"shared\":true}", true)]
    [InlineData("create_saved_view", "{\"shared\":false}", false)]
    [InlineData("create_saved_view", "{}", false)]
    [InlineData("create_saved_view", "not json", false)]
    [InlineData("update_saved_view", "{}", false)]
    public void IsRequired_Tool_CoversDeletesAndSharedViews(string toolName, string arguments, bool expected)
    {
        // Act
        bool result = AssistantConfirmation.IsRequired(toolName, arguments);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/stack/prod-errors", "prod-errors", null)]
    [InlineData("/event/checkout?time=all", "checkout", null)]
    [InlineData("/stream?saved=537650f3b77efe23a47914aa", "537650f3b77efe23a47914aa", null)]
    [InlineData("/stack/537650f3b77efe23a47914aa", null, "537650f3b77efe23a47914aa")]
    [InlineData("/stack", null, null)]
    [InlineData(null, null, null)]
    public void GetCurrentSavedView_PagePath_TellsViewsFromStacks(string? path, string? expectedView, string? expectedStack)
    {
        // Act
        string? view = AssistantService.GetCurrentSavedView(path);
        string? stack = AssistantService.GetCurrentResourceId(path, "stack");

        // Assert
        Assert.Equal(expectedView, view);
        Assert.Equal(expectedStack, stack);
    }
}
