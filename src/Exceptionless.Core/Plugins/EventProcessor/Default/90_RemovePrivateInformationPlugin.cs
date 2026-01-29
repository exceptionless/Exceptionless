using System.Text.Json;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor.Default;

[Priority(90)]
public sealed class RemovePrivateInformationPlugin : EventProcessorPluginBase
{
    private readonly JsonSerializerOptions _jsonOptions;

    public RemovePrivateInformationPlugin(JsonSerializerOptions jsonOptions, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _jsonOptions = jsonOptions;
    }

    public override Task EventProcessingAsync(EventContext context)
    {
        if (context.IncludePrivateInformation)
            return Task.CompletedTask;

        context.Event.RemoveUserIdentity();

        var description = context.Event.GetUserDescription(_jsonOptions);
        if (description is not null)
        {
            description.EmailAddress = null;
            context.Event.SetUserDescription(description);
        }

        return Task.CompletedTask;
    }
}
