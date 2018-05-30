using System;
using System.Linq;
using System.Linq.Expressions;
using Exceptionless.Web.Utility;

namespace Exceptionless.Web.Extensions {
    public static class DeltaExtensions {
        public static bool ContainsChangedProperty<T>(this Delta<T> value, Expression<Func<T, object>> action) where T : class, new() {
            if (!value.GetChangedPropertyNames().Any())
                return false;

            var expression = action.Body as MemberExpression ?? ((UnaryExpression)action.Body).Operand as MemberExpression;
            return expression != null && value.GetChangedPropertyNames().Contains(expression.Member.Name);
        }
    }
}