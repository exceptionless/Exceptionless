using System;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class StackValidator : AbstractValidator<Stack> {
        public StackValidator() {
            RuleFor(s => s.OrganizationId).NotEmpty().WithMessage("Please specify a valid organization id.");
            RuleFor(s => s.ProjectId).NotEmpty().WithMessage("Please specify a valid project id.");
            
            // TODO: Should we require that title be set? If so, we need a default plugin.
            //RuleFor(s => s.Title).NotEmpty().WithMessage("Please specify a valid title.");
            RuleFor(s => s.SignatureHash).NotEmpty().WithMessage("Please specify a valid signature hash.");
            RuleFor(s => s.SignatureInfo).NotNull().WithMessage("Please specify a valid signature info.");
        }
    }
}