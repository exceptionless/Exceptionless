using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;


namespace Exceptionless.Extensions {
    internal static class ReflectionExtensions {
        public static AssemblyName GetAssemblyName(this Assembly assembly) {
            return new AssemblyName(assembly.FullName);
        }

        public static string GetVersion(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyVersionAttribute)) as AssemblyVersionAttribute;
            if (attr != null)
                return attr.Version;

            return assembly.GetAssemblyName().Version.ToString();
        }

        public static string GetFileVersion(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyFileVersionAttribute)) as AssemblyFileVersionAttribute;
            return attr != null ? attr.Version : null;
        }

        public static string GetInformationalVersion(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
            return attr != null ? attr.InformationalVersion : null;
        }

        public static string GetProduct(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyProductAttribute)) as AssemblyProductAttribute;
            return attr != null ? attr.Product : null;
        }

        public static string GetCompany(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyCompanyAttribute)) as AssemblyCompanyAttribute;
            return attr != null ? attr.Company : null;
        }

        public static string GetCopyright(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyCopyrightAttribute)) as AssemblyCopyrightAttribute;
            return attr != null ? attr.Copyright : null;
        }
        
        public static string GetTitle(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyTitleAttribute)) as AssemblyTitleAttribute;
            return attr != null ? attr.Title : null;
        }

        public static string GetDescription(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyDescriptionAttribute)) as AssemblyDescriptionAttribute;
            return attr != null ? attr.Description : null;
        }
        
        public static string GetTrademark(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyTrademarkAttribute)) as AssemblyTrademarkAttribute;
            return attr != null ? attr.Trademark : null;
        }
    }
}