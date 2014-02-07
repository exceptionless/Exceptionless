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
using Exceptionless.Core.Extensions;
using Exceptionless.Models;

namespace Exceptionless.Core.Utility {
    public class ErrorSignature {
        private readonly HashSet<string> _userNamespaces = new HashSet<string>();
        private readonly HashSet<string> _userCommonMethods = new HashSet<string>();
        private static readonly string[] _defaultNonUserNamespaces = new[] { "System", "Microsoft" };
        // TODO: Add support for user public key token on signed assemblies

        public ErrorSignature(ErrorInfo error, IEnumerable<string> userNamespaces = null, IEnumerable<string> userCommonMethods = null, bool emptyNamespaceIsUserMethod = true, bool shouldFlagSignatureTarget = true) {
            if (error == null)
                throw new ArgumentNullException("error");

            Error = error;

            _userNamespaces = userNamespaces == null
                ? new HashSet<string>()
                : new HashSet<string>(userNamespaces);

            _userCommonMethods = userCommonMethods == null
                ? new HashSet<string>()
                : new HashSet<string>(userCommonMethods);

            EmptyNamespaceIsUserMethod = emptyNamespaceIsUserMethod;

            SignatureInfo = new ConfigurationDictionary();
            ShouldFlagSignatureTarget = shouldFlagSignatureTarget;

            Parse();
        }

        public string[] UserNamespaces { get { return _userNamespaces.ToArray(); } }

        public string[] UserCommonMethods { get { return _userCommonMethods.ToArray(); } }

        public ErrorInfo Error { get; private set; }

        public bool EmptyNamespaceIsUserMethod { get; private set; }

        public ConfigurationDictionary SignatureInfo { get; private set; }

        public string SignatureHash { get; private set; }

        public bool IsUser { get; private set; }
        public bool ShouldFlagSignatureTarget { get; private set; }

        private void Parse() {
            SignatureInfo.Clear();

            // start at the inner most exception and work our way out until we find a user method
            ErrorInfo current = Error;
            var errorStack = new List<ErrorInfo> {
                current
            };
            while (current.Inner != null) {
                current = current.Inner;
                errorStack.Add(current);
            }

            errorStack.Reverse();

            // reset all flags before we figure out which method to tag as the new target.
            if (ShouldFlagSignatureTarget)
                errorStack.ForEach(es => es.StackTrace.ForEach(st => st.IsSignatureTarget = false));

            foreach (ErrorInfo e in errorStack) {
                StackFrameCollection stackTrace = e.StackTrace;
                if (stackTrace == null)
                    continue;

                foreach (StackFrame stackFrame in stackTrace.Where(IsUserFrame)) {
                    SignatureInfo.Add("ExceptionType", e.Type);
                    SignatureInfo.Add("Method", GetStackFrameSignature(stackFrame));
                    if (ShouldFlagSignatureTarget)
                        stackFrame.IsSignatureTarget = true;
                    AddSpecialCaseDetails(e);
                    UpdateInfo(true);
                    return;
                }
            }

            // we haven't found a user method yet, try some alternatives with the inner most error
            ErrorInfo innerMostError = errorStack[0];

            if (innerMostError.TargetMethod != null) {
                // use the target method if it exists
                SignatureInfo.Add("ExceptionType", innerMostError.Type);
                SignatureInfo.Add("Method", GetStackFrameSignature(innerMostError.TargetMethod));
                if (ShouldFlagSignatureTarget)
                    innerMostError.TargetMethod.IsSignatureTarget = true;
                AddSpecialCaseDetails(innerMostError);
            } else if (innerMostError.StackTrace != null && innerMostError.StackTrace.Count > 0) {
                // use the topmost stack frame
                SignatureInfo.Add("ExceptionType", innerMostError.Type);
                SignatureInfo.Add("Method", GetStackFrameSignature(innerMostError.StackTrace[0]));
                if (ShouldFlagSignatureTarget)
                    innerMostError.StackTrace[0].IsSignatureTarget = true;
                AddSpecialCaseDetails(innerMostError);
            } else {
                // all else failed, use the type and message
                SignatureInfo.Add("ExceptionType", innerMostError.Type);
                SignatureInfo.Add("Message", innerMostError.Message);
                AddSpecialCaseDetails(innerMostError);
            }

            UpdateInfo(false);
        }

        private void UpdateInfo(bool isUser) {
            IsUser = isUser;
            RecalculateHash();
        }

        public void RecalculateHash() {
            SignatureHash = SignatureInfo.Values.ToSHA1();
        }

        private string GetStackFrameSignature(Method method) {
            var builder = new StringBuilder(255);

            if (method == null)
                return builder.ToString();

            builder.Append(method.Signature);

            return builder.ToString();
        }

        private bool IsUserFrame(StackFrame frame) {
            if (frame == null)
                throw new ArgumentNullException("frame");

            if (frame.Name == null)
                return false;

            // Assume user method if no namespace
            bool isEmptyNamespaceMethod = EmptyNamespaceIsUserMethod && frame.DeclaringNamespace.IsNullOrEmpty();
            if (!isEmptyNamespaceMethod) {
                bool isUserNamespace = IsUserNamespace(frame.DeclaringTypeFullName);
                if (!isUserNamespace)
                    return false;
            }

            return !UserCommonMethods.Any(frame.Signature.Contains);
        }

        private bool IsUserNamespace(string fullName) {
            // if no user namespaces were set, return any non-system namespace as true
            if (UserNamespaces == null || _userNamespaces.Count == 0)
                return !_defaultNonUserNamespaces.Any(fullName.StartsWith);

            return UserNamespaces.Any(fullName.StartsWith);
        }

        private void AddSpecialCaseDetails(ErrorInfo error) {
            if (!error.ExtendedData.ContainsKey(ExtendedDataDictionary.EXCEPTION_INFO_KEY))
                return;

            var extraProperties = error.ExtendedData.GetValue<Dictionary<string, object>>(ExtendedDataDictionary.EXCEPTION_INFO_KEY);
            if (extraProperties == null)
                return;

            if (extraProperties.ContainsKey("Number"))
                SignatureInfo.Add("Number", extraProperties["Number"].ToString());

            if (extraProperties.ContainsKey("ErrorCode"))
                SignatureInfo.Add("ErrorCode", extraProperties["ErrorCode"].ToString());
        }
    }
}