using System;

namespace Exceptionless.Core.Extensions {
    public static class EnumHelper {
        /// <summary>
        /// Will try and parse an enum and it's default type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns>True if the enum value is defined.</returns>
        public static bool TryEnumIsDefined(Type type, object value) {
            if (type == null || value == null || !type.IsEnum)
                return false;

            // Return true if the value is an enum and is a matching type. 
            if (type == value.GetType())
                return true;

            if (TryEnumIsDefined<int>(type, value))
                return true;
            if (TryEnumIsDefined<string>(type, value))
                return true;
            if (TryEnumIsDefined<byte>(type, value))
                return true;
            if (TryEnumIsDefined<short>(type, value))
                return true;
            if (TryEnumIsDefined<long>(type, value))
                return true;
            if (TryEnumIsDefined<sbyte>(type, value))
                return true;
            if (TryEnumIsDefined<ushort>(type, value))
                return true;
            if (TryEnumIsDefined<uint>(type, value))
                return true;
            if (TryEnumIsDefined<ulong>(type, value))
                return true;

            return false;
        }

        private static bool TryEnumIsDefined<T>(Type type, object value) {
            // Catch any casting errors that can occur or if 0 is not defined as a default value.
            try {
                if (value is T && Enum.IsDefined(type, (T)value))
                    return true;
            } catch (Exception) {}

            return false;
        }
    }
}
