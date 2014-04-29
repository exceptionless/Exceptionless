using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using KellermanSoftware.CompareNetObjects;

namespace Exceptionless.Core.Extensions {
    public static class DeltaExtensions {
        public static List<string> GetChangedProperties<T>(this Web.Delta<T> value, T original, List<String> includeList = null) where T : class, new() {
            if (value == null)
                throw new ArgumentNullException("value");

            if (original == null)
                throw new ArgumentNullException("original");

            var changedProperties = value.GetChangedPropertyNames();
            if (!changedProperties.Any())
                return new List<string>();

            var compareObjects = new CompareObjects {
                MaxDifferences = 100,
                ElementsToInclude = includeList ?? value.GetChangedPropertyNames().ToList()
            };

            compareObjects.ElementsToInclude.Add(typeof(T).Name);

            bool result = compareObjects.Compare(original, value.GetEntity());

            return !result ? compareObjects.Differences.Select(d => d.PropertyName).Distinct().ToList() : new List<string>();
        }

        public static bool ContainsChangedProperty<T>(this Web.Delta<T> value, Expression<Func<T, object>> action) where T : class, new() {
            if (!value.GetChangedPropertyNames().Any())
                return false;

            MemberExpression expression = action.Body as MemberExpression ?? ((UnaryExpression)action.Body).Operand as MemberExpression;
            return expression != null && value.GetChangedPropertyNames().Contains(expression.Member.Name);
        }
    }
}