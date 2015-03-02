using System;
using System.Text;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Extensions {
    public static class StackFrameCollectionExtensions {
        public static string ToExceptionStackString(this StackFrameCollection stackFrames) {
            var sb = new StringBuilder(255);
            AppendStackFrames(stackFrames, sb);
            return sb.ToString();
        }

        internal static void AppendStackFrames(StackFrameCollection stackFrames, StringBuilder sb, bool appendNewLine = false, string methodPrefix = "at ", bool linkFilePath = false, string traceIndentValue = "   ") {
            bool first = true;
            foreach (StackFrame frame in stackFrames) {
                if (String.IsNullOrEmpty(frame.Name))
                    continue;

                if (first)
                    first = false;
                else
                    sb.Append(Environment.NewLine);

                StackFrameExtensions.AppendStackFrame(frame, sb, methodPrefix, linkFilePath: linkFilePath, traceIndentValue: traceIndentValue);
            }

            if (appendNewLine)
                sb.Append(Environment.NewLine);
        }

    }
}
