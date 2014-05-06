using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.CSharp;
#if !SILVERLIGHT
using Microsoft.VisualBasic;
#if !EMBEDDED
using CodeSmith.Core.Text;
using CodeSmith.Core.Collections;
#endif
using System.Web;
#endif

#if !EMBEDDED
namespace CodeSmith.Core.Extensions {
    public
#else
namespace Exceptionless.Extensions {
    public
#endif
    static class StringExtensions
    {
        private static readonly Regex _splitNameRegex = new Regex(@"[\W_]+");
        private static readonly Regex _properWordRegex = new Regex(@"([A-Z][a-z]*)|([0-9]+)");
        private static readonly Regex _identifierRegex = new Regex(@"[^\p{Ll}\p{Lu}\p{Lt}\p{Lo}\p{Nd}\p{Nl}\p{Mn}\p{Mc}\p{Cf}\p{Pc}\p{Lm}]");
        private static readonly Regex _htmlIdentifierRegex = new Regex(@"[^A-Za-z0-9-_:\.]");

        /// <summary>
        /// Truncates the specified text.
        /// </summary>
        /// <param name="text">The text to truncate.</param>
        /// <param name="keep">The number of characters to keep.</param>
        /// <returns>A truncate String.</returns>
        public static string Truncate(this string text, int keep)
        {
            if (String.IsNullOrEmpty(text))
                return String.Empty;

            string buffer = NormalizeLineEndings(text);
            if (buffer.Length <= keep)
                return buffer;

            return String.Concat(buffer.Substring(0, keep - 3), "...");
        }

        public static string Truncate(this string text, int length, string ellipsis, bool keepFullWordAtEnd) {
            if (String.IsNullOrEmpty(text))
                return String.Empty;

            if (text.Length < length)
                return text;

            text = text.Substring(0, length);

            if (keepFullWordAtEnd && text.LastIndexOf(' ') > 0)
                text = text.Substring(0, text.LastIndexOf(' '));

            return String.Format("{0}{1}", text, ellipsis);
        }

        public static string TruncatePath(this string path, int length) {
            // NOTE: This code was taken from: http://stackoverflow.com/questions/1764204/how-to-display-abbreviated-path-names-in-net
            if (String.IsNullOrEmpty(path))
                return String.Empty;

            //simple guards
            if (path.Length <= length)
                return path;

            const string ellipsisChars = "...";
            int ellipsisLength = ellipsisChars.Length;
            if (length <= ellipsisLength)
                return ellipsisChars;

            //alternate between taking a section from the start (firstPart) or the path and the end (lastPart)
            bool isFirstPartsTurn = true; //drive letter has first priority, so start with that and see what else there is room for

            //variables for accumulating the first and last parts of the final shortened path
            string firstPart = "";
            string lastPart = "";
            //keeping track of how many first/last parts have already been added to the shortened path
            int firstPartsUsed = 0;
            int lastPartsUsed = 0;

            string[] pathParts = path.Split(Path.DirectorySeparatorChar);
            for (int i = 0; i < pathParts.Length; i++) {
                if (isFirstPartsTurn)
                {
                    string partToAdd = String.Format("{0}{1}", pathParts[firstPartsUsed], Path.DirectorySeparatorChar);
                    if ((firstPart.Length + lastPart.Length + partToAdd.Length + ellipsisLength) > length)
                        break;

                    firstPart = firstPart + partToAdd;
                    if (partToAdd != Path.DirectorySeparatorChar.ToString())
                        isFirstPartsTurn = false;

                    firstPartsUsed++;
                }
                else
                {
                    int index = pathParts.Length - lastPartsUsed - 1; //-1 because of length vs. zero-based indexing
                    string partToAdd = String.Format("{0}{1}", Path.DirectorySeparatorChar, pathParts[index]);
                    if ((firstPart.Length + lastPart.Length + partToAdd.Length + ellipsisLength) > length)
                        break;

                    lastPart = partToAdd + lastPart;
                    if (partToAdd != Path.DirectorySeparatorChar.ToString())
                        isFirstPartsTurn = true;

                    lastPartsUsed++;
                }
            }

            if (lastPart == String.Empty) {
                // the filename (and root path) in itself was longer than maxLength, shorten it
                lastPart = pathParts[pathParts.Length - 1];
                lastPart = lastPart.Substring(lastPart.Length + ellipsisLength + firstPart.Length - length, length - ellipsisLength - firstPart.Length);
            }

            return String.Format("{0}{1}{2}", firstPart, ellipsisChars, lastPart);
        }

        /// <summary>
        /// Calculates a hash code for the string that is guaranteed to be stable across .NET versions.
        /// </summary>
        /// <param name="value">The string to hash.</param>
        /// <returns>The hash code</returns>
        public static int GetStableHashCode(this string value)
        {
            int h = 0;
            int n = 0;

            for (; n < value.Length - 1; n += 2)
            {
                h = unchecked((h << 5) - h + value[n]);
                h = unchecked((h << 5) - h + value[n + 1]);
            }

            if (n < value.Length)
                h = unchecked((h << 5) - h + value[n]);

            return h;
        }

        public static string NormalizeLineEndings(this string text, string lineEnding = null)
        {
            if (String.IsNullOrEmpty(lineEnding))
                lineEnding = Environment.NewLine;

            text = text.Replace("\r\n", "\n");
            if (lineEnding != "\n")
                text = text.Replace("\r\n", lineEnding);

            return text;
        }

        /// <summary>
        /// Indicates whether the specified String object is null or an empty string
        /// </summary>
        /// <param name="item">A String reference</param>
        /// <returns>
        ///     <c>true</c> if is null or empty; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsNullOrEmpty(this string item)
        {
            return String.IsNullOrEmpty(item);
        }

        /// <summary>
        /// Indicates whether a specified string is null, empty, or consists only of white-space characters
        /// </summary>
        /// <param name="item">A String reference</param>
        /// <returns>
        ///      <c>true</c> if is null or empty; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsNullOrWhiteSpace(this string item) {
            return String.IsNullOrEmpty(item) || item.All(Char.IsWhiteSpace);
        }

        public static string AsNullIfEmpty(this string items)
        {
            return String.IsNullOrEmpty(items) ? null : items;
        }

        public static string AsNullIfWhiteSpace(this string items)
        {
            return IsNullOrWhiteSpace(items) ? null : items;
        }

        /// <summary>
        /// Determines if the string looks like JSON content.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsJson(this string value) {
            return value.GetJsonType() != JsonType.None;
        }

        public static JsonType GetJsonType(this string value) {
            if (String.IsNullOrEmpty(value))
                return JsonType.None;

            for (int i = 0; i < value.Length; i++) {
                if (Char.IsWhiteSpace(value[i]))
                    continue;

                if (value[i] == '{')
                    return JsonType.Object;

                if (value[i] == '[')
                    return JsonType.Array;

                break;
            }

            return JsonType.None;
        }

        /// <summary>
        /// Formats a string without throwing a FormatException.
        /// </summary>
        /// <param name="format">A String reference</param>
        /// <param name="args">Object parameters that should be formatted</param>
        /// <returns>Formatted string if no error is thrown, else reutrns the format parameter.</returns>
        public static string SafeFormat(this string format, params object[] args)
        {
            try {
                return String.Format(format, args);
            }
            catch (FormatException) {
                return format;
            }
        }

#if !EMBEDDED
        /// <summary>
        /// Uses the string as a format
        /// </summary>
        /// <param name="format">A String reference</param>
        /// <param name="args">Object parameters that should be formatted</param>
        /// <returns>Formatted string</returns>
        public static string FormatWith(this string format, params object[] args)
        {
            format.Require("format").NotNullOrEmpty();

            return String.Format(format, args);
        }

        /// <summary>
        /// Applies a format to the item
        /// </summary>
        /// <param name="item">Item to format</param>
        /// <param name="format">Format string</param>
        /// <returns>Formatted string</returns>
        public static string FormatAs(this object item, string format)
        {
            format.Require("format").NotNullOrEmpty();

            return String.Format(format, item);
        }
#endif 

#if !EMBEDDED && !SILVERLIGHT
        /// <summary>
        /// Uses the string as a format.
        /// </summary>
        /// <param name="format">A String reference</param>
        /// <param name="source">Object that should be formatted</param>
        /// <returns>Formatted string</returns>
        public static string FormatName(this string format, object source)
        {
            format.Require("format").NotNullOrEmpty();

            return NameFormatter.Format(format, source);
        }

        /// <summary>
        /// Applies a format to the item
        /// </summary>
        /// <param name="item">Item to format</param>
        /// <param name="format">Format string</param>
        /// <returns>Formatted string</returns>
        public static string FormatNameAs(this object item, string format)
        {
            format.Require("format").NotNullOrEmpty();

            return NameFormatter.Format(format, item);
        }
#endif

        /// <summary>
        /// Creates a string from the sequence by concatenating the result
        /// of the specified string selector function for each element.
        /// </summary>
        public static string ToConcatenatedString<T>(this IEnumerable<T> values, Func<T, string> stringSelector) {
            return values.ToConcatenatedString(stringSelector, String.Empty);
        }

        /// <summary>
        /// Creates a string from the sequence by concatenating the result
        /// of the specified string selector function for each element.
        /// </summary>
        ///<param name="action"></param>
        ///<param name="separator">The string which separates each concatenated item.</param>
        ///<param name="values"></param>
        public static string ToConcatenatedString<T>(this IEnumerable<T> values, Func<T, string> action, string separator) {
            var sb = new StringBuilder();
            foreach (var item in values) {
                if (sb.Length > 0)
                    sb.Append(separator);

                sb.Append(action(item));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts an IEnumerable of values to a delimited String.
        /// </summary>
        /// <typeparam name="T">
        /// The type of objects to delimit.
        /// </typeparam>
        /// <param name="values">
        /// The IEnumerable string values to convert.
        /// </param>
        /// <param name="delimiter">
        /// The delimiter.
        /// </param>
        /// <returns>
        /// A delimited string of the values.
        /// </returns>
        public static string ToDelimitedString<T>(this IEnumerable<T> values, string delimiter)
        {
            var sb = new StringBuilder();
            foreach (var i in values)
            {
                if (sb.Length > 0)
                    sb.Append(delimiter);
                sb.Append(i.ToString());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts an IEnumerable of values to a delimited String.
        /// </summary>
        /// <param name="values">The IEnumerable string values to convert.</param>
        /// <returns>A delimited string of the values.</returns>
        public static string ToDelimitedString(this IEnumerable<string> values)
        {
            return ToDelimitedString(values, ",");
        }

        /// <summary>
        /// Converts an IEnumerable of values to a delimited String.
        /// </summary>
        /// <param name="values">The IEnumerable string values to convert.</param>
        /// <param name="delimiter">The delimiter.</param>
        /// <returns>A delimited string of the values.</returns>
        public static string ToDelimitedString(this IEnumerable<string> values, string delimiter)
        {
            var sb = new StringBuilder();
            foreach (var i in values)
            {
                if (sb.Length > 0)
                    sb.Append(delimiter);
                sb.Append(i);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Converts a string to use camelCase.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The to camel case. </returns>
        public static string ToCamelCase(this string value)
        {
            if (String.IsNullOrEmpty(value))
                return value;

            string output = ToPascalCase(value);
            if (output.Length > 2)
                return Char.ToLower(output[0]) + output.Substring(1);

            return output.ToLower();
        }

        /// <summary>
        /// Converts a string to use PascalCase.
        /// </summary>
        /// <param name="value">Text to convert</param>
        /// <returns>The string</returns>
        public static string ToTitleCase(this string value) {
            // if the value is mixed case, we assume it's that way for a reason.
            if (value.IsMixedCase())
                return value;

            return Char.ToUpper(value[0]) + value.Substring(1).ToLower();
        }

        /// <summary>
        /// Converts a string to use PascalCase.
        /// </summary>
        /// <param name="value">Text to convert</param>
        /// <returns>The string</returns>
        public static string ToPascalCase(this string value)
        {
            return value.ToPascalCase(_splitNameRegex);
        }

#if !SILVERLIGHT

        // TODO: WE need to cache this value as it is expensive...
        //private static readonly Lazy<CSharpCodeProvider> _csharpCodeProvider = new Lazy<CSharpCodeProvider>();

        /// <summary>
        /// Converts a string to an C# escaped literal String.
        /// </summary>
        /// <param name="value">Text to escape</param>
        /// <returns>The escaped string</returns>
        public static string ToCSharpLiteral(this string value)
        {
            var writer = new StringWriter();
            new CSharpCodeProvider().GenerateCodeFromExpression(new CodePrimitiveExpression(value), writer, null);
            return writer.GetStringBuilder().ToString();
        }

        /// <summary>
        /// Converts a string to a valid C# identifier String.
        /// </summary>
        /// <param name="value">Text to convert.</param>
        /// <returns>The valid identifier</returns>
        public static string ToCSharpIdentifier(this string value)
        {
            string identifier = _identifierRegex.Replace(value, String.Empty);
            identifier = new CSharpCodeProvider().CreateEscapedIdentifier(identifier);

            return identifier;
        }
        
        // TODO: WE need to cache this value as it is expensive...
        //private static readonly Lazy<VBCodeProvider> _vbCodeProvider = new Lazy<VBCodeProvider>();

        /// <summary>
        /// Converts a string to an VB escaped literal String.
        /// </summary>
        /// <param name="value">Text to escape</param>
        /// <returns>The escaped string</returns>
        public static string ToVbLiteral(this string value)
        {
            var writer = new StringWriter();
            new VBCodeProvider().GenerateCodeFromExpression(new CodePrimitiveExpression(value), writer, null);
            return writer.GetStringBuilder().ToString();
        }

        /// <summary>
        /// Converts a string to a valid C# identifier String.
        /// </summary>
        /// <param name="value">Text to convert.</param>
        /// <returns>The valid identifier</returns>
        public static string ToVbIdentifier(this string value)
        {
            string identifier = _identifierRegex.Replace(value, String.Empty);
            identifier = new VBCodeProvider().CreateEscapedIdentifier(identifier);

            return identifier;
        }
#endif

        /// <summary>
        /// Converts a string to a valid HTML identifier String.
        /// </summary>
        /// <param name="value">Text to convert.</param>
        /// <returns>The valid identifier</returns>
        public static string ToHtmlIdentifier(this string value)
        {
            string identifier = _htmlIdentifierRegex.Replace(value, String.Empty);
            if (identifier.StartsWith("__"))
                identifier = "_" + identifier;

            return identifier;
        }

        /// <summary>
        /// Converts a string to a valid .NET identifier String.
        /// </summary>
        /// <param name="value">Text to convert.</param>
        /// <returns>The valid identifier</returns>
        public static string ToIdentifier(this string value)
        {
            if (String.IsNullOrEmpty(value))
                throw new ArgumentNullException("value");

            string identifier = _identifierRegex.Replace(value, String.Empty);
            if (identifier.StartsWith("__") || Char.IsDigit(identifier, 0))
                identifier = "_" + identifier;

            return identifier;
        }

        /// <summary>
        /// Checks to see if a string is a valid .NET identifier String.
        /// </summary>
        /// <param name="value">String identifier to check.</param>
        /// <returns>Returns true if value is a valid identifier</returns>
        public static bool IsValidIdentifier(this string value)
        {
            if (value.IsNullOrWhiteSpace())
                return false;

            return !_identifierRegex.IsMatch(value) && (Char.IsLetter(value[0]) || (value[0] == '_' && value.Length > 1));
        }

        /// <summary>
        /// Checks to see if a string is a valid .NET namespace.
        /// </summary>
        /// <param name="value">String identifier to check.</param>
        /// <returns>Returns true if value is a valid namespace.</returns>
        public static bool IsValidNamespace(this string value)
        {
            if (value.IsNullOrWhiteSpace())
                return false;

            return !value.Split('.').Any(v => !v.IsValidIdentifier());
        }

        /// <summary>
        /// Replicates the given String.
        /// </summary>
        /// <param name="value">Text to replicate</param>
        /// <param name="count">Times to replicate</param>
        /// <returns>The replicated string</returns>
        public static string Replicate(this string value, int count)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < count; i++)
                builder.Append(value);

            return builder.ToString();
        }

        /// <summary>
        /// Converts a string to use PascalCase.
        /// </summary>
        /// <param name="value">Text to convert</param>
        /// <param name="splitRegex">Regular Expression to split words on.</param>
        /// <returns>The string</returns>
        public static string ToPascalCase(this string value, Regex splitRegex)
        {
            if (String.IsNullOrEmpty(value))
                return value;

            var mixedCase = value.IsMixedCase();
            var names = splitRegex.Split(value);
            var output = new StringBuilder();

            if (names.Length > 1)
            {
                foreach (string name in names)
                {
                    if (name.Length > 1)
                    {
                        output.Append(Char.ToUpper(name[0]));
                        output.Append(mixedCase ? name.Substring(1) : name.Substring(1).ToLower());
                    }
                    else
                    {
                        output.Append(name);
                    }
                }
            }
            else if (value.Length > 1)
            {
                output.Append(Char.ToUpper(value[0]));
                output.Append(mixedCase ? value.Substring(1) : value.Substring(1).ToLower());
            }
            else
            {
                output.Append(value.ToUpper());
            }

            return output.ToString();
        }

        /// <summary>
        /// Takes a NameIdentifier and spaces it out into words "Name Identifier".
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The string</returns>
        public static string[] ToWords(this string value)
        {
            var words = new List<string>();
            value = ToPascalCase(value);

            MatchCollection wordMatches = _properWordRegex.Matches(value);
            foreach (Match word in wordMatches)
            {
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

        private static readonly Regex _whitespace = new Regex(@"\s");

        /// <summary>
        /// Removes all whitespace from a String.
        /// </summary>
        /// <param name="s">Initial String.</param>
        /// <returns>String with no whitespace.</returns>
        public static string RemoveWhiteSpace(this string s)
        {
            return _whitespace.Replace(s, String.Empty);
        }

        public static string ReplaceFirst(this string s, string find, string replace)
        {
            var i = s.IndexOf(find);
            if (i >= 0)
            {
                var pre = s.Substring(0, i);
                var post = s.Substring(i + find.Length);
                return String.Concat(pre, replace, post);
            }

            return s;
        }

        public static void AppendFormatLine(this StringBuilder sb, string format, params string[] args)
        {
            sb.AppendLine(String.Format(format, args));
        }

#if !SILVERLIGHT && !EMBEDDED

        private const string _paraBreak = "\n\n";
        private const string _link = "<a href=\"{0}\">{1}</a>";
        private const string _linkWithRel = "<a href=\"{0}\" rel=\"{1}\">{2}</a>";

        /// <summary>
        /// Returns a copy of this string converted to HTML markup.
        /// </summary>
        public static string ToHtml(this string s) {
            return ToHtml(s, null);
        }

        /// <summary>
        /// Returns a copy of this string converted to HTML markup.
        /// </summary>
        /// <param name="s"> </param>
        /// <param name="rel">If specified, links will have the rel attribute set to this value
        /// attribute</param>
        public static string ToHtml(this string s, string rel) {
            s = s.NormalizeLineEndings("\n");
            var sb = new StringBuilder();

            int pos = 0;
            while (pos < s.Length) {
                // Extract next paragraph
                int start = pos;
                pos = s.IndexOf(_paraBreak, start);
                if (pos < 0)
                    pos = s.Length;
                string para = s.Substring(start, pos - start).Trim();

                // Encode non-empty paragraph
                if (para.Length > 0)
                    EncodeParagraph(para, sb, rel);

                // Skip over paragraph break
                pos += _paraBreak.Length;
            }
            // Return result
            return sb.ToString();
        }

        /// <summary>
        /// Encodes a single paragraph to HTML.
        /// </summary>
        /// <param name="s">Text to encode</param>
        /// <param name="sb">StringBuilder to write results</param>
        /// <param name="rel">If specified, links will have the rel attribute set to this value
        /// attribute</param>
        private static void EncodeParagraph(string s, StringBuilder sb, string rel = null) {
            // Start new paragraph
            sb.AppendLine("<p>");

            // HTML encode text
            s = HttpUtility.HtmlEncode(s);

            // Convert single newlines to <br>
            s = s.Replace("\n", "<br />\r\n");
            
            if (!String.IsNullOrEmpty(rel))
                s = _linkRegex.Replace(s, String.Format(_linkWithRel, "$1", rel, "$1"));
            else
                s = _linkRegex.Replace(s, String.Format(_link, "$1", "$1"));

            // Encode any hyperlinks
            EncodeLinks(s, sb, rel);

            // Close paragraph
            sb.AppendLine("\r\n</p>");
        }

        private static readonly Regex _linkRegex = new Regex(@"\b
                (                       # Capture 1: entire matched URL
                  (?:
                    https?://               # http or https protocol
                    |                       #   or
                    www\d{0,3}[.]           # ""www."", ""www1."", ""www2."" … ""www999.""
                    |                           #   or
                    [a-z0-9.\-]+[.][a-z]{2,4}/  # looks like domain name followed by a slash
                  )
                  (?:                       # One or more:
                    [^\s()<>]+                  # Run of non-space, non-()<>
                    |                           #   or
                    \(([^\s()<>]+|(\([^\s()<>]+\)))*\)  # balanced parens, up to 2 levels
                  )+
                  (?:                       # End with:
                    \(([^\s()<>]+|(\([^\s()<>]+\)))*\)  # balanced parens, up to 2 levels
                    |                               #   or
                    [^\s`!()\[\]{};:'"".,<>?«»“”‘’]        # not a space or one of these punct chars
                  )
                )", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);

        public static MatchCollection GetHyperlinkMatches(this string value) {
            if (String.IsNullOrEmpty(value))
                return null;

            return _linkRegex.Matches(value);
        }

        /// <summary>
        /// Encodes [[URL]] and [[Text][URL]] links to HTML.
        /// </summary>
        /// <param name="s">Text to encode</param>
        /// <param name="sb">StringBuilder to write results</param>
        /// <param name="rel">If specified, links will have the rel attribute set to this value
        /// attribute</param>
        private static void EncodeLinks(string s, StringBuilder sb, string rel) {
            // Parse and encode any hyperlinks
            int pos = 0;
            while (pos < s.Length) {
                // Look for next link
                int start = pos;
                pos = s.IndexOf("[[", pos);
                if (pos < 0)
                    pos = s.Length;
                // Copy text before link
                sb.Append(s.Substring(start, pos - start));
                if (pos < s.Length) {
                    string label, link;

                    start = pos + 2;
                    pos = s.IndexOf("]]", start);
                    if (pos < 0)
                        pos = s.Length;
                    label = s.Substring(start, pos - start);
                    int i = label.IndexOf("][");
                    if (i >= 0) {
                        link = label.Substring(i + 2);
                        label = label.Substring(0, i);
                    } else {
                        link = label;
                    }
                    
                    if (String.IsNullOrEmpty(rel))
                        sb.Append(String.Format(_link, link, label));
                    else
                        sb.Append(String.Format(_linkWithRel, link, rel, label));

                    // Skip over closing "]]"
                    pos += 2;
                }
            }
        }
#endif

        /// <summary>
        /// Creates a string to be used in HTML that won't be automatically turned into a link.
        /// </summary>
        /// <param name="url"></param>
        /// <returns>A </returns>
        public static string ToNonAutoLinkUrl(this string url) {
            if (String.IsNullOrEmpty(url))
                return url;

            int colonIndex = url.IndexOf(':');
            if (colonIndex > 0)
                url = url.Substring(0, colonIndex) + "<span>:</span>" + url.Substring(colonIndex + 1);
            
            int dotIndex = url.LastIndexOf('.');
            if (dotIndex > 0)
                url = url.Substring(0, dotIndex) + "<span>.</span>" + url.Substring(dotIndex + 1);

            return url;
        }

#if !SILVERLIGHT
        #region ReplaceMultiple

        private class ReplaceKey
        {
            public string Key { get; set; }
            public int Length { get; set; }
        }

        public static string ReplaceMultiple(this string s, IDictionary<string, string> replaceMap)
        {
            return s.ReplaceMultiple(replaceMap, false);
        }

        public static string ReplaceMultiple(this string s, IDictionary<string, string> replaceMap, bool isRegexFind)
        {
            var indexes = new SortedDictionary<int, ReplaceKey>();
            foreach (var pair in replaceMap)
            {
                if (isRegexFind)
                    FindMultipleRegex(ref s, pair.Key, indexes);
                else
                    FindMultipleString(ref s, pair.Key, indexes);
            }

            return (indexes.Count > 0)
                ? BuildReplaceMultiple(ref s, indexes, replaceMap)
                : s;
        }

        private static void FindMultipleRegex(ref string s, string find, IDictionary<int, ReplaceKey> indexes)
        {
            var regex = new Regex(find, RegexOptions.IgnoreCase);
            var matches = regex.Matches(s);
            for (int i = 0; i < matches.Count; i++)
            {
                indexes.Add(
                    matches[i].Index, new ReplaceKey { Key = find, Length = matches[i].Length });
            }
        }

        private static void FindMultipleString(ref string s, string find, IDictionary<int, ReplaceKey> indexes)
        {
            int index;
            int start = 0;
            do
            {
                index = s.IndexOf(find, start);
                if (index < 0)
                    continue;
                indexes.Add(
                    index, new ReplaceKey { Key = find, Length = find.Length });
                start = index + find.Length;
            }
            while (index >= 0);
        }

        private static string BuildReplaceMultiple(ref string s, SortedDictionary<int, ReplaceKey> indexes, IDictionary<string, string> replaceMap)
        {
            var sb = new StringBuilder();

            var previous = 0;
            foreach (var pair in indexes)
            {
                sb.Append(s.Substring(previous, pair.Key - previous));
                sb.Append(replaceMap[pair.Value.Key]);
                previous = pair.Key + pair.Value.Length;
            }

            sb.Append(s.Substring(previous));

            return sb.ToString();
        }

        #endregion
#endif
        /// <summary>
        /// Strips NewLines and Tabs
        /// </summary>
        /// <param name="s">The string to strip.</param>
        /// <returns>Stripped String.</returns>
        public static string StripInvisible(this string s)
        {
            return s
                .Replace("\r\n", " ")
                .Replace('\n', ' ')
                .Replace('\t', ' ');
        }

        /// <summary>
        /// Returns true if s contains substring value.
        /// </summary>
        /// <param name="s">Initial value</param>
        /// <param name="value">Substring value</param>
        /// <returns>Boolean</returns>
        public static bool Contains(this string s, string value)
        {
            return s.IndexOf(value) > -1;
        }

        /// <summary>
        /// Returns true if the string is contained in any of the values to match list of strings.
        /// </summary>
        /// <param name="value">The value to look for.</param>
        /// <param name="valuesToMatch">List of string values to iterate through and see if value matches any of them.</param>
        /// <param name="ignoreCase">True to ignore case.</param>
        /// <returns>True if the value is contained.</returns>
        public static bool AnyContains(this string value, IEnumerable<string> valuesToMatch, bool ignoreCase = false) {
            if (!ignoreCase)
                return valuesToMatch.Any(item => item.Contains(value));

            string loweredValue = value.ToLower();
            return valuesToMatch.Any(item => loweredValue.Contains(item.ToLower()));
        }

        /// <summary>
        /// Returns true if the string pattern is matched in any of a list of strings. Asterik wildcards 
        /// can be used at the start and end or both sides of the target strings.
        /// </summary>
        /// <param name="value">The value to look for.</param>
        /// <param name="patternsToMatch">List of string patterns to iterate through and see if value matches any of them.</param>
        /// <param name="ignoreCase">True to ignore case.</param>
        /// <returns>True if the value matches.</returns>
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

        /// <summary>
        /// Returns true if s contains substring value.
        /// </summary>
        /// <param name="s">Initial value</param>
        /// <param name="value">Substring value</param>
        /// <param name="comparison">StringComparison options.</param>
        /// <returns>Boolean</returns>
        public static bool Contains(this string s, string value, StringComparison comparison)
        {
            return s.IndexOf(value, comparison) > -1;
        }

        /// <summary>
        /// Indicates whether a string contains x occurrences of a String. 
        /// </summary>
        /// <param name="s">The string to search.</param>
        /// <param name="value">The string to search for.</param>
        /// <returns>
        ///     <c>true</c> if the string contains at least two occurrences of {value}; otherwise, <c>false</c>.
        /// </returns>
        public static bool ContainsMultiple(this string s, string value)
        {
            return s.ContainsMultiple(value, 2);
        }

        /// <summary>
        /// Indicates whether a string contains x occurrences of a String. 
        /// </summary>
        /// <param name="s">The string to search.</param>
        /// <param name="value">The string to search for.</param>
        /// <param name="count">The number of occurrences to search for.</param>
        /// <returns>
        ///     <c>true</c> if the string contains at least {count} occurrences of {value}; otherwise, <c>false</c>.
        /// </returns>
        public static bool ContainsMultiple(this string s, string value, int count)
        {
            if (count == 0)
                return true;

            int index = s.IndexOf(value);
            if (index > -1)
            {
                return s.Substring(index + 1).ContainsMultiple(value, --count);
            }

            return false;
        }

        public static string[] SplitAndTrim(this string s, params string[] separator)
        {
            if (s.IsNullOrEmpty())
                return new string[0];

            var result = ((separator == null) || (separator.Length == 0))
                ? s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                : s.Split(separator, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < result.Length; i++)
                result[i] = result[i].Trim();

            return result;
        }

        public static string[] SplitAndTrim(this string s, params char[] separator)
        {
            if (s.IsNullOrEmpty())
                return new string[0];

            var result = s.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < result.Length; i++)
                result[i] = result[i].Trim();

            return result;

        }

#if !SILVERLIGHT
        /// <summary>
        /// Convert UTF8 string to ASCII.
        /// </summary>
        /// <param name="s">The UTF8 String.</param>
        /// <returns>The ASCII String.</returns>
        public static string ToASCII(this string s)
        {
            Encoding encoding = Encoding.GetEncoding(
                Encoding.ASCII.EncodingName,
                new EncoderReplacementFallback(String.Empty),
                new DecoderExceptionFallback());

            byte[] inputBytes = Encoding.Convert(
                Encoding.UTF8,
                encoding,
                Encoding.UTF8.GetBytes(s));

            return Encoding.ASCII.GetString(inputBytes);
        }
#endif

        /// <summary>
        /// Do any of the strings contain both uppercase and lowercase characters?
        /// </summary>
        /// <param name="values">String values.</param>
        /// <returns>True if any contain mixed cases.</returns>
        public static bool IsMixedCase(this IEnumerable<string> values)
        {
            foreach (var value in values)
                if (value.IsMixedCase())
                    return true;

            return false;
        }

        /// <summary>
        /// Is the string all lower case characters?
        /// </summary>
        /// <param name="s">The value.</param>
        /// <returns>True if all lower case.</returns>
        public static bool IsAllLowerCase(this string s) {
            if (s.IsNullOrEmpty())
                return false;

            return !s.ContainsUpper();
        }

        /// <summary>
        /// Is the string all upper case characters?
        /// </summary>
        /// <param name="s">The value.</param>
        /// <returns>True if all upper case.</returns>
        public static bool IsAllUpperCase(this string s) {
            if (s.IsNullOrEmpty())
                return false;

            return !s.ContainsLower();
        }

        /// <summary>
        /// Does string contain uppercase characters?
        /// </summary>
        /// <param name="s">The value.</param>
        /// <returns>True if contain upper case.</returns>
        public static bool ContainsUpper(this string s) {
            if (s.IsNullOrEmpty())
                return false;

            return s.ToArray().Any(Char.IsUpper);
        }

        /// <summary>
        /// Does string contain lowercase characters?
        /// </summary>
        /// <param name="s">The value.</param>
        /// <returns>True if contain lower case.</returns>
        public static bool ContainsLower(this string s) {
            if (s.IsNullOrEmpty())
                return false;

            return s.ToArray().Any(Char.IsLower);
        }

        /// <summary>
        /// Does string contain both uppercase and lowercase characters?
        /// </summary>
        /// <param name="s">The value.</param>
        /// <returns>True if contain mixed case.</returns>
        public static bool IsMixedCase(this string s)
        {
            if (s.IsNullOrEmpty())
                return false;

            var containsUpper = false;
            var containsLower = false;

            foreach (var c in s)
            {
                if (Char.IsUpper(c))
                    containsUpper = true;

                if (Char.IsLower(c))
                    containsLower = true;
            }

            return containsLower && containsUpper;
        }

        public static Dictionary<string, string> ParseConfigValues(this string value) {
            var attributes = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            string[] keyValuePairs = value.Split(';');
            foreach (string t in keyValuePairs) {
                string[] keyValuePair = t.Split('=');
                if (keyValuePair.Length == 2)
                    attributes.Add(keyValuePair[0].Trim(), keyValuePair[1].Trim());
                else
                    attributes.Add(t.Trim(), null);
            }

            return attributes;
        }


        public static string ToCommandLineArgument(this string path)
        {
            if (String.IsNullOrEmpty(path))
                return String.Empty;

            return path.Contains(" ") ? String.Format("\"{0}\"", path) : path;
        }

        public static string ToFileExtension(this string path)
        {
            if (String.IsNullOrEmpty(path))
                return String.Empty;

            string extension = Path.GetExtension(path) ?? String.Empty;
            return extension.Length > 0 ? extension.ToLowerInvariant() : String.Empty;
        }

        public static string GetDomainOfUrl(string url) {
            var i = url.IndexOf("://");
            i = i == -1 ? 0 : i + 3;
            var s = url.IndexOf('/', i);
            return s == -1 ? url : url.Substring(0, s + 1);
        }

#if !EMBEDDED && !SILVERLIGHT
        /// <summary>
        /// Parses a person's full name from a single String.
        /// </summary>
        /// <param name="fullName">The person's full name.</param>
        public static PersonNameInfo ParsePersonName(this string fullName) {
            var info = new PersonNameInfo();

            var seperators = new[] { " " };
            var parts = fullName.Split(seperators, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                throw new ArgumentException("Full name parameter must contain at least one part.");

            switch (parts.Length) {
                case 1:
                    // Options:
                    //  1: 0=first

                    info.FirstName = parts[0].ToTitleCase();
                    break;
                case 2:
                    // Options:
                    //  1: 0=salutation, 1=last (Mr. Smith)
                    //  2: 0=last, 1=first (Smith, Eric)
                    //  2: 0=first, 1=last (Eric Smith)

                    if (parts[0].IsSalutation()) { // option 1
                        info.Salutation = parts[0].ToTitleCase();
                        info.LastName = parts[1].ToTitleCase();
                    } else if (parts[0].EndsWith(",")) { // option 2
                        info.LastName = parts[0].TrimEnd(',').ToTitleCase();
                        info.FirstName = parts[1].ToTitleCase();
                    } else { // option 3
                        info.FirstName = parts[0].ToTitleCase();
                        info.LastName = parts[1].ToTitleCase();
                    }
                    break;
                case 3:
                    // Options:
                    //  1: 0=salutation, 1=first, 2=last (Mr. Eric Smith)
                    //  2: 0=last, 1=first, 3=middle (Smith, Eric J.)
                    //  3: 0=first, 1=middle, 2=last (Eric James Smith)
                    //  4: 0=first, 1=last, 3=suffix (Eric Smith Jr.) *ambiguous*

                    if (parts[0].IsSalutation()) { // option 1
                        info.Salutation = parts[0].ToTitleCase();
                        info.FirstName = parts[1].ToTitleCase();
                        info.LastName = parts[2].ToTitleCase();
                    } else if (parts[0].EndsWith(",")) { // option 2
                        info.LastName = parts[0].TrimEnd(',').ToTitleCase();
                        info.FirstName = parts[1].ToTitleCase();
                        info.MiddleName = parts[2].ToTitleCase();
                    } else { // option 3
                        info.FirstName = parts[0].ToTitleCase();
                        info.MiddleName = parts[1].ToTitleCase();
                        info.LastName = parts[2].ToTitleCase();
                    }
                    break;
                case 4:
                    // Options:
                    //  1: 0=salutation, 1=first, 2=middle, 3=last (Mr. Eric J Smith Jr.)
                    //  2: 0=salutation, 1=first, 2=last, 3=suffix (Mr. Eric Smith Jr.) *ambiguous*
                    //  3: 0=first, 1=middle, 2=last, 3=suffix (Eric J Smith Jr.)

                    if (parts[0].IsSalutation()) { // option 1
                        info.Salutation = parts[0].ToTitleCase();
                        info.FirstName = parts[1].ToTitleCase();
                        info.MiddleName = parts[2].ToTitleCase();
                        info.LastName = parts[3].ToTitleCase();
                    } else { // option 3
                        info.FirstName = parts[0].ToTitleCase();
                        info.MiddleName = parts[1].ToTitleCase();
                        info.LastName = parts[2].ToTitleCase();
                        info.Suffix = parts[3].ToTitleCase();
                    }
                    break;
                default:
                    // Options:
                    //  1: 0=salutation, 1=first, 2=last, 3=suffix, 4=???, ...
                    //  2: 0=first, 1=middle, 2=last, 3=???, 4=???, ...

                    if (parts[0].EndsWith(".") && parts[0].IsSalutation()) { // option 1
                        info.Salutation = parts[0].ToTitleCase();
                        info.FirstName = parts[1].ToTitleCase();
                        info.MiddleName = parts[2].ToTitleCase();
                        info.LastName = parts[3].ToTitleCase();
                        info.Suffix = String.Join(" ", parts.AsIndexedEnumerable().Where(p => p.Index > 3).Select(p => p.Value).ToArray());
                    } else { // option 2
                        info.FirstName = parts[0].ToTitleCase();
                        info.MiddleName = parts[1].ToTitleCase();
                        info.LastName = parts[2].ToTitleCase();
                        info.Suffix = String.Join(" ", parts.AsIndexedEnumerable().Where(p => p.Index > 2).Select(p => p.Value).ToArray());
                    }
                    break;
            }

            return info;
        }

        private static readonly string[] _salutations = new[] {
            "MR", "MRS", "MS", "MISS", "DR", "SIR", "MADAM", "SIR", "MONSIEUR", "MADEMOISELLE", "MADAME", "SIRE",
            "COL", "SENOR", "SR", "SENORA", "SRA", "SENORITA", "SRTA", "HERR", "FRAU", "DHR", "HR", "FR",
            "SHRI", "SHRIMATI", "SIGNORE", "SIG", "SIGNORA", "SIG.RA", "PAN", "PANI", "SENHOR", "SENHORA", "SENHORITA",
            "MENEER", "MEVROU", "MEJUFFROU"
        };

        public static bool IsSalutation(this string value) {
            value = value.ToASCII();
            value = value.Trim();
            value = value.TrimEnd('.');

            return _salutations.Any(s => s.ToUpper() == value);
        }
#endif
    }

#if !EMBEDDED
    public class PersonNameInfo {
        public string Salutation { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MiddleName { get; set; }
        public string MiddleInitial {
            get {
                return String.IsNullOrEmpty(MiddleName) ? MiddleName : MiddleName[0].ToString();
            }
        }
        public string Suffix { get; set; }

        public override string ToString() {
            var sb = new StringBuilder();

            if (Salutation.IsNullOrWhiteSpace())
                sb.Append(Salutation).Append(" ");

            if (FirstName.IsNullOrWhiteSpace())
                sb.Append(FirstName).Append(" ");

            if (MiddleName.IsNullOrWhiteSpace())
                sb.Append(MiddleName).Append(" ");

            if (LastName.IsNullOrWhiteSpace())
                sb.Append(LastName).Append(" ");

            if (Suffix.IsNullOrWhiteSpace())
                sb.Append(Suffix).Append(" ");

            return sb.ToString().Trim();
        }
    }
#endif

    public enum JsonType : byte {
        None,
        Object,
        Array
    }
}
