using System;
using System.Text;

namespace CodeSmith.Core.Extensions {
    public static class StringBuilderExtensions {
        public static StringBuilder AppendFormatLine(this StringBuilder sb, String format, params object[] args) {
            sb.AppendFormat(format, args);
            sb.AppendLine();

            return sb;
        }
    }
}
