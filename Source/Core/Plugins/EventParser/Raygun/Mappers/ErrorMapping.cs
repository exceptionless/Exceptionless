
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

            foreach (var stackTrace in stackTraces) {
                var stackFrame = new Core.Models.Data.StackFrame();

                stackFrame.Name = stackTrace.MethodName;
                stackFrame.LineNumber = stackTrace.LineNumber;
                stackFrame.Column = stackTrace.ColumnNumber;
                stackFrame.DeclaringType = stackTrace.ClassName;
                stackFrame.FileName = stackTrace.FileName;

                stackFrameCollection.Add(stackFrame);
            }

            return stackFrameCollection;
        }

        private static Core.Models.Data.Method MapTargetMethod(Error error) {
            var firstStackError = error.StackTrace?.FirstOrDefault();

            if (firstStackError == null) {
                return null;
            }

            // TODO separate the namespace portion of the declaringType and put it in the Namespace property.
            // TODO separate the methodname from the parameters and put them in the parameters property.

            var method = new Core.Models.Data.Method();
            method.Name = firstStackError.MethodName;
            method.DeclaringType = firstStackError.ClassName;

            return method;            
        }
    }
}
