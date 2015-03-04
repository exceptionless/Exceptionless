using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;

namespace Exceptionless.EventMigration.Models {
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

        public Exceptionless.Core.Models.Data.RequestInfo ToRequestInfo() {
            var requestInfo = new Exceptionless.Core.Models.Data.RequestInfo() {
                ClientIpAddress = ClientIpAddress,
                Cookies = Cookies,
                Host = Host,
                HttpMethod = HttpMethod,
                IsSecure = IsSecure,
                Path = Path,
                Port = Port,
                PostData = PostData,
                QueryString = QueryString,
                Referrer = Referrer,
                UserAgent = UserAgent
            };

            if (ExtendedData != null && ExtendedData.Count > 0)
                requestInfo.Data.AddRange(ExtendedData.ToData());

            return requestInfo;
        }
    }
}