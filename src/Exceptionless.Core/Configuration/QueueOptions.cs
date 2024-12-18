﻿using Exceptionless.Core.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class QueueOptions
{
    public string? ConnectionString { get; internal set; }
    public string? Provider { get; internal set; }
    public Dictionary<string, string> Data { get; internal set; } = null!;

    public string Scope { get; internal set; } = null!;
    public string ScopePrefix { get; internal set; } = null!;
    public bool MetricsPollingEnabled { get; set; } = true;
    public TimeSpan MetricsPollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    public static QueueOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        var options = new QueueOptions { Scope = appOptions.AppScope };
        options.ScopePrefix = !String.IsNullOrEmpty(options.Scope) ? $"{options.Scope}-" : String.Empty;

        string? cs = config.GetConnectionString("Queue");
        if (cs != null)
        {
            options.Data = cs.ParseConnectionString();
            options.Provider = options.Data.GetString(nameof(options.Provider));
        }
        else
        {
            var redisConnectionString = config.GetConnectionString("Redis");
            if (!String.IsNullOrEmpty(redisConnectionString))
            {
                options.Provider = "redis";
            }
        }

        string? providerConnectionString = !String.IsNullOrEmpty(options.Provider) ? config.GetConnectionString(options.Provider) : null;
        if (!String.IsNullOrEmpty(providerConnectionString))
        {
            var providerOptions = providerConnectionString.ParseConnectionString(defaultKey: "server");
            options.Data ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            options.Data.AddRange(providerOptions);
        }

        options.ConnectionString = options.Data.BuildConnectionString(new HashSet<string> { nameof(options.Provider) });

        options.MetricsPollingInterval = appOptions.AppMode == AppMode.Development ? TimeSpan.FromSeconds(15) : TimeSpan.FromSeconds(5);

        return options;
    }
}
