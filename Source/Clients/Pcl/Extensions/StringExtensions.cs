using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Exceptionless.Extensions {
    internal static class StringExtensions {
        public static bool AnyWildcardMatches(this string value, IEnumerable<string> patternsToMatch, bool ignoreCase = false) {
            if (ignoreCase)
                value = value.ToLower();

            return patternsToMatch.Any(pattern => CheckForMatch(pattern, value, ignoreCase));
        }

        private static bool CheckForMatch(string pattern, string value, bool ignoreCase = true) {
            bool startsWithWildcard = pattern.StartsWith("*");
            if (startsWithWildcard)
                pattern = pattern.Substring(1);

            bool endsWithWildcard = pattern.EndsWith("*");
            if (endsWithWildcard)
                pattern = pattern.Substring(0, pattern.Length - 1);

            if (ignoreCase)
                pattern = pattern.ToLower();

            if (startsWithWildcard && endsWithWildcard)
                return value.Contains(pattern);

            if (startsWithWildcard)
                return value.EndsWith(pattern);

            if (endsWithWildcard)
                return value.StartsWith(pattern);

            return value.Equals(pattern);
        }

        public static byte[] ToByteArray(this string hex) {
            return Enumerable.Range(0, hex.Length).
                   Where(x => 0 == x % 2).
                   Select(x => Convert.ToByte(hex.Substring(x, 2), 16)).
                   ToArray();
        }

        public static string ToHex(this IEnumerable<byte> bytes) {
            var sb = new StringBuilder();
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
