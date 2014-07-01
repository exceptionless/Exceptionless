using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exceptionless.Models.Data;

namespace Exceptionless.Extensions {
    public static class RequestInfoExtensions {

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
            if (includeQueryString && requestInfo.QueryString.Count > 0)
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
                    sb.Append(EscapeUriDataStringRfc3986(p.Value));
                sb.Append('&');
            }
            sb.Length--; // remove trailing &

            return sb.ToString();
        }

        private static readonly char[] _uriRfc3986CharsToEscape = { '!', '*', '\'', '(', ')' };

        private static string EscapeUriDataStringRfc3986(string value) {
            if (value == null)
                throw new ArgumentNullException("value");

            return value; //.HexEscape(_uriRfc3986CharsToEscape);
        }
    }
}
