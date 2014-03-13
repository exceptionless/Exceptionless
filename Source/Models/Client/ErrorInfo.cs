#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Exceptionless.Models {
    public class ErrorInfo {
        public ErrorInfo() {
            ExtendedData = new ExtendedDataDictionary();
            StackTrace = new StackFrameCollection();
        }

        /// <summary>
        /// The error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The error type.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// The error code.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Extended data entries for this error.
        /// </summary>
        public ExtendedDataDictionary ExtendedData { get; set; }

        /// <summary>
        /// An inner (nested) error.
        /// </summary>
        public ErrorInfo Inner { get; set; }

        /// <summary>
        /// The stack trace for the error.
        /// </summary>
        public StackFrameCollection StackTrace { get; set; }

        /// <summary>
        /// The target method.
        /// </summary>
        public Method TargetMethod { get; set; }

        public override string ToString() {
            var sb = new StringBuilder(2048);
            AppendError(sb);
            return sb.ToString().Trim();
        }

        public string ToHtmlString() {
            var sb = new StringBuilder(4096);
            AppendError(sb, true, traceIndentValue: " ");
            return sb.ToString().Trim();
        }

        public override int GetHashCode() {
#if !PFX_LEGACY_3_5
            return String.Concat(Type, Code, Inner != null, String.Join("", StackTrace.Select(st => st.FullName))).GetHashCode();
#else
            return String.Concat(Type, Code, Inner != null, String.Join("", StackTrace.Select(st => st.FullName).ToArray())).GetHashCode();
#endif
        }

        internal void AppendError(StringBuilder sb, bool html = false, string traceIndentValue = "   ") {
            var exList = new List<ErrorInfo>();
            ErrorInfo currentEx = this;
            if (html)
                sb.Append("<span class=\"ex-header\">");
            while (currentEx != null) {
                if (html)
                    sb.Append("<span class=\"ex-type\">");
#if !PFX_LEGACY_3_5 && !PORTABLE40
                sb.Append(html ? WebUtility.HtmlEncode(currentEx.Type) : currentEx.Type);
#else
                sb.Append(currentEx.Type);
#endif
                if (html)
                    sb.Append("</span>");

                if (!String.IsNullOrEmpty(currentEx.Message)) {
                    if (html)
                        sb.Append("<span class=\"ex-message\">");
#if !PFX_LEGACY_3_5 && !PORTABLE40
                    sb.Append(": ").Append(html ? WebUtility.HtmlEncode(currentEx.Message) : currentEx.Message);
#else
                    sb.Append(": ").Append(currentEx.Message);
#endif
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
            foreach (ErrorInfo ex in exList) {
                if (ex.StackTrace != null && ex.StackTrace.Count > 0) {
                    ex.StackTrace.AppendStackFrames(sb, true, linkFilePath: html, traceIndentValue: traceIndentValue);

                    if (exList.Count > 1)
                        sb.Append(traceIndentValue).AppendLine("--- End of inner exception stack trace ---");
                }
            }
        }
    }
}