using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Exceptionless.Core.Extensions {
    public static class ConfigurationExtensions {
        public static IServiceCollection AddAppOptions(this IServiceCollection services, AppOptions appOptions) {
            services.AddSingleton(appOptions);
            services.AddSingleton(appOptions.CacheOptions);
            services.AddSingleton(appOptions.MessageBusOptions);
            services.AddSingleton(appOptions.MetricOptions);
            services.AddSingleton(appOptions.QueueOptions);
            services.AddSingleton(appOptions.StorageOptions);
            services.AddSingleton(appOptions.EmailOptions);
            services.AddSingleton(appOptions.ElasticsearchOptions);
            services.AddSingleton(appOptions.IntercomOptions);
            services.AddSingleton(appOptions.SlackOptions);
            services.AddSingleton(appOptions.StripeOptions);
            services.AddSingleton(appOptions.AuthOptions);

            return services;
        }

        public static string ToScope(this AppMode mode) {
            switch (mode) {
                case AppMode.Development:
                    return "dev";
                case AppMode.Staging:
                    return "stage";
                case AppMode.Production:
                    return "prod";
            }

            return String.Empty;
        }
        
        public static List<string> GetValueList(this IConfiguration config, string key, char[] separators = null) {
            string value = config.GetValue<string>(key);
            if (String.IsNullOrEmpty(value))
                return new List<string>();

            if (separators == null)
                separators = new[] { ',' };

            return value.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        }

        public static Dictionary<string, object> ToDictionary(this IConfiguration section, params string[] sectionsToSkip) {
            if (sectionsToSkip == null)
                sectionsToSkip = new string[0];

            var dict = new Dictionary<string, object>();
            foreach (var value in section.GetChildren()) {
                // kubernetes service variables
                if (value.Key.StartsWith("DEV_", StringComparison.Ordinal))
                    continue;
                
                if (String.IsNullOrEmpty(value.Key) || sectionsToSkip.Contains(value.Key, StringComparer.OrdinalIgnoreCase))
                    continue;
                
                if (value.Value != null)
                    dict[value.Key] = value.Value;
                
                var subDict = ToDictionary(value);
                if (subDict.Count > 0)
                    dict[value.Key] = subDict;
            }

            return dict;
        }
    }
}