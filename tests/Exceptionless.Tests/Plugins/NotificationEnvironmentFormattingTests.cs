using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.Formatting;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public sealed class NotificationEnvironmentFormattingTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Theory]
    [InlineData("production", "New Error in Production: Boom")]
    [InlineData("PRODUCTION", "New Error in Production: Boom")]
    [InlineData("qa-West", "New Error in qa-West: Boom")]
    [InlineData(null, "New Error: Boom")]
    public void GetEventNotificationMailMessageData_WithEnvironment_MentionsItUsingTheListedCasing(string? environment, string expectedSubject)
    {
        // Arrange
        var project = CreateProject();
        var ev = CreateError(environment);

        // Act
        var message = GetService<FormattingPluginManager>().GetEventNotificationMailMessageData(ev, project, false, true, false);

        // Assert
        Assert.Equal(expectedSubject, message.Subject);
    }

    [Theory]
    [InlineData("production", "[Notification project] A new error in Production: ")]
    [InlineData("qa-West", "[Notification project] A new error in qa-West: ")]
    [InlineData(null, "[Notification project] A new error: ")]
    public void GetSlackEventNotificationMessage_WithEnvironment_MentionsItUsingTheListedCasing(string? environment, string expectedPrefix)
    {
        // Arrange
        var project = CreateProject();
        var ev = CreateError(environment);

        // Act
        var message = GetService<FormattingPluginManager>().GetSlackEventNotificationMessage(ev, project, false, true, false);

        // Assert
        Assert.StartsWith(expectedPrefix, message.Text);
    }

    [Fact]
    public void GetEventNotificationMailMessageData_WithCriticalRegressionInEnvironment_KeepsTheNotificationTypeBeforeTheEnvironment()
    {
        // Arrange
        var project = CreateProject();
        var ev = CreateError("production");

        // Act
        var message = GetService<FormattingPluginManager>().GetEventNotificationMailMessageData(ev, project, true, false, true);

        // Assert
        Assert.Equal("Critical Error regression in Production: Boom", message.Subject);
    }

    private static Project CreateProject() => new()
    {
        Id = "537650f3b77efe23a47914f4",
        Name = "Notification project",
        Environments = [new ProjectEnvironment { Name = "Production" }]
    };

    private static PersistentEvent CreateError(string? environment) => new()
    {
        Id = "event-id",
        StackId = "stack-id",
        ProjectId = "537650f3b77efe23a47914f4",
        Type = Event.KnownTypes.Error,
        Environment = environment,
        Data = new DataDictionary { { Event.KnownDataKeys.SimpleError, new SimpleError { Message = "Boom" } } }
    };
}
