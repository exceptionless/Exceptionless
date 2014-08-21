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
using System.Text.RegularExpressions;

namespace Exceptionless.Extensions {
    public static class StringExtensions {
        public static string ToLowerUnderscoredWords(this string value) {
            var builder = new StringBuilder(value.Length + 10);
            for (int index = 0; index < value.Length; index++) {
                char c = value[index];
                if (Char.IsUpper(c)) {
                    if (index > 0 && value[index - 1] != '_')
                        builder.Append('_');

                    builder.Append(Char.ToLower(c));
                } else {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        public static bool AnyWildcardMatches(this string value, IEnumerable<string> patternsToMatch, bool ignoreCase = false) {
            if (patternsToMatch == null)
                return false;

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

        public static bool IsNullOrEmpty(this string item) {
            return String.IsNullOrEmpty(item);
        }

        public static bool IsNullOrWhiteSpace(this string item) {
            return String.IsNullOrWhiteSpace(item);
        }

        public static bool IsMixedCase(this string s) {
            if (s.IsNullOrEmpty())
                return false;

            var containsUpper = false;
            var containsLower = false;

            foreach (char c in s) {
                if (Char.IsUpper(c))
                    containsUpper = true;

                if (Char.IsLower(c))
                    containsLower = true;
            }

            return containsLower && containsUpper;
        }

        public static string ToPascalCase(this string value, Regex splitRegex) {
            if (String.IsNullOrEmpty(value))
                return value;

            var mixedCase = value.IsMixedCase();
            var names = splitRegex.Split(value);
            var output = new StringBuilder();

            if (names.Length > 1) {
                foreach (string name in names) {
                    if (name.Length > 1) {
                        output.Append(Char.ToUpper(name[0]));
                        output.Append(mixedCase ? name.Substring(1) : name.Substring(1).ToLower());
                    } else {
                        output.Append(name);
                    }
                }
            } else if (value.Length > 1) {
                output.Append(Char.ToUpper(value[0]));
                output.Append(mixedCase ? value.Substring(1) : value.Substring(1).ToLower());
            } else {
                output.Append(value.ToUpper());
            }

            return output.ToString();
        }

        public static string ToPascalCase(this string value) {
            return value.ToPascalCase(_splitNameRegex);
        }

        public static string ToLowerFiltered(this string value, char[] charsToRemove) {
            var builder = new StringBuilder(value.Length);

            for (int index = 0; index < value.Length; index++) {
                char c = value[index];
                if (Char.IsUpper(c))
                    c = Char.ToLower(c);
                
                if (charsToRemove.Contains(c))
                    continue;

                builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Takes a NameIdentifier and spaces it out into words "Name Identifier".
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The string</returns>
        public static string[] ToWords(this string value) {
            var words = new List<string>();
            value = ToPascalCase(value);

            MatchCollection wordMatches = _properWordRegex.Matches(value);
            foreach (Match word in wordMatches) {
                if (!word.Value.IsNullOrWhiteSpace())
                    words.Add(word.Value);
            }

            return words.ToArray();
        }

        /// <summary>
        /// Takes a NameIdentifier and spaces it out into words "Name Identifier".
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The string</returns>
        public static string ToSpacedWords(this string value) {
            string[] words = ToWords(value);

            var spacedName = new StringBuilder();
            foreach (string word in words) {
                spacedName.Append(word);
                spacedName.Append(' ');
            }

            return spacedName.ToString().Trim();
        }

        private static readonly Regex _properWordRegex = new Regex(@"([A-Z][a-z]*)|([0-9]+)");
        private static readonly Regex _splitNameRegex = new Regex(@"[\W_]+");
    }
}
