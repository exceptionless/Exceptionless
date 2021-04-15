using System;
using FluentValidation.Validators;

namespace Exceptionless.Core.Validation {
    public class IsObjectIdValidator : PropertyValidator {
        protected override string GetDefaultMessageTemplate() {
            return "Value is not a valid object id.";
        }

        protected override bool IsValid(PropertyValidatorContext context) {
            string value = context.PropertyValue as string;
            if (String.IsNullOrEmpty(value))
                return false;

            return value.Length == 24;
        }
    }
}