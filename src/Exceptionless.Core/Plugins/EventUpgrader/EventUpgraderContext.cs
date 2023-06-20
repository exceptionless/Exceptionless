﻿using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventUpgrader;

public class EventUpgraderContext : ExtensibleObject
{
    public EventUpgraderContext(string json, Version version = null, bool isMigration = false)
    {
        var jsonType = json.GetJsonType();
        if (jsonType == JsonType.Object)
        {
            var doc = JsonConvert.DeserializeObject<JObject>(json);
            Documents = new JArray(doc);
        }
        else if (jsonType == JsonType.Array)
        {
            var docs = JsonConvert.DeserializeObject<JArray>(json);
            Documents = docs;
        }
        else
        {
            throw new ArgumentException("Invalid json data specified.", "");
        }

        Version = version;
        IsMigration = isMigration;
    }

    public EventUpgraderContext(JObject doc, Version version = null, bool isMigration = false)
    {
        Documents = new JArray(doc);
        Version = version;
        IsMigration = isMigration;
    }

    public EventUpgraderContext(JArray docs, Version version = null, bool isMigration = false)
    {
        Documents = docs;
        Version = version;
        IsMigration = isMigration;
    }

    public JArray Documents { get; set; }
    public Version Version { get; set; }
    public bool IsMigration { get; set; }
}
