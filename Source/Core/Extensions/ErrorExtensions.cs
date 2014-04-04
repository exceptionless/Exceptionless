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
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using Newtonsoft.Json;

namespace Exceptionless.Core.Extensions {
    public static class ErrorExtensions {
        public static Error ToProjectLocalTime(this Error error, Project project) {
            if (error == null)
                return null;

            error.OccurrenceDate = TimeZoneInfo.ConvertTime(error.OccurrenceDate, project.DefaultTimeZone());
            return error;
        }

        public static Error ToProjectLocalTime(this Error error, IProjectRepository repository) {
            if (error == null)
                return null;

            return error.ToProjectLocalTime(repository.GetByIdCached(error.ProjectId));
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

        public static Tuple<ErrorInfo, Method> GetStackingTarget(this Error error) {
            ErrorInfo targetError = error;
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

        public static ErrorInfo GetInnermostError(this Error error) {
            if (error == null)
                throw new ArgumentNullException("error");

            ErrorInfo current = error;
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
    }
}