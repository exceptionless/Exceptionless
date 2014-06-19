using System;
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Storage;

namespace Exceptionless.Configuration {
    public static class SettingsManager {
        public static void ApplySavedServerSettings(ExceptionlessConfiguration config) {
            string configPath = config.GetQueueName() + "\\server-settings.json";
            var fileStorage = config.Resolver.GetFileStorage();
            var serializer = config.Resolver.GetJsonSerializer();
            if (!fileStorage.Exists(configPath))
                return;

            try {
                var savedServerSettings = fileStorage.GetObject<SettingsDictionary>(configPath, serializer);
                config.Settings.Apply(savedServerSettings);
            } catch (Exception ex) {
                config.Resolver.GetLog().FormattedError(typeof(SettingsManager), ex, "Unable to read and apply saved server settings: {0}", ex.Message);
            }
        }

        public static void CheckVersion(int version, ExceptionlessConfiguration config) {
            string configPath = config.GetQueueName() + "\\server-settings.json";
            var fileStorage = config.Resolver.GetFileStorage();
            var serializer = config.Resolver.GetJsonSerializer();
            var client = config.Resolver.GetSubmissionClient();
            var persistedClientData = config.Resolver.Resolve<PersistedDictionary>();
            if (version <= persistedClientData.GetInt32("ServerConfigVersion", -1))
                return;

            var response = client.GetSettings(config, serializer);
            if (!response.Success)
                return;

            config.Settings.Apply(response.Settings);
            persistedClientData["ServerConfigVersion"] = response.SettingsVersion.ToString();
            fileStorage.SaveObject(configPath, response.Settings, serializer);
        }
    }
}
