using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor.Default;

[Priority(90)]
public sealed class RemovePrivateInformationPlugin : EventProcessorPluginBase
{
    public RemovePrivateInformationPlugin(AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory) { }

    public override Task EventProcessingAsync(EventContext context)
    {
        if (context.IncludePrivateInformation)
            return Task.CompletedTask;

        context.Event.RemoveUserIdentity();

        var description = context.Event.GetUserDescription();
        if (description is not null)
        {
            description.EmailAddress = null;
            context.Event.SetUserDescription(description);
        }

        return Task.CompletedTask;
    }
}
