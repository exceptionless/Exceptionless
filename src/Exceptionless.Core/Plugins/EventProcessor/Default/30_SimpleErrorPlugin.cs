using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor;

[Priority(30)]
public sealed class SimpleErrorPlugin : EventProcessorPluginBase
{
    private readonly ITextSerializer _serializer;

    public SimpleErrorPlugin(ITextSerializer serializer, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _serializer = serializer;
    }

    public override Task EventProcessingAsync(EventContext context)
    {
        if (!context.Event.IsError())
            return Task.CompletedTask;

        var error = context.Event.GetSimpleError(_serializer);
        if (error is null)
            return Task.CompletedTask;

        if (String.IsNullOrWhiteSpace(context.Event.Message))
            context.Event.Message = error.Message;

        if (context.StackSignatureData.Count > 0)
            return Task.CompletedTask;

        // TODO: Parse the stack trace and upgrade this to a full error.
        if (!String.IsNullOrEmpty(error.Type))
            context.StackSignatureData.Add("ExceptionType", error.Type);

        if (!String.IsNullOrEmpty(error.StackTrace))
            context.StackSignatureData.Add("StackTrace", error.StackTrace.ToSHA1());

        error.SetTargetInfo(new SettingsDictionary(context.StackSignatureData));
        return Task.CompletedTask;
    }
}
