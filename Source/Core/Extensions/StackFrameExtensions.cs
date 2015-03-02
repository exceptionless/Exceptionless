using System;
using System.Text;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Extensions {
    public static class StackFrameExtensions {
        public static string ToExceptionStackString(this StackFrame frame) {
            var sb = new StringBuilder(255);
            AppendStackFrame(frame, sb, String.Empty, traceIndentValue: String.Empty);
            return sb.ToString();
        }

        internal static void AppendStackFrame(StackFrame frame, StringBuilder sb, string methodPrefix = "at ", bool appendNewLine = false, bool includeOffsets = false, bool includeColumn = false, bool linkFilePath = false, string traceIndentValue = "   ") {
            if (!String.IsNullOrEmpty(traceIndentValue))
                sb.Append(traceIndentValue);

            if (!String.IsNullOrEmpty(methodPrefix))
                sb.Append(methodPrefix);

            if (String.IsNullOrEmpty(frame.Name)) {
                sb.Append("<null>");

                if (appendNewLine)
                    sb.Append(Environment.NewLine);

                return;
            }

            MethodExtensions.AppendMethod(frame, sb);

            if (includeOffsets && (frame.Data.ContainsKey("ILOffset") || frame.Data.ContainsKey("NativeOffset")))
                sb.AppendFormat(" at offset {0}", frame.Data["ILOffset"] ?? frame.Data["NativeOffset"]);

            if (!String.IsNullOrEmpty(frame.FileName)) {
                sb.Append(" in ");
                if (!linkFilePath)
                    sb.Append(frame.FileName);
                else {
                    Uri uri;
                    if (Uri.TryCreate(frame.FileName, UriKind.Absolute, out uri))
                        sb.AppendFormat("<a href=\"").Append(uri.AbsoluteUri).Append("\" target=\"_blank\">").Append(frame.FileName).Append("</a>");
                    else
                        sb.Append(frame.FileName);
                }

                if (frame.LineNumber > 0)
                    sb.Append(":line ").Append(frame.LineNumber);

                if (includeColumn && frame.Column > 0)
                    sb.Append(":col ").Append(frame.Column);
            }

            if (appendNewLine)
                sb.Append(Environment.NewLine);
        }
    }
}
