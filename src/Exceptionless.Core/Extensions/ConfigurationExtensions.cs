using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Extensions {
    public static class ConfigurationExtensions {
        public static List<string> GetValueList(this IConfiguration config, string key, string defaultValues = null, char[] separators = null) {
            string value = config.GetValue<string>(key);
            if (String.IsNullOrEmpty(value))
                return new List<string>();

            if (separators == null)
                separators = new[] { ',' };

            return value.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        }

        public static Dictionary<string, object> ToDictionary(this IConfiguration section) {
            var dict = new Dictionary<string, object>();
            foreach (var value in section.GetChildren()) {
                if (String.IsNullOrEmpty(value.Key))
                    continue;
                
                if (value.Value != null)
                    dict.Add(value.Key, value.Value);
                
                var subDict = ToDictionary(value);
                if (subDict.Count > 0)
                    dict.Add(value.Key, subDict);
            }

            return dict;
        }
    }
}