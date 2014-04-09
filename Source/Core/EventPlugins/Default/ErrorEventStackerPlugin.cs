using System;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Models.Data;

namespace Exceptionless.Core.EventPlugins.Default {
    [Priority(10)]
    public class ErrorEventStackerPlugin : EventPluginBase {
        public override void EventProcessing(EventContext context) {
            if (!context.Event.IsError())
                return;

            Error error = context.Event.GetError();
            if (error == null)
                return;

            string[] commonUserMethods = { "DataContext.SubmitChanges", "Entities.SaveChanges" };
            if (context.HasProperty("CommonMethods"))
                commonUserMethods = context.GetProperty<string>("CommonMethods").SplitAndTrim(',');

            string[] userNamespaces = null;
            if (context.HasProperty("UserNamespaces"))
                userNamespaces = context.GetProperty<string>("UserNamespaces").SplitAndTrim(',');

            var signature = new ErrorSignature(error, userCommonMethods: commonUserMethods, userNamespaces: userNamespaces);
            if (signature.SignatureInfo.Count <= 0)
                return;

            foreach (var key in signature.SignatureInfo.Keys)
                context.StackSignatureData.Add(key, signature.SignatureInfo[key]);
        }
    }
}