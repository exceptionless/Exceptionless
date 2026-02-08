using Exceptionless.Core.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public class StorageOptions
{
    public string? ConnectionString { get; internal set; }
    public string? Provider { get; internal set; }
    public Dictionary<string, string> Data { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);

    public string Scope { get; internal set; } = null!;
    public string ScopePrefix { get; internal set; } = null!;

    public static StorageOptions ReadFromConfiguration(IConfiguration config, AppOptions appOptions)
    {
        var options = new StorageOptions
        {
            Scope = appOptions.AppScope,
            ScopePrefix = !String.IsNullOrEmpty(appOptions.AppScope) ? $"{appOptions.AppScope}-" : String.Empty
        };

        string? cs = config.GetConnectionString("Storage");
        if (!String.IsNullOrWhiteSpace(cs))
        {
            options.Data = cs.ParseConnectionString();
            options.Provider = options.Data.GetString(nameof(options.Provider));
        }
        else
        {
            string? azureStorageConnectionString = config.GetConnectionString("AzureStorage");
            if (!String.IsNullOrEmpty(azureStorageConnectionString))
            {
                options.Provider = "azurestorage";
                options.ConnectionString = azureStorageConnectionString;
                options.Data = azureStorageConnectionString.ParseConnectionString();
                return options;
            }
        }

        string? providerConnectionString = !String.IsNullOrEmpty(options.Provider) ? config.GetConnectionString(options.Provider) : null;
        if (!String.IsNullOrEmpty(providerConnectionString))
            options.Data.AddRange(providerConnectionString.ParseConnectionString());

        options.ConnectionString = options.Data.BuildConnectionString(new HashSet<string> { nameof(options.Provider) });
        return options;
    }
}
