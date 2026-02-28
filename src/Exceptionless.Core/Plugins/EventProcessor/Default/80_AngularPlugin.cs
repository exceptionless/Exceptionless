using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor;

[Priority(80)]
public sealed class AngularPlugin : EventProcessorPluginBase
{
    private readonly ITextSerializer _serializer;

    public AngularPlugin(ITextSerializer serializer, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _serializer = serializer;
    }

    public override Task EventProcessingAsync(EventContext context)
    {
        if (!context.Event.IsError())
            return Task.CompletedTask;

        var error = context.Event.GetError(_serializer);
        if (error is null)
            return Task.CompletedTask;

        string? submissionMethod = context.Event.GetSubmissionMethod();
        if (submissionMethod is null || !String.Equals("$exceptionHandler", submissionMethod))
            return Task.CompletedTask;

        if (context.StackSignatureData.Count != 1 || !context.StackSignatureData.ContainsKey("NoStackingInformation"))
            return Task.CompletedTask;

        string? cause = context.Event.Message;
        if (String.IsNullOrEmpty(cause))
            return Task.CompletedTask;

        if (cause.StartsWith("Possibly unhandled rejection"))
        {
            context.StackSignatureData.Remove("NoStackingInformation");
            context.StackSignatureData.Add("ExceptionType", error.Type ?? "Error");
            context.StackSignatureData.Add("Source", "unhandledRejection");

            error.SetTargetInfo(new SettingsDictionary(context.StackSignatureData));
        }

        return Task.CompletedTask;
    }
}
