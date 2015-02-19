using System;
using System.Linq;
using Exceptionless.Dependency;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Storage;

namespace Exceptionless.Configuration {
    public static class SettingsManager {
        public static void ApplySavedServerSettings(ExceptionlessConfiguration config) {
            if (String.IsNullOrEmpty(config.ApiKey) || String.Equals(config.ApiKey, "API_KEY_HERE", StringComparison.OrdinalIgnoreCase)) {
                config.Resolver.GetLog().Error(typeof(SettingsManager), "Unable to apply saved server settings: ApiKey is not set.");
                return;
            }

            var savedServerSettings = GetSavedServerSettings(config);
            config.Settings.Apply(savedServerSettings);
        }

        private static SettingsDictionary GetSavedServerSettings(ExceptionlessConfiguration config) {
            string configPath = GetConfigPath(config);
            if (String.IsNullOrEmpty(configPath))
                return new SettingsDictionary();

            var fileStorage = config.Resolver.GetFileStorage();
            if (!fileStorage.Exists(configPath))
                return new SettingsDictionary();

            try {
                config.Resolver.GetJsonSerializer();
                return fileStorage.GetObject<SettingsDictionary>(configPath);
            } catch (Exception ex) {
                config.Resolver.GetLog().FormattedError(typeof(SettingsManager), ex, "Unable to read and apply saved server settings: {0}", ex.Message);
            }

            return new SettingsDictionary();
        }

        public static void CheckVersion(int version, ExceptionlessConfiguration config) {
            if (String.IsNullOrEmpty(config.ApiKey) || String.Equals(config.ApiKey, "API_KEY_HERE", StringComparison.OrdinalIgnoreCase)) {
                config.Resolver.GetLog().Error(typeof(SettingsManager), "Unable to check version: ApiKey is not set.");
                return;
            }

            var persistedClientData = config.Resolver.Resolve<PersistedDictionary>();
            if (version <= persistedClientData.GetInt32(String.Concat(config.GetQueueName(), "-ServerConfigVersion"), -1))
                return;

            UpdateSettings(config);
        }

        public static void UpdateSettings(ExceptionlessConfiguration config) {
            if (String.IsNullOrEmpty(config.ApiKey) || String.Equals(config.ApiKey, "API_KEY_HERE", StringComparison.OrdinalIgnoreCase)) {
                config.Resolver.GetLog().Error(typeof(SettingsManager), "Unable to update settings: ApiKey is not set.");
                return;
            }

            var serializer = config.Resolver.GetJsonSerializer();
            var client = config.Resolver.GetSubmissionClient();

            var response = client.GetSettings(config, serializer);
            if (!response.Success || response.Settings == null)
                return;

            var savedServerSettings = GetSavedServerSettings(config);
            config.Settings.Apply(response.Settings);

            // TODO: Store snapshot of settings after reading from config and attributes and use that to revert to defaults.
            // Remove any existing server settings that are not in the new server settings.
            foreach (string key in savedServerSettings.Keys.Except(response.Settings.Keys)) {
                if (config.Settings.ContainsKey(key))
                    config.Settings.Remove(key);
            }

            var persistedClientData = config.Resolver.Resolve<PersistedDictionary>();
            persistedClientData[String.Concat(config.GetQueueName(), "-ServerConfigVersion")] = response.SettingsVersion.ToString();

            var fileStorage = config.Resolver.GetFileStorage();
            fileStorage.SaveObject(GetConfigPath(config), response.Settings);
        }

        private static string GetConfigPath(ExceptionlessConfiguration config) {
            return config.GetQueueName() + "\\server-settings.json";
        }
    }
}
