using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;

#if !EMBEDDED
namespace CodeSmith.Core.Extensions {
    public
#else
namespace Exceptionless.Extensions {
    internal
#endif
    static class EnumHelper {
        /// <summary>
        /// Determines whether any flag is on for the specified mask.
        /// </summary>
        /// <typeparam name="T">The flag type.</typeparam>
        /// <param name="mask">The mask to check if the flag is on.</param>
        /// <param name="flag">The flag to check for in the mask.</param>
        /// <returns>
        /// 	<c>true</c> if any flag is on for the specified mask; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsAnyFlagOn<T>(this Enum mask, T flag)
            where T : struct, IComparable, IFormattable, IConvertible {
            ulong flagInt = Convert.ToUInt64(flag);
            ulong maskInt = Convert.ToUInt64(mask);

            return (maskInt & flagInt) != 0;
        }

        /// <summary>
        /// Determines whether the flag is on for the specified mask.
        /// </summary>
        /// <typeparam name="T">The flag type.</typeparam>
        /// <param name="mask">The mask to check if the flag is on.</param>
        /// <param name="flag">The flag to check for in the mask.</param>
        /// <returns>
        /// 	<c>true</c> if the flag is on for the specified mask; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsFlagOn<T>(this Enum mask, T flag)
            where T : struct, IComparable, IFormattable, IConvertible {
            ulong flagInt = Convert.ToUInt64(flag);
            ulong maskInt = Convert.ToUInt64(mask);

            return (maskInt & flagInt) == flagInt;
        }

        /// <summary>
        /// Sets the flag on in the specified mask.
        /// </summary>
        /// <typeparam name="T">The flag type.</typeparam>
        /// <param name="mask">The mask to set flag on.</param>
        /// <param name="flag">The flag to set.</param>
        /// <returns>The mask with the flag set to on.</returns>
        public static T SetFlagOn<T>(this Enum mask, T flag)
            where T : struct, IComparable, IFormattable, IConvertible {
            ulong flagInt = Convert.ToUInt64(flag);
            ulong maskInt = Convert.ToUInt64(mask);

            maskInt |= flagInt;

            return ConvertFlag<T>(maskInt);
        }

        /// <summary>
        /// Sets the flag off in the specified mask.
        /// </summary>
        /// <typeparam name="T">The flag type.</typeparam>
        /// <param name="mask">The mask to set flag off.</param>
        /// <param name="flag">The flag to set.</param>
        /// <returns>The mask with the flag set to off.</returns>
        public static T SetFlagOff<T>(this Enum mask, T flag)
            where T : struct, IComparable, IFormattable, IConvertible {
            ulong flagInt = Convert.ToUInt64(flag);
            ulong maskInt = Convert.ToUInt64(mask);

            maskInt &= ~flagInt;

            return ConvertFlag<T>(maskInt);
        }

        /// <summary>
        /// Toggles the flag in the specified mask.
        /// </summary>
        /// <typeparam name="T">The flag type.</typeparam>
        /// <param name="mask">The mask to toggle the flag against.</param>
        /// <param name="flag">The flag to toggle.</param>
        /// <returns>The mask with the flag set in the opposite position then it was.</returns>
        public static T ToggleFlag<T>(this Enum mask, T flag)
            where T : struct, IComparable, IFormattable, IConvertible {
            ulong flagInt = Convert.ToUInt64(flag);
            ulong maskInt = Convert.ToUInt64(mask);

            maskInt ^= flagInt;

            return ConvertFlag<T>(maskInt);
        }

        /// <summary>
        /// Gets the string hex of the enum.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <param name="enum">The enum to get the string hex from.</param>
        /// <returns></returns>
        public static string ToStringHex<T>(this Enum @enum)
            where T : struct, IComparable, IFormattable, IConvertible {
            return String.Format("{0:x8}", @enum); //hex            
        }

        /// <summary>
        /// Tries to get an enum from a String.
        /// </summary>
        /// <typeparam name="T">The enum type.</typeparam>
        /// <param name="value">The enum value.</param>
        /// <param name="input">The input String.</param>
        /// <param name="returnValue">The return enum value.</param>
        /// <returns>
        /// 	<c>true</c> if the string was able to be parsed to an enum; otherwise, <c>false</c>.
        /// </returns>
        public static bool TryParseEnum<T>(this Enum value, string input, out T returnValue)
             where T : struct, IComparable, IFormattable, IConvertible {
            returnValue = default(T);
            if (String.IsNullOrEmpty(input))
                return false;

            Type t = typeof(T);
            if (t.IsEnum && Enum.IsDefined(t, input)) {
                returnValue = (T)Enum.Parse(t, input, true);
                return true;
            }
            return false;
        }

        private static T ConvertFlag<T>(ulong maskInt) {
            Type t = typeof(T);
            if (t.IsEnum)
                return (T)Enum.ToObject(t, maskInt);

            return (T)Convert.ChangeType(maskInt, t, Thread.CurrentThread.CurrentUICulture);
        }

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

#if !SILVERLIGHT
        /// <summary>
        /// Gets the default defined value of an enum.
        /// </summary>
        /// <param name="type">The enum.</param>
        /// <returns>If the value cannot be determined, 0 will be returned.</returns>
        public static object GetEnumDefaultValue(Type type) {
            if (type == null || !type.IsEnum)
                return 0;

            object defaultValue;
            if (TryGetEnumDefaultValue<int>(type, out defaultValue))
                return defaultValue;
            if (TryGetEnumDefaultValue<byte>(type, out defaultValue))
                return defaultValue;
            if (TryGetEnumDefaultValue<short>(type, out defaultValue))
                return defaultValue;
            if (TryGetEnumDefaultValue<long>(type, out defaultValue))
                return defaultValue;
            if (TryGetEnumDefaultValue<sbyte>(type, out defaultValue))
                return defaultValue;
            if (TryGetEnumDefaultValue<ushort>(type, out defaultValue))
                return defaultValue;
            if (TryGetEnumDefaultValue<uint>(type, out defaultValue))
                return defaultValue;
            if (TryGetEnumDefaultValue<ulong>(type, out defaultValue))
                return defaultValue;

            return 0;
        }

        /// <summary>
        /// Attempts to get the default value of an enum.
        /// </summary>
        /// <typeparam name="T">The System Type.</typeparam>
        /// <param name="type"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        private static bool TryGetEnumDefaultValue<T>(Type type, out object defaultValue) {
            defaultValue = null;

            try {
                defaultValue = (T)type.GetField(Enum.GetValues(type).GetValue(0).ToString()).GetValue(null);

                return true;
            } catch (Exception) {
            }

            return false;
        }
#endif

        /// <summary>
        /// Retrieve the description on the enum, e.g.
        /// [Description("Bright Pink")]
        /// BrightPink = 2,
        /// Then when you pass in the enum, it will retrieve the description
        /// </summary>
        /// <param name="en">The Enumeration</param>
        /// <returns>A string representing the friendly name</returns>
        public static string GetDescription(Enum en) {
            Type type = en.GetType();

            MemberInfo[] memInfo = type.GetMember(en.ToString());

            if (memInfo.Length > 0) {
                object[] attrs = memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);

                if (attrs.Length > 0)
                    return ((DescriptionAttribute)attrs[0]).Description;
            }

            return en.ToString();
        }

        public static List<string> GetValues<T>() where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum) 
                throw new ArgumentException("T must be an enumerated type");

            var list = new List<string>();
            foreach (T item in Enum.GetValues(typeof(T)))
                list.Add(item.ToString());

            return list;
        }
    }
}
