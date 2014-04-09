using System;
using CodeSmith.Core.Component;
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

            var signature = new ErrorSignature(error, userCommonMethods: new[] { "DataContext.SubmitChanges", "Entities.SaveChanges" });
            if (signature.SignatureInfo.Count <= 0)
                return;

            foreach (var key in signature.SignatureInfo.Keys)
                context.StackSignatureData.Add(key, signature.SignatureInfo[key]);
        }
    }
}