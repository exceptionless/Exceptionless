using System;
using System.Globalization;

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
    }
}
