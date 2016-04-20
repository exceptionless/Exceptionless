using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Exceptionless.Core.Helpers {
    public class TypeHelper {
        public static IEnumerable<Type> GetDerivedTypes<TAction>(IEnumerable<Assembly> assemblies = null) {
            if (assemblies == null)
                assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var types = new List<Type>();
            foreach (var assembly in assemblies) {
                try {
                    types.AddRange(from type in assembly.GetTypes() where type.IsClass && !type.IsNotPublic && !type.IsAbstract && typeof(TAction).IsAssignableFrom(type) select type);
                } catch (ReflectionTypeLoadException ex) {
                    string loaderMessages = String.Join(", ", ex.LoaderExceptions.ToList().Select(le => le.Message));
                    Trace.TraceInformation("Unable to search types from assembly \"{0}\" for plugins of type \"{1}\": {2}", assembly.FullName, typeof(TAction).Name, loaderMessages);
                }
            }

            return types;
        }
    }
}