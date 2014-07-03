using System;
using Exceptionless.Models.Admin;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class ApplicationValidator : AbstractValidator<Application> {
        public ApplicationValidator() {
            RuleFor(u => u.OrganizationId).NotEmpty().WithMessage("Please specify a valid organization id.");
        }
    }
}