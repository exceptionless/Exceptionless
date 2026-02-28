using System.Text.Json;
using System.Text.Json.Nodes;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Pipeline;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventUpgrader;

[Priority(2000)]
public class V2EventUpgrade : PluginBase, IEventUpgraderPlugin
{
    public V2EventUpgrade(AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory) { }

    public void Upgrade(EventUpgraderContext ctx)
    {
        if (ctx.Version > new Version(2, 0))
            return;

        foreach (var doc in ctx.Documents.OfType<JsonObject>())
        {
            bool isNotFound = doc.GetPropertyStringValue("Code") == "404";

            if (ctx.IsMigration)
            {
                doc.Rename("ErrorStackId", "StackId");
            }
            else
            {
                doc.RenameOrRemoveIfNullOrEmpty("Id", "ReferenceId");
                doc.Remove("OrganizationId");
                doc.Remove("ProjectId");
                doc.Remove("ErrorStackId");
            }

            doc.RenameOrRemoveIfNullOrEmpty("OccurrenceDate", "Date");
            doc.Remove("ExceptionlessClientInfo");
            if (!doc.RemoveIfNullOrEmpty("Tags"))
            {
                var tags = doc["Tags"];
                if (tags is JsonArray tagsArray)
                {
                    var tagsToRemove = new List<JsonNode?>();
                    foreach (var tag in tagsArray)
                    {
                        string? t = tag?.ToString();
                        if (String.IsNullOrEmpty(t) || t.Length > 255)
                            tagsToRemove.Add(tag);
                    }
                    foreach (var tag in tagsToRemove)
                        tagsArray.Remove(tag);
                }
            }

            doc.RenameOrRemoveIfNullOrEmpty("RequestInfo", "@request");
            bool hasRequestInfo = doc["@request"] is not null;

            if (!isNotFound)
                doc.RenameOrRemoveIfNullOrEmpty("EnvironmentInfo", "@environment");
            else
                doc.Remove("EnvironmentInfo");

            doc.RenameAll("ExtendedData", "Data");

            var extendedData = doc["Data"] as JsonObject;
            if (extendedData is not null)
            {
                if (!isNotFound)
                    extendedData.RenameOrRemoveIfNullOrEmpty("TraceLog", "@trace");
                else
                    extendedData.Remove("TraceLog");
            }

            if (isNotFound && hasRequestInfo)
            {
                doc.RemoveAll("Code", "Type", "Message", "Inner", "StackTrace", "TargetMethod", "Modules");
                if (extendedData?["__ExceptionInfo"] is not null)
                    extendedData.Remove("__ExceptionInfo");

                doc.Add("Type", JsonValue.Create("404"));
            }
            else
            {
                var error = new JsonObject();

                if (!doc.RemoveIfNullOrEmpty("Message"))
                {
                    var messageValue = doc["Message"]?.GetValue<string>();
                    if (messageValue is not null)
                        error.Add("Message", JsonValue.Create(messageValue));
                }

                error.MoveOrRemoveIfNullOrEmpty(doc, "Code", "Type", "Inner", "StackTrace", "TargetMethod", "Modules");

                // Copy the exception info from root extended data to the current errors extended data.
                if (extendedData?["__ExceptionInfo"] is not null)
                {
                    error.Add("Data", new JsonObject());
                    ((JsonObject)error["Data"]!).MoveOrRemoveIfNullOrEmpty(extendedData, "__ExceptionInfo");
                }

                string? id = doc["Id"]?.GetValue<string>();
                RenameAndValidateExtraExceptionProperties(id, error);

                var inner = error["Inner"] as JsonObject;
                while (inner is not null)
                {
                    RenameAndValidateExtraExceptionProperties(id, inner);
                    inner = inner["Inner"] as JsonObject;
                }

                doc.Add("Type", JsonValue.Create(isNotFound ? "404" : "error"));
                doc.Add("@error", error);
            }

            string? emailAddress = doc.GetPropertyStringValueAndRemove("UserEmail");
            string? userDescription = doc.GetPropertyStringValueAndRemove("UserDescription");
            if (!String.IsNullOrWhiteSpace(emailAddress) && !String.IsNullOrWhiteSpace(userDescription))
                doc.Add("@user_description", JsonSerializer.SerializeToNode(new UserDescription(emailAddress, userDescription)));

            string? identity = doc.GetPropertyStringValueAndRemove("UserName");
            if (!String.IsNullOrWhiteSpace(identity))
                doc.Add("@user", JsonSerializer.SerializeToNode(new UserInfo(identity)));

            doc.RemoveAllIfNullOrEmpty("Data", "GenericArguments", "Parameters");
        }
    }

    private void RenameAndValidateExtraExceptionProperties(string? id, JsonObject error)
    {
        var extendedData = error["Data"] as JsonObject;
        if (extendedData?["__ExceptionInfo"] is null)
            return;

        string? json = extendedData["__ExceptionInfo"]?.ToString();
        extendedData.Remove("__ExceptionInfo");

        if (String.IsNullOrWhiteSpace(json))
            return;

        if (json.Length > 200000)
        {
            _logger.LogError("__ExceptionInfo on {Id} is Too Big: {Length}", id, json.Length);
            return;
        }

        var ext = new JsonObject();
        try
        {
            var extraProperties = JsonNode.Parse(json) as JsonObject;
            if (extraProperties is not null)
            {
                foreach (var property in extraProperties.ToList())
                {
                    if (property.Value.IsNullOrEmpty())
                        continue;

                    string dataKey = property.Key;
                    if (extendedData[dataKey] is not null)
                        dataKey = "_" + dataKey;

                    // Need to detach the node before adding to another parent
                    extraProperties.Remove(property.Key);
                    ext.Add(dataKey, property.Value);
                }
            }
        }
        catch (Exception) { }

        if (ext.IsNullOrEmpty())
            return;

        extendedData.Add("@ext", ext);
    }
}
