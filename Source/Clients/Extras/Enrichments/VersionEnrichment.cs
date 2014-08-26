using System;
using System.Diagnostics;
using System.Reflection;
using Exceptionless.Extras;
using Exceptionless.Models;

namespace Exceptionless.Enrichments {
    public class VersionEnrichment : IEventEnrichment {
        private static bool _checkedForVersion;

        public void Enrich(EventEnrichmentContext context, Event ev) {
            if (ev.Data.ContainsKey(Event.KnownDataKeys.Version))
                return;

            object value;
            if (context.Client.Configuration.DefaultData.TryGetValue(Event.KnownDataKeys.Version, out value) && value is string) {
                ev.Data[Event.KnownDataKeys.Version] = value;
                return;
            }

            if (_checkedForVersion)
                return;
            
            _checkedForVersion = true;
            string version = null;
            try {
                version = GetVersionFromAssembly(Assembly.GetEntryAssembly());
                if (String.IsNullOrEmpty(version))
                    version = GetVersionFromStackTrace();
            } catch (Exception) {}

            if (String.IsNullOrEmpty(version))
                return;

            ev.Data[Event.KnownDataKeys.Version] = context.Client.Configuration.DefaultData[Event.KnownDataKeys.Version] = version;
        }

        private string GetVersionFromAssembly(Assembly assembly) {
            if (assembly == null)
                return null;

            string version = assembly.GetInformationalVersion();
            if (!String.IsNullOrEmpty(version))
                return version;

            version = assembly.GetFileVersion();
            if (!String.IsNullOrEmpty(version))
                return version;

            version = assembly.GetVersion();
            if (!String.IsNullOrEmpty(version))
                return version;

            var assemblyName = assembly.GetAssemblyName();
            return assemblyName != null ? assemblyName.Version.ToString() : null;
        }

        private string GetVersionFromStackTrace() {
            var trace = new StackTrace(false);
            for (int i = 0; i < trace.FrameCount; i++) {
                StackFrame frame = trace.GetFrame(i);
                MethodBase methodBase = frame.GetMethod();
                Type type = methodBase.DeclaringType;
                if (type == null)
                    continue;

                if (type.Assembly.IsDynamic || type.Assembly == typeof(ExceptionlessClient).Assembly || type.Assembly == GetType().Assembly || type.Assembly == typeof(object).Assembly)
                    continue;

                if (String.IsNullOrEmpty(type.Assembly.FullName) || type.Assembly.FullName.StartsWith("System.") || type.Assembly.FullName.StartsWith("Microsoft."))
                    continue;
           
                string company = type.Assembly.GetCompany();
                if (String.IsNullOrEmpty(company) || String.Equals(company, "Exceptionless", StringComparison.OrdinalIgnoreCase))
                    continue;

                string version = GetVersionFromAssembly(type.Assembly);
                if (!String.IsNullOrEmpty(version))
                    return version;
            }

            return null;
        }
    }
}