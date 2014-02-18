#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Exceptionless.Models {
    public class RequestInfo {
        public RequestInfo() {
            ExtendedData = new ExtendedDataDictionary();
            Cookies = new Dictionary<string, string>();
            QueryString = new Dictionary<string, string>();
        }

        /// <summary>
        /// The user agent used for the request.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// The HTTP method for the request.
        /// </summary>
        public string HttpMethod { get; set; }

        /// <summary>
        /// Wither the request was secure or not.
        /// </summary>
        public bool IsSecure { get; set; }

        /// <summary>
        /// The host of the request.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// The port of the request.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// The path of the request.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The full path for the request including host, path and query String.
        /// </summary>
        public string GetFullPath(bool includeHttpMethod = false, bool includeQueryString = true) {
            var sb = new StringBuilder();
            if (includeHttpMethod)
                sb.Append(HttpMethod).Append(" ");
            sb.Append(IsSecure ? "https://" : "http://");
            sb.Append(Host);
            if (Port != 80 && Port != 443)
                sb.Append(":").Append(Port);
            if (!Path.StartsWith("/"))
                sb.Append("/");
            sb.Append(Path);
            if (includeQueryString && QueryString.Count > 0)
                sb.Append("?").Append(CreateQueryString(QueryString));

            return sb.ToString();
        }

        /// <summary>
        /// The referring url for the request.
        /// </summary>
        public string Referrer { get; set; }

        /// <summary>
        /// The client's IP address when the error occurred.
        /// </summary>
        public string ClientIpAddress { get; set; }

        /// <summary>
        /// The request cookies.
        /// </summary>
        public Dictionary<string, string> Cookies { get; set; }

        /// <summary>
        /// The data that was POSTed for the request.
        /// </summary>
        public object PostData { get; set; }

        /// <summary>
        /// The query string values from the request.
        /// </summary>
        public Dictionary<string, string> QueryString { get; set; }

        /// <summary>
        /// Extended data entries for this error.
        /// </summary>
        public ExtendedDataDictionary ExtendedData { get; set; }

        private static string CreateQueryString(IEnumerable<KeyValuePair<string, string>> args) {
            if (args == null)
                throw new ArgumentNullException("args");

            if (!args.Any())
                return String.Empty;

            var sb = new StringBuilder(args.Count() * 10);

            foreach (var p in args) {
                if (String.IsNullOrEmpty(p.Key))
                    throw new NullReferenceException(String.Format("Key \"{0}\" is not allowed to be null.", p.Key));
                if (p.Value == null)
                    throw new NullReferenceException(String.Format("Value for key \"{0}\" is not allowed to be null.", p.Key));
                sb.Append(Uri.EscapeDataString(p.Key));
                sb.Append('=');
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

            return value.HexEscape(_uriRfc3986CharsToEscape);
        }
    }
}