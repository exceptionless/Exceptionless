using System;
using System.Linq;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Extensions {
    public static class ErrorExtensions {
        public static StackingTarget GetStackingTarget(this Error error) {
            if (error == null)
                return null;

            InnerError targetError = error;
            while (targetError != null) {
                var frame = targetError.StackTrace?.FirstOrDefault(st => st.IsSignatureTarget);
                if (frame != null)
                    return new StackingTarget {
                        Error = targetError,
                        Method = frame
                    };

                if (targetError.TargetMethod != null && targetError.TargetMethod.IsSignatureTarget)
                    return new StackingTarget {
                        Error = targetError,
                        Method = targetError.TargetMethod
                    };

                targetError = targetError.Inner;
            }

            // fallback to default
            var defaultError = error.GetInnermostError();
            var defaultMethod = defaultError.StackTrace?.FirstOrDefault();
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
            return error?.GetStackingTarget();
        }

        public static InnerError GetInnermostError(this InnerError error) {
            if (error == null)
                throw new ArgumentNullException(nameof(error));

            var current = error;
            while (current.Inner != null)
                current = current.Inner;

            return current;
        }
    }

    public class StackingTarget {
        public Method Method { get; set; }
        public InnerError Error { get; set; }
    }
}