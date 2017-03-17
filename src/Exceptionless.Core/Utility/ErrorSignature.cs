using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Utility {
    public class ErrorSignature {
        private readonly HashSet<string> _userNamespaces;
        private readonly HashSet<string> _userCommonMethods;
        private static readonly string[] _defaultNonUserNamespaces = { "System", "Microsoft" };
        // TODO: Add support for user public key token on signed assemblies

        public ErrorSignature(Error error, IEnumerable<string> userNamespaces = null, IEnumerable<string> userCommonMethods = null, bool emptyNamespaceIsUserMethod = true, bool shouldFlagSignatureTarget = true) {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            Error = error;

            _userNamespaces = userNamespaces == null
                ? new HashSet<string>()
                : new HashSet<string>(userNamespaces);

            _userCommonMethods = userCommonMethods == null
                ? new HashSet<string>()
                : new HashSet<string>(userCommonMethods);

            EmptyNamespaceIsUserMethod = emptyNamespaceIsUserMethod;

            SignatureInfo = new SettingsDictionary();
            ShouldFlagSignatureTarget = shouldFlagSignatureTarget;

            Parse();
        }

        public string[] UserNamespaces => _userNamespaces.ToArray();

        public string[] UserCommonMethods => _userCommonMethods.ToArray();

        public Error Error { get; private set; }

        public bool EmptyNamespaceIsUserMethod { get; private set; }

        public SettingsDictionary SignatureInfo { get; private set; }

        public string SignatureHash { get; private set; }

        public bool IsUser { get; private set; }
        public bool ShouldFlagSignatureTarget { get; private set; }

        private void Parse() {
            SignatureInfo.Clear();

            // start at the inner most exception and work our way out until we find a user method
            InnerError current = Error;
            var errorStack = new List<InnerError> { current };
            while (current.Inner != null) {
                current = current.Inner;
                errorStack.Add(current);
            }

            errorStack.Reverse();

            // reset all flags before we figure out which method to tag as the new target.
            if (ShouldFlagSignatureTarget)
                errorStack.ForEach(es => es.StackTrace.ForEach(st => st.IsSignatureTarget = false));

            foreach (InnerError e in errorStack) {
                StackFrameCollection stackTrace = e.StackTrace;
                if (stackTrace == null)
                    continue;

                foreach (StackFrame stackFrame in stackTrace.Where(IsUserFrame)) {
                    SignatureInfo.AddItemIfNotEmpty("ExceptionType", e.Type);
                    SignatureInfo.AddItemIfNotEmpty("Method", GetStackFrameSignature(stackFrame));
                    if (ShouldFlagSignatureTarget)
                        stackFrame.IsSignatureTarget = true;
                    AddSpecialCaseDetails(e);
                    UpdateInfo(true);
                    return;
                }
            }

            // We haven't found a user method yet, try some alternatives with the inner most error.
            InnerError innerMostError = errorStack[0];

            if (innerMostError.TargetMethod != null) {
                // Use the target method if it exists.
                SignatureInfo.AddItemIfNotEmpty("ExceptionType", innerMostError.Type);
                SignatureInfo.AddItemIfNotEmpty("Method", GetStackFrameSignature(innerMostError.TargetMethod));
                if (ShouldFlagSignatureTarget)
                    innerMostError.TargetMethod.IsSignatureTarget = true;
            } else if (innerMostError.StackTrace != null && innerMostError.StackTrace.Count > 0) {
                // Use the topmost stack frame.
                SignatureInfo.AddItemIfNotEmpty("ExceptionType", innerMostError.Type);
                SignatureInfo.AddItemIfNotEmpty("Method", GetStackFrameSignature(innerMostError.StackTrace[0]));
                if (ShouldFlagSignatureTarget)
                    innerMostError.StackTrace[0].IsSignatureTarget = true;
            } else {
                // All else failed, use the type.
                SignatureInfo.AddItemIfNotEmpty("ExceptionType", innerMostError.Type);
            }

            AddSpecialCaseDetails(innerMostError);
            if (SignatureInfo.Count == 0)
                SignatureInfo.Add("NoStackingInformation", Guid.NewGuid().ToString());

            UpdateInfo(false);
        }

        private void UpdateInfo(bool isUser) {
            IsUser = isUser;
            RecalculateHash();
        }

        public void RecalculateHash() {
            SignatureHash = SignatureInfo.Values.Any(v => v != null) ? SignatureInfo.Values.ToSHA1() : null;
        }

        private string GetStackFrameSignature(Method method) {
            var builder = new StringBuilder(255);

            if (method == null)
                return builder.ToString();

            builder.Append(method.GetSignature());

            return builder.ToString();
        }

        private bool IsUserFrame(StackFrame frame) {
            if (frame == null)
                throw new ArgumentNullException(nameof(frame));

            if (frame.Name == null)
                return false;

            // Assume user method if no namespace
            bool isEmptyNamespaceMethod = EmptyNamespaceIsUserMethod && frame.DeclaringNamespace.IsNullOrEmpty();
            if (!isEmptyNamespaceMethod) {
                bool isUserNamespace = IsUserNamespace(frame.DeclaringNamespace);
                if (!isUserNamespace)
                    return false;
            }

            return !UserCommonMethods.Any(frame.GetSignature().Contains);
        }

        private bool IsUserNamespace(string ns) {
            if (String.IsNullOrEmpty(ns))
                return false;

            // if no user namespaces were set, return any non-system namespace as true
            if (UserNamespaces == null || _userNamespaces.Count == 0)
                return !_defaultNonUserNamespaces.Any(ns.StartsWith);

            return UserNamespaces.Any(ns.StartsWith);
        }

        private void AddSpecialCaseDetails(InnerError error) {
            if (!error.Data.ContainsKey(Error.KnownDataKeys.ExtraProperties))
                return;

            var extraProperties = error.Data.GetValue<Dictionary<string, object>>(Error.KnownDataKeys.ExtraProperties);
            if (extraProperties == null) {
                error.Data.Remove(Error.KnownDataKeys.ExtraProperties);
                return;
            }

            if (extraProperties.TryGetValue("Number", out object value))
                SignatureInfo.Add("Number", value.ToString());

            if (extraProperties.TryGetValue("ErrorCode", out value))
                SignatureInfo.Add("ErrorCode", value.ToString());
        }
    }
}