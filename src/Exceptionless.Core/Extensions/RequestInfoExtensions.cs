using System.Text;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;

namespace Exceptionless.Core.Extensions;

public static class RequestInfoExtensions
{
    public const int MaximumDataValueLength = 1000;

    public static readonly IReadOnlyList<string> DefaultDataExclusions =
    [
        "*VIEWSTATE*",
        "*EVENTVALIDATION*",
        "*ASPX*",
        "__RequestVerificationToken",
        "ASP.NET_SessionId",
        "__LastErrorId",
        "WAWebSiteID",
        "ARRAffinity"
    ];

    public static RequestInfo ApplyDataExclusions(this RequestInfo request, ITextSerializer serializer, IList<string> exclusions, int maxLength = 1000)
    {
        request.Cookies = ApplyExclusions(request.Cookies, exclusions, maxLength);
        request.QueryString = ApplyExclusions(request.QueryString, exclusions, maxLength);
        request.PostData = ApplyPostDataExclusions(request.PostData, serializer, exclusions, maxLength);

        return request;
    }

    /// <summary>
    /// Applies request exclusions to already materialized JSON-compatible post data. Unlike the
    /// legacy string overload, this handles nested V3 object and array values before persistence.
    /// </summary>
    public static RequestInfo ApplyDataExclusions(this RequestInfo request, IList<string> exclusions, int maxLength = 1000)
    {
        request.Cookies = ApplyExclusions(request.Cookies, exclusions, maxLength);
        request.QueryString = ApplyExclusions(request.QueryString, exclusions, maxLength);
        request.PostData = ApplyObjectExclusions(request.PostData, exclusions, maxLength);
        return request;
    }

    private static object? ApplyPostDataExclusions(object? data, ITextSerializer serializer, IEnumerable<string> exclusions, int maxLength)
    {
        if (data is null)
        {
            return null;
        }

        var dictionary = data as Dictionary<string, string>;
        if (dictionary is null && data is string json)
        {
            if (!json.IsJson())
            {
                return data;
            }

            try
            {
                dictionary = serializer.Deserialize<Dictionary<string, string>>(json);
            }
            catch (Exception) { }
        }

        return dictionary is not null ? ApplyExclusions(dictionary, exclusions, maxLength) : data;
    }

    private static object? ApplyObjectExclusions(object? value, IList<string> exclusions, int maxLength)
    {
        if (value is IDictionary<string, object?> objectDictionary)
        {
            foreach (string key in objectDictionary.Keys.ToArray())
            {
                if (String.IsNullOrEmpty(key) || key.AnyWildcardMatches(exclusions, true))
                {
                    objectDictionary.Remove(key);
                    continue;
                }

                objectDictionary[key] = ApplyObjectExclusions(objectDictionary[key], exclusions, maxLength);
            }

            return value;
        }

        if (value is IDictionary<string, string> stringDictionary)
        {
            return ApplyExclusions(new Dictionary<string, string>(stringDictionary), exclusions, maxLength);
        }

        if (value is IList<object?> values)
        {
            for (int index = 0; index < values.Count; index++)
            {
                values[index] = ApplyObjectExclusions(values[index], exclusions, maxLength);
            }

            return value;
        }

        return value is string text && text.Length > maxLength
            ? "Value is too large to be included."
            : value;
    }

    private static Dictionary<string, string>? ApplyExclusions(Dictionary<string, string>? dictionary, IEnumerable<string> exclusions, int maxLength)
    {
        if (dictionary is null || dictionary.Count == 0)
        {
            return dictionary;
        }

        foreach (string key in dictionary.Keys.Where(k => String.IsNullOrEmpty(k) || StringExtensions.AnyWildcardMatches(k, exclusions, true)).ToList())
        {
            dictionary.Remove(key);
        }

        foreach (string key in dictionary.Where(kvp => kvp.Value is not null && kvp.Value.Length > maxLength).Select(kvp => kvp.Key).ToList())
        {
            dictionary[key] = String.Format("Value is too large to be included.");
        }

        return dictionary;
    }

    /// <summary>
    /// The full path for the request including host, path and query String.
    /// </summary>
    public static string GetFullPath(this RequestInfo requestInfo, bool includeHttpMethod = false, bool includeHost = true, bool includeQueryString = true)
    {
        var sb = new StringBuilder();
        if (includeHttpMethod && !String.IsNullOrEmpty(requestInfo.HttpMethod))
        {
            sb.Append(requestInfo.HttpMethod).Append(" ");
        }

        if (includeHost && !String.IsNullOrEmpty(requestInfo.Host))
        {
            sb.Append(requestInfo.IsSecure.GetValueOrDefault() ? "https://" : "http://");
            sb.Append(requestInfo.Host);
            if (requestInfo.Port.HasValue && requestInfo.Port != 80 && requestInfo.Port != 443)
            {
                sb.Append(":").Append(requestInfo.Port);
            }
        }

        if (requestInfo.Path is not null)
        {
            if (!requestInfo.Path.StartsWith("/"))
            {
                sb.Append("/");
            }

            sb.Append(requestInfo.Path);
        }

        if (includeQueryString && requestInfo.QueryString is not null && requestInfo.QueryString.Count > 0)
        {
            sb.Append("?").Append(CreateQueryString(requestInfo.QueryString));
        }

        return sb.ToString();
    }

    private static string CreateQueryString(IEnumerable<KeyValuePair<string, string>> args)
    {
        if (args is null)
        {
            return String.Empty;
        }

        if (!args.Any())
        {
            return String.Empty;
        }

        var sb = new StringBuilder(args.Count() * 10);

        foreach (var p in args)
        {
            if (String.IsNullOrEmpty(p.Key) && p.Value is null)
            {
                continue;
            }

            if (!String.IsNullOrEmpty(p.Key))
            {
                sb.Append(Uri.EscapeDataString(p.Key));
                sb.Append('=');
            }
            if (p.Value is not null)
            {
                sb.Append(p.Value);
            }

            sb.Append('&');
        }
        sb.Length--; // remove trailing &

        return sb.ToString();
    }
}
