using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Component;
using CodeSmith.Core.Dependency;
using CodeSmith.Core.Helpers;
using Exceptionless.Core.EventPlugins;
using Exceptionless.Core.Mail.Models;
using Exceptionless.Models;
using NLog.Fluent;

namespace Exceptionless.Core.FormattingPlugins {
    public class FormattingPluginManager {
        private readonly IDependencyResolver _dependencyResolver;

        public FormattingPluginManager(IDependencyResolver dependencyResolver = null) {
            _dependencyResolver = dependencyResolver ?? new DefaultDependencyResolver();
            Plugins = new SortedList<int, IFormattingPlugin>();
            LoadDefaultFormattingPlugins();
        }

        /// <summary>
        /// Runs through the formatting plugins to calculate an html summary for the event.
        /// </summary>
        public string GetEventSummaryHtml(Event ev) {
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
        public string GetStackTitle(Event ev) {
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
        public MailContent GetEventMailContent(EventNotificationModel model) {
            foreach (var plugin in Plugins.Values.ToList()) {
                try {
                    MailContent result = plugin.GetEventMailNotificationContent(model);
                    if (result != null)
                        return result;
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Error calling GetEventMailNotificationContent in plugin \"{0}\": {1}", plugin.GetType().FullName, ex.Message).Write();
                }
            }

            return null;
        }

        /// <summary>
        /// Runs through the formatting plugins to calculate a razor view name for an event.
        /// </summary>
        public string GetEventViewName(Event ev) {
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

        public SortedList<int, IFormattingPlugin> Plugins { get; private set; }

        public void AddPlugin(Type pluginType) {
            var attr = pluginType.GetCustomAttributes(typeof(PriorityAttribute), true).FirstOrDefault() as PriorityAttribute;
            int priority = attr != null ? attr.Priority : 0;

            var plugin = _dependencyResolver.GetService(pluginType) as IFormattingPlugin;
            Plugins.Add(priority, plugin);
        }

        private void LoadDefaultFormattingPlugins() {
            var pluginTypes = TypeHelper.GetDerivedTypes<IFormattingPlugin>();

            foreach (var type in pluginTypes) {
                try {
                    AddPlugin(type);
                } catch (Exception ex) {
                    Log.Error().Exception(ex).Message("Unabled to instantiate plugin of type \"{0}\": {1}", type.FullName, ex.Message).Write();
                }
            }
        }
    }
}
