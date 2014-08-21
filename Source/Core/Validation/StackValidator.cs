using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class StackValidator : AbstractValidator<Stack> {
        public StackValidator() {
            RuleFor(e => e.Id).IsObjectId().WithMessage("Please specify a valid id.");
            RuleFor(s => s.OrganizationId).IsObjectId().WithMessage("Please specify a valid organization id.");
            RuleFor(s => s.ProjectId).IsObjectId().WithMessage("Please specify a valid project id.");
            RuleFor(s => s.Title).Length(0, 1000).WithMessage("Title cannot be longer than 1000 characters.");
            RuleFor(s => s.Type).NotEmpty().Length(1, 100).WithMessage("Type must be specified and cannot be longer than 100 characters.");
            RuleForEach(s => s.Tags).NotEmpty().WithMessage("Tags can't be empty.");
            RuleForEach(s => s.Tags).Length(1, 255).WithMessage("A tag cannot be longer than 255 characters.");

            RuleFor(s => s.SignatureHash).NotEmpty().WithMessage("Please specify a valid signature hash.");
            RuleFor(s => s.SignatureInfo).NotNull().WithMessage("Please specify a valid signature info.");
        }
    }
}