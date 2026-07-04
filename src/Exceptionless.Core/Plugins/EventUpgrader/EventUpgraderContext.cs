using System.Text.Json.Nodes;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;

namespace Exceptionless.Core.Plugins.EventUpgrader;

public class EventUpgraderContext : ExtensibleObject
{
    public EventUpgraderContext(string json, Version? version = null, bool isMigration = false)
    {
        var jsonType = json.GetJsonType();
        if (jsonType == JsonType.Object)
        {
            var doc = JsonNode.Parse(json) as JsonObject;
            if (doc is not null)
                Documents = new JsonArray(doc);
            else
                throw new ArgumentException("Invalid json object specified", nameof(json));
        }
        else if (jsonType == JsonType.Array)
        {
            var docs = JsonNode.Parse(json) as JsonArray;
            if (docs is not null)
                Documents = docs;
            else
                throw new ArgumentException("Invalid json array specified", nameof(json));
        }
        else
        {
            throw new ArgumentException("Invalid json data specified", nameof(json));
        }

        Version = version;
        IsMigration = isMigration;
    }

    public EventUpgraderContext(JsonObject doc, Version? version = null, bool isMigration = false)
    {
        Documents = new JsonArray(doc);
        Version = version;
        IsMigration = isMigration;
    }

    public EventUpgraderContext(JsonArray docs, Version? version = null, bool isMigration = false)
    {
        Documents = docs;
        Version = version;
        IsMigration = isMigration;
    }

    public JsonArray Documents { get; set; }
    public Version? Version { get; set; }
    public bool IsMigration { get; set; }
}
