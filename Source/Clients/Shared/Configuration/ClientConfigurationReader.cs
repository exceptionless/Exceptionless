#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Exceptionless.IO;
using Exceptionless.Json;
using Exceptionless.Logging;
using Exceptionless.Utility;
#if !PORTABLE40
using System.Configuration;

#endif

namespace Exceptionless.Configuration {
    internal static class ClientConfigurationReader {
        public static bool Read(ClientConfiguration configuration, ExceptionlessClient client) {
            try {
                // step 1: read attributes into settings
                ReadAttributes(configuration, client);

#if !SILVERLIGHT && !PORTABLE40
                // step 2: read app.config into settings
                ReadApplicationConfiguration(configuration, client);
#endif

                // step 3: load settings saved to disk
                ReadSavedConfiguration(configuration, client);

                // set default values
                ReadDefaults(configuration, client);

                // fix up paths with |DataDirectory|
                ExpandPaths(configuration);

                return true;
            } catch (Exception ex) {
                client.Log.FormattedError(ex, "Error reading configuration: {0}", ex.Message);
                return false;
            }
        }

        #region Attribute Methods

        private static void ReadAttributes(ClientConfiguration configuration, IExceptionlessLogAccessor logAccessor) {
            ReadAttribute(configuration, logAccessor);

            var settingAttributes = new List<ExceptionlessSettingAttribute>();

            // search all loaded assemblies
            GetSettingAttributes(settingAttributes);

            foreach (ExceptionlessSettingAttribute attribute in settingAttributes) {
                if (!String.IsNullOrEmpty(attribute.Name))
                    configuration[attribute.Name] = attribute.Value;
            }
        }

        private static void ReadAttribute(ClientConfiguration configuration, IExceptionlessLogAccessor logAccessor) {
            ExceptionlessAttribute exceptionlessAttribute = null;

            // first try entry
            Assembly assembly = AssemblyHelper.GetRootAssembly();
            if (assembly != null)
                exceptionlessAttribute = GetAttribute(assembly);

            // search all loaded assemblies
            if (exceptionlessAttribute == null) {
                foreach (Assembly a in AssemblyHelper.GetAssemblies()) {
                    assembly = a;
                    exceptionlessAttribute = GetAttribute(assembly);
                    if (exceptionlessAttribute != null)
                        break;
                }
            }

            if (exceptionlessAttribute == null)
                return;

            if (assembly != null)
                logAccessor.Log.FormattedInfo(typeof(ClientConfigurationReader), "Found exceptionless attribute in assembly '{0}'", assembly.FullName);

            if (exceptionlessAttribute.ApiKey != null)
                configuration.ApiKey = exceptionlessAttribute.ApiKey;
            if (exceptionlessAttribute.QueuePath != null)
                configuration.QueuePath = exceptionlessAttribute.QueuePath;
            if (exceptionlessAttribute.ServerUrl != null)
                configuration.ServerUrl = exceptionlessAttribute.ServerUrl;
            configuration.EnableSSL = exceptionlessAttribute.EnableSSL;
            configuration.EnableLogging = exceptionlessAttribute.EnableLogging;
            if (exceptionlessAttribute.LogPath != null)
                configuration.LogPath = exceptionlessAttribute.LogPath;
            configuration.Enabled = exceptionlessAttribute.Enabled;
        }

        private static void GetSettingAttributes(ICollection<ExceptionlessSettingAttribute> settingAttributes) {
            foreach (Assembly assembly in AssemblyHelper.GetAssemblies()) {
                if (null == assembly)
                    return;

                object[] attributes = assembly.GetCustomAttributes(typeof(ExceptionlessSettingAttribute), false);
                foreach (ExceptionlessSettingAttribute settingAttribute in attributes.OfType<ExceptionlessSettingAttribute>())
                    settingAttributes.Add(settingAttribute);
            }
        }

        private static ExceptionlessAttribute GetAttribute(Assembly assembly) {
            if (null == assembly)
                return null;

            object[] attributes = assembly.GetCustomAttributes(typeof(ExceptionlessAttribute), false);
            if (attributes.Length > 0)
                return attributes[0] as ExceptionlessAttribute;

            return null;
        }

        #endregion

#if !SILVERLIGHT && !PORTABLE40
        private static void ReadApplicationConfiguration(ClientConfiguration configuration, IExceptionlessLogAccessor logAccessor) {
            try {
                ExceptionlessSection exceptionlessSection = GetApplicationConfiguration(logAccessor);
                if (exceptionlessSection == null)
                    return;

                // only update if not null
                if (!String.IsNullOrEmpty(exceptionlessSection.ApiKey))
                    configuration.ApiKey = exceptionlessSection.ApiKey;

                // if an appsetting is present for apikey, then it will override the other api keys
                string apiKeyOverride = ConfigurationManager.AppSettings["Exceptionless:ApiKey"] ?? String.Empty;
                if (!String.IsNullOrEmpty(apiKeyOverride))
                    configuration.ApiKey = apiKeyOverride;

                if (!String.IsNullOrEmpty(exceptionlessSection.QueuePath))
                    configuration.QueuePath = exceptionlessSection.QueuePath;

                if (!String.IsNullOrEmpty(exceptionlessSection.ServerUrl))
                    configuration.ServerUrl = exceptionlessSection.ServerUrl;

                configuration.Enabled = exceptionlessSection.Enabled;

                if (exceptionlessSection.EnableSSL.HasValue)
                    configuration.EnableSSL = exceptionlessSection.EnableSSL.Value;

                if (exceptionlessSection.EnableLogging.HasValue)
                    configuration.EnableLogging = exceptionlessSection.EnableLogging.Value;

                if (!String.IsNullOrEmpty(exceptionlessSection.LogPath))
                    configuration.LogPath = exceptionlessSection.LogPath;

                // if a log path is specified and enable logging setting isn't specified, then enable logging.
                if (!String.IsNullOrEmpty(exceptionlessSection.LogPath) && !exceptionlessSection.EnableLogging.HasValue)
                    configuration.EnableLogging = true;

                if (exceptionlessSection.Settings == null)
                    return;

                foreach (NameValueConfigurationElement setting in exceptionlessSection.Settings) {
                    if (!String.IsNullOrEmpty(setting.Name))
                        configuration[setting.Name] = setting.Value;
                }
            } catch (ConfigurationErrorsException ex) {
                logAccessor.Log.FormattedError(typeof(ClientConfigurationReader), ex, "Error getting ExceptionlessSection: {0}", ex.Message);
            }
        }

        private static ExceptionlessSection _configSection;
        private static bool _configRead = false;

        public static ExceptionlessSection GetApplicationConfiguration(IExceptionlessLogAccessor logAccessor) {
            if (!_configRead) {
                try {
                    _configRead = true;
                    _configSection = ConfigurationManager.GetSection("exceptionless") as ExceptionlessSection;
                } catch (Exception ex) {
                    logAccessor.Log.FormattedError(typeof(ClientConfigurationReader), ex, "Error getting ExceptionlessSection: {0}", ex.Message);
                }
            }

            return _configSection;
        }
#endif

        private static void ReadSavedConfiguration(ClientConfiguration configuration, IExceptionlessLogAccessor logAccessor) {
            try {
                for (int retry = 0; retry < 2; retry++) {
                    using (var dir = new IsolatedStorageDirectory(configuration.StoreId)) {
                        try {
                            if (!dir.FileExists(ClientConfiguration.CachedServerConfigFile))
                                return;

                            var savedConfig = dir.ReadFile<Dictionary<string, string>>(ClientConfiguration.CachedServerConfigFile);
                            if (savedConfig == null)
                                return;

                            foreach (string key in savedConfig.Keys)
                                configuration[key] = savedConfig[key];

                            return;
                        } catch (JsonReaderException) {
                            // try deleting the invalid config file so we don't keep trying to read it.
                            try {
                                dir.DeleteFile(ClientConfiguration.CachedServerConfigFile);
                            } catch {}
                        } catch (Exception ex) {
                            // File is being used by another process or thread or the file does not exist.
                            logAccessor.Log.FormattedError(typeof(ClientConfigurationReader), ex, "Error while reading settings from the configuration file: {0}", ex.Message);
                            Thread.Sleep(50);
                        }
                    } // storage
                } // retry
            } catch (Exception ex) {
                logAccessor.Log.FormattedError(typeof(ClientConfigurationReader), ex, "Error while reading settings from the configuration file: {0}", ex.Message);
            }
        }

        private static void ExpandPaths(ClientConfiguration configuration) {
#if !SILVERLIGHT
            if (!String.IsNullOrEmpty(configuration.QueuePath))
                configuration.QueuePath = PathHelper.ExpandPath(configuration.QueuePath);
#endif
        }

        private static void ReadDefaults(ClientConfiguration configuration, ExceptionlessClient client) {
            if (String.IsNullOrEmpty(configuration.ServerUrl))
                configuration.ServerUrl = "http://collector.exceptionless.com";

            if (!configuration.EnableLogging)
                return;

            if (!String.IsNullOrEmpty(configuration.LogPath)) {
                string directoryName = Path.GetDirectoryName(configuration.LogPath);
                if (directoryName != null && !Directory.Exists(directoryName))
                    Directory.CreateDirectory(directoryName);

                client.Log = new FileExceptionlessLog(configuration.LogPath);
            } else {
                const string logFile = "exceptionless.log";
                client.Log = new IsolatedStorageFileExceptionlessLog(configuration.StoreId, logFile);
                configuration.LogPath = logFile;
            }
        }
    }
}