using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Validation;
using FluentValidation;
using FluentValidation.Results;

namespace Exceptionless.Core.Extensions {
    public static class ValidationExtensions {
        public static string ToErrorMessage(this IEnumerable<ValidationFailure> failures) {
            return failures == null ? null : String.Join(Environment.NewLine, failures.Select(f => f.ErrorMessage));
        }

        public static IRuleBuilderOptions<T, TProperty> IsObjectId<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder) {
            return ruleBuilder.SetValidator(new IsObjectIdValidator());
        }
    }
}
