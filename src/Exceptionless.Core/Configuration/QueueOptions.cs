﻿using Exceptionless.Core.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class QueueOptions
{
    public string ConnectionString { get; internal set; }
    public string Provider { get; internal set; }
    public Dictionary<string, string> Data { get; internal set; }

    public string Scope { get; internal set; }
    public string ScopePrefix { get; internal set; }

    public static QueueOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        var options = new QueueOptions();

        options.Scope = appOptions.AppScope;
        options.ScopePrefix = !String.IsNullOrEmpty(options.Scope) ? options.Scope + "-" : String.Empty;

        string cs = config.GetConnectionString("Queue");
        options.Data = cs.ParseConnectionString();
        options.Provider = options.Data.GetString(nameof(options.Provider));

        var providerConnectionString = !String.IsNullOrEmpty(options.Provider) ? config.GetConnectionString(options.Provider) : null;
        if (!String.IsNullOrEmpty(providerConnectionString))
            options.Data.AddRange(providerConnectionString.ParseConnectionString());

        options.ConnectionString = options.Data.BuildConnectionString(new HashSet<string> { nameof(options.Provider) });

        return options;
    }
}
