using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core {
    public abstract class SettingsBase<T>: SingletonBase<T>, IInitializable where T: class {
        private static Dictionary<string, string> _environmentVariables;

        protected static bool GetBool(string name, bool defaultValue = false) {
            string value = GetEnvironmentalVariable(name);
            if (String.IsNullOrEmpty(value))
                return ConfigurationManager.AppSettings.GetBool(name, defaultValue);

            bool boolean;
            return Boolean.TryParse(value, out boolean) ? boolean : defaultValue;
        }

        protected static string GetConnectionString(string name) {
            string value = GetEnvironmentalVariable(name);
            if (!String.IsNullOrEmpty(value))
                return value;

            var connectionString = ConfigurationManager.ConnectionStrings[name];
            return connectionString != null ? connectionString.ConnectionString : null;
        }

        protected static TEnum GetEnum<TEnum>(string name, TEnum? defaultValue = null) where TEnum : struct {
            string value = GetEnvironmentalVariable(name);
            if (String.IsNullOrEmpty(value))
                return ConfigurationManager.AppSettings.GetEnum(name, defaultValue);

            try {
                return (TEnum)Enum.Parse(typeof(TEnum), value, true);
            } catch (ArgumentException ex) {
                if (defaultValue is TEnum)
                    return (TEnum)defaultValue;

                string message = String.Format("Configuration key '{0}' has value '{1}' that could not be parsed as a member of the {2} enum type.", name, value, typeof(TEnum).Name);
                throw new ConfigurationErrorsException(message, ex);
            }
        }

        protected static int GetInt(string name, int defaultValue = 0) {
            string value = GetEnvironmentalVariable(name);
            if (String.IsNullOrEmpty(value))
                return ConfigurationManager.AppSettings.GetInt(name, defaultValue);

            int number;
            return Int32.TryParse(value, out number) ? number : defaultValue;
        }

        protected static string GetString(string name) {
            return GetEnvironmentalVariable(name) ?? ConfigurationManager.AppSettings[name];
        }

        protected static List<string> GetStringList(string name, string defaultValues = null, char[] separators = null) {
            string value = GetEnvironmentalVariable(name);
            if (String.IsNullOrEmpty(value))
                return ConfigurationManager.AppSettings.GetStringList(name, defaultValues, separators);

            if (separators == null)
                separators = new[] { ',' };

            return value.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        }

        private static string GetEnvironmentalVariable(string name) {
            if (String.IsNullOrEmpty(name))
                return null;
            
            if (_environmentVariables == null) {
                try {
                    _environmentVariables = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().ToDictionary(e => e.Key.ToString(), e => e.Value.ToString());
                } catch (Exception ex) {
                    _environmentVariables = new Dictionary<string, string>();

                    NLog.Fluent.Log.Error().Exception(ex).Message("An Error occurred while reading environmental variables.").Write();
                    return null;
                }
            }
            
            if (!_environmentVariables.ContainsKey(EnvironmentVariablePrefix + name))
                return null;

            return _environmentVariables[EnvironmentVariablePrefix + name];
        }

        protected static string EnvironmentVariablePrefix { get; set; }

        public abstract void Initialize();
    }
}