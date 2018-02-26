using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;

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
        /// Whether the request was secure or not.
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

        protected bool Equals(RequestInfo other) {
            return string.Equals(UserAgent, other.UserAgent) && string.Equals(HttpMethod, other.HttpMethod) && IsSecure == other.IsSecure && string.Equals(Host, other.Host) && Port == other.Port && string.Equals(Path, other.Path) && string.Equals(Referrer, other.Referrer) && string.Equals(ClientIpAddress, other.ClientIpAddress) && Cookies.CollectionEquals(other.Cookies) && QueryString.CollectionEquals(other.QueryString) && Equals(Data, other.Data);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != this.GetType())
                return false;
            return Equals((RequestInfo)obj);
        }

        private static readonly List<string> _cookieHashCodeExclusions = new List<string> { "__LastReferenceId" };

        public override int GetHashCode() {
            unchecked {
                var hashCode = UserAgent?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ (HttpMethod?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ IsSecure.GetHashCode();
                hashCode = (hashCode * 397) ^ (Host?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ Port;
                hashCode = (hashCode * 397) ^ (Path?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (Referrer?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (ClientIpAddress?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (Cookies?.GetCollectionHashCode(_cookieHashCodeExclusions) ?? 0);
                hashCode = (hashCode * 397) ^ (QueryString?.GetCollectionHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (Data?.GetCollectionHashCode() ?? 0);
                return hashCode;
            }
        }
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