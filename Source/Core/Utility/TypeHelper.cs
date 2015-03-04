using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Exceptionless.Core.Helpers
{
    public class TypeHelper
    {
        public static T ChangeType<T>(object v)
        {
            Type pType = typeof(T);
            Type vType = v.GetType();

            if (pType.Equals(vType))
                return (T)v;
            if (pType.IsEnum && vType.Equals(typeof(string)))
                return (T)Enum.Parse(pType, v.ToString());
            if (pType.Equals(typeof(bool)))
                return (T)ToBoolean(v);

            // Must use InvariantCulture otherwise we run into localization issues.
            return (T)Convert.ChangeType(v, typeof(T), CultureInfo.InvariantCulture);
        }

        private static object ToBoolean(object value)
        {
            bool b;
            if (bool.TryParse(value.ToString(), out b))
                return b;

            int i;
            if (int.TryParse(value.ToString(), out i))
                return Convert.ToBoolean(i);

            return Convert.ToBoolean(value);
        }

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
