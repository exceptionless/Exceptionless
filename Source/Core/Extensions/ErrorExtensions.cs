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
using System.Text;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Newtonsoft.Json;

namespace Exceptionless.Core.Extensions {
    public static class ErrorExtensions {
        public static Event ToProjectLocalTime(this Event data, Project project) {
            if (data == null)
                return null;

            data.Date = TimeZoneInfo.ConvertTime(data.Date, project.DefaultTimeZone());
            return data;
        }

        public static Event ToProjectLocalTime(this Event data, IProjectRepository repository) {
            if (data == null)
                return null;

            return data.ToProjectLocalTime(repository.GetByIdCached(data.ProjectId));
        }

        public static T GetValue<T>(this DataDictionary extendedData, string key) {
            if (!extendedData.ContainsKey(key))
                throw new KeyNotFoundException(String.Format("Key \"{0}\" not found in the dictionary.", key));

            object data = extendedData[key];
            if (data is T)
                return (T)data;

            if (data is string) {
                try {
                    return JsonConvert.DeserializeObject<T>((string)data);
                } catch {}
            }

            try {
                return data.ToType<T>();
            } catch {}

            return default(T);
        }

        public static Tuple<Error, Method> GetStackingTarget(this Error error) {
            Error targetError = error;
            while (targetError != null) {
                StackFrame m = targetError.StackTrace.FirstOrDefault(st => st.IsSignatureTarget);
                if (m != null)
                    return Tuple.Create(targetError, m as Method);

                if (targetError.TargetMethod != null && targetError.TargetMethod.IsSignatureTarget)
                    return Tuple.Create(targetError, targetError.TargetMethod);

                targetError = targetError.Inner;
            }

            return null;
        }

        public static Error GetInnermostError(this Error error) {
            if (error == null)
                throw new ArgumentNullException("error");

            Error current = error;
            while (current.Inner != null)
                current = current.Inner;

            return current;
        }

        public static StackingInfo GetStackingInfo(this Error error, ErrorSignatureFactory errorSignatureFactory = null) {
            if (errorSignatureFactory == null)
                errorSignatureFactory = new ErrorSignatureFactory();

            return error == null ? null : new StackingInfo(error, errorSignatureFactory);
        }

        public static bool Is404(this Error error) {
            if (error == null)
                return false;

            return error.Code == "404";
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
            var exList = new List<Error>();
            Error currentEx = error;
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
            foreach (Error ex in exList) {
                if (ex.StackTrace != null && ex.StackTrace.Count > 0) {
                    StackFrameCollectionExtensions.AppendStackFrames(ex.StackTrace, sb, true, linkFilePath: html, traceIndentValue: traceIndentValue);

                    if (exList.Count > 1)
                        sb.Append(traceIndentValue).AppendLine("--- End of inner exception stack trace ---");
                }
            }
        }
    }
}