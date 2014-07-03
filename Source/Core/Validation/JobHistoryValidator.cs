using System;
using Exceptionless.Core.Jobs;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class JobHistoryValidator : AbstractValidator<JobHistory> {
        public JobHistoryValidator() {
            RuleFor(u => u.Name).NotEmpty().WithMessage("Please specify a valid name.");
        }
    }
}