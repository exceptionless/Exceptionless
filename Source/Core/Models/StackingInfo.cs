#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Utility;
using Exceptionless.Models;

namespace Exceptionless.Core.Models {
    public class StackingInfo {
        public StackingInfo(Error error, ErrorSignatureFactory errorSignatureFactory) {
            if (error == null)
                throw new ArgumentNullException("error");

            ErrorInfo innerMostError = error.GetInnermostError();
            Method defaultMethod = innerMostError.StackTrace != null ? innerMostError.StackTrace.FirstOrDefault() : null;
            if (defaultMethod == null && error.StackTrace != null)
                defaultMethod = error.StackTrace.FirstOrDefault();

            Tuple<ErrorInfo, Method> st = error.GetStackingTarget();

            // If we can't find the info, try doing a new signature to mark the target.
            if (st == null) {
                errorSignatureFactory.GetSignature(error);
                st = error.GetStackingTarget();
            }

            Error = st != null ? st.Item1 ?? error : error.GetInnermostError();
            Method = st != null ? st.Item2 : defaultMethod;

            if (error.Code == "404" && error.RequestInfo != null && !String.IsNullOrEmpty(error.RequestInfo.Path)) {
                Is404 = true;
                Path = error.RequestInfo.Path;
            }
        }

        public string Message { get { return Error != null && !String.IsNullOrWhiteSpace(Error.Message) ? Error.Message : "(None)"; } }
        public string FullTypeName { get { return Error != null && !String.IsNullOrWhiteSpace(Error.Type) ? Error.Type : "(None)"; } }
        public string MethodName { get { return Method != null && !String.IsNullOrWhiteSpace(Method.FullName) ? Method.FullName : "(None)"; } }
        public bool Is404 { get; private set; }
        public string Path { get; private set; }
        public ErrorInfo Error { get; private set; }
        public Method Method { get; private set; }
    }
}