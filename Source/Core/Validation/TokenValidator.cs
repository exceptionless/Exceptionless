using System;
using Exceptionless.Models.Admin;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class TokenValidator : AbstractValidator<Token> {
        public TokenValidator() {
            RuleFor(t => t.OrganizationId).NotEmpty().WithMessage("Please specify a valid organization id.");
            RuleFor(t => t.CreatedUtc).NotEmpty().WithMessage("Please specify a valid created date.");
        }
    }
}