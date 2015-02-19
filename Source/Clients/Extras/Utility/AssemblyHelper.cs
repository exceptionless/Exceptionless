using System;
using System.Collections.Generic;
using System.Reflection;
using Exceptionless.Logging;

namespace Exceptionless.Extras.Utility {
    public class AssemblyHelper {
        public static Assembly GetRootAssembly() {
            return Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        }

        public static string GetAssemblyTitle() {
            // Get all attributes on this assembly
            Assembly assembly = GetRootAssembly();
            if (assembly == null)
                return String.Empty;

            object[] attributes = assembly.GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
            // If there aren't any attributes, return an empty string
            if (attributes.Length == 0)
                return String.Empty;

            // If there is an attribute, return its value
            return ((AssemblyTitleAttribute)attributes[0]).Title;
        }

        public static List<Type> GetTypes(IExceptionlessLog log) {
            var types = new List<Type>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                try {
                    if (assembly.IsDynamic)
                        continue;

                    types.AddRange(assembly.GetExportedTypes());
                } catch (Exception ex) {
                    log.Error(typeof(ExceptionlessExtraConfigurationExtensions), ex, String.Format("An error occurred while getting types for assembly \"{0}\".", assembly));
                }
            }

            return types;
        }
    }
}