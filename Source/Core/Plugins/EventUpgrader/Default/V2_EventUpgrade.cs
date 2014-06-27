using System;
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
            if (isNotFound)
                ctx.Document.Remove("Id");
            else
                ctx.Document.RenameOrRemoveIfNull("Id", "ReferenceId");

            ctx.Document.RenameOrRemoveIfNull("OccurrenceDate", "Date");
            ctx.Document.Remove("OrganizationId");
            ctx.Document.Remove("ProjectId");
            ctx.Document.Remove("ErrorStackId");
            ctx.Document.Remove("ExceptionlessClientInfo");
            ctx.Document.Remove("IsFixed");
            ctx.Document.Remove("IsHidden");
            ctx.Document.RenameOrRemoveIfNull("RequestInfo", "req");
            ctx.Document.RenameOrRemoveIfNull("EnvironmentInfo", "env");

            ctx.Document.RenameAll("ExtendedData", "Data");
            ctx.Document.RenameOrRemoveIfNull("ExtendedData", "Data");
            var extendedData = ctx.Document.Property("Data") != null ? ctx.Document.Property("Data").Value as JObject : null;
            if (extendedData != null)
                extendedData.RenameOrRemoveIfNull("TraceLog", "trace");

            string emailAddress = ctx.Document.GetPropertyStringValueAndRemove("UserEmail");
            string userDescription = ctx.Document.GetPropertyStringValueAndRemove("UserDescription");
            if (!String.IsNullOrWhiteSpace(emailAddress) && !String.IsNullOrWhiteSpace(userDescription))
                ctx.Document.Add("desc", new JObject(new UserDescription(emailAddress, userDescription)));

            string identity = ctx.Document.GetPropertyStringValueAndRemove("UserName");
            if (!String.IsNullOrWhiteSpace(identity))
                ctx.Document.Add("user", new JObject(new UserInfo(identity)));

            var error = new JObject();
            error.CopyOrRemoveIfNull(ctx.Document, "Code");
            error.CopyOrRemoveIfNull(ctx.Document, "Type");
            error.CopyOrRemoveIfNull(ctx.Document, "Message");
            error.CopyOrRemoveIfNull(ctx.Document, "Inner");
            error.CopyOrRemoveIfNull(ctx.Document, "StackTrace");
            error.CopyOrRemoveIfNull(ctx.Document, "TargetMethod");
            error.CopyOrRemoveIfNull(ctx.Document, "Modules");

            MoveExtraExceptionProperties(error, extendedData);
            var inner = error["inner"] as JObject;
            while (inner != null) {
                MoveExtraExceptionProperties(inner);
                inner = inner["inner"] as JObject;
            }

            ctx.Document.Add("err", error);
            ctx.Document.Add("Type", new JValue(isNotFound ? "404" : "error"));
        }

        private void MoveExtraExceptionProperties(JObject obj, JObject extendedData = null) {
            if (obj == null)
                return;

            if (extendedData == null)
                extendedData = obj["Data"] as JObject;

            string json = extendedData != null && extendedData["__ExceptionInfo"] != null ? extendedData["__ExceptionInfo"].ToString() : null;
            if (String.IsNullOrEmpty(json))
                return;

            try {
                var extraProperties = JObject.Parse(json);
                foreach (var property in extraProperties.Properties())
                    obj.Add(property.Name, property.Value);
            } catch (Exception) {}

            extendedData.Remove("__ExceptionInfo");
        }
    }
}