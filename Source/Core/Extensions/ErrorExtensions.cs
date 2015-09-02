using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Extensions {
    public static class ErrorExtensions {
        public static StackingTarget GetStackingTarget(this Error error) {
            if (error == null)
                return null;

            InnerError targetError = error;
            while (targetError != null) {
                StackFrame m = targetError.StackTrace.FirstOrDefault(st => st.IsSignatureTarget);
                if (m != null)
                    return new StackingTarget {
                        Error = targetError,
                        Method = m
                    };

                if (targetError.TargetMethod != null && targetError.TargetMethod.IsSignatureTarget)
                    return new StackingTarget {
                        Error = targetError,
                        Method = targetError.TargetMethod
                    };

                targetError = targetError.Inner;
            }

            // fallback to default
            InnerError defaultError = error.GetInnermostError();
            Method defaultMethod = defaultError.StackTrace != null ? defaultError.StackTrace.FirstOrDefault() : null;
            if (defaultMethod == null && error.StackTrace != null) {
                defaultMethod = error.StackTrace.FirstOrDefault();
                defaultError = error;
            }

            return new StackingTarget {
                Error = defaultError,
                Method = defaultMethod
            };
        }

        public static StackingTarget GetStackingTarget(this Event ev) {
            var error = ev.GetError();
            return error != null ? error.GetStackingTarget() : null;
        }

        public static InnerError GetInnermostError(this InnerError error) {
            if (error == null)
                throw new ArgumentNullException("error");

            InnerError current = error;
            while (current.Inner != null)
                current = current.Inner;

            return current;
        }

        public static string ToExceptionStackString(this Error error) {
            var sb = new StringBuilder(2048);
            AppendError(error, sb);
            return sb.ToString().Trim();
        }

        public static string ToHtmlExceptionStackString(this Error error) {
            var sb = new StringBuilder(4096);
            AppendError(error, sb, true, traceIndentValue: " ");
            return sb.ToString().Trim();
        }

        internal static void AppendError(Error error, StringBuilder sb, bool html = false, string traceIndentValue = "   ") {
            var exList = new List<InnerError>();
            InnerError currentEx = error;
            if (html)
                sb.Append("<span class=\"ex-header\">");
            while (currentEx != null) {
                if (html)
                    sb.Append("<span class=\"ex-type\">");
                sb.Append(html ? currentEx.Type.HtmlEntityEncode() : currentEx.Type);
                if (html)
                    sb.Append("</span>");

                if (!String.IsNullOrEmpty(currentEx.Message)) {
                    if (html)
                        sb.Append("<span class=\"ex-message\">");
                    sb.Append(": ").Append(html ? currentEx.Message.HtmlEntityEncode() : currentEx.Message);
                    if (html)
                        sb.Append("</span>");
                }

                if (currentEx.Inner != null) {
                    if (html)
                        sb.Append("</span><span class=\"ex-header\"><span class=\"ex-innersep\">");
                    sb.Append(" ---> ");
                    if (html)
                        sb.Append("</span>");
                }

                exList.Add(currentEx);
                currentEx = currentEx.Inner;
            }
            if (html)
                sb.Append("</span>");
            else
                sb.AppendLine();

            exList.Reverse();
            foreach (InnerError ex in exList) {
                if (ex.StackTrace != null && ex.StackTrace.Count > 0) {
                    StackFrameCollectionExtensions.AppendStackFrames(ex.StackTrace, sb, true, linkFilePath: html, traceIndentValue: traceIndentValue);

                    if (exList.Count > 1)
                        sb.Append(traceIndentValue).AppendLine("--- End of inner exception stack trace ---");
                }
            }
        }
    }

    public class StackingTarget {
        public Method Method { get; set; }
        public InnerError Error { get; set; }
    }
}