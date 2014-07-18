using System;
using System.Linq;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Models.Data;
using MongoDB.Bson.Serialization;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(30)]
    public class SimpleErrorPlugin : EventProcessorPluginBase {
        public override void Startup() {
            if (!BsonClassMap.IsClassMapRegistered(typeof(SimpleError))) {
                BsonClassMap.RegisterClassMap<SimpleError>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.Message).SetElementName(FieldNames.Message).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.Type).SetElementName(FieldNames.Type).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.Data).SetElementName(FieldNames.Data).SetShouldSerializeMethod(obj => ((InnerError)obj).Data.Any());
                    cmm.GetMemberMap(c => c.Inner).SetElementName(FieldNames.Inner).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.StackTrace).SetElementName(FieldNames.StackTrace).SetIgnoreIfNull(true);
                });
            }
        }

        public override void EventProcessing(EventContext context) {
            if (!context.Event.IsError())
                return;

            SimpleError error = context.Event.GetSimpleError();
            if (error == null)
                return;
            
            if (String.IsNullOrWhiteSpace(context.Event.Message))
                context.Event.Message = error.Message;

            // TODO: Parse the stack trace and run it through the ErrorSignature.
            context.StackSignatureData.Add("ExceptionType", error.Type);
            context.StackSignatureData.Add("StackTrace", error.StackTrace.ToSHA1());
        }

        private static class FieldNames {
            public const string SimpleError = "serr";

            public const string Message = "msg";
            public const string Type = "typ";
            public const string Data = CommonFieldNames.Data;
            public const string Inner = "inr";
            public const string StackTrace = "st";
        }
    }
}