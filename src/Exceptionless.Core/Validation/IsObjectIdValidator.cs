using FluentValidation;
using FluentValidation.Validators;

namespace Exceptionless.Core.Validation;

public class IsObjectIdValidator<T, TProperty> : PropertyValidator<T, TProperty> {
    public override string Name => "IsObjectIdValidator";

    public override bool IsValid(ValidationContext<T> context, TProperty value) {
        if (value is not string stringValue)
            return false;

        if (String.IsNullOrEmpty(stringValue))
            return false;

        return stringValue.Length == 24;
    }

    protected override string GetDefaultMessageTemplate(string errorCode)
        => "Value for {PropertyName} is not a valid object id.";
}
