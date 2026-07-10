using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Microsoft.Extensions.Configuration;

namespace Exceptionless.Core.Configuration;

internal sealed record ProviderConfiguration(
    string? Provider,
    string? ConnectionString,
    Dictionary<string, string?> Data);

internal static class ProviderConfigurationResolver
{
    private const string ProviderKey = "provider";
    private const string ServerKey = "server";

    public static ProviderConfiguration Resolve(
        IConfiguration configuration,
        string connectionStringName,
        string? providerConnectionStringDefaultKey = null)
    {
        string? selector = configuration.GetConnectionString(connectionStringName);
        if (selector is null)
            return new ProviderConfiguration(null, null, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));

        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        string? inlineConnectionString = null;

        try
        {
            data.AddRange(selector.ParseConnectionString());
        }
        catch (ArgumentException ex)
        {
            if (!TryParseInlineUri(selector, data, out inlineConnectionString))
                throw CreateInvalidConfigurationException(connectionStringName, ex);
        }

        string? provider = data.GetString(ProviderKey);
        if (String.IsNullOrWhiteSpace(provider))
        {
            if (data.Count == 0)
                return new ProviderConfiguration(null, null, data);

            throw CreateInvalidConfigurationException(connectionStringName);
        }

        provider = provider.Trim().ToLowerInvariant();
        data[ProviderKey] = provider;

        string? providerConnectionString = configuration.GetConnectionString(provider);
        if (providerConnectionString is null)
        {
            string? connectionString = inlineConnectionString ?? data.BuildConnectionString([ProviderKey]);
            return new ProviderConfiguration(provider, connectionString, data);
        }

        if (TryGetAbsoluteUri(providerConnectionString, out string? rawConnectionString))
        {
            data[ServerKey] = rawConnectionString;
            return new ProviderConfiguration(provider, rawConnectionString, data);
        }

        try
        {
            data.AddRange(providerConnectionString.ParseConnectionString(defaultKey: providerConnectionStringDefaultKey));
        }
        catch (ArgumentException ex)
        {
            throw CreateInvalidConfigurationException(provider, ex);
        }

        return new ProviderConfiguration(provider, data.BuildConnectionString([ProviderKey]), data);
    }

    private static bool TryParseInlineUri(
        string selector,
        Dictionary<string, string?> data,
        out string? connectionString)
    {
        connectionString = null;

        int separatorIndex = selector.IndexOf(';');
        if (separatorIndex < 0)
            return false;

        string providerSelector = selector[..separatorIndex];
        Dictionary<string, string?> providerData;
        try
        {
            providerData = providerSelector.ParseConnectionString();
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (providerData.Count != 1 || String.IsNullOrWhiteSpace(providerData.GetString(ProviderKey)))
            return false;

        if (!TryGetAbsoluteUri(selector[(separatorIndex + 1)..], out connectionString))
            return false;

        data.AddRange(providerData);
        data[ServerKey] = connectionString;
        return true;
    }

    private static bool TryGetAbsoluteUri(string value, out string? connectionString)
    {
        connectionString = TrimMatchingQuotes(value.Trim());
        return Uri.TryCreate(connectionString, UriKind.Absolute, out Uri? uri) && !uri.IsFile;
    }

    private static string TrimMatchingQuotes(string value)
    {
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1];

        return value;
    }

    private static InvalidOperationException CreateInvalidConfigurationException(string connectionStringName, Exception? innerException = null)
    {
        return new InvalidOperationException(
            $"ConnectionStrings:{connectionStringName} must specify a provider and use either key/value settings or an absolute provider URI.",
            innerException);
    }
}
