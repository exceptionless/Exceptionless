using System.Collections;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

public static class CustomEnvironmentVariablesExtensions
{
    public static IConfigurationBuilder AddCustomEnvironmentVariables(this IConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Add(new CustomEnvironmentVariablesConfigurationSource());
        return configurationBuilder;
    }
}

public class CustomEnvironmentVariablesConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new CustomEnvironmentVariablesConfigurationProvider();
    }
}

public class CustomEnvironmentVariablesConfigurationProvider : ConfigurationProvider
{
    public override void Load() => Load(Environment.GetEnvironmentVariables());

    internal void Load(IDictionary envVariables)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var variables = envVariables.Cast<DictionaryEntry>()
            .Select(entry => new KeyValuePair<string, string?>((string)entry.Key, (string?)entry.Value))
            .ToList();

        // Aspire-style variables are the base. Product-specific EX_ variables are
        // applied second so their precedence never depends on dictionary iteration order.
        AddVariables(data, variables, prefixed: false);
        AddVariables(data, variables, prefixed: true);

        Data = data;
    }

    private static void AddVariables(
        IDictionary<string, string?> data,
        IEnumerable<KeyValuePair<string, string?>> variables,
        bool prefixed)
    {
        foreach ((string key, string? value) in variables)
        {
            string normalizedKey = Normalize(key);
            bool hasPrefix = normalizedKey.StartsWith("EX_", StringComparison.OrdinalIgnoreCase);
            if (hasPrefix != prefixed)
                continue;

            data[hasPrefix ? normalizedKey[3..] : normalizedKey] = value;
        }
    }

    private static string Normalize(string key) => key.Replace("__", ConfigurationPath.KeyDelimiter);
}
