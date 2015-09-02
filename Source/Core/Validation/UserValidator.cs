using System;
using Exceptionless.Core.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class UserValidator : AbstractValidator<User> {
        public UserValidator() {
            RuleFor(u => u.FullName).NotEmpty().WithMessage("Please specify a valid full name.");
            RuleFor(u => u.EmailAddress).NotEmpty().EmailAddress().WithMessage("Please specify a valid email address.");
            RuleFor(u => u.IsEmailAddressVerified).Equal(false).When(u => String.IsNullOrEmpty(u.EmailAddress)).WithMessage("An email address cannot be verified if it doesn't exist");
            RuleFor(u => u.VerifyEmailAddressToken).NotEmpty().When(u => !u.IsEmailAddressVerified).WithMessage("A verify email address token must be set if the email address has not been verified.");
            RuleFor(u => u.VerifyEmailAddressTokenExpiration).NotEmpty().When(u => !u.IsEmailAddressVerified).WithMessage("A verify email address token expiration must be set if the email address has not been verified.");
        }
    }
}