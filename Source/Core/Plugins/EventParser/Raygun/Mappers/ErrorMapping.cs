
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Mappers {
    public static class ErrorMapping {
        public static Core.Models.Data.Error Map(RaygunModel raygunModel) {
            var raygunError = raygunModel?.Details?.Error;

            if (raygunError == null) {
                return null;
            }

            return MapError(raygunError);
        }

        private static Core.Models.Data.Error MapError(Error error) {
            var exceptionlessError = new Core.Models.Data.Error();

            exceptionlessError.TargetMethod = MapTargetMethod(error);
            exceptionlessError.Message = error.Message;
            exceptionlessError.Type = error.ClassName;
            exceptionlessError.StackTrace = MapStackFrames(error.StackTrace);

            exceptionlessError.Inner = MapInnerError(error.InnerError);

            return exceptionlessError;
        }

        private static Core.Models.Data.InnerError MapInnerError(Error error) {
            if (error == null) {
                return null;
            }

            var exceptionlessInnerError = new Core.Models.Data.InnerError();

            exceptionlessInnerError.TargetMethod = MapTargetMethod(error);
            exceptionlessInnerError.Message = error.Message;
            exceptionlessInnerError.Type = error.ClassName;
            exceptionlessInnerError.StackTrace = MapStackFrames(error.StackTrace);

            exceptionlessInnerError.Inner = MapInnerError(error.InnerError);

            return exceptionlessInnerError;
        }

        private static Core.Models.StackFrameCollection MapStackFrames(IList<StackTrace> stackTraces) {
            var stackFrameCollection = new Core.Models.StackFrameCollection();

            // raygun seems to put one fake element when there's no stacktrace at all. Try to detect this fake element
            // and return an empty collection instead.
            if (stackFrameCollection.Count == 1 && stackFrameCollection.First().FileName == "none") {
                return stackFrameCollection;
            }

            foreach (var stackTrace in stackTraces) {
                var stackFrame = new Core.Models.Data.StackFrame();

                stackFrame.Name = stackTrace.MethodName;
                stackFrame.Name = GetMethodNameWithoutParameter(stackTrace.MethodName);
                stackFrame.LineNumber = stackTrace.LineNumber;
                stackFrame.Column = stackTrace.ColumnNumber;
                stackFrame.FileName = stackTrace.FileName;
                stackFrame.ModuleId = -1;

                var declaringInfo = GetDeclaringInfo(stackTrace.ClassName);
                stackFrame.DeclaringType = declaringInfo.Item1;
                stackFrame.DeclaringNamespace = declaringInfo.Item2;

                stackFrameCollection.Add(stackFrame);
            }

            return stackFrameCollection;
        }

        private static Core.Models.Data.Method MapTargetMethod(Error error) {
            var firstStackError = error.StackTrace?.FirstOrDefault();

            if (firstStackError == null) {
                return null;
            }

            var method = new Core.Models.Data.Method();
            method.ModuleId = -1;
            method.Name = GetMethodNameWithoutParameter(firstStackError.MethodName);

            var declaringInfo = GetDeclaringInfo(firstStackError.ClassName);
            method.DeclaringType = declaringInfo.Item1;
            method.DeclaringNamespace = declaringInfo.Item2;

            return method;            
        }

        private static Tuple<string, string> GetDeclaringInfo(string className) {
            if (string.IsNullOrEmpty(className)) {
                return new Tuple<string, string>(null, null);
            }

            string declaringType = null;
            string declaringNamespace = null;
            int lastDotIndex = className.LastIndexOf('.');

            if (lastDotIndex == -1) {
                declaringType = className;
            } else {
                declaringType = className.Substring(lastDotIndex + 1);
                declaringNamespace = className.Substring(0, lastDotIndex);
            }

            // raygun seems to put the word "(unknown)" when there's no declaringType. We catch that and we put
            // null instead.
            if (declaringType == "(unknown)") {
                declaringType = null;
            }

            return new Tuple<string, string>(declaringType, declaringNamespace);
        }

        private static string GetMethodNameWithoutParameter(string methodName) {
            if (string.IsNullOrEmpty(methodName)) {
                return null;
            }

            string methodNameWithoutParameter = null;
            int firstBracketIndex = methodName.IndexOf('(');

            if (firstBracketIndex == -1) {
                methodNameWithoutParameter = methodName;
            } else {
                methodNameWithoutParameter = methodName.Substring(0, firstBracketIndex);
            }

            return methodNameWithoutParameter;
        }
    }
}
