using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Exceptionless.Core.Helpers {
    public static class TypeHelper {
        public static readonly Type ObjectType = typeof(object);
        public static readonly Type StringType = typeof(string);
        public static readonly Type CharType = typeof(char);
        public static readonly Type NullableCharType = typeof(char?);
        public static readonly Type DateTimeType = typeof(DateTime);
        public static readonly Type NullableDateTimeType = typeof(DateTime?);
        public static readonly Type BoolType = typeof(bool);
        public static readonly Type NullableBoolType = typeof(bool?);
        public static readonly Type ByteArrayType = typeof(byte[]);
        public static readonly Type ByteType = typeof(byte);
        public static readonly Type SByteType = typeof(sbyte);
        public static readonly Type SingleType = typeof(float);
        public static readonly Type DecimalType = typeof(decimal);
        public static readonly Type Int16Type = typeof(short);
        public static readonly Type UInt16Type = typeof(ushort);
        public static readonly Type Int32Type = typeof(int);
        public static readonly Type UInt32Type = typeof(uint);
        public static readonly Type Int64Type = typeof(long);
        public static readonly Type UInt64Type = typeof(ulong);
        public static readonly Type DoubleType = typeof(double);

        public static string GetTypeName(string assemblyQualifiedName) {
            if (String.IsNullOrEmpty(assemblyQualifiedName))
                return null;

            string[] parts = assemblyQualifiedName.Split(',');
            int i = parts[0].LastIndexOf('.');
            if (i < 0)
                return null;

            return parts[0].Substring(i + 1);
        }

        public static bool AreSameValue(object a, object b) {
            if (a.GetType() != b.GetType()) {
                try {
                    b = ChangeType(b, a.GetType());
                } catch { }
            }

            if (a is JToken && b is JToken)
                return a.ToString().Equals(b.ToString());

            if (a != b && !a.Equals(b))
                return false;

            return true;
        }

        public static T ChangeType<T>(object v) {
            return (T)ChangeType(v, typeof(T));
        }

        public static object ChangeType(object v, Type desiredType) {
            Type currentType = v.GetType();

            if (desiredType == currentType)
                return v;
            if (desiredType.IsEnum && currentType == typeof(string))
                return Enum.Parse(desiredType, v.ToString());
            if (desiredType == typeof(bool))
                return ToBoolean(v);

            // Must use InvariantCulture otherwise we run into localization issues.
            return Convert.ChangeType(v, desiredType, CultureInfo.InvariantCulture);
        }

        private static object ToBoolean(object value) {
            bool b;
            if (bool.TryParse(value.ToString(), out b))
                return b;

            int i;
            if (int.TryParse(value.ToString(), out i))
                return Convert.ToBoolean(i);

            return Convert.ToBoolean(value);
        }

        public static IEnumerable<Type> GetDerivedTypes<TAction>(IEnumerable<Assembly> assemblies = null) {
            return GetDerivedTypes(typeof(TAction));
        }

        public static IEnumerable<Type> GetDerivedTypes(Type type, IEnumerable<Assembly> assemblies = null) {
            if (assemblies == null)
                assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var types = new List<Type>();
            foreach (var assembly in assemblies) {
                try {
                    types.AddRange(from implementingType in assembly.GetTypes() where implementingType.IsClass && !implementingType.IsNotPublic && !implementingType.IsAbstract && type.IsAssignableFrom(implementingType) select implementingType);
                } catch (ReflectionTypeLoadException ex) {
                    string loaderMessages = String.Join(", ", ex.LoaderExceptions.ToList().Select(le => le.Message));
                    Trace.TraceInformation("Unable to search types from assembly '{0}' for plugins of type '{1}': {2}", assembly.FullName, type.Name, loaderMessages);
                }
            }

            return types;
        }

        public static IEnumerable<Type> GetAllTypesImplementingOpenGenericType(Type openGenericType, IEnumerable<Assembly> assemblies = null) {
            if (assemblies == null)
                assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var implementingTypes = new List<Type>();
            foreach (var assembly in assemblies)
                implementingTypes.AddRange(
                    from x in assembly.GetTypes()
                    from z in x.GetInterfaces()
                    let y = x.BaseType
                    where
                        (y != null && y.IsGenericType && openGenericType.IsAssignableFrom(y.GetGenericTypeDefinition()))
                        || (z.IsGenericType && openGenericType.IsAssignableFrom(z.GetGenericTypeDefinition()))
                    select x
                );

            return implementingTypes;
        }
    }
}
