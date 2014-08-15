using System;
using System.Threading;
using System.Windows.Forms;
using Exceptionless.Dependency;
using Exceptionless.Enrichments;
using Exceptionless.Logging;

namespace Exceptionless.Windows.Extensions {
    public static class ExceptionlessClientExtensions {
        private static ThreadExceptionEventHandler _onApplicationThreadException;

        public static void RegisterApplicationThreadExceptionHandler(this ExceptionlessClient client) {
            if (_onApplicationThreadException == null)
                _onApplicationThreadException = (sender, args) => {
                    var contextData = new ContextData();
                    contextData.MarkAsUnhandledError();
                    contextData.SetSubmissionMethod("ApplicationThreadException");

                    args.Exception.ToExceptionless(contextData, client).Submit();
                };

            try {
                Application.ThreadException -= _onApplicationThreadException;
                Application.ThreadException += _onApplicationThreadException;
            } catch (Exception ex) {
                client.Configuration.Resolver.GetLog().Error(typeof(ExceptionlessClientExtensions), ex, "An error occurred while wiring up to the unobserved task exception event.");
            }
        }

        public static void UnregisterApplicationThreadExceptionHandler(this ExceptionlessClient client) {
            if (_onApplicationThreadException == null)
                return;

            Application.ThreadException -= _onApplicationThreadException;
            _onApplicationThreadException = null;
        }
    }
}