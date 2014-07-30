using System;
using System.Diagnostics;
using System.Linq;
using CodeSmith.Core.Component;
using Exceptionless.Core.Extensions;
using Exceptionless.Models.Data;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    [Priority(2000)]
    public class V2EventUpgrade : IEventUpgraderPlugin {
        public void Upgrade(EventUpgraderContext ctx) {
            if (ctx.Version > new Version(2, 0))
                return;

            bool isNotFound = ctx.Document.GetPropertyStringValue("Code") == "404";

            if (ctx.IsMigration) {
                ctx.Document.Rename("ErrorStackId", "StackId");
            } else {
                if (isNotFound)
                    ctx.Document.Remove("Id");
                else
                    ctx.Document.RenameOrRemoveIfNullOrEmpty("Id", "ReferenceId");

                ctx.Document.Remove("OrganizationId");
                ctx.Document.Remove("ProjectId");
                ctx.Document.Remove("ErrorStackId");
            }

            ctx.Document.RenameOrRemoveIfNullOrEmpty("OccurrenceDate", "Date");
            ctx.Document.Remove("ExceptionlessClientInfo");
            if (!ctx.Document.RemoveIfNullOrEmpty("Tags")) {
                var tags = ctx.Document.GetValue("Tags");
                if (tags.Type == JTokenType.Array) {
                    foreach (JToken tag in tags.Where(tag => tag.ToString().Length > 255))
                        tag.Remove();
                }
            }

            ctx.Document.RenameOrRemoveIfNullOrEmpty("RequestInfo", "req");
            ctx.Document.RenameOrRemoveIfNullOrEmpty("EnvironmentInfo", "env");

            ctx.Document.RenameAll("ExtendedData", "Data");
            var extendedData = ctx.Document.Property("Data") != null ? ctx.Document.Property("Data").Value as JObject : null;
            if (extendedData != null)
                extendedData.RenameOrRemoveIfNullOrEmpty("TraceLog", "trace");

            var error = new JObject();
            error.MoveOrRemoveIfNullOrEmpty(ctx.Document, "Code");
            error.MoveOrRemoveIfNullOrEmpty(ctx.Document, "Type");
            error.CopyOrRemoveIfNullOrEmpty(ctx.Document, "Message");
            error.MoveOrRemoveIfNullOrEmpty(ctx.Document, "Inner");
            error.MoveOrRemoveIfNullOrEmpty(ctx.Document, "StackTrace");
            error.MoveOrRemoveIfNullOrEmpty(ctx.Document, "TargetMethod");
            error.MoveOrRemoveIfNullOrEmpty(ctx.Document, "Modules");

            MoveExtraExceptionProperties(error, extendedData);
            var inner = error["inner"] as JObject;
            while (inner != null) {
                MoveExtraExceptionProperties(inner);
                inner = inner["inner"] as JObject;
            }

            ctx.Document.Add("Type", new JValue(isNotFound ? "404" : "error"));
            ctx.Document.Add("err", error);

            string emailAddress = ctx.Document.GetPropertyStringValueAndRemove("UserEmail");
            string userDescription = ctx.Document.GetPropertyStringValueAndRemove("UserDescription");
            if (!String.IsNullOrWhiteSpace(emailAddress) && !String.IsNullOrWhiteSpace(userDescription))
                ctx.Document.Add("desc", JObject.FromObject(new UserDescription(emailAddress, userDescription)));

            string identity = ctx.Document.GetPropertyStringValueAndRemove("UserName");
            if (!String.IsNullOrWhiteSpace(identity))
                ctx.Document.Add("user", JObject.FromObject(new UserInfo(identity)));

            ctx.Document.RemoveAllIfNullOrEmpty("Data");
            ctx.Document.RemoveAllIfNullOrEmpty("GenericArguments");
            ctx.Document.RemoveAllIfNullOrEmpty("Parameters");
        }

        private void MoveExtraExceptionProperties(JObject doc, JObject extendedData = null) {
            if (doc == null)
                return;

            if (extendedData == null)
                extendedData = doc["Data"] as JObject;

            string json = extendedData != null && extendedData["__ExceptionInfo"] != null ? extendedData["__ExceptionInfo"].ToString() : null;
            if (String.IsNullOrEmpty(json))
                return;

            try {
                var extraProperties = JObject.Parse(json);
                foreach (var property in extraProperties.Properties())
                    doc.Add(property.Name, property.Value);
            } catch (Exception) {}

            extendedData.Remove("__ExceptionInfo");
        }
    }
}