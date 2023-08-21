using System.Text;
using Exceptionless.Core.Models.Data;
using Newtonsoft.Json;

namespace Exceptionless.Core.Extensions;

public static class RequestInfoExtensions
{
    public static RequestInfo ApplyDataExclusions(this RequestInfo request, IList<string> exclusions, int maxLength = 1000)
    {
        request.Cookies = ApplyExclusions(request.Cookies, exclusions, maxLength);
        request.QueryString = ApplyExclusions(request.QueryString, exclusions, maxLength);
        request.PostData = ApplyPostDataExclusions(request.PostData, exclusions, maxLength);

        return request;
    }

    private static object? ApplyPostDataExclusions(object? data, IEnumerable<string> exclusions, int maxLength)
    {
        if (data is null)
            return null;

        var dictionary = data as Dictionary<string, string>;
        if (dictionary is null && data is string)
        {
            string json = (string)data;
            if (!json.IsJson())
                return data;

            try
            {
                dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
            catch (Exception) { }
        }

        return dictionary is not null ? ApplyExclusions(dictionary, exclusions, maxLength) : data;
    }

    private static Dictionary<string, string>? ApplyExclusions(Dictionary<string, string>? dictionary, IEnumerable<string> exclusions, int maxLength)
    {
        if (dictionary is null || dictionary.Count == 0)
            return dictionary;

        foreach (string key in dictionary.Keys.Where(k => String.IsNullOrEmpty(k) || StringExtensions.AnyWildcardMatches(k, exclusions, true)).ToList())
            dictionary.Remove(key);

        foreach (string key in dictionary.Where(kvp => kvp.Value is not null && kvp.Value.Length > maxLength).Select(kvp => kvp.Key).ToList())
            dictionary[key] = String.Format("Value is too large to be included.");

        return dictionary;
    }

    /// <summary>
    /// The full path for the request including host, path and query String.
    /// </summary>
    public static string GetFullPath(this RequestInfo requestInfo, bool includeHttpMethod = false, bool includeHost = true, bool includeQueryString = true)
    {
        var sb = new StringBuilder();
        if (includeHttpMethod && !String.IsNullOrEmpty(requestInfo.HttpMethod))
            sb.Append(requestInfo.HttpMethod).Append(" ");

        if (includeHost && !String.IsNullOrEmpty(requestInfo.Host))
        {
            sb.Append(requestInfo.IsSecure.GetValueOrDefault() ? "https://" : "http://");
            sb.Append(requestInfo.Host);
            if (requestInfo.Port.HasValue && requestInfo.Port != 80 && requestInfo.Port != 443)
                sb.Append(":").Append(requestInfo.Port);
        }

        if (requestInfo.Path is not null)
        {
            if (!requestInfo.Path.StartsWith("/"))
                sb.Append("/");

            sb.Append(requestInfo.Path);
        }

        if (includeQueryString && requestInfo.QueryString is not null && requestInfo.QueryString.Count > 0)
            sb.Append("?").Append(CreateQueryString(requestInfo.QueryString));

        return sb.ToString();
    }

    private static string CreateQueryString(IEnumerable<KeyValuePair<string, string>> args)
    {
        if (args is null)
            return String.Empty;

        if (!args.Any())
            return String.Empty;

        var sb = new StringBuilder(args.Count() * 10);

        foreach (var p in args)
        {
            if (String.IsNullOrEmpty(p.Key) && p.Value is null)
                continue;

            if (!String.IsNullOrEmpty(p.Key))
            {
                sb.Append(Uri.EscapeDataString(p.Key));
                sb.Append('=');
            }
            if (p.Value is not null)
                sb.Append(p.Value);
            sb.Append('&');
        }
        sb.Length--; // remove trailing &

        return sb.ToString();
    }
}
