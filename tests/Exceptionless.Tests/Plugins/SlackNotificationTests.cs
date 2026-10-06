using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public sealed class SlackNotificationTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Theory]
    [InlineData("http://localhost:7131")]
    [InlineData("http://localhost:7131/#!")]
    public void GetSlackEventNotificationMessage_BaseUrlVariant_UsesRootDestinations(string baseUrl)
    {
        // Arrange
        GetService<AppOptions>().BaseURL = baseUrl;
        var ev = new PersistentEvent { Id = "event-id", StackId = "stack-id", ProjectId = "project-id", Message = "Notification event" };
        var project = new Project { Id = ev.ProjectId, Name = "Notification project" };

        // Act
        var message = GetService<FormattingPluginManager>().GetSlackEventNotificationMessage(ev, project, false, true, false);

        // Assert
        var attachment = Assert.Single(message.Attachments);
        var actions = Assert.Single(attachment.Fields, field => field.Title == "Other Actions");
        Assert.Contains("<http://localhost:7131/event/event-id|View Event>", actions.Value);
        Assert.Contains("<http://localhost:7131/stack/stack-id/mark-fixed|Mark event as fixed>", actions.Value);
        Assert.Contains("<http://localhost:7131/stack/stack-id/ignored|Stop sending notifications for this event>", actions.Value);
        Assert.Contains("<http://localhost:7131/stack/stack-id/discarded|Discard future event occurrences>", actions.Value);
        Assert.Contains("<http://localhost:7131/project/project-id/integrations|Change your notification settings for this project>", actions.Value);
        Assert.DoesNotContain("/#!", actions.Value);
    }
}
