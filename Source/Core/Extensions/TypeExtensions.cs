﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Exceptionless.Core.Pipeline;

namespace Exceptionless.Core.Extensions {
    public static class TypeExtensions {
        public static IList<Type> SortByPriority(this IEnumerable<Type> types) {
            return types.OrderBy(t => {
                var priorityAttribute = t.GetCustomAttributes(typeof(PriorityAttribute), true).FirstOrDefault() as PriorityAttribute;
                return priorityAttribute != null ? priorityAttribute.Priority : 0;
            }).ToList();
        } 

        public static bool IsNullable(this Type type) {
            if (type.IsValueType)
                return false;

            return type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        private static readonly ConcurrentDictionary<Type, object> _defaultValues = new ConcurrentDictionary<Type, object>(); 
        public static object GetDefaultValue(this Type type) {
            return _defaultValues.GetOrAdd(type, t => type.IsValueType ? Activator.CreateInstance(type) : null);
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

        public static T ToType<T>(this object value) {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            Type targetType = typeof(T);
            TypeConverter converter = TypeDescriptor.GetConverter(targetType);
            Type valueType = value.GetType();

            if (targetType.IsAssignableFrom(valueType))
                return (T)value;

            if ((valueType.IsEnum || value is string) && targetType.IsEnum) {
                // attempt to match enum by name.
                if (EnumHelper.TryEnumIsDefined(targetType, value.ToString())) {
                    object parsedValue = Enum.Parse(targetType, value.ToString(), false);
                    return (T)parsedValue;
                }

                var message = $"The Enum value of '{value}' is not defined as a valid value for '{targetType.FullName}'.";
                throw new ArgumentException(message);
            }

            if (valueType.IsNumeric() && targetType.IsEnum)
                return (T)Enum.ToObject(targetType, value);

            if (converter != null && converter.CanConvertFrom(valueType)) {
                object convertedValue = converter.ConvertFrom(value);
                return (T)convertedValue;
            }

            if (value is IConvertible) {
                try {
                    object convertedValue = Convert.ChangeType(value, targetType);
                    return (T)convertedValue;
                } catch (Exception e) {
                    throw new ArgumentException($"An incompatible value specified.  Target Type: {targetType.FullName} Value Type: {value.GetType().FullName}", nameof(value), e);
                }
            }

            throw new ArgumentException($"An incompatible value specified.  Target Type: {targetType.FullName} Value Type: {value.GetType().FullName}", nameof(value));
        }

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
    }
}