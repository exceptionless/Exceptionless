using System;
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Storage;

namespace Exceptionless.Configuration {
    public static class SettingsManager {
        public static void ApplySavedServerSettings(ExceptionlessConfiguration config) {
            string configPath = GetConfigPath(config);
            var fileStorage = config.Resolver.GetFileStorage();
            if (!fileStorage.Exists(configPath))
                return;

            try {
                var serializer = config.Resolver.GetJsonSerializer();
                var savedServerSettings = fileStorage.GetObject<SettingsDictionary>(configPath, serializer);
                config.Settings.Apply(savedServerSettings);
            } catch (Exception ex) {
                config.Resolver.GetLog().FormattedError(typeof(SettingsManager), ex, "Unable to read and apply saved server settings: {0}", ex.Message);
            }
        }

        public static void CheckVersion(int version, ExceptionlessConfiguration config) {
            var persistedClientData = config.Resolver.Resolve<PersistedDictionary>();
            if (version <= persistedClientData.GetInt32("ServerConfigVersion", -1))
                return;

            UpdateSettings(config);
        }

        public static void UpdateSettings(ExceptionlessConfiguration config) {
            var serializer = config.Resolver.GetJsonSerializer();
            var client = config.Resolver.GetSubmissionClient();

            var response = client.GetSettings(config, serializer);
            if (!response.Success)
                return;

            config.Settings.Apply(response.Settings);

            var persistedClientData = config.Resolver.Resolve<PersistedDictionary>();
            persistedClientData["ServerConfigVersion"] = response.SettingsVersion.ToString();

            var fileStorage = config.Resolver.GetFileStorage();
            fileStorage.SaveObject(GetConfigPath(config), response.Settings, serializer);
        }

        private static string GetConfigPath(ExceptionlessConfiguration config) {
            return config.GetQueueName() + "\\server-settings.json";
        }
    }
}
