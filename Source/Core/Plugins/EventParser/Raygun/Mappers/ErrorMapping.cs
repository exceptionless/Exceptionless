using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Plugins.EventParser.Raygun.Models;

namespace Exceptionless.Core.Plugins.EventParser.Raygun.Mappers {
    public static class ErrorMapping {
        public static Core.Models.Data.Error Map(RaygunModel model) {
            var error = model?.Details?.Error;
            if (error == null)
                return null;

            return MapError(error);
        }

        private static Core.Models.Data.Error MapError(Error sourceError) {
            var error = new Core.Models.Data.Error {
                TargetMethod = MapTargetMethod(sourceError),
                Message = sourceError.Message,
                Type = sourceError.ClassName,
                StackTrace = MapStackFrames(sourceError.StackTrace),
                Inner = MapInnerError(sourceError.InnerError)
            };

            if (sourceError.Data != null)
                error.Data.AddRange(sourceError.Data);

            return error;
        }

        private static Core.Models.Data.InnerError MapInnerError(Error error) {
            if (error?.StackTrace == null)
                return null;

            var innerError = new Core.Models.Data.InnerError {
                TargetMethod = MapTargetMethod(error),
                Message = error.Message,
                Type = error.ClassName,
                StackTrace = MapStackFrames(error.StackTrace),
                Inner = MapInnerError(error.InnerError)
            };

            if (error.Data != null)
                innerError.Data.AddRange(error.Data);

            return innerError;
        }

        private static Core.Models.StackFrameCollection MapStackFrames(IList<StackTrace> stackTraces) {
            var frames = new Core.Models.StackFrameCollection();

            // raygun seems to put one fake element when there's no stacktrace at all. Try to detect this fake element
            // and return an empty collection instead.
            if (stackTraces.Count == 1 && stackTraces.First().FileName == "none")
                return frames;

            foreach (var stackTrace in stackTraces) {
                var di = GetDeclaringInfo(stackTrace.ClassName);
                var frame = new Core.Models.Data.StackFrame {
                    DeclaringType = di.Item1,
                    DeclaringNamespace = di.Item2,
                    Name = GetMethodNameWithoutParameter(stackTrace.MethodName),
                    LineNumber = stackTrace.LineNumber,
                    Column = stackTrace.ColumnNumber,
                    FileName = stackTrace.FileName,
                    ModuleId = -1
                };
                
                // TODO Fill in generics and parameter info.
                frames.Add(frame);
            }

            return frames;
        }

        private static Core.Models.Data.Method MapTargetMethod(Error error) {
            var firstFrame = error.StackTrace?.FirstOrDefault();
            if (firstFrame == null)
                return null;

            // TODO Fill in generics and parameter info.
            var di = GetDeclaringInfo(firstFrame.ClassName);
            return new Core.Models.Data.Method {
                DeclaringType = di.Item1,
                DeclaringNamespace = di.Item2,
                Name = GetMethodNameWithoutParameter(firstFrame.MethodName),
                ModuleId = -1
            };      
        }

        private static Tuple<string, string> GetDeclaringInfo(string className) {
            if (String.IsNullOrEmpty(className))
                return new Tuple<string, string>(null, null);

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
            if (declaringType == "(unknown)")
                declaringType = null;

            return new Tuple<string, string>(declaringType, declaringNamespace);
        }

        private static string GetMethodNameWithoutParameter(string methodName) {
            if (String.IsNullOrEmpty(methodName))
                return null;

            string methodNameWithoutParameter;
            int firstBracketIndex = methodName.IndexOf('(');
            if (firstBracketIndex == -1)
                methodNameWithoutParameter = methodName;
            else
                methodNameWithoutParameter = methodName.Substring(0, firstBracketIndex);

            return methodNameWithoutParameter;
        }
    }
}
