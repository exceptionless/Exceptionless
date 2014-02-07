using System;
using System.Diagnostics;

namespace CodeSmith.Core.Extensions
{
    public static class StringValidationExtensions
    {
        [DebuggerHidden]
        public static Validation<string> ShorterThan(this Validation<string> item, int limit)
        {
            if (item.Value.Length >= limit)
                throw new ArgumentException("Parameter {0} must be shorter than {1} chars".FormatWith(item.ArgName, limit));

            return item;
        }

        [DebuggerHidden]
        public static Validation<string> LongerThan(this Validation<string> item, int limit)
        {
            if (item.Value.Length <= limit)
                throw new ArgumentException("Parameter {0} must be longer than {1} chars".FormatWith(item.ArgName, limit));

            return item;
        }

        [DebuggerHidden]
        public static Validation<string> StartsWith(this Validation<string> item, string pattern)
        {
            if (!item.Value.StartsWith(pattern))
                throw new ArgumentException("Parameter {0} must start with {1}".FormatWith(item.ArgName, pattern));

            return item;
        }

        [DebuggerHidden]
        public static Validation<string> ExactLenght(this Validation<string> item, int length)
        {
            if (item.Value.Length != length)
#if SILVERLIGHT
                throw new ArgumentOutOfRangeException(item.ArgName, "Parameter {0} has to be {1} characters long.".FormatWith(item.ArgName, length));
#else
                throw new ArgumentOutOfRangeException(item.ArgName, item.Value, "Parameter {0} has to be {1} characters long.".FormatWith(item.ArgName, length));
#endif
            return item;
        }

        [DebuggerHidden]
        public static Validation<string> NotEmpty(this Validation<string> item)
        {
            if (item == "")
                throw new ArgumentException("Parameter {0} may not be empty".FormatWith(item.ArgName), item.ArgName);

            return item;
        }

        [DebuggerHidden]
        public static Validation<string> NotNullOrEmpty(this Validation<string> item)
        {
            item.NotNull();
            item.NotEmpty();

            return item;
        }
    }
}