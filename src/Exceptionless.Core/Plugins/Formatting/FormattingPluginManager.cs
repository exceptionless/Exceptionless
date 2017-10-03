using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Models;
using Foundatio.Metrics;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.Formatting {
    public class FormattingPluginManager : PluginManagerBase<IFormattingPlugin> {
        public FormattingPluginManager(IServiceProvider serviceProvider, IMetricsClient metricsClient = null, ILoggerFactory loggerFactory = null) : base(serviceProvider, metricsClient, loggerFactory) { }

        /// <summary>
        /// Runs through the formatting plugins to calculate an html summary for the stack based on the event data.
        /// </summary>
        public SummaryData GetStackSummaryData(Stack stack) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    var result = plugin.GetStackSummaryData(stack);
                    if (result != null)
                        return result;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error calling GetStackSummaryHtml for stack {stack} in plugin {PluginName}: {Message}", stack.Id, plugin.Name, ex.Message);
                }
            }

            return null;
        }

        /// <summary>
        /// Runs through the formatting plugins to calculate an html summary for the event.
        /// </summary>
        public SummaryData GetEventSummaryData(PersistentEvent ev) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    var result = plugin.GetEventSummaryData(ev);
                    if (result != null)
                        return result;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error calling GetEventSummaryHtml for Event {id} in plugin {PluginName}: {Message}", ev.Id, plugin.Name, ex.Message);
                }
            }

            return null;
        }

        /// <summary>
        /// Runs through the formatting plugins to calculate a stack title based on an event.
        /// </summary>
        public string GetStackTitle(PersistentEvent ev) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    string result = plugin.GetStackTitle(ev);
                    if (!String.IsNullOrEmpty(result))
                        return result;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error calling GetStackTitle for Event {id} in plugin {PluginName}: {Message}", ev.Id, plugin.Name, ex.Message);
                }
            }

            return null;
        }

        /// <summary>
        /// Runs through the formatting plugins to get notification mail content for an event.
        /// </summary>
        public MailMessageData GetEventNotificationMailMessageData(PersistentEvent ev, bool isCritical, bool isNew, bool isRegression) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    var result = plugin.GetEventNotificationMailMessageData(ev, isCritical, isNew, isRegression);
                    if (result != null)
                        return result;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error calling GetEventNotificationMailMessage for Event {id} in plugin {PluginName}: {Message}", ev.Id, plugin.Name, ex.Message);
                }
            }

            return null;
        }

        /// <summary>
        /// Runs through the formatting plugins to get notification mail content for an event.
        /// </summary>
        public SlackMessage GetSlackEventNotificationMessage(PersistentEvent ev, Project project, bool isCritical, bool isNew, bool isRegression) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    var message = plugin.GetSlackEventNotification(ev, project, isCritical, isNew, isRegression);
                    if (message != null)
                        return message;
                } catch (Exception ex) {
                    _logger.LogError(ex, "Error calling GetSlackEventNotificationMessage for Event {id} in plugin {PluginName}: {Message}", ev.Id, plugin.Name, ex.Message);
                }
            }

            return null;
        }
    }
}