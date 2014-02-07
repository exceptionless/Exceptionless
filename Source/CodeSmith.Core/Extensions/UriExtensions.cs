using System;
using System.Collections.Generic;
using System.Collections.Specialized;
#if !PFX_LEGACY_3_5
using System.Data;
using System.Diagnostics.Contracts;
#endif
using System.Linq;
using System.Text;
using System.Web;

#if !EMBEDDED
namespace CodeSmith.Core.Extensions {
#else
namespace Exceptionless.Extensions {
#endif
    public static class UriExtensions {
        /// <summary>
        /// Sets the specified query parameter key-value pair of the URI.
        /// If the key already exists, the value is overwritten.
        /// </summary>
        public static Uri AddOrUpdateQueryParameter(this Uri uri, string key, string value) {
            var builder = new UriBuilder(uri);
            builder.AddOrUpdateQueryParameter(key, value);

            return builder.Uri;
        }

        /// <summary>
        /// Sets the specified query parameter key-value pair of the URI.
        /// If the key already exists, the value is overwritten.
        /// </summary>
        public static UriBuilder AddOrUpdateQueryParameter(this UriBuilder uri, string key, string value) {
            var collection = uri.ParseQuery();
            // add (or replace existing) key-value pair
            collection.Set(Uri.EscapeDataString(key), Uri.EscapeDataString(value));

            uri.Query = BuildQueryString(collection);

            return uri;
        }

        /// <summary>
        /// Sets the specified query parameter key-value pair of the URI.
        /// If the key already exists, the value is overwritten.
        /// </summary>
        public static Uri RemoveQueryParameter(this Uri uri, string key) {
            var builder = new UriBuilder(uri);
            builder.RemoveQueryParameter(key);

            return builder.Uri;
        }

        /// <summary>
        /// Sets the specified query parameter key-value pair of the URI.
        /// If the key already exists, the value is overwritten.
        /// </summary>
        public static UriBuilder RemoveQueryParameter(this UriBuilder uri, string key) {
            NameValueCollection collection = uri.ParseQuery();

            if (!collection.HasKeys() || collection[key] == null)
                return uri;

            collection.Remove(Uri.EscapeDataString(key));

            uri.Query = BuildQueryString(collection);

            return uri;
        }

        private static string BuildQueryString(NameValueCollection collection) {
            return collection.AsKeyValuePairs().ToConcatenatedString(pair => pair.Key == null ? pair.Value : String.Format("{0}={1}", pair.Key, pair.Value), "&");
        }

        /// <summary>
        /// Gets the query string key-value pairs of the URI.
        /// Note that the one of the keys may be null ("?123") and
        /// that one of the keys may be an empty string ("?=123").
        /// </summary>
        public static IEnumerable<KeyValuePair<string, string>> GetQueryParameters(this UriBuilder uri) {
            return uri.ParseQuery().AsKeyValuePairs();
        }

        /// <summary>
        /// Converts the legacy NameValueCollection into a strongly-typed KeyValuePair sequence.
        /// </summary>
        private static IEnumerable<KeyValuePair<string, string>> AsKeyValuePairs(this NameValueCollection collection) {
            return collection.AllKeys.Select(key => new KeyValuePair<string, string>(key, collection.Get(key)));
        }

        /// <summary>
        /// Parses the query string of the URI into a NameValueCollection.
        /// </summary>
        private static NameValueCollection ParseQuery(this UriBuilder uri) {
            return HttpUtility.ParseQueryString(uri.Query);
        }

		public static void AppendQueryArgs(this UriBuilder builder, IEnumerable<KeyValuePair<string, string>> args) {
            if (builder == null)
                throw new ArgumentNullException("builder");

		    if (args == null || !args.Any())
		        return;

		    var sb = new StringBuilder(50 + (args.Count() * 10));
		    if (!String.IsNullOrEmpty(builder.Query)) {
		        sb.Append(builder.Query.Substring(1));
		        sb.Append('&');
		    }
		    sb.Append(CreateQueryString(args));

		    builder.Query = sb.ToString();
		}

 		internal static string CreateQueryString(IEnumerable<KeyValuePair<string, string>> args) {
            if (args == null)
                throw new ArgumentNullException("args");
#if !PFX_LEGACY_3_5
			Contract.Ensures(Contract.Result<string>() != null);
#endif

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

        private static readonly string[] UriRfc3986CharsToEscape = new[] { "!", "*", "'", "(", ")" };
        public static string EscapeUriDataStringRfc3986(string value) {
            if (value == null)
                throw new ArgumentNullException("value");

			// Start with RFC 2396 escaping by calling the .NET method to do the work.
			// This MAY sometimes exhibit RFC 3986 behavior (according to the documentation).
			// If it does, the escaping we do that follows it will be a no-op since the
			// characters we search for to replace can't possibly exist in the String.
			var escaped = new StringBuilder(Uri.EscapeDataString(value));

			// Upgrade the escaping to RFC 3986, if necessary.
			for (int i = 0; i < UriRfc3986CharsToEscape.Length; i++) {
				escaped.Replace(UriRfc3986CharsToEscape[i], Uri.HexEscape(UriRfc3986CharsToEscape[i][0]));
			}

			// Return the fully-RFC3986-escaped String.
			return escaped.ToString();
		}
   }
}