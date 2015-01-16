using System;
using System.Web.Http.ExceptionHandling;

namespace Exceptionless.Api.Utility {
    public class NLogExceptionLogger : ExceptionLogger {
        public override void Log(ExceptionLoggerContext context) {
            NLog.Fluent.Log.Error().Exception(context.Exception).Message("Unhandled error: {0}", context.Exception.Message).Write();
        }
    }
}
