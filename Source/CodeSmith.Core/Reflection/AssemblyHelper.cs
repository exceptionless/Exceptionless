using System;
using System.Collections.Generic;
using System.Reflection;

namespace CodeSmith.Core.Reflection {
    public class AssemblyHelper {
        public static Assembly GetRootAssembly() {
            Assembly assembly;
#if SILVERLIGHT
            assembly = GetAssemblies().FirstOrDefault();
#else
            assembly = Assembly.GetCallingAssembly();
#endif
            if (assembly == null)
                assembly = Assembly.GetCallingAssembly();
            if (assembly == null)
                assembly = Assembly.GetExecutingAssembly();

            return assembly;
        }

        public static IEnumerable<Assembly> GetAssemblies() {
#if SILVERLIGHT
            return from part in System.Windows.Deployment.Current.Parts
                   let uri = new Uri(part.Source, UriKind.Relative)
                   let resourceStream = System.Windows.Application.GetResourceStream(uri)
                   select part.Load(resourceStream.Stream);
#else
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }

        public static string GetAssemblyProduct() {
            // Get all Product attributes on this assembly
            Assembly assembly = GetRootAssembly();
            if (assembly == null)
                return String.Empty;

            object[] attributes = assembly.GetCustomAttributes(typeof(AssemblyProductAttribute), false);
            // If there aren't any Product attributes, return an empty string
            if (attributes.Length == 0)
                return String.Empty;
            // If there is a Product attribute, return its value
            return ((AssemblyProductAttribute)attributes[0]).Product;
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