using System;
using System.Collections.Generic;

namespace Exceptionless.Core.Helpers
{
    public class LambdaComparer<T> : IEqualityComparer<T>, IComparer<T>
    {
        private readonly Func<T, T, int> _compareValuesFunc;
        private readonly Func<T, int> _getHashCodeFunc;

        public LambdaComparer(Func<T, int> getComparisonValueFunc)
            : this((a, b) => getComparisonValueFunc(a).CompareTo(getComparisonValueFunc(b)), o => getComparisonValueFunc(o).GetHashCode()) {
        }

        public LambdaComparer(Func<T, long> getComparisonValueFunc)
            : this((a, b) => getComparisonValueFunc(a).CompareTo(getComparisonValueFunc(b)), o => getComparisonValueFunc(o).GetHashCode()) {
        }

        public LambdaComparer(Func<T, string> getComparisonValue)
            : this((a, b) => String.CompareOrdinal(getComparisonValue(a), getComparisonValue(b)), o => getComparisonValue(o).GetHashCode()) {
        }

        public LambdaComparer(Func<T, T, int> compareValuesFunc) : this(compareValuesFunc, o => o.GetHashCode())
        {
        }

        public LambdaComparer(Func<T, T, int> compareValuesFunc, Func<T, int> getHashCodeFunc)
        {
            if (compareValuesFunc == null)
                throw new ArgumentNullException("compareValuesFunc");
            if (getHashCodeFunc == null)
                throw new ArgumentNullException("getHashCodeFunc");

            _compareValuesFunc = compareValuesFunc;
            _getHashCodeFunc = getHashCodeFunc;
        }

        public bool Equals(T x, T y)
        {
            return Compare(x, y) == 0;
        }

        public int GetHashCode(T obj)
        {
            return _getHashCodeFunc(obj);
        }

        public int Compare(T x, T y)
        {
            return _compareValuesFunc(x, y);
        }
    }
}
