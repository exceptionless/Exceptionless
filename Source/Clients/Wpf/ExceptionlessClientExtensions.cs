using System;
using System.Threading;
using System.Windows.Threading;
using Exceptionless.Dependency;
using Exceptionless.Enrichments;
using Exceptionless.Logging;

namespace Exceptionless.Wpf.Extensions {
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
                System.Windows.Forms.Application.ThreadException -= _onApplicationThreadException;
                System.Windows.Forms.Application.ThreadException += _onApplicationThreadException;
            } catch (Exception ex) {
                client.Configuration.Resolver.GetLog().Error(typeof(ExceptionlessClientExtensions), ex, "An error occurred while wiring up to the unobserved task exception event.");
            }
        }

        public static void UnregisterApplicationThreadExceptionHandler(this ExceptionlessClient client) {
            if (_onApplicationThreadException == null)
                return;

            System.Windows.Forms.Application.ThreadException -= _onApplicationThreadException;
            _onApplicationThreadException = null;
        }

        private static DispatcherUnhandledExceptionEventHandler _onApplicationDispatcherUnhandledException;

        public static void RegisterApplicationDispatcherUnhandledExceptionHandler(this ExceptionlessClient client) {
            if (System.Windows.Application.Current == null)
                return;
            
            if (_onApplicationDispatcherUnhandledException == null)
                _onApplicationDispatcherUnhandledException = (sender, args) => {
                    var contextData = new ContextData();
                    contextData.MarkAsUnhandledError();
                    contextData.SetSubmissionMethod("DispatcherUnhandledException");

                    args.Exception.ToExceptionless(contextData, client).Submit();
                    args.Handled = true;
                };

            try {
                System.Windows.Application.Current.DispatcherUnhandledException -= _onApplicationDispatcherUnhandledException;
                System.Windows.Application.Current.DispatcherUnhandledException += _onApplicationDispatcherUnhandledException;
            } catch (Exception ex) {
                client.Configuration.Resolver.GetLog().Error(typeof(ExceptionlessClientExtensions), ex, "An error occurred while wiring up to the unobserved task exception event.");
            }
        }

        public static void UnregisterApplicationDispatcherUnhandledExceptionHandler(this ExceptionlessClient client) {
            if (_onApplicationDispatcherUnhandledException == null)
                return;

            if (System.Windows.Application.Current != null)
                System.Windows.Application.Current.DispatcherUnhandledException -= _onApplicationDispatcherUnhandledException;
            
            _onApplicationDispatcherUnhandledException = null;
        }
    }
}