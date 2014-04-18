using System;
using System.Linq;
using Exceptionless.Dependency;
using Exceptionless.Logging;
using Exceptionless.Models;

namespace Exceptionless.Plugins {
    public static class ExceptionlessPluginManager {
        /// <summary>
        /// Is called after the error object is created and can be used to add any essential information to the error report
        /// and set the client info.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="data">Event that was created.</param>
        /// <param name="exception">The exception that the error report was created from.</param>
        public static void AfterCreated(ExceptionlessPluginContext context, Event data, Exception exception) {
            foreach (IExceptionlessPlugin plugin in context.Client.Configuration.Plugins.ToList()) {
                try {
                    plugin.AfterCreated(context, data, exception);
                } catch (Exception ex) {
                    var log = context.Client.Configuration.Resolver.GetLog();
                    log.FormattedError(typeof(ExceptionlessPluginManager), ex, "An error occurred while running {0}.AfterCreated(): {1}", plugin.GetType().FullName, ex.Message);
                }
            }
        }

        /// <summary>
        /// Add any additional non-essential information to the error report.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="data">Event that was created.</param>
        public static void AddDefaultInformation(ExceptionlessPluginContext context, Event data) {
            foreach (IExceptionlessPlugin plugin in context.Client.Configuration.Plugins.ToList()) {
                try {
                    plugin.AddDefaultInformation(context, data);
                } catch (Exception ex) {
                    var log = context.Client.Configuration.Resolver.GetLog();
                    log.FormattedError(typeof(ExceptionlessPluginManager), ex, "An error occurred while running {0}.AddDefaultInformation(): {1}", plugin.GetType().FullName, ex.Message);
                }
            }
        }

        /// <summary>
        /// Shows the submission UI for GUI clients and returns true if the error should be sent to the server.
        /// </summary>
        /// <param name="context">Context information.</param>
        /// <param name="data">Event that was created.</param>
        /// <returns>True if the error should be sent to the server.</returns>
        public static bool ShowUnhandledErrorSubmissionUI(ExceptionlessPluginContext context, Event data) {
            foreach (IExceptionlessPlugin plugin in context.Client.Configuration.Plugins.ToList()) {
                try {
                    if(!plugin.SupportsShowingUnhandledErrorSubmissionUI)
                        continue;

                    return plugin.ShowUnhandledErrorSubmissionUI(context, data);
                } catch (Exception ex) {
                    var log = context.Client.Configuration.Resolver.GetLog();
                    log.FormattedError(typeof(ExceptionlessPluginManager), ex, "An error occurred while running {0}.ShowUnhandledErrorSubmissionUI(): {1}", plugin.GetType().FullName, ex.Message);
                }
            }

            return false;
        }
    }
}
