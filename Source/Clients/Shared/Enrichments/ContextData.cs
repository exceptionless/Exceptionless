using System;
using System.Collections.Generic;

namespace Exceptionless.Enrichments {
    public class ContextData : Dictionary<string, object> {
        public ContextData() : base(StringComparer.OrdinalIgnoreCase) { }
        public ContextData(IDictionary<string, object> dictionary) : base(dictionary, StringComparer.OrdinalIgnoreCase) { }

        public void SetException(Exception ex) {
            this[KnownKeys.Exception] = ex;
        }

        public bool HasException() {
            return ContainsKey(KnownKeys.Exception);
        }

        public Exception GetException() {
            if (!HasException())
                return null;

            return  this[KnownKeys.Exception] as Exception;
        }

        public void SetUnhandled() {
            this[KnownKeys.IsUnhandled] = true;
        }

        public bool IsUnhandled {
            get {
                if (!ContainsKey(KnownKeys.IsUnhandled))
                    return false;

                if (!(this[KnownKeys.IsUnhandled] is bool))
                    return false;
                
                return (bool)this[KnownKeys.IsUnhandled];
            }
        }

        public void SetSubmissionMethod(string method) {
            this[KnownKeys.SubmissionMethod] = method;
        }

        public string GetSubmissionMethod() {
            if (!ContainsKey(KnownKeys.SubmissionMethod))
                return null;

            return this[KnownKeys.SubmissionMethod] as string;
        }

        public static class KnownKeys {
            public const string IsUnhandled = "@@_IsUnhandled";
            public const string SubmissionMethod = "@@_SubmissionMethod";
            public const string Exception = "@@_Exception";
        }
    }
}
