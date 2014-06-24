using System;
using System.Web;
using Exceptionless.Dependency;
using Exceptionless.Enrichments;
using Exceptionless.Logging;

namespace Exceptionless.Web.Extensions {
    public static class ExceptionlessClientExtensions {
        private static EventHandler _onHttpApplicationError;

        public static void RegisterHttpApplicationErrorHandler(this ExceptionlessClient client, HttpApplication app) {
            if (_onHttpApplicationError == null)
                _onHttpApplicationError = (sender, args) => {
                    if (HttpContext.Current == null)
                        return;

                    Exception exception = HttpContext.Current.Server.GetLastError();
                    if (exception == null)
                        return;

                    var contextData = new ContextData();
                    contextData.MarkAsUnhandledError();
                    contextData.SetSubmissionMethod("HttpApplicationError");
                    contextData.Add("HttpContext", HttpContext.Current.ToWrapped());

                    exception.ToExceptionless(contextData, client).Submit();
                };

            try {
                app.Error -= _onHttpApplicationError;
                app.Error += _onHttpApplicationError;
            } catch (Exception ex) {
                client.Configuration.Resolver.GetLog().Error(typeof(ExceptionlessClientExtensions), ex, "An error occurred while wiring up to the unobserved task exception event.");
            }
        }

        public static void UnregisterHttpApplicationErrorExceptionHandler(this ExceptionlessClient client, HttpApplication app) {
            if (_onHttpApplicationError == null)
                return;

            app.Error -= _onHttpApplicationError;
            _onHttpApplicationError = null;
        }
    }
}