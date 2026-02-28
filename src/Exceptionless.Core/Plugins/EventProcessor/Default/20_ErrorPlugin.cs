using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Utility;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor;

[Priority(20)]
public sealed class ErrorPlugin : EventProcessorPluginBase
{
    private readonly ITextSerializer _serializer;

    public ErrorPlugin(ITextSerializer serializer, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
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

        if (String.IsNullOrWhiteSpace(context.Event.Message))
            context.Event.Message = error.Message;

        if (context.StackSignatureData.Count > 0)
            return Task.CompletedTask;

        string[]? userCommonMethods = ["DataContext.SubmitChanges", "Entities.SaveChanges"];
        if (context.HasProperty("CommonMethods"))
            userCommonMethods = context.GetProperty<string>("CommonMethods")?.SplitAndTrim([',']);

        string[]? userNamespaces = null;
        if (context.HasProperty("UserNamespaces"))
            userNamespaces = context.GetProperty<string>("UserNamespaces")?.SplitAndTrim([',']);

        var signature = new ErrorSignature(error, _serializer, userNamespaces, userCommonMethods);
        if (signature.SignatureInfo.Count <= 0)
            return Task.CompletedTask;

        var targetInfo = new SettingsDictionary(signature.SignatureInfo);
        var stackingTarget = error.GetStackingTarget();
        if (stackingTarget.Error.StackTrace?.Count > 0 && !targetInfo.ContainsKey("Message"))
            targetInfo.AddItemIfNotEmpty("Message", stackingTarget.Error.Message);

        error.SetTargetInfo(targetInfo);
        foreach (string key in signature.SignatureInfo.Keys)
            context.StackSignatureData.Add(key, signature.SignatureInfo[key]);

        return Task.CompletedTask;
    }
}
