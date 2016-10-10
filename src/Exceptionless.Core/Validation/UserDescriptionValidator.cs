using System;
using Exceptionless.Core.Models.Data;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class UserDescriptionValidator : AbstractValidator<UserDescription> {
        public UserDescriptionValidator() {
            RuleFor(u => u.EmailAddress)
                .EmailAddress()
                .Unless(u => String.IsNullOrEmpty(u.EmailAddress))
                .WithMessage("Please specify a valid email address.");

            RuleFor(u => u.Description)
                .NotEmpty()
                .WithMessage("Please specify a description.");
        }
    }
}