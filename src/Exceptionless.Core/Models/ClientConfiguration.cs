using System;

namespace Exceptionless.Core.Models {
    public class ClientConfiguration {
        public ClientConfiguration() {
            Settings = new SettingsDictionary();
        }

        public int Version { get; set; }
        public SettingsDictionary Settings { get; private set; }

        public void IncrementVersion() {
            Version++;
        }
    }
}