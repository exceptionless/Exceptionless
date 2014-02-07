#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Globalization;
using System.Text;

namespace Exceptionless.Net {
    internal class ContentTypeHeader {
        public string ContentType { get; set; }
        public string Charset { get; set; }

        public ContentTypeHeader(string header) {
            Parse(header);
        }

        public void Parse(string header) {
            header = header.ToLower(CultureInfo.InvariantCulture);

            string[] parsedList = header.Split(new[] { ';', '=', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            char state = 'm';
            foreach (string item in parsedList) {
                switch (state) {
                    case 'm':
                        ContentType = item;
                        state = ' ';
                        break;
                    case 'c':
                        Charset = item;
                        state = ' ';
                        break;
                }

                if (item == "charset")
                    state = 'c';
            }
        }

        public override string ToString() {
            var sb = new StringBuilder();
            sb.Append(ContentType);
            if (!String.IsNullOrEmpty(Charset))
                sb.AppendFormat("; charset={0}", Charset);

            return sb.ToString();
        }

        public static implicit operator string(ContentTypeHeader header) {
            return header.ToString();
        }

        public static implicit operator ContentTypeHeader(string header) {
            return new ContentTypeHeader(header);
        }
    }
}