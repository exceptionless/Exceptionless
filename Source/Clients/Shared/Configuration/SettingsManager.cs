using System;
using Exceptionless.Dependency;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Storage;
using Exceptionless.Submission;

namespace Exceptionless.Configuration {
    public class SettingsManager {
        private readonly ExceptionlessConfiguration _config;
        private readonly ISubmissionClient _client;
        private readonly IFileStorage _fileStorage;
        private readonly PersistedDictionary _persistedClientData;
        private readonly IJsonSerializer _serializer;
        private readonly string _configPath;

        public SettingsManager(ExceptionlessConfiguration config, ISubmissionClient client, IFileStorage fileStorage, PersistedDictionary persistedClientData, IJsonSerializer serializer) {
            _config = config;
            _client = client;
            _fileStorage = fileStorage;
            _persistedClientData = persistedClientData;
            _serializer = serializer;
            _configPath = _config.GetQueueName() + "\\server-settings.json";
        }

        public void Init() {
            if (!_fileStorage.Exists(_configPath))
                return;

            try {
                var savedServerSettings = _fileStorage.GetObject<SettingsDictionary>(_configPath, _serializer);
                _config.Settings.Apply(savedServerSettings);
            } catch (Exception ex) {
                _config.Resolver.GetLog().FormattedError(typeof(SettingsManager), ex, "Unable to read and apply saved server settings: {0}", ex.Message);
            }
        }

        public void CheckVersion(int version) {
            if (version <= _persistedClientData.GetInt32("ServerConfigVersion", -1))
                return;

            var response = _client.GetSettings(_config, _serializer);
            if (!response.Success)
                return;
            
            _config.Settings.Apply(response.Settings);
            _persistedClientData["ServerConfigVersion"] = response.SettingsVersion.ToString();
            _fileStorage.SaveObject(_configPath, response.Settings, _serializer);
        }
    }
}
