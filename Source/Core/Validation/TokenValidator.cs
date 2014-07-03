using System;
using Exceptionless.Models.Admin;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class TokenValidator : AbstractValidator<Token> {
        public TokenValidator() {
            RuleFor(u => u.OrganizationId).NotEmpty().WithMessage("Please specify a valid organization id.");
            RuleFor(u => u.CreatedUtc).NotEmpty().WithMessage("Please specify a valid created date.");
        }
    }
}