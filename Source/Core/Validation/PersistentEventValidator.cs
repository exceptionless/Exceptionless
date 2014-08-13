using System;
using Exceptionless.Core.Extensions;
using Exceptionless.Models;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class PersistentEventValidator : AbstractValidator<PersistentEvent> {
        public PersistentEventValidator() {
            RuleFor(e => e.Id).IsObjectId().WithMessage("Please specify a valid id.");
            RuleFor(e => e.OrganizationId).IsObjectId().WithMessage("Please specify a valid organization id.");
            RuleFor(e => e.ProjectId).IsObjectId().WithMessage("Please specify a valid project id.");
            RuleFor(e => e.StackId).IsObjectId().WithMessage("Please specify a valid stack id.");

            RuleFor(s => s.Type).Length(1, 100).WithMessage("Type cannot be longer than 100 characters.");
            RuleFor(s => s.Message).Length(1, 2000).When(s => s.Message != null).WithMessage("Message cannot be longer than 2000 characters.");
            RuleFor(s => s.Source).Length(1, 2000).When(s => s.Source != null).WithMessage("Source cannot be longer than 2000 characters.");

            RuleFor(e => e.ReferenceId)
                .NotEmpty()
                .Length(8, 32)
                .Unless(u => String.IsNullOrEmpty(u.ReferenceId))
                .WithMessage("ReferenceId must contain between 8 and 32 characters");
        }
    }
}