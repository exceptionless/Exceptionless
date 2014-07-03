using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class StackValidator : AbstractValidator<Stack> {
        public StackValidator() {
            RuleFor(u => u.OrganizationId).NotEmpty().WithMessage("Please specify a valid organization id.");
            RuleFor(u => u.ProjectId).NotEmpty().WithMessage("Please specify a valid project id.");
            RuleFor(u => u.SignatureHash).NotEmpty().WithMessage("Please specify a valid signature hash.");
            RuleFor(u => u.SignatureInfo).NotNull().WithMessage("Please specify a valid signature info.");
        }
    }
}