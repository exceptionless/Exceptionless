using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Exceptionless.Extensions {
    internal static class TypeExtensions {
        public static PropertyInfo[] GetPublicProperties(this Type type) {
            if (type.IsInterface) {
                var propertyInfos = new List<PropertyInfo>();

                var considered = new List<Type>();
                var queue = new Queue<Type>();
                considered.Add(type);
                queue.Enqueue(type);
                while (queue.Count > 0) {
                    var subType = queue.Dequeue();
                    foreach (var subInterface in subType.GetInterfaces()) {
                        if (considered.Contains(subInterface))
                            continue;

                        considered.Add(subInterface);
                        queue.Enqueue(subInterface);
                    }

                    var typeProperties = subType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance);

                    var newPropertyInfos = typeProperties.Where(x => !propertyInfos.Contains(x));

                    propertyInfos.InsertRange(0, newPropertyInfos);
                }

                return propertyInfos.ToArray();
            }

            return type.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance);
        }

        public static bool IsNullable(this Type type) {
            if (type.IsValueType)
                return false;

            return type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        public static bool IsPrimitiveType(this Type type) {
            if (type.IsPrimitive)
                return true;

            if (type == typeof(Decimal)
                || type == typeof(String)
                || type == typeof(Guid)
                || type == typeof(TimeSpan)
                || type == typeof(Uri))
                return true;

            if (type.IsEnum)
                return true;

            if (type.IsNullable())
                return IsPrimitiveType(Nullable.GetUnderlyingType(type));

            return false;
        }
        
        public static bool IsNumeric(this Type type) {
            if (type.IsArray)
                return false;

            switch (Type.GetTypeCode(type)) {
                case TypeCode.Byte:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
            }

            return false;
        }
    }
}