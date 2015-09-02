using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Models.Data {
    public class RequestInfo : IData {
        public RequestInfo() {
            Data = new DataDictionary();
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
        /// Extended data entries for this request.
        /// </summary>
        public DataDictionary Data { get; set; }

        public static class KnownDataKeys {
            public const string Browser = "@browser";
            public const string BrowserVersion = "@browser_version";
            public const string BrowserMajorVersion = "@browser_major_version";

            public const string Device = "@device";

            public const string OS = "@os";
            public const string OSVersion = "@os_version";
            public const string OSMajorVersion = "@os_major_version";

            public const string IsBot = "@is_bot";
        }
    }
}