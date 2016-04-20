using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Exceptionless.Core.Pipeline;

namespace Exceptionless.Core.Extensions {
    public static class TypeExtensions {
        public static IList<Type> SortByPriority(this IEnumerable<Type> types) {
            return types.OrderBy(t => {
                var priorityAttribute = t.GetCustomAttributes(typeof(PriorityAttribute), true).FirstOrDefault() as PriorityAttribute;
                return priorityAttribute?.Priority ?? 0;
            }).ToList();
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
    }
}