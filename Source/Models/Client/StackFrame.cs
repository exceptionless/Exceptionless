#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Text;

namespace Exceptionless.Models {
    public class StackFrame : Method {
        public string FileName { get; set; }
        public int LineNumber { get; set; }
        public int Column { get; set; }

        public override string ToString() {
            var sb = new StringBuilder(255);
            AppendStackFrame(sb, String.Empty, traceIndentValue: String.Empty);
            return sb.ToString();
        }

        internal void AppendStackFrame(StringBuilder sb, string methodPrefix = "at ", bool appendNewLine = false, bool includeOffsets = false, bool includeColumn = false, bool linkFilePath = false, string traceIndentValue = "   ") {
            if (!String.IsNullOrEmpty(traceIndentValue))
                sb.Append(traceIndentValue);

            if (!String.IsNullOrEmpty(methodPrefix))
                sb.Append(methodPrefix);

            if (String.IsNullOrEmpty(Name)) {
                sb.Append("<null>");

                if (appendNewLine)
                    sb.Append(Environment.NewLine);

                return;
            }

            AppendMethod(sb);

            if (includeOffsets && (ExtendedData.ContainsKey("ILOffset") || ExtendedData.ContainsKey("NativeOffset")))
                sb.AppendFormat(" at offset {0}", ExtendedData["ILOffset"] ?? ExtendedData["NativeOffset"]);

            if (!String.IsNullOrEmpty(FileName)) {
                sb.Append(" in ");
                if (!linkFilePath)
                    sb.Append(FileName);
                else {
                    Uri uri;
                    if (Uri.TryCreate(FileName, UriKind.Absolute, out uri))
                        sb.AppendFormat("<a href=\"").Append(uri.AbsoluteUri).Append("\" target=\"_blank\">").Append(FileName).Append("</a>");
                    else
                        sb.Append(FileName);
                }

                if (LineNumber > 0)
                    sb.Append(":line ").Append(LineNumber);

                if (includeColumn && Column > 0)
                    sb.Append(":col ").Append(Column);
            }

            if (appendNewLine)
                sb.Append(Environment.NewLine);
        }
    }
}