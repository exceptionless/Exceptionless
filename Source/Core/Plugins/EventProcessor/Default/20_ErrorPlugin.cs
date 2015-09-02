﻿using System;
using System.Threading.Tasks;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(20)]
    public class ErrorPlugin : EventProcessorPluginBase {
        public override async Task EventProcessingAsync(EventContext context) {
            if (!context.Event.IsError())
                return;

            Error error = context.Event.GetError();
            if (error == null)
                return;

            if (String.IsNullOrWhiteSpace(context.Event.Message))
                context.Event.Message = error.Message;

            string[] commonUserMethods = { "DataContext.SubmitChanges", "Entities.SaveChanges" };
            if (context.HasProperty("CommonMethods"))
                commonUserMethods = context.GetProperty<string>("CommonMethods").SplitAndTrim(',');

            string[] userNamespaces = null;
            if (context.HasProperty("UserNamespaces"))
                userNamespaces = context.GetProperty<string>("UserNamespaces").SplitAndTrim(',');

            var signature = new ErrorSignature(error, userCommonMethods: commonUserMethods, userNamespaces: userNamespaces);
            if (signature.SignatureInfo.Count <= 0)
                return;

            var targetInfo = new SettingsDictionary(signature.SignatureInfo);
            var stackingTarget = error.GetStackingTarget();
            if (stackingTarget != null && stackingTarget.Error != null && !targetInfo.ContainsKey("Message"))
                targetInfo["Message"] = stackingTarget.Error.Message;

            error.Data[Error.KnownDataKeys.TargetInfo] = targetInfo;

            foreach (var key in signature.SignatureInfo.Keys)
                context.StackSignatureData.Add(key, signature.SignatureInfo[key]);
        }
    }
}