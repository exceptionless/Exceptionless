using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CodeSmith.Core.Component;

namespace CodeSmith.Core.Helpers
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

            return assemblies.SelectMany(assembly => (
                from type in assembly.GetTypes()
                where (type.IsClass && !type.IsNotPublic) && !type.IsAbstract && type.IsSubclassOf(typeof(TAction))
                select type));
        }
    }
}
