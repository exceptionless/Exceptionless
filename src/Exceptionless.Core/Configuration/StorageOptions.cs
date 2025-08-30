using Exceptionless.Core.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class StorageOptions
{
    public string? ConnectionString { get; internal set; }
    public string? Provider { get; internal set; }
    public Dictionary<string, string> Data { get; internal set; } = null!;

    public string Scope { get; internal set; } = null!;
    public string ScopePrefix { get; internal set; } = null!;

    public static StorageOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        var options = new StorageOptions { Scope = appOptions.AppScope };
        options.ScopePrefix = !String.IsNullOrEmpty(options.Scope) ? $"{options.Scope}-" : String.Empty;

        string? cs = config.GetConnectionString("Storage");
        if (cs != null)
        {
            options.Data = cs.ParseConnectionString();
            options.Provider = options.Data.GetString(nameof(options.Provider));
        }
        else
        {
            string? minioConnectionString = config.GetConnectionString("MinIO");
            if (!String.IsNullOrEmpty(minioConnectionString))
            {
                options.Provider = "minio";
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

        return options;
    }
}
