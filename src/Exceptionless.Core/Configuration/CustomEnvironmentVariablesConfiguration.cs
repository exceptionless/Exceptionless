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

        IDictionaryEnumerator e = envVariables.GetEnumerator();
        try
        {
            while (e.MoveNext())
            {
                string key = (string)e.Entry.Key;
                string? value = (string?)e.Entry.Value;

                var normalizedKey = Normalize(key);
                // remove EX_ prefix
                if (normalizedKey.StartsWith("EX_"))
                    data[normalizedKey.Substring(3)] = value;
                else
                    data[normalizedKey] = value;
            }
        }
        finally
        {
            (e as IDisposable)?.Dispose();
        }

        Data = data;
    }

    private static string Normalize(string key) => key.Replace("__", ConfigurationPath.KeyDelimiter);
}
