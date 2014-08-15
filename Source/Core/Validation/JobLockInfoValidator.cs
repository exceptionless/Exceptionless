using System;
using Exceptionless.Core.Jobs;
using FluentValidation;

namespace Exceptionless.Core.Validation {
    public class JobLockInfoValidator : AbstractValidator<JobLockInfo> {
        public JobLockInfoValidator() {
            RuleFor(j => j.Name).NotEmpty().WithMessage("Please specify a valid name.");
            RuleFor(j => j.CreatedDate).NotEmpty().WithMessage("Please specify a valid created date.");
        }
    }
}