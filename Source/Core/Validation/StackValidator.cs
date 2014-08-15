using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class StackValidator : AbstractValidator<Stack> {
        public StackValidator() {
            //RuleFor(e => e.Id).IsObjectId().WithMessage("Please specify a valid id.");
            RuleFor(s => s.OrganizationId).IsObjectId().WithMessage("Please specify a valid organization id.");
            RuleFor(s => s.ProjectId).IsObjectId().WithMessage("Please specify a valid project id.");
            RuleFor(s => s.Title).Length(1, 1000).When(s => s.Title != null).WithMessage("Title cannot be longer than 1000 characters.");
            RuleFor(s => s.SignatureHash).NotEmpty().WithMessage("Please specify a valid signature hash.");
            RuleFor(s => s.SignatureInfo).NotNull().WithMessage("Please specify a valid signature info.");
            RuleForEach(s => s.Tags).Length(1, 255).WithMessage("A tag cannot be longer than 255 characters.");
        }
    }
}