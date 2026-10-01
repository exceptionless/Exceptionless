using Exceptionless.Core;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.Formatting;
using Xunit;

namespace Exceptionless.Tests.Plugins;

public sealed class SlackNotificationTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Fact]
    public void GetSlackEventNotificationMessage_LegacyBaseUrl_UsesRootActionConfirmations()
    {
        var options = GetService<AppOptions>();
        options.BaseURL = "http://localhost:7131/#!";
        var ev = new PersistentEvent { Id = "event-id", StackId = "stack-id", ProjectId = "project-id", Message = "Notification event" };
        var project = new Project { Id = ev.ProjectId, Name = "Notification project" };

        var message = GetService<FormattingPluginManager>().GetSlackEventNotificationMessage(ev, project, false, true, false);

        var attachment = Assert.Single(message.Attachments);
        var actions = Assert.Single(attachment.Fields, field => field.Title == "Other Actions");
        Assert.Contains("<http://localhost:7131/event/event-id|View Event>", actions.Value);
        Assert.Contains("<http://localhost:7131/stack/stack-id?action=fixed|Mark event as fixed>", actions.Value);
        Assert.Contains("<http://localhost:7131/stack/stack-id?action=ignored|Stop sending notifications for this event>", actions.Value);
        Assert.Contains("<http://localhost:7131/stack/stack-id?action=discarded|Discard future event occurrences>", actions.Value);
        Assert.Contains("<http://localhost:7131/project/project-id/integrations|Change your notification settings for this project>", actions.Value);
    }
}
