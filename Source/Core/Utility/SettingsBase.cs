using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using Exceptionless.Core.Extensions;
using Newtonsoft.Json;

namespace Exceptionless.Core {
    public abstract class SettingsBase<T> : SingletonBase<T>, IInitializable where T : class {
        public abstract void Initialize();

        protected static bool GetBool(string name, bool defaultValue = false) {
            string value = GetEnvironmentVariable(name) ?? GetConfigVariable(name);
            if (String.IsNullOrEmpty(value))
                return ConfigurationManager.AppSettings.GetBool(name, defaultValue);

            bool boolean;
            return Boolean.TryParse(value, out boolean) ? boolean : defaultValue;
        }

        protected static string GetConnectionString(string name, string defaultValue = null) {
            string value = GetEnvironmentVariable(name) ?? GetConfigVariable(name);
            if (!String.IsNullOrEmpty(value))
                return value;

            var connectionString = ConfigurationManager.ConnectionStrings[name];
            return connectionString != null ? connectionString.ConnectionString : defaultValue;
        }

        protected static TEnum GetEnum<TEnum>(string name, TEnum? defaultValue = null) where TEnum : struct {
            string value = GetEnvironmentVariable(name) ?? GetConfigVariable(name);
            if (String.IsNullOrEmpty(value))
                return ConfigurationManager.AppSettings.GetEnum(name, defaultValue);

            try {
                return (TEnum)Enum.Parse(typeof(TEnum), value, true);
            } catch (ArgumentException ex) {
                if (defaultValue is TEnum)
                    return (TEnum)defaultValue;

                string message = $"Configuration key '{name}' has value '{value}' that could not be parsed as a member of the {typeof(TEnum).Name} enum type.";
                throw new ConfigurationErrorsException(message, ex);
            }
        }

        protected static int GetInt(string name, int defaultValue = 0) {
            string value = GetEnvironmentVariable(name) ?? GetConfigVariable(name);
            if (String.IsNullOrEmpty(value))
                return ConfigurationManager.AppSettings.GetInt(name, defaultValue);

            int number;
            return Int32.TryParse(value, out number) ? number : defaultValue;
        }

        protected static string GetString(string name, string defaultValue = null) {
            return GetEnvironmentVariable(name) ?? GetConfigVariable(name) ?? ConfigurationManager.AppSettings[name] ?? defaultValue;
        }

        protected static List<string> GetStringList(string name, string defaultValues = null, char[] separators = null) {
            string value = GetEnvironmentVariable(name) ?? GetConfigVariable(name);
            if (String.IsNullOrEmpty(value))
                return ConfigurationManager.AppSettings.GetStringList(name, defaultValues, separators);

            if (separators == null)
                separators = new[] { ',' };

            return value.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        }

        private static Dictionary<string, string> _configVariables;
        protected static string GetConfigVariable(string name) {
            if (String.IsNullOrEmpty(name))
                return null;

            if (_configVariables == null) {
                try {
                    string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "config.json");
                    // check to see if environment specific config exists and use that instead
                    if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\", "config.json")))
                        configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\", "config.json");

                    if (!File.Exists(configPath)) {
                        _configVariables = new Dictionary<string, string>();
                        return null;
                    }

                    _configVariables = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(configPath));
                } catch (Exception) {
                    _configVariables = new Dictionary<string, string>();
                    return null;
                }
            }

            return _configVariables.ContainsKey(name) ? _configVariables[name] : null;
        }

        protected static string EnvironmentVariablePrefix { get; set; }

        private static Dictionary<string, string> _environmentVariables;
        private static string GetEnvironmentVariable(string name) {
            if (String.IsNullOrEmpty(name))
                return null;

            if (_environmentVariables == null) {
                try {
                    _environmentVariables = Environment.GetEnvironmentVariables().OfType<DictionaryEntry>().ToDictionary(e => e.Key.ToString(), e => e.Value.ToString());
                } catch (Exception) {
                    _environmentVariables = new Dictionary<string, string>();
                    return null;
                }
            }

            if (!_environmentVariables.ContainsKey(EnvironmentVariablePrefix + name))
                return null;

            return _environmentVariables[EnvironmentVariablePrefix + name];
        }
    }
}
