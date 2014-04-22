using System;
using System.Linq;
using System.Net.Mail;
using CodeSmith.Core.Dependency;
using Exceptionless.Core.Queues;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.Plugins.Formatting {
    public class FormattingPluginManager : PluginManagerBase<IFormattingPlugin> {
        public FormattingPluginManager(IDependencyResolver dependencyResolver = null) : base(dependencyResolver) { }

        /// <summary>
        /// Runs through the formatting plugins to calculate an html summary for the stack based on the event data.
        /// </summary>
        public string GetStackSummaryHtml(PersistentEvent ev) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    string result = plugin.GetStackSummaryHtml(ev);
                    if (!String.IsNullOrEmpty(result))
                        return result;
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error calling GetStackSummaryHtml in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
                }
            }

            return null;
        }

        /// <summary>
        /// Runs through the formatting plugins to calculate an html summary for the event.
        /// </summary>
        public string GetEventSummaryHtml(PersistentEvent ev) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    string result = plugin.GetEventSummaryHtml(ev);
                    if (!String.IsNullOrEmpty(result))
                        return result;
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error calling GetEventSummaryHtml in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
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
                    Log.Error().Exception(ex).Message("Error calling GetStackTitle in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
                }
            }

            return null;
        }

        /// <summary>
        /// Runs through the formatting plugins to get notification mail content for an event.
        /// </summary>
        public MailMessage GetEventNotificationMailMessage(EventNotification model) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    MailMessage result = plugin.GetEventNotificationMailMessage(model);
                    if (result != null)
                        return result;
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error calling GetEventNotificationMailMessage in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
                }
            }

            return null;
        }

        /// <summary>
        /// Runs through the formatting plugins to calculate a razor view name for an event.
        /// </summary>
        public string GetEventViewName(PersistentEvent ev) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    string result = plugin.GetStackTitle(ev);
                    if (!String.IsNullOrEmpty(result))
                        return result;
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error calling GetEventViewName in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
                }
            }

            return null;
        }
    }
}