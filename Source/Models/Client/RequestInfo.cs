#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Exceptionless.Models {
    public class RequestInfo {
        public RequestInfo() {
            ExtendedData = new DataDictionary();
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
        /// Wether the request was secure or not.
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
        public DataDictionary ExtendedData { get; set; }

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