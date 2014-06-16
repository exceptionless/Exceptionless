using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;


namespace Exceptionless.Extras {
    internal static class ReflectionExtensions {
        public static bool IsBrowsable(this PropertyInfo property) {
            object[] attributes = property.GetCustomAttributes(typeof(BrowsableAttribute), true);

            // make sure property hasn't been marked as non-browsable.
            return attributes.Length <= 0 || ((BrowsableAttribute)attributes[0]).Browsable;
        }

        public static string Description(this PropertyInfo property) {
            object[] attributes = property.GetCustomAttributes(typeof(DescriptionAttribute), true);
            if (attributes.Length > 0 && attributes[0] is DescriptionAttribute) {
                return ((DescriptionAttribute)attributes[0]).Description;
            }

            return String.Empty;
        }

        public static FileVersionInfo GetFileVersionInfo(this Assembly assembly) {
            FileVersionInfo fileVersionInfo = null;
            try {
                fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            } catch {}

            return fileVersionInfo;
        }

        public static AssemblyName GetAssemblyName(this Assembly assembly) {
            try {
                return assembly.GetName();
            } catch { }

            return new AssemblyName(assembly.FullName);
        }

        public static DateTime? GetCreationTime(this Assembly assembly) {
            try {
                return File.GetCreationTime(assembly.Location);
            } catch {}

            return null;
        }

        public static DateTime? GetLastWriteTime(this Assembly assembly) {
            try {
                return File.GetLastWriteTime(assembly.Location);
            } catch {}

            return null;
        }

        public static string GetVersion(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyVersionAttribute), true) as AssemblyVersionAttribute;
            if (attr != null)
                return attr.Version;

            return assembly.GetAssemblyName().Version.ToString();
        }

        public static string GetFileVersion(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyFileVersionAttribute), true) as AssemblyFileVersionAttribute;
            return attr != null ? attr.Version : null;
        }

        public static string GetInformationalVersion(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyInformationalVersionAttribute), true) as AssemblyInformationalVersionAttribute;
            return attr != null ? attr.InformationalVersion : null;
        }

        public static string GetProduct(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyProductAttribute), true) as AssemblyProductAttribute;
            return attr != null ? attr.Product : null;
        }

        public static string GetCompany(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyCompanyAttribute), true) as AssemblyCompanyAttribute;
            return attr != null ? attr.Company : null;
        }

        public static string GetCopyright(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyCopyrightAttribute), true) as AssemblyCopyrightAttribute;
            return attr != null ? attr.Copyright : null;
        }
        
        public static string GetTitle(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyTitleAttribute), true) as AssemblyTitleAttribute;
            return attr != null ? attr.Title : null;
        }

        public static string GetDescription(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyDescriptionAttribute), true) as AssemblyDescriptionAttribute;
            return attr != null ? attr.Description : null;
        }
        
        public static string GetTrademark(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(AssemblyTrademarkAttribute), true) as AssemblyTrademarkAttribute;
            return attr != null ? attr.Trademark : null;
        }
        
        public static ObfuscateAssemblyAttribute GetObfuscateAssembly(this Assembly assembly) {
            var attr = Attribute.GetCustomAttribute(assembly, typeof(ObfuscateAssemblyAttribute), true) as ObfuscateAssemblyAttribute;
            return attr;
        }
    }
}