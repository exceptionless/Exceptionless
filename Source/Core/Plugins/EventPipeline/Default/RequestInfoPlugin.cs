using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Component;
using Exceptionless.Core.Repositories;
using Exceptionless.Models.Data;
using MongoDB.Bson.Serialization;

namespace Exceptionless.Core.Plugins.EventPipeline {
    [Priority(20)]
    public class RequestInfoPlugin : EventPluginBase {
        public override void Startup() {
            if (!BsonClassMap.IsClassMapRegistered(typeof(RequestInfo))) {
                BsonClassMap.RegisterClassMap<RequestInfo>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.UserAgent).SetElementName(FieldNames.UserAgent);
                    cmm.GetMemberMap(c => c.HttpMethod).SetElementName(FieldNames.HttpMethod);
                    cmm.GetMemberMap(c => c.IsSecure).SetElementName(FieldNames.IsSecure);
                    cmm.GetMemberMap(c => c.Host).SetElementName(FieldNames.Host);
                    cmm.GetMemberMap(c => c.Port).SetElementName(FieldNames.Port);
                    cmm.GetMemberMap(c => c.Path).SetElementName(FieldNames.Path);
                    cmm.GetMemberMap(c => c.Referrer).SetElementName(FieldNames.Referrer).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.ClientIpAddress).SetElementName(FieldNames.ClientIpAddress);
                    cmm.GetMemberMap(c => c.Cookies).SetElementName(FieldNames.Cookies).SetShouldSerializeMethod(obj => ((RequestInfo)obj).Cookies.Any());
                    cmm.GetMemberMap(c => c.PostData).SetElementName(FieldNames.PostData).SetShouldSerializeMethod(obj => ShouldSerializePostData(obj as RequestInfo));
                    cmm.GetMemberMap(c => c.QueryString).SetElementName(FieldNames.QueryString).SetShouldSerializeMethod(obj => ((RequestInfo)obj).QueryString.Any());
                    cmm.GetMemberMap(c => c.Data).SetElementName(FieldNames.Data).SetShouldSerializeMethod(obj => ((RequestInfo)obj).Data.Any());
                });
            }
        }

        private bool ShouldSerializePostData(RequestInfo requestInfo) {
            if (requestInfo == null)
                return false;

            if (requestInfo.PostData is Dictionary<string, string>)
                return ((Dictionary<string, string>)requestInfo.PostData).Any();

            return requestInfo.PostData != null;
        }

        public static class FieldNames {
            public const string RequestInfo = "req";

            public const string Data = CommonFieldNames.Data;
            public const string UserAgent = "ag";
            public const string HttpMethod = "verb";
            public const string IsSecure = "sec";
            public const string Host = "hst";
            public const string Port = "prt";
            public const string Path = "url";
            public const string Referrer = "ref";
            public const string ClientIpAddress = "ip";
            public const string Cookies = "cok";
            public const string PostData = "pst";
            public const string QueryString = "qry";
        }
    }
}