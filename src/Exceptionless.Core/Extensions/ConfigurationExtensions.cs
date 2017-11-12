using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Extensions {
    public static class ConfigurationExtensions {
        public static List<string> GetValueList(this IConfiguration config, string key, string defaultValues = null, char[] separators = null) {
            var value = config.GetValue<string>(key);
            if (String.IsNullOrEmpty(value))
                return new List<string>();

            if (separators == null)
                separators = new[] { ',' };

            return value.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        }
    }
}