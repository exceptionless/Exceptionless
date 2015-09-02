using System;
using System.Collections.Generic;

namespace Exceptionless.EventMigration.Models {
    public class ConfigurationDictionary : Dictionary<string, string> {
        public ConfigurationDictionary() : base(StringComparer.OrdinalIgnoreCase) {}
    }
}