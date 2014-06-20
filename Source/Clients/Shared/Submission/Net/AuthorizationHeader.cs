#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Exceptionless.Submission.Net {
    public class AuthorizationHeader {
        public AuthorizationHeader() {
            Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public AuthorizationHeader(string headerText) : this() {
            Parse(headerText);
        }

        public string Scheme { get; set; }
        public IDictionary<string, string> Parameters { get; private set; }

        private string _parameterText;

        public string ParameterText { get { return _parameterText; } set { _parameterText = value; } }

        public void Parse(string header) {
            var buffer = new StringBuilder();
            var reader = new StringReader(header);
            var parameterText = new StringBuilder();

            bool withinQuotes = false;

            do {
                var c = (char)reader.Read();

                if (!String.IsNullOrEmpty(Scheme))
                    parameterText.Append(c);

                if (c == '"') {
                    withinQuotes = !withinQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(c) && buffer.Length == 0 && !withinQuotes)
                    continue;

                if ((c == ',' || char.IsWhiteSpace(c)) && buffer.Length > 0 && !withinQuotes) {
                    // end of token                    
                    ReadToken(buffer);
                    continue;
                }

                buffer.Append(c);
            } while (reader.Peek() != -1);

            ReadToken(buffer);

            _parameterText = parameterText.ToString();
        }

        private void ReadToken(StringBuilder buffer) {
            if (buffer.Length == 0)
                return;

            string text = buffer.ToString();
            if (String.IsNullOrEmpty(Scheme)) {
                Scheme = text.Trim();
                buffer.Length = 0;
                return;
            }

            string[] parts = text.Split('=');
            if (parts.Length == 2)
                Parameters[parts[0]] = parts[1];

            buffer.Length = 0;
        }

        public override string ToString() {
            var buffer = new StringBuilder();
            buffer.Append(Scheme);
            buffer.Append(" ");

            bool haveParam = false;
            foreach (var p in Parameters) {
                if (haveParam)
                    buffer.Append(",");

                buffer.Append(p.Key);
                buffer.Append("=");
                buffer.Append("\"");
                buffer.Append(p.Value);
                buffer.Append("\"");

                haveParam = true;
            }

            if (!haveParam && !String.IsNullOrEmpty(ParameterText))
                buffer.Append(ParameterText);

            return buffer.ToString();
        }
    }
}