using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Security.Cryptography;

namespace Exceptionless.Core.Extensions {
    public static class StringExtensions {
        public static bool IsLocalHost(this string ip) {
            if (String.IsNullOrEmpty(ip))
                return false;

            return String.Equals(ip, "127.0.0.1") || String.Equals(ip, "::1");
        }

        /// <summary>
        /// Very basic check to try and remove the port from any ipv4 or ipv6 address.
        /// </summary>
        /// <returns>ip address without port</returns>
        public static string ToAddress(this string ip) {
            if (String.IsNullOrEmpty(ip) || String.Equals(ip, "::1"))
                return ip;

            var parts = ip.Split(new [] {':' }, 9);
            if (parts.Length == 2)  // 1.2.3.4:port
                return parts[0];
            if (parts.Length > 8) // 1:2:3:4:5:6:7:8:port
                return String.Join(":", parts.Take(8));

            return ip;
        }

        public static bool IsPrivateNetwork(this string ip) {
            if (String.IsNullOrEmpty(ip))
                return false;

            if (ip.IsLocalHost())
                return true;

            // 10.0.0.0 – 10.255.255.255 (Class A)
            if (ip.StartsWith("10."))
                return true;

            // 172.16.0.0 – 172.31.255.255 (Class B)
            if (ip.StartsWith("172.")) {
                for (int range = 16; range < 32; range++) {
                    if (ip.StartsWith("172." + range + "."))
                        return true;
                }
            }

            // 192.168.0.0 – 192.168.255.255 (Class C)
            return ip.StartsWith("192.168.");
        }

        public static string GetNewToken() {
            return GetRandomString(40);
        }

        public static string GetRandomString(int length, string allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789") {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "length cannot be less than zero.");

            if (String.IsNullOrEmpty(allowedChars))
                throw new ArgumentException("allowedChars may not be empty.");

            const int byteSize = 0x100;
            var allowedCharSet = new HashSet<char>(allowedChars).ToArray();
            if (byteSize < allowedCharSet.Length)
                throw new ArgumentException($"allowedChars may contain no more than {byteSize} characters.");

            using (var rng = new RNGCryptoServiceProvider()) {
                var result = new StringBuilder();
                var buf = new byte[128];

                while (result.Length < length) {
                    rng.GetBytes(buf);
                    for (int i = 0; i < buf.Length && result.Length < length; ++i) {
                        int outOfRangeStart = byteSize - (byteSize % allowedCharSet.Length);
                        if (outOfRangeStart <= buf[i])
                            continue;
                        result.Append(allowedCharSet[buf[i] % allowedCharSet.Length]);
                    }
                }

                return result.ToString();
            }
        }

        // TODO: Add support for detecting the culture number separators as well as suffix (Ex. 100d)
        public static bool IsNumeric(this string value) {
            if (String.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < value.Length; i++) {
                if (Char.IsNumber(value[i]))
                    continue;

                if (i == 0 && value[i] == '-')
                    continue;

                return false;
            }

            return true;
        }

        public static bool IsValidFieldName(this string value) {
            if (value == null || value.Length > 25)
                return false;

            return IsValidIdentifier(value);
        }

        public static bool IsValidIdentifier(this string value) {
            if (value == null)
                return false;

            for (int index = 0; index < value.Length; index++) {
                if (!Char.IsLetterOrDigit(value[index]) && value[index] != '-')
                    return false;
            }

            return true;
        }

        public static string ToSaltedHash(this string password, string salt) {
            var passwordBytes = Encoding.Unicode.GetBytes(password);
            var saltBytes = Convert.FromBase64String(salt);
            var hashStrategy = new HMACSHA256();
            if (hashStrategy.Key.Length == saltBytes.Length) {
                hashStrategy.Key = saltBytes;
            } else if (hashStrategy.Key.Length < saltBytes.Length) {
                var keyBytes = new byte[hashStrategy.Key.Length];
                Buffer.BlockCopy(saltBytes, 0, keyBytes, 0, keyBytes.Length);
                hashStrategy.Key = keyBytes;
            } else {
                var keyBytes = new byte[hashStrategy.Key.Length];
                for (int i = 0; i < keyBytes.Length;) {
                    int len = Math.Min(saltBytes.Length, keyBytes.Length - i);
                    Buffer.BlockCopy(saltBytes, 0, keyBytes, i, len);
                    i += len;
                }
                hashStrategy.Key = keyBytes;
            }

            var result = hashStrategy.ComputeHash(passwordBytes);
            return Convert.ToBase64String(result);
        }

        public static string ToDelimitedString(this IEnumerable<string> values, string delimiter = ",") {
            if (String.IsNullOrEmpty(delimiter))
                delimiter = ",";

            var sb = new StringBuilder();
            foreach (string i in values) {
                if (sb.Length > 0)
                    sb.Append(delimiter);

                sb.Append(i);
            }

            return sb.ToString();
        }

        public static string[] FromDelimitedString(this string value, string delimiter = ",") {
            if (String.IsNullOrEmpty(value))
                return null;

            if (String.IsNullOrEmpty(delimiter))
                delimiter = ",";

            return value.Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries).ToArray();
        }

        public static string ToLowerUnderscoredWords(this string value) {
            var builder = new StringBuilder(value.Length + 10);
            for (int index = 0; index < value.Length; index++) {
                char c = value[index];
                if (Char.IsUpper(c)) {
                    if (index > 0 && value[index - 1] != '_')
                        builder.Append('_');

                    builder.Append(Char.ToLowerInvariant(c));
                } else {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        public static bool AnyWildcardMatches(this string value, IEnumerable<string> patternsToMatch, bool ignoreCase = false) {
            if (ignoreCase)
                value = value.ToLowerInvariant();

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
                pattern = pattern.ToLowerInvariant();

            if (startsWithWildcard && endsWithWildcard)
                return value.Contains(pattern);

            if (startsWithWildcard)
                return value.EndsWith(pattern);

            if (endsWithWildcard)
                return value.StartsWith(pattern);

            return value.Equals(pattern);
        }

        public static string ToConcatenatedString<T>(this IEnumerable<T> values, Func<T, string> stringSelector) {
            return values.ToConcatenatedString(stringSelector, String.Empty);
        }

        public static string ToConcatenatedString<T>(this IEnumerable<T> values, Func<T, string> action, string separator) {
            var sb = new StringBuilder();
            foreach (var item in values) {
                if (sb.Length > 0)
                    sb.Append(separator);

                sb.Append(action(item));
            }

            return sb.ToString();
        }

        public static string ReplaceFirst(this string input, string find, string replace) {
            if (String.IsNullOrEmpty(input))
                return input;

            int i = input.IndexOf(find, StringComparison.Ordinal);
            if (i < 0)
                return input;

            string pre = input.Substring(0, i);
            string post = input.Substring(i + find.Length);
            return String.Concat(pre, replace, post);
        }

        public static IEnumerable<string> SplitLines(this string text) {
            return text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Where(l => !String.IsNullOrWhiteSpace(l)).Select(l => l.Trim());
        }

        public static string StripInvisible(this string s) {
            return s.Replace("\r\n", " ").Replace('\n', ' ').Replace('\t', ' ');
        }

        public static string NormalizeLineEndings(this string text, string lineEnding = null) {
            if (String.IsNullOrEmpty(lineEnding))
                lineEnding = Environment.NewLine;

            text = text.Replace("\r\n", "\n");
            if (lineEnding != "\n")
                text = text.Replace("\r\n", lineEnding);

            return text;
        }

        public static string Truncate(this string text, int keep) {
            if (String.IsNullOrEmpty(text))
                return String.Empty;

            string buffer = NormalizeLineEndings(text);
            if (buffer.Length <= keep)
                return buffer;

            return String.Concat(buffer.Substring(0, keep - 3), "...");
        }

        public static string TypeName(this string typeFullName) {
            return typeFullName?.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Last();
        }

        public static string ToLowerFiltered(this string value, char[] charsToRemove) {
            var builder = new StringBuilder(value.Length);

            for (int index = 0; index < value.Length; index++) {
                char c = value[index];
                if (Char.IsUpper(c))
                    c = Char.ToLowerInvariant(c);

                bool includeChar = true;
                for (int i = 0; i < charsToRemove.Length; i++) {
                    if (charsToRemove[i] == c) {
                        includeChar = false;
                        break;
                    }
                }

                if (includeChar)
                    builder.Append(c);
            }

            return builder.ToString();
        }

        public static string[] SplitAndTrim(this string s, char[] separator) {
            if (s.IsNullOrEmpty())
                return new string[0];

            var result = s.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < result.Length; i++)
                result[i] = result[i].Trim();

            return result;

        }

        public static bool IsNullOrEmpty(this string item) {
            return String.IsNullOrEmpty(item);
        }

        private static readonly Regex _entityResolver = new Regex("([&][#](?'decimal'[0-9]+);)|([&][#][(x|X)](?'hex'[0-9a-fA-F]+);)|([&](?'html'\\w+);)");

        public static string HtmlEntityDecode(this string encodedText) {
            return _entityResolver.Replace(encodedText, new MatchEvaluator(ResolveEntityAngleAmp));
        }

        private static string ResolveEntityAngleAmp(Match matchToProcess) {
            return !matchToProcess.Groups["decimal"].Success ? (!matchToProcess.Groups["hex"].Success ? (!matchToProcess.Groups["html"].Success ? "Y" : EntityLookup(matchToProcess.Groups["html"].Value)) : Convert.ToChar(HexToInt(matchToProcess.Groups["hex"].Value)).ToString()) : Convert.ToChar(Convert.ToInt32(matchToProcess.Groups["decimal"].Value)).ToString();
        }

        public static int HexToInt(string input) {
            int num = 0;
            input = input.ToUpperInvariant();
            var chArray = input.ToCharArray();
            for (int index = chArray.Length - 1; index >= 0; --index) {
                if ((int)chArray[index] >= 48 && (int)chArray[index] <= 57)
                    num += ((int)chArray[index] - 48) * (int)Math.Pow(16.0, (double)(chArray.Length - 1 - index));
                else if ((int)chArray[index] >= 65 && (int)chArray[index] <= 70) {
                    num += ((int)chArray[index] - 55) * (int)Math.Pow(16.0, (double)(chArray.Length - 1 - index));
                } else {
                    num = 0;
                    break;
                }
            }
            return num;
        }

        private static string EntityLookup(string entity) {
            string str = "";
            switch (entity) {
                case "Aacute":
                    str = Convert.ToChar(193).ToString();
                    break;
                case "aacute":
                    str = Convert.ToChar(225).ToString();
                    break;
                case "acirc":
                    str = Convert.ToChar(226).ToString();
                    break;
                case "Acirc":
                    str = Convert.ToChar(194).ToString();
                    break;
                case "acute":
                    str = Convert.ToChar(180).ToString();
                    break;
                case "AElig":
                    str = Convert.ToChar(198).ToString();
                    break;
                case "aelig":
                    str = Convert.ToChar(230).ToString();
                    break;
                case "Agrave":
                    str = Convert.ToChar(192).ToString();
                    break;
                case "agrave":
                    str = Convert.ToChar(224).ToString();
                    break;
                case "alefsym":
                    str = Convert.ToChar(8501).ToString();
                    break;
                case "Alpha":
                    str = Convert.ToChar(913).ToString();
                    break;
                case "alpha":
                    str = Convert.ToChar(945).ToString();
                    break;
                case "amp":
                    str = Convert.ToChar(38).ToString();
                    break;
                case "and":
                    str = Convert.ToChar(8743).ToString();
                    break;
                case "ang":
                    str = Convert.ToChar(8736).ToString();
                    break;
                case "aring":
                    str = Convert.ToChar(229).ToString();
                    break;
                case "Aring":
                    str = Convert.ToChar(197).ToString();
                    break;
                case "asymp":
                    str = Convert.ToChar(8776).ToString();
                    break;
                case "Atilde":
                    str = Convert.ToChar(195).ToString();
                    break;
                case "atilde":
                    str = Convert.ToChar(227).ToString();
                    break;
                case "auml":
                    str = Convert.ToChar(228).ToString();
                    break;
                case "Auml":
                    str = Convert.ToChar(196).ToString();
                    break;
                case "bdquo":
                    str = Convert.ToChar(8222).ToString();
                    break;
                case "Beta":
                    str = Convert.ToChar(914).ToString();
                    break;
                case "beta":
                    str = Convert.ToChar(946).ToString();
                    break;
                case "brvbar":
                    str = Convert.ToChar(166).ToString();
                    break;
                case "bull":
                    str = Convert.ToChar(8226).ToString();
                    break;
                case "cap":
                    str = Convert.ToChar(8745).ToString();
                    break;
                case "Ccedil":
                    str = Convert.ToChar(199).ToString();
                    break;
                case "ccedil":
                    str = Convert.ToChar(231).ToString();
                    break;
                case "cedil":
                    str = Convert.ToChar(184).ToString();
                    break;
                case "cent":
                    str = Convert.ToChar(162).ToString();
                    break;
                case "chi":
                    str = Convert.ToChar(967).ToString();
                    break;
                case "Chi":
                    str = Convert.ToChar(935).ToString();
                    break;
                case "circ":
                    str = Convert.ToChar(710).ToString();
                    break;
                case "clubs":
                    str = Convert.ToChar(9827).ToString();
                    break;
                case "cong":
                    str = Convert.ToChar(8773).ToString();
                    break;
                case "copy":
                    str = Convert.ToChar(169).ToString();
                    break;
                case "crarr":
                    str = Convert.ToChar(8629).ToString();
                    break;
                case "cup":
                    str = Convert.ToChar(8746).ToString();
                    break;
                case "curren":
                    str = Convert.ToChar(164).ToString();
                    break;
                case "dagger":
                    str = Convert.ToChar(8224).ToString();
                    break;
                case "Dagger":
                    str = Convert.ToChar(8225).ToString();
                    break;
                case "darr":
                    str = Convert.ToChar(8595).ToString();
                    break;
                case "dArr":
                    str = Convert.ToChar(8659).ToString();
                    break;
                case "deg":
                    str = Convert.ToChar(176).ToString();
                    break;
                case "Delta":
                    str = Convert.ToChar(916).ToString();
                    break;
                case "delta":
                    str = Convert.ToChar(948).ToString();
                    break;
                case "diams":
                    str = Convert.ToChar(9830).ToString();
                    break;
                case "divide":
                    str = Convert.ToChar(247).ToString();
                    break;
                case "eacute":
                    str = Convert.ToChar(233).ToString();
                    break;
                case "Eacute":
                    str = Convert.ToChar(201).ToString();
                    break;
                case "Ecirc":
                    str = Convert.ToChar(202).ToString();
                    break;
                case "ecirc":
                    str = Convert.ToChar(234).ToString();
                    break;
                case "Egrave":
                    str = Convert.ToChar(200).ToString();
                    break;
                case "egrave":
                    str = Convert.ToChar(232).ToString();
                    break;
                case "empty":
                    str = Convert.ToChar(8709).ToString();
                    break;
                case "emsp":
                    str = Convert.ToChar(8195).ToString();
                    break;
                case "ensp":
                    str = Convert.ToChar(8194).ToString();
                    break;
                case "epsilon":
                    str = Convert.ToChar(949).ToString();
                    break;
                case "Epsilon":
                    str = Convert.ToChar(917).ToString();
                    break;
                case "equiv":
                    str = Convert.ToChar(8801).ToString();
                    break;
                case "Eta":
                    str = Convert.ToChar(919).ToString();
                    break;
                case "eta":
                    str = Convert.ToChar(951).ToString();
                    break;
                case "eth":
                    str = Convert.ToChar(240).ToString();
                    break;
                case "ETH":
                    str = Convert.ToChar(208).ToString();
                    break;
                case "Euml":
                    str = Convert.ToChar(203).ToString();
                    break;
                case "euml":
                    str = Convert.ToChar(235).ToString();
                    break;
                case "euro":
                    str = Convert.ToChar(8364).ToString();
                    break;
                case "exist":
                    str = Convert.ToChar(8707).ToString();
                    break;
                case "fnof":
                    str = Convert.ToChar(402).ToString();
                    break;
                case "forall":
                    str = Convert.ToChar(8704).ToString();
                    break;
                case "frac12":
                    str = Convert.ToChar(189).ToString();
                    break;
                case "frac14":
                    str = Convert.ToChar(188).ToString();
                    break;
                case "frac34":
                    str = Convert.ToChar(190).ToString();
                    break;
                case "frasl":
                    str = Convert.ToChar(8260).ToString();
                    break;
                case "gamma":
                    str = Convert.ToChar(947).ToString();
                    break;
                case "Gamma":
                    str = Convert.ToChar(915).ToString();
                    break;
                case "ge":
                    str = Convert.ToChar(8805).ToString();
                    break;
                case "gt":
                    str = Convert.ToChar(62).ToString();
                    break;
                case "hArr":
                    str = Convert.ToChar(8660).ToString();
                    break;
                case "harr":
                    str = Convert.ToChar(8596).ToString();
                    break;
                case "hearts":
                    str = Convert.ToChar(9829).ToString();
                    break;
                case "hellip":
                    str = Convert.ToChar(8230).ToString();
                    break;
                case "Iacute":
                    str = Convert.ToChar(205).ToString();
                    break;
                case "iacute":
                    str = Convert.ToChar(237).ToString();
                    break;
                case "icirc":
                    str = Convert.ToChar(238).ToString();
                    break;
                case "Icirc":
                    str = Convert.ToChar(206).ToString();
                    break;
                case "iexcl":
                    str = Convert.ToChar(161).ToString();
                    break;
                case "Igrave":
                    str = Convert.ToChar(204).ToString();
                    break;
                case "igrave":
                    str = Convert.ToChar(236).ToString();
                    break;
                case "image":
                    str = Convert.ToChar(8465).ToString();
                    break;
                case "infin":
                    str = Convert.ToChar(8734).ToString();
                    break;
                case "int":
                    str = Convert.ToChar(8747).ToString();
                    break;
                case "Iota":
                    str = Convert.ToChar(921).ToString();
                    break;
                case "iota":
                    str = Convert.ToChar(953).ToString();
                    break;
                case "iquest":
                    str = Convert.ToChar(191).ToString();
                    break;
                case "isin":
                    str = Convert.ToChar(8712).ToString();
                    break;
                case "iuml":
                    str = Convert.ToChar(239).ToString();
                    break;
                case "Iuml":
                    str = Convert.ToChar(207).ToString();
                    break;
                case "kappa":
                    str = Convert.ToChar(954).ToString();
                    break;
                case "Kappa":
                    str = Convert.ToChar(922).ToString();
                    break;
                case "Lambda":
                    str = Convert.ToChar(923).ToString();
                    break;
                case "lambda":
                    str = Convert.ToChar(955).ToString();
                    break;
                case "lang":
                    str = Convert.ToChar(9001).ToString();
                    break;
                case "laquo":
                    str = Convert.ToChar(171).ToString();
                    break;
                case "larr":
                    str = Convert.ToChar(8592).ToString();
                    break;
                case "lArr":
                    str = Convert.ToChar(8656).ToString();
                    break;
                case "lceil":
                    str = Convert.ToChar(8968).ToString();
                    break;
                case "ldquo":
                    str = Convert.ToChar(8220).ToString();
                    break;
                case "le":
                    str = Convert.ToChar(8804).ToString();
                    break;
                case "lfloor":
                    str = Convert.ToChar(8970).ToString();
                    break;
                case "lowast":
                    str = Convert.ToChar(8727).ToString();
                    break;
                case "loz":
                    str = Convert.ToChar(9674).ToString();
                    break;
                case "lrm":
                    str = Convert.ToChar(8206).ToString();
                    break;
                case "lsaquo":
                    str = Convert.ToChar(8249).ToString();
                    break;
                case "lsquo":
                    str = Convert.ToChar(8216).ToString();
                    break;
                case "lt":
                    str = Convert.ToChar(60).ToString();
                    break;
                case "macr":
                    str = Convert.ToChar(175).ToString();
                    break;
                case "mdash":
                    str = Convert.ToChar(8212).ToString();
                    break;
                case "micro":
                    str = Convert.ToChar(181).ToString();
                    break;
                case "middot":
                    str = Convert.ToChar(183).ToString();
                    break;
                case "minus":
                    str = Convert.ToChar(8722).ToString();
                    break;
                case "Mu":
                    str = Convert.ToChar(924).ToString();
                    break;
                case "mu":
                    str = Convert.ToChar(956).ToString();
                    break;
                case "nabla":
                    str = Convert.ToChar(8711).ToString();
                    break;
                case "nbsp":
                    str = Convert.ToChar(160).ToString();
                    break;
                case "ndash":
                    str = Convert.ToChar(8211).ToString();
                    break;
                case "ne":
                    str = Convert.ToChar(8800).ToString();
                    break;
                case "ni":
                    str = Convert.ToChar(8715).ToString();
                    break;
                case "not":
                    str = Convert.ToChar(172).ToString();
                    break;
                case "notin":
                    str = Convert.ToChar(8713).ToString();
                    break;
                case "nsub":
                    str = Convert.ToChar(8836).ToString();
                    break;
                case "ntilde":
                    str = Convert.ToChar(241).ToString();
                    break;
                case "Ntilde":
                    str = Convert.ToChar(209).ToString();
                    break;
                case "Nu":
                    str = Convert.ToChar(925).ToString();
                    break;
                case "nu":
                    str = Convert.ToChar(957).ToString();
                    break;
                case "oacute":
                    str = Convert.ToChar(243).ToString();
                    break;
                case "Oacute":
                    str = Convert.ToChar(211).ToString();
                    break;
                case "Ocirc":
                    str = Convert.ToChar(212).ToString();
                    break;
                case "ocirc":
                    str = Convert.ToChar(244).ToString();
                    break;
                case "OElig":
                    str = Convert.ToChar(338).ToString();
                    break;
                case "oelig":
                    str = Convert.ToChar(339).ToString();
                    break;
                case "ograve":
                    str = Convert.ToChar(242).ToString();
                    break;
                case "Ograve":
                    str = Convert.ToChar(210).ToString();
                    break;
                case "oline":
                    str = Convert.ToChar(8254).ToString();
                    break;
                case "Omega":
                    str = Convert.ToChar(937).ToString();
                    break;
                case "omega":
                    str = Convert.ToChar(969).ToString();
                    break;
                case "Omicron":
                    str = Convert.ToChar(927).ToString();
                    break;
                case "omicron":
                    str = Convert.ToChar(959).ToString();
                    break;
                case "oplus":
                    str = Convert.ToChar(8853).ToString();
                    break;
                case "or":
                    str = Convert.ToChar(8744).ToString();
                    break;
                case "ordf":
                    str = Convert.ToChar(170).ToString();
                    break;
                case "ordm":
                    str = Convert.ToChar(186).ToString();
                    break;
                case "Oslash":
                    str = Convert.ToChar(216).ToString();
                    break;
                case "oslash":
                    str = Convert.ToChar(248).ToString();
                    break;
                case "otilde":
                    str = Convert.ToChar(245).ToString();
                    break;
                case "Otilde":
                    str = Convert.ToChar(213).ToString();
                    break;
                case "otimes":
                    str = Convert.ToChar(8855).ToString();
                    break;
                case "Ouml":
                    str = Convert.ToChar(214).ToString();
                    break;
                case "ouml":
                    str = Convert.ToChar(246).ToString();
                    break;
                case "para":
                    str = Convert.ToChar(182).ToString();
                    break;
                case "part":
                    str = Convert.ToChar(8706).ToString();
                    break;
                case "permil":
                    str = Convert.ToChar(8240).ToString();
                    break;
                case "perp":
                    str = Convert.ToChar(8869).ToString();
                    break;
                case "Phi":
                    str = Convert.ToChar(934).ToString();
                    break;
                case "phi":
                    str = Convert.ToChar(966).ToString();
                    break;
                case "Pi":
                    str = Convert.ToChar(928).ToString();
                    break;
                case "pi":
                    str = Convert.ToChar(960).ToString();
                    break;
                case "piv":
                    str = Convert.ToChar(982).ToString();
                    break;
                case "plusmn":
                    str = Convert.ToChar(177).ToString();
                    break;
                case "pound":
                    str = Convert.ToChar(163).ToString();
                    break;
                case "Prime":
                    str = Convert.ToChar(8243).ToString();
                    break;
                case "prime":
                    str = Convert.ToChar(8242).ToString();
                    break;
                case "prod":
                    str = Convert.ToChar(8719).ToString();
                    break;
                case "prop":
                    str = Convert.ToChar(8733).ToString();
                    break;
                case "psi":
                    str = Convert.ToChar(968).ToString();
                    break;
                case "Psi":
                    str = Convert.ToChar(936).ToString();
                    break;
                case "quot":
                    str = Convert.ToChar(34).ToString();
                    break;
                case "radic":
                    str = Convert.ToChar(8730).ToString();
                    break;
                case "rang":
                    str = Convert.ToChar(9002).ToString();
                    break;
                case "raquo":
                    str = Convert.ToChar(187).ToString();
                    break;
                case "rarr":
                    str = Convert.ToChar(8594).ToString();
                    break;
                case "rArr":
                    str = Convert.ToChar(8658).ToString();
                    break;
                case "rceil":
                    str = Convert.ToChar(8969).ToString();
                    break;
                case "rdquo":
                    str = Convert.ToChar(8221).ToString();
                    break;
                case "real":
                    str = Convert.ToChar(8476).ToString();
                    break;
                case "reg":
                    str = Convert.ToChar(174).ToString();
                    break;
                case "rfloor":
                    str = Convert.ToChar(8971).ToString();
                    break;
                case "rho":
                    str = Convert.ToChar(961).ToString();
                    break;
                case "Rho":
                    str = Convert.ToChar(929).ToString();
                    break;
                case "rlm":
                    str = Convert.ToChar(8207).ToString();
                    break;
                case "rsaquo":
                    str = Convert.ToChar(8250).ToString();
                    break;
                case "rsquo":
                    str = Convert.ToChar(8217).ToString();
                    break;
                case "sbquo":
                    str = Convert.ToChar(8218).ToString();
                    break;
                case "Scaron":
                    str = Convert.ToChar(352).ToString();
                    break;
                case "scaron":
                    str = Convert.ToChar(353).ToString();
                    break;
                case "sdot":
                    str = Convert.ToChar(8901).ToString();
                    break;
                case "sect":
                    str = Convert.ToChar(167).ToString();
                    break;
                case "shy":
                    str = Convert.ToChar(173).ToString();
                    break;
                case "sigma":
                    str = Convert.ToChar(963).ToString();
                    break;
                case "Sigma":
                    str = Convert.ToChar(931).ToString();
                    break;
                case "sigmaf":
                    str = Convert.ToChar(962).ToString();
                    break;
                case "sim":
                    str = Convert.ToChar(8764).ToString();
                    break;
                case "spades":
                    str = Convert.ToChar(9824).ToString();
                    break;
                case "sub":
                    str = Convert.ToChar(8834).ToString();
                    break;
                case "sube":
                    str = Convert.ToChar(8838).ToString();
                    break;
                case "sum":
                    str = Convert.ToChar(8721).ToString();
                    break;
                case "sup":
                    str = Convert.ToChar(8835).ToString();
                    break;
                case "sup1":
                    str = Convert.ToChar(185).ToString();
                    break;
                case "sup2":
                    str = Convert.ToChar(178).ToString();
                    break;
                case "sup3":
                    str = Convert.ToChar(179).ToString();
                    break;
                case "supe":
                    str = Convert.ToChar(8839).ToString();
                    break;
                case "szlig":
                    str = Convert.ToChar(223).ToString();
                    break;
                case "Tau":
                    str = Convert.ToChar(932).ToString();
                    break;
                case "tau":
                    str = Convert.ToChar(964).ToString();
                    break;
                case "there4":
                    str = Convert.ToChar(8756).ToString();
                    break;
                case "theta":
                    str = Convert.ToChar(952).ToString();
                    break;
                case "Theta":
                    str = Convert.ToChar(920).ToString();
                    break;
                case "thetasym":
                    str = Convert.ToChar(977).ToString();
                    break;
                case "thinsp":
                    str = Convert.ToChar(8201).ToString();
                    break;
                case "thorn":
                    str = Convert.ToChar(254).ToString();
                    break;
                case "THORN":
                    str = Convert.ToChar(222).ToString();
                    break;
                case "tilde":
                    str = Convert.ToChar(732).ToString();
                    break;
                case "times":
                    str = Convert.ToChar(215).ToString();
                    break;
                case "trade":
                    str = Convert.ToChar(8482).ToString();
                    break;
                case "Uacute":
                    str = Convert.ToChar(218).ToString();
                    break;
                case "uacute":
                    str = Convert.ToChar(250).ToString();
                    break;
                case "uarr":
                    str = Convert.ToChar(8593).ToString();
                    break;
                case "uArr":
                    str = Convert.ToChar(8657).ToString();
                    break;
                case "Ucirc":
                    str = Convert.ToChar(219).ToString();
                    break;
                case "ucirc":
                    str = Convert.ToChar(251).ToString();
                    break;
                case "Ugrave":
                    str = Convert.ToChar(217).ToString();
                    break;
                case "ugrave":
                    str = Convert.ToChar(249).ToString();
                    break;
                case "uml":
                    str = Convert.ToChar(168).ToString();
                    break;
                case "upsih":
                    str = Convert.ToChar(978).ToString();
                    break;
                case "Upsilon":
                    str = Convert.ToChar(933).ToString();
                    break;
                case "upsilon":
                    str = Convert.ToChar(965).ToString();
                    break;
                case "Uuml":
                    str = Convert.ToChar(220).ToString();
                    break;
                case "uuml":
                    str = Convert.ToChar(252).ToString();
                    break;
                case "weierp":
                    str = Convert.ToChar(8472).ToString();
                    break;
                case "Xi":
                    str = Convert.ToChar(926).ToString();
                    break;
                case "xi":
                    str = Convert.ToChar(958).ToString();
                    break;
                case "yacute":
                    str = Convert.ToChar(253).ToString();
                    break;
                case "Yacute":
                    str = Convert.ToChar(221).ToString();
                    break;
                case "yen":
                    str = Convert.ToChar(165).ToString();
                    break;
                case "Yuml":
                    str = Convert.ToChar(376).ToString();
                    break;
                case "yuml":
                    str = Convert.ToChar((int)Byte.MaxValue).ToString();
                    break;
                case "zeta":
                    str = Convert.ToChar(950).ToString();
                    break;
                case "Zeta":
                    str = Convert.ToChar(918).ToString();
                    break;
                case "zwj":
                    str = Convert.ToChar(8205).ToString();
                    break;
                case "zwnj":
                    str = Convert.ToChar(8204).ToString();
                    break;
            }
            return str;
        }
    }
}
