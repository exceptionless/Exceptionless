using System;

namespace Exceptionless.Core.Extensions {
    public static class NumericExtensions {
        public static int NormalizeValue(this int value) {
            return value != -1 ? value : Int32.MaxValue;
        }
    }
}