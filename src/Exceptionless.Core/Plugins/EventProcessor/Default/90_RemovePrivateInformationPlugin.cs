using Exceptionless.Core.Pipeline;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor.Default;

[Priority(90)]
public sealed class RemovePrivateInformationPlugin : EventProcessorPluginBase
{
    private readonly ITextSerializer _serializer;

    public RemovePrivateInformationPlugin(ITextSerializer serializer, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _serializer = serializer;
    }

    public override Task EventProcessingAsync(EventContext context)
    {
        if (context.IncludePrivateInformation)
            return Task.CompletedTask;

        context.Event.RemoveUserIdentity();

        var description = context.Event.GetUserDescription(_serializer);
        if (description is not null)
        {
            description.EmailAddress = null;
            context.Event.SetUserDescription(description);
        }

        return Task.CompletedTask;
    }
}
