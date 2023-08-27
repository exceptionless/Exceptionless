using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stripe;

namespace Exceptionless.Core.Plugins.EventUpgrader;

public class EventUpgraderContext : ExtensibleObject
{
    public EventUpgraderContext(string json, Version? version = null, bool isMigration = false)
    {
        var jsonType = json.GetJsonType();
        if (jsonType == JsonType.Object)
        {
            var doc = JsonConvert.DeserializeObject<JObject>(json);
            if (doc is not null)
                Documents = new JArray(doc);
            else
                throw new ArgumentException("Invalid json object specified", nameof(json));
        }
        else if (jsonType == JsonType.Array)
        {
            var docs = JsonConvert.DeserializeObject<JArray>(json);
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

    public EventUpgraderContext(JObject doc, Version? version = null, bool isMigration = false)
    {
        Documents = new JArray(doc);
        Version = version;
        IsMigration = isMigration;
    }

    public EventUpgraderContext(JArray docs, Version? version = null, bool isMigration = false)
    {
        Documents = docs;
        Version = version;
        IsMigration = isMigration;
    }

    public JArray Documents { get; set; }
    public Version? Version { get; set; }
    public bool IsMigration { get; set; }
}
