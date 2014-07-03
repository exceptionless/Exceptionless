using System;
using Exceptionless.Core.Jobs;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class JobHistoryValidator : AbstractValidator<JobHistory> {
        public JobHistoryValidator() {
            RuleFor(j => j.Name).NotEmpty().WithMessage("Please specify a valid name.");
        }
    }
}