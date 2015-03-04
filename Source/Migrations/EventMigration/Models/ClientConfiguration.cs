using System;

namespace Exceptionless.EventMigration.Models {
    public class ClientConfiguration {
        public ClientConfiguration() {
            Settings = new ConfigurationDictionary();
        }

        public int Version { get; set; }
        public ConfigurationDictionary Settings { get; set; }
    }
}