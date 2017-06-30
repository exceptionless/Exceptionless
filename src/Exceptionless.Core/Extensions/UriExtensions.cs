using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Exceptionless.Core.Extensions {
    public static class UriExtensions {
        public static string ToQueryString(this NameValueCollection collection) {
            return collection.AsKeyValuePairs().ToQueryString();
        }

        public static string ToQueryString(this IEnumerable<KeyValuePair<string, string>> collection) {
            return collection.ToConcatenatedString(pair => pair.Key == null ? pair.Value : $"{pair.Key}={Uri.EscapeDataString(pair.Value)}", "&");
        }

        /// <summary>
        /// Converts the legacy NameValueCollection into a strongly-typed KeyValuePair sequence.
        /// </summary>
        private static IEnumerable<KeyValuePair<string, string>> AsKeyValuePairs(this NameValueCollection collection) {
            return collection.AllKeys.Select(key => new KeyValuePair<string, string>(key, collection.Get(key)));
        }

        public static string GetBaseUrl(this Uri uri) {
            return uri.Scheme + "://" + uri.Authority + uri.AbsolutePath;
        }		
    }
}