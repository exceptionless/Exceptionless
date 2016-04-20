using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exceptionless.Core.Models.Data;
using Newtonsoft.Json;

namespace Exceptionless.Core.Extensions {
    public static class RequestInfoExtensions {
        public static RequestInfo ApplyDataExclusions(this RequestInfo request, IList<string> exclusions, int maxLength = 1000) {
            if (request == null)
                return null;

            request.Cookies = ApplyExclusions(request.Cookies, exclusions, maxLength);
            request.QueryString = ApplyExclusions(request.QueryString, exclusions, maxLength);
            request.PostData = ApplyPostDataExclusions(request.PostData, exclusions, maxLength);

            return request;
        }

        private static object ApplyPostDataExclusions(object data, IEnumerable<string> exclusions, int maxLength) {
            if (data == null)
                return null;

            var dictionary = data as Dictionary<string, string>;
            if (dictionary == null && data is string) {
                string json = (string)data;
                if (!json.IsJson())
                    return data;

                try {
                    dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                } catch (Exception) {}
            }

            return dictionary != null ? ApplyExclusions(dictionary, exclusions, maxLength) : data;
        }

        private static Dictionary<string, string> ApplyExclusions(Dictionary<string, string> dictionary, IEnumerable<string> exclusions, int maxLength) {
            if (dictionary == null || dictionary.Count == 0)
                return dictionary;

            foreach (var key in dictionary.Keys.Where(k => String.IsNullOrEmpty(k) || StringExtensions.AnyWildcardMatches(k, exclusions, true)).ToList())
                dictionary.Remove(key);

            foreach (var key in dictionary.Where(kvp => kvp.Value != null && kvp.Value.Length > maxLength).Select(kvp => kvp.Key).ToList())
                dictionary[key] = String.Format("Value is too large to be included.");

            return dictionary;
        }

        /// <summary>
        /// The full path for the request including host, path and query String.
        /// </summary>
        public static string GetFullPath(this RequestInfo requestInfo, bool includeHttpMethod = false, bool includeHost = true, bool includeQueryString = true) {
            var sb = new StringBuilder();
            if (includeHttpMethod)
                sb.Append(requestInfo.HttpMethod).Append(" ");

            if (includeHost) {
                sb.Append(requestInfo.IsSecure ? "https://" : "http://");
                sb.Append(requestInfo.Host);
                if (requestInfo.Port != 80 && requestInfo.Port != 443)
                    sb.Append(":").Append(requestInfo.Port);
            }

            if (!requestInfo.Path.StartsWith("/"))
                sb.Append("/");

            sb.Append(requestInfo.Path);

            if (includeQueryString && requestInfo.QueryString != null && requestInfo.QueryString.Count > 0)
                sb.Append("?").Append(CreateQueryString(requestInfo.QueryString));

            return sb.ToString();
        }

        private static string CreateQueryString(IEnumerable<KeyValuePair<string, string>> args) {
            if (args == null)
                return String.Empty;

            if (!args.Any())
                return String.Empty;

            var sb = new StringBuilder(args.Count() * 10);

            foreach (var p in args) {
                if (String.IsNullOrEmpty(p.Key) && p.Value == null)
                    continue;

                if (!String.IsNullOrEmpty(p.Key)) {
                    sb.Append(Uri.EscapeDataString(p.Key));
                    sb.Append('=');
                }
                if (p.Value != null)
                    sb.Append(p.Value);
                sb.Append('&');
            }
            sb.Length--; // remove trailing &

            return sb.ToString();
        }
    }
}
