using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Utility {
    public class AssemblyDetail {
        public string AssemblyName { get; private set; }

        public string AssemblyTitle { get; private set; }

        public string AssemblyDescription { get; private set; }

        public string AssemblyProduct { get; private set; }

        public string AssemblyCompany { get; private set; }

        public string AssemblyCopyright { get; private set; }

        public string AssemblyConfiguration { get; private set; }

        public string AssemblyVersion { get; private set; }

        public string AssemblyFileVersion { get; private set; }

        public string AssemblyInformationalVersion { get; private set; }


        private static readonly ConcurrentDictionary<Assembly, AssemblyDetail> _detailCache = new ConcurrentDictionary<Assembly, AssemblyDetail>();

        public static AssemblyDetail Extract(Assembly assembly) {
            var detail = _detailCache.GetOrAdd(assembly, a => {
                var assemblyDetail = new AssemblyDetail();
                var assemblyName = a.GetName();

                assemblyDetail.AssemblyName = assemblyName.Name;
                assemblyDetail.AssemblyVersion = assemblyName.Version?.ToString();

                assemblyDetail.AssemblyTitle = a.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
                assemblyDetail.AssemblyDescription = a.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
                assemblyDetail.AssemblyProduct = a.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
                assemblyDetail.AssemblyCompany = a.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
                assemblyDetail.AssemblyCopyright = a.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
                assemblyDetail.AssemblyConfiguration = a.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
                assemblyDetail.AssemblyFileVersion = a.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
                assemblyDetail.AssemblyInformationalVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

                return assemblyDetail;
            });

            return detail;
        }

        public static IEnumerable<AssemblyDetail> ExtractAll(string filter = "Exceptionless*") {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies) {
                if (!assembly.FullName.AnyWildcardMatches(new [] { filter }))
                    continue;

                yield return Extract(assembly);
            }
        }
    }
}