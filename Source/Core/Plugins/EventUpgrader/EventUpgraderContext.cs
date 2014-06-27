#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Utility;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Plugins.EventUpgrader {
    public class EventUpgraderContext : ExtensibleObject {
        public EventUpgraderContext(string json, Version version = null) {
            // TODO: Handle parsing errors.
            JObject document = JObject.Parse(json);
            Document = document;
            Version = version;
        }

        public JObject Document { get; set; }
        public Version Version { get; set; }
    }
}