using System;
using System.Reflection;

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
    }
}