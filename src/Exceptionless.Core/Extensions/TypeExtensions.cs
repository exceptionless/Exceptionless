using System.ComponentModel;
using Exceptionless.Core.Pipeline;

namespace Exceptionless.Core.Extensions;

public static class TypeExtensions
{
    public static IList<Type> SortByPriority(this IEnumerable<Type> types)
    {
        return types.OrderBy(t =>
        {
            var priorityAttribute = t.GetCustomAttributes(typeof(PriorityAttribute), true).FirstOrDefault() as PriorityAttribute;
            return priorityAttribute?.Priority ?? 0;
        }).ToList();
    }

    public static bool IsNumeric(this Type type)
    {
        if (type.IsArray)
            return false;

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => true,
            TypeCode.Decimal => true,
            TypeCode.Double => true,
            TypeCode.Int16 => true,
            TypeCode.Int32 => true,
            TypeCode.Int64 => true,
            TypeCode.SByte => true,
            TypeCode.Single => true,
            TypeCode.UInt16 => true,
            TypeCode.UInt32 => true,
            TypeCode.UInt64 => true,
            _ => false
        };
    }

    public static T ToType<T>(this object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var targetType = typeof(T);
        var converter = TypeDescriptor.GetConverter(targetType);
        var valueType = value.GetType();

        if (targetType.IsAssignableFrom(valueType))
            return (T)value;

        if ((valueType.IsEnum || value is string) && targetType.IsEnum)
        {
            // attempt to match enum by name.
            if (EnumHelper.TryEnumIsDefined(targetType, value.ToString()))
            {
                object parsedValue = Enum.Parse(targetType, value.ToString()!, false);
                return (T)parsedValue;
            }

            string message = $"The Enum value of '{value}' is not defined as a valid value for '{targetType.FullName}'.";
            throw new ArgumentException(message, nameof(value));
        }

        if (valueType.IsNumeric() && targetType.IsEnum)
            return (T)Enum.ToObject(targetType, value);

        if (converter is not null && converter.CanConvertFrom(valueType))
        {
            object? convertedValue = converter.ConvertFrom(value);
            return (convertedValue is T convertedValue1 ? convertedValue1 : default)
                   ?? throw new ArgumentException($"An incompatible value specified.  Target Type: {targetType.FullName} Value Type: {value.GetType().FullName}", nameof(value));
        }

        if (value is IConvertible)
        {
            try
            {
                object convertedValue = Convert.ChangeType(value, targetType);
                return (T)convertedValue;
            }
            catch (Exception e)
            {
                throw new ArgumentException($"An incompatible value specified.  Target Type: {targetType.FullName} Value Type: {value.GetType().FullName}", nameof(value), e);
            }
        }

        throw new ArgumentException($"An incompatible value specified.  Target Type: {targetType.FullName} Value Type: {value.GetType().FullName}", nameof(value));
    }
}
