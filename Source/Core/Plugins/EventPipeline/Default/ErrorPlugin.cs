using System;
using System.Linq;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Models.Data;
using MongoDB.Bson.Serialization;

namespace Exceptionless.Core.Plugins.EventPipeline {
    [Priority(15)]
    public class ErrorPlugin : EventPluginBase {
        public override void Startup() {
            if (!BsonClassMap.IsClassMapRegistered(typeof(Error))) {
                BsonClassMap.RegisterClassMap<Error>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.Modules).SetElementName(FieldNames.Modules).SetIgnoreIfDefault(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(InnerError))) {
                BsonClassMap.RegisterClassMap<InnerError>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.Message).SetElementName(FieldNames.Message).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.Type).SetElementName(FieldNames.Type);
                    cmm.GetMemberMap(c => c.Code).SetElementName(FieldNames.Code);
                    cmm.GetMemberMap(c => c.Data).SetElementName(FieldNames.Data).SetShouldSerializeMethod(obj => ((InnerError)obj).Data.Any());
                    cmm.GetMemberMap(c => c.Inner).SetElementName(FieldNames.Inner);
                    cmm.GetMemberMap(c => c.StackTrace).SetElementName(FieldNames.StackTrace).SetShouldSerializeMethod(obj => ((InnerError)obj).StackTrace.Any());
                    cmm.GetMemberMap(c => c.TargetMethod).SetElementName(FieldNames.TargetMethod).SetIgnoreIfNull(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(Module))) {
                BsonClassMap.RegisterClassMap<Module>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.ModuleId).SetElementName(FieldNames.ModuleId).SetIgnoreIfDefault(true);
                    cmm.GetMemberMap(c => c.Name).SetElementName(FieldNames.Name);
                    cmm.GetMemberMap(c => c.Version).SetElementName(FieldNames.Version).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.IsEntry).SetElementName(FieldNames.IsEntry).SetIgnoreIfDefault(true);
                    cmm.GetMemberMap(c => c.CreatedDate).SetElementName(FieldNames.CreatedDate);
                    cmm.GetMemberMap(c => c.ModifiedDate).SetElementName(FieldNames.ModifiedDate);
                    cmm.GetMemberMap(c => c.Data).SetElementName(FieldNames.Data).SetShouldSerializeMethod(obj => ((Module)obj).Data.Any());
                });
            }
        }

        public override void EventProcessing(EventContext context) {
            if (!context.Event.IsError)
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

        public static class FieldNames {
            public const string Error = "err";

            public const string Message = "msg";
            public const string Type = "typ";
            public const string Modules = "mod";
            public const string Code = "cod";
            public const string Data = CommonFieldNames.Data;
            public const string Inner = "inr";
            public const string StackTrace = "st";
            public const string TargetMethod = "meth";
            public const string Name = "nm";
            public const string Version = "ver";
            public const string ModuleId = "mid";
            public const string IsEntry = "ent";
            public const string CreatedDate = "crt";
            public const string ModifiedDate = "mod";
        }
    }
}